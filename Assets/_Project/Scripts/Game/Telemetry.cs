using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AlienInvaders.Core;
using AlienInvaders.Core.Telemetry;
using AlienInvaders.Views;
using UnityEngine;
using UnityEngine.Networking;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Measures how the game is played and sends it, with the players' feedback, to our Quest Games API, so we know
    /// how a free game is received: how many people play, how long their sessions and matches last, how far they get,
    /// what they open, how the game runs on their headset and what they would improve. The player is only a random id
    /// made on the first launch; nothing personal is sent. Events wait in a queue saved on the headset and go out every
    /// Balance.TelemetryFlushSeconds when there is internet, so a match played offline is not lost. Editor sessions
    /// are not sent unless asked, so tests never mix with real players.
    /// </summary>
    public class Telemetry : MonoBehaviour
    {
        private const string PlayerKey = "telemetry.player";
        private const string LaunchesKey = "telemetry.launches";

        [SerializeField] private GameController _game;
        [SerializeField] private StartScreenView _startScreen;
        [SerializeField] private FeedbackView _feedback;
        [Tooltip("Quest Games API, without a slash at the end.")]
        [SerializeField] private string _endpoint = "https://arturoocampo.com/quest-api";
        [SerializeField] private string _gameId = "alien-invaders";
        [Tooltip("Key of this game in the API (it only tells our games apart, it is not a secret).")]
        [SerializeField] private string _gameKey = "";
        [Tooltip("Off: nothing is recorded or sent.")]
        [SerializeField] private bool _enabled = true;
        [Tooltip("Also record and send from the editor (off, so tests never mix with real players).")]
        [SerializeField] private bool _sendFromEditor;

        private readonly TelemetryQueue _events = new TelemetryQueue(Balance.TelemetryMaxPending);
        private readonly TelemetryQueue _forms = new TelemetryQueue(Balance.TelemetryMaxPending);
        private JsonFields _device;
        private string _player;
        private string _session;
        private string _eventsPath;
        private string _formsPath;
        private bool _sending;
        private float _flushTimer;
        private float _sessionSeconds;
        private DateTime _pausedAt;
        private bool _paused;
        private int _matchesThisSession;
        private float _stateStartTime;
        private GameState _previousState;

        // The match being played.
        private int _powerUps;
        private int _towerHits;
        private int _frames;
        private float _fightSeconds;
        private float _slowSeconds;

        private bool Active => _enabled && (!Application.isEditor || _sendFromEditor);

        private void Awake()
        {
            _eventsPath = Path.Combine(Application.persistentDataPath, "telemetry_events.jsonl");
            _formsPath = Path.Combine(Application.persistentDataPath, "telemetry_feedback.jsonl");
            _player = PlayerPrefs.GetString(PlayerKey, string.Empty);
            bool firstLaunch = _player.Length == 0;
            if (firstLaunch)
            {
                _player = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(PlayerKey, _player);
            }
            int launches = PlayerPrefs.GetInt(LaunchesKey, 0) + 1;
            PlayerPrefs.SetInt(LaunchesKey, launches);
            PlayerPrefs.Save();

            _device = new JsonFields()
                .With("model", SystemInfo.deviceModel)
                .With("os", SystemInfo.operatingSystem)
                .With("language", Application.systemLanguage.ToString());
            if (!Active)
            {
                return;
            }
            _events.Load(_eventsPath);
            _forms.Load(_formsPath);
            StartSession(new JsonFields()
                .With("first_launch", firstLaunch)
                .With("launch", launches)
                .With("memory_mb", SystemInfo.systemMemorySize)
                .With("refresh_hz", Screen.currentResolution.refreshRateRatio.value));
        }

        private void OnEnable()
        {
            _game.OnStateChanged += HandleStateChanged;
            _game.OnRecordSaved += HandleRecordSaved;
            _startScreen.OnAboutOpened += HandleAboutOpened;
            _feedback.OnOpened += HandleFeedbackOpened;
            _feedback.OnSubmitted += HandleFeedbackSubmitted;
        }

        private void OnDisable()
        {
            _game.OnStateChanged -= HandleStateChanged;
            _game.OnRecordSaved -= HandleRecordSaved;
            _startScreen.OnAboutOpened -= HandleAboutOpened;
            _feedback.OnOpened -= HandleFeedbackOpened;
            _feedback.OnSubmitted -= HandleFeedbackSubmitted;
        }

        private void Start()
        {
            // The core systems exist once GameController.Awake has run.
            _game.Boost.OnCollected += HandlePowerUp;
            _game.Tower.OnDamaged += HandleTowerHit;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Boost != null)
            {
                _game.Boost.OnCollected -= HandlePowerUp;
                _game.Tower.OnDamaged -= HandleTowerHit;
            }
        }

        private void Update()
        {
            if (!Active)
            {
                return;
            }
            float dt = Time.unscaledDeltaTime;
            _sessionSeconds += dt;
            if (_game.InFight)
            {
                _frames++;
                _fightSeconds += dt;
                if (dt > 1f / Balance.TelemetrySlowFps)
                {
                    _slowSeconds += dt;
                }
            }
            _flushTimer -= dt;
            if (_flushTimer <= 0f)
            {
                _flushTimer = Balance.TelemetryFlushSeconds;
                StartCoroutine(Flush());
            }
        }

        // The headset was taken off (pause) or put back on. A long pause ends the session.
        private void OnApplicationPause(bool paused)
        {
            if (!Active || paused == _paused)
            {
                return;
            }
            _paused = paused;
            if (paused)
            {
                _pausedAt = DateTime.UtcNow;
                Track("session_end", new JsonFields()
                    .With("seconds", _sessionSeconds)
                    .With("matches", _matchesThisSession)
                    .With("state", _game.State.ToString())
                    .With("in_match", _game.InFight));
                SaveQueues();
                StartCoroutine(Flush());
                return;
            }
            if ((DateTime.UtcNow - _pausedAt).TotalSeconds > Balance.TelemetrySessionTimeoutSeconds)
            {
                StartSession(new JsonFields().With("resumed", true));
            }
        }

        private void OnApplicationQuit()
        {
            if (Active && !_paused)
            {
                Track("session_end", new JsonFields()
                    .With("seconds", _sessionSeconds)
                    .With("matches", _matchesThisSession)
                    .With("state", _game.State.ToString())
                    .With("in_match", _game.InFight));
            }
            SaveQueues();
        }

        /// <summary>Records something that happened, with its values, to be sent with the next batch.</summary>
        public void Track(string type, JsonFields data)
        {
            if (Active)
            {
                _events.Add(TelemetryPayload.Event(type, _session, DateTime.UtcNow, data));
            }
        }

        private void StartSession(JsonFields data)
        {
            _session = Guid.NewGuid().ToString("N");
            _sessionSeconds = 0f;
            _matchesThisSession = 0;
            Track("session_start", data);
        }

        private void HandleStateChanged(GameState state)
        {
            float secondsInPrevious = Time.unscaledTime - _stateStartTime;
            _stateStartTime = Time.unscaledTime;
            switch (state)
            {
                case GameState.Calibration:
                    if (_previousState == GameState.Results)
                    {
                        Track("play_again", new JsonFields().With("results_seconds", secondsInPrevious));
                    }
                    break;
                case GameState.Ascent:
                    _matchesThisSession++;
                    _powerUps = 0;
                    _towerHits = 0;
                    _frames = 0;
                    _fightSeconds = 0f;
                    _slowSeconds = 0f;
                    Track("match_start", new JsonFields()
                        .With("match", _matchesThisSession)
                        .With("start_screen_seconds", secondsInPrevious));
                    break;
                case GameState.Results:
                    MatchResult result = _game.LastResult;
                    Track("match_end", new JsonFields()
                        .With("match", _matchesThisSession)
                        .With("seconds", result.SecondsSurvived)
                        .With("wave", result.WaveReached)
                        .With("score", result.Score)
                        .With("kills", result.ShipsDestroyed)
                        .With("shots", result.ShotsFired)
                        .With("hits", result.ShotsHit)
                        .With("accuracy", result.Accuracy)
                        .With("power_ups", _powerUps)
                        .With("tower_hits", _towerHits)
                        .With("avg_fps", _fightSeconds > 0f ? _frames / _fightSeconds : 0f)
                        .With("slow_seconds", _slowSeconds));
                    StartCoroutine(Flush());
                    break;
            }
            _previousState = state;
        }

        private void HandleRecordSaved(int place, MatchResult result)
        {
            Track("record_saved", new JsonFields()
                .With("place", place + 1)
                .With("initials", result.Initials)
                .With("score", result.Score));
        }

        private void HandleAboutOpened()
        {
            Track("about_opened", new JsonFields().With("state", _game.State.ToString()));
        }

        private void HandleFeedbackOpened()
        {
            Track("feedback_opened", new JsonFields().With("match", _matchesThisSession));
        }

        private void HandleFeedbackSubmitted(int rating, IReadOnlyList<string> tags, string message)
        {
            if (!Active)
            {
                return;
            }
            MatchResult last = _game.LastResult;
            var context = new JsonFields()
                .With("matches_this_session", _matchesThisSession)
                .With("last_seconds", last.SecondsSurvived)
                .With("last_wave", last.WaveReached)
                .With("last_score", last.Score)
                .With("device", SystemInfo.deviceModel);
            _forms.Add(TelemetryPayload.Feedback(_gameId, _player, _session, Application.version, rating, tags, message, context));
            Track("feedback_sent", new JsonFields()
                .With("rating", rating)
                .With("tags", tags)
                .With("message_chars", message == null ? 0 : message.Length));
            SaveQueues();
            StartCoroutine(Flush());
        }

        private void HandlePowerUp()
        {
            _powerUps++;
        }

        private void HandleTowerHit(TowerSide side, float damage)
        {
            _towerHits++;
        }

        // Sends what is waiting: one batch of events, then the feedback forms one by one. What fails stays for later.
        private IEnumerator Flush()
        {
            if (_sending || !Active || string.IsNullOrEmpty(_gameKey))
            {
                yield break;
            }
            _sending = true;
            List<string> batch = _events.Peek(Balance.TelemetryBatchSize);
            if (batch.Count > 0)
            {
                string body = TelemetryPayload.Events(_gameId, _player, _session, Application.version, _device, batch);
                bool sent = false;
                yield return Post("/v1/events", body, ok => sent = ok);
                if (sent)
                {
                    _events.Remove(batch.Count);
                }
            }
            while (_forms.Count > 0)
            {
                bool sent = false;
                yield return Post("/v1/feedback", _forms.Peek(1)[0], ok => sent = ok);
                if (!sent)
                {
                    break;
                }
                _forms.Remove(1);
            }
            SaveQueues();
            _sending = false;
        }

        private IEnumerator Post(string path, string json, Action<bool> done)
        {
            using (var request = new UnityWebRequest(_endpoint + path, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-Game-Key", _gameKey);
                request.timeout = 15;
                yield return request.SendWebRequest();
                bool ok = request.result == UnityWebRequest.Result.Success;
                // A rejected form (bad data) is dropped instead of being retried forever.
                bool rejected = request.responseCode == 400 || request.responseCode == 413;
                if (!ok && !rejected)
                {
                    Debug.Log($"[Telemetry] {path} not sent ({request.responseCode} {request.error}); it will be retried.");
                }
                done(ok || rejected);
            }
        }

        private void SaveQueues()
        {
            if (!Active)
            {
                return;
            }
            try
            {
                _events.Save(_eventsPath);
                _forms.Save(_formsPath);
            }
            catch (Exception e)
            {
                // A full disk must never stop the game.
                Debug.LogWarning($"[Telemetry] Could not save the queue: {e.Message}");
            }
        }
    }
}
