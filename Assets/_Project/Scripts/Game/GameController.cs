using System;
using System.Collections;
using System.IO;
using AlienInvaders.Core;
using AlienInvaders.Core.Motion;
using AlienInvaders.Core.Records;
using AlienInvaders.Views;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// The game's state machine and the owner of every core system. It is the only place that decides
    /// which state comes next; views and other MonoBehaviours just react to OnStateChanged.
    /// Flow: Calibration (AR) -> Ascent -> Arrival -> Defense (VR) -> ... -> Results (AR).
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [SerializeField] private PlatformCalibration _calibration;
        [SerializeField] private PassthroughFader _passthrough;
        [SerializeField] private ElevatorView _elevator;
        [SerializeField] private ShipSpawner _ships;
        [SerializeField] private PortalSpawner _portals;
        [SerializeField] private ResultsView _results;
        [SerializeField] private ScreenFader _screenFade;
        [Tooltip("Components that implement IMotionOutput (camera, haptics, future platform). Disabled ones are skipped.")]
        [SerializeField] private MonoBehaviour[] _motionOutputs;

        private const string LastInitialsKey = "records.initials";

        private float _motionTimer;
        private MatchResult _lastResult;

        /// <summary>Current state of the session.</summary>
        public GameState State { get; private set; }

        /// <summary>True in the states where ships attack and the player fights back.</summary>
        public bool InFight => State == GameState.Defense || State == GameState.Respite || State == GameState.Critical;

        /// <summary>Raised after every state change so views can show or hide themselves.</summary>
        public event Action<GameState> OnStateChanged;

        // Core systems (pure C#). Created here so every MonoBehaviour shares the same instances.
        public TowerIntegrity Tower { get; private set; }
        public TowerMotionModel TowerMotion { get; private set; }
        public WeaponHeat Weapon { get; private set; }
        public ScoreSystem Score { get; private set; }
        public WaveGenerator Waves { get; private set; }
        public WaveCycle WaveCycle { get; private set; }
        public MotionBus Motion { get; private set; }
        public HighScoreTable Records { get; private set; }
        public MotionRecorder Recorder { get; private set; }

        private void Awake()
        {
            Tower = new TowerIntegrity();
            TowerMotion = new TowerMotionModel(Tower);
            Weapon = new WeaponHeat();
            Score = new ScoreSystem();
            Waves = new WaveGenerator(Environment.TickCount);
            WaveCycle = new WaveCycle(Waves);
            Motion = new MotionBus();
            Records = new HighScoreTable(
                Path.Combine(Application.persistentDataPath, "records.json"), new JsonUtilityRecordSerializer());
            Records.Load();

            // The wave rhythm drives the fight states: calm and portals = Respite, ships attacking = Defense.
            WaveCycle.OnAttackStarted += HandleAttackStarted;
            WaveCycle.OnWaveCleared += HandleWaveCleared;
            // Structural thresholds drive the end of the match: 25 % = critical, 0 % = collapse and rescue.
            Tower.OnThresholdReached += HandleThreshold;

            // The recorder is one more motion output: the whole match is saved as a motion track for the future platform.
            Recorder = new MotionRecorder();
            Motion.Register(Recorder);

            // Unity cannot show interface fields in the inspector, so outputs are plain components checked here.
            foreach (MonoBehaviour behaviour in _motionOutputs)
            {
                if (behaviour is IMotionOutput output && behaviour.enabled)
                {
                    Motion.Register(output);
                }
            }
        }

        private void OnEnable()
        {
            _calibration.OnCalibrated += HandleCalibrated;
            _results.OnPlayAgain += HandlePlayAgain;
            _results.OnInitialsConfirmed += HandleInitialsConfirmed;
        }

        private void OnDisable()
        {
            _calibration.OnCalibrated -= HandleCalibrated;
            _results.OnPlayAgain -= HandlePlayAgain;
            _results.OnInitialsConfirmed -= HandleInitialsConfirmed;
        }

        private void Start()
        {
            // Start from the last known platform position; the player confirms (or fixes) it with the trigger.
            _calibration.LoadSaved();
            EnterState(GameState.Calibration);
        }

        /// <summary>
        /// Switches to a new state. Each case prepares the scene for that state; timed sequences run
        /// as coroutines that call EnterState again when they finish.
        /// </summary>
        public void EnterState(GameState next)
        {
            // A new state cancels whatever the previous one was still animating (sequence and fades).
            StopAllCoroutines();

            State = next;
            Debug.Log($"[Game] Enter {next}");

            switch (next)
            {
                case GameState.Calibration:
                    _results.Hide();
                    _screenFade.SetImmediate(0f);
                    _elevator.Hide();
                    _passthrough.SetImmediate(1f);
                    _calibration.Begin();
                    break;

                case GameState.Ascent:
                    ResetMatch();
                    StartCoroutine(AscentRoutine());
                    break;

                case GameState.Arrival:
                    // A new fight starts with a cold weapon (the gun appears while the cabin opens).
                    Weapon.Reset();
                    StartCoroutine(ArrivalRoutine());
                    break;

                case GameState.Defense:
                case GameState.Respite:
                    // Fight states: the WaveCycle (ticked in Update) switches between them; the weapon arms itself.
                    break;

                case GameState.Critical:
                    // Still playable: the views add the alarm, the blinking bar and the heartbeat haptics.
                    break;

                case GameState.Rescue:
                    StartCoroutine(RescueRoutine());
                    break;

                case GameState.Results:
                    StartCoroutine(ResultsRoutine());
                    break;
            }

            OnStateChanged?.Invoke(next);
        }

        private void Update()
        {
            if (!InFight)
            {
                return;
            }

            WaveCycle.Tick(Time.deltaTime, _portals.ShipsLeft);
            Score.Tick(Time.deltaTime);

            // The motion model runs every frame (smooth tilt and drops); outputs get a sample 20 times per second,
            // the rate a physical platform and the recording use.
            MotionSample sample = TowerMotion.Tick(Time.deltaTime);
            _motionTimer += Time.deltaTime;
            float interval = 1f / Balance.MotionSamplesPerSecond;
            if (_motionTimer >= interval)
            {
                _motionTimer %= interval;
                Motion.Publish(sample);
            }
        }

        private void HandleAttackStarted(WaveConfig wave)
        {
            if (State == GameState.Respite)
            {
                EnterState(GameState.Defense);
            }
        }

        private void HandleWaveCleared(WaveConfig wave)
        {
            if (State == GameState.Defense)
            {
                EnterState(GameState.Respite);
            }
        }

        private void HandleThreshold(int threshold)
        {
            if (!InFight)
            {
                return;
            }
            if (threshold <= 0)
            {
                EnterState(GameState.Rescue);
            }
            else if (threshold <= Balance.CriticalIntegrity && State != GameState.Critical)
            {
                EnterState(GameState.Critical);
            }
        }

        private void HandlePlayAgain()
        {
            if (State == GameState.Results)
            {
                EnterState(GameState.Calibration);
            }
        }

        private void HandleInitialsConfirmed(string initials)
        {
            if (State != GameState.Results)
            {
                return;
            }

            _lastResult.Initials = initials;
            int place = Records.Add(_lastResult);
            try
            {
                Records.Save();
            }
            catch (Exception e)
            {
                // A full or read-only disk must not freeze the results screen.
                Debug.LogWarning($"[Game] Could not save records: {e.Message}");
            }
            PlayerPrefs.SetString(LastInitialsKey, initials);
            PlayerPrefs.Save();
            Debug.Log($"[Game] Record saved: {initials} in place {place + 1}");

            _results.ShowTable(Records.Entries, place);
            _results.SetPlayAgainVisible(true);
        }

        /// <summary>Puts every system back to the start, so "Play again" begins a clean match.</summary>
        private void ResetMatch()
        {
            Tower.Reset();
            TowerMotion.Reset();
            Weapon.Reset();
            Score.Reset();
            WaveCycle.Reset();
            _ships.ClearAll();
            _portals.StopAll();
            Motion.ResetAll();
            Recorder.Clear();
            _motionTimer = 0f;
        }

        private void HandleCalibrated()
        {
            if (State == GameState.Calibration)
            {
                EnterState(GameState.Ascent);
            }
        }

        private IEnumerator AscentRoutine()
        {
            // Close the cabin first, so the real room disappears behind solid walls and not around the player.
            yield return _elevator.Close(Balance.ElevatorCloseSeconds);
            StartCoroutine(_passthrough.FadeTo(0f, Balance.PassthroughFadeSeconds));
            yield return _elevator.PlayAscent(Balance.AscentSeconds);
            EnterState(GameState.Arrival);
        }

        private IEnumerator RescueRoutine()
        {
            // No more ships come out. The emergency cabin slams shut first, so the world never vanishes in view.
            _portals.StopAll();
            Coroutine fall = StartCoroutine(_elevator.PlayRescue(Balance.RescueSeconds));
            yield return new WaitForSeconds(_elevator.RescueCloseSeconds);

            // Behind closed walls: remove the ships and level the world for the next match.
            _ships.ClearAll();
            Motion.ResetAll();
            yield return fall;

            // The fall ends in black; the real room is switched on behind it.
            yield return _screenFade.FadeTo(1f, Balance.FadeToBlackSeconds);
            _elevator.Hide();
            _passthrough.SetImmediate(1f);
            SaveMotionTrack();
            EnterState(GameState.Results);
        }

        private IEnumerator ResultsRoutine()
        {
            _lastResult = Score.BuildResult(string.Empty, Waves.CurrentWave, DateTime.UtcNow);
            Debug.Log($"[Game] Match over: {_lastResult.Score} points, {_lastResult.SecondsSurvived:0} s, wave {_lastResult.WaveReached}");
            _results.Show(_lastResult, Records.Entries, -1);
            if (Records.Qualifies(_lastResult))
            {
                // A place in the Top 10: ask for initials first; the record is saved when they are confirmed.
                _results.BeginInitials(PlayerPrefs.GetString(LastInitialsKey, "AAA"));
            }
            else
            {
                _results.SetPlayAgainVisible(true);
            }
            yield return _screenFade.FadeTo(0f, Balance.FadeFromBlackSeconds);
        }

        // Writes the motion recorded during the match to motion_yyyyMMdd_HHmmss.json next to the records.
        private void SaveMotionTrack()
        {
            if (Recorder.Samples.Count == 0)
            {
                return;
            }
            string path = Path.Combine(Application.persistentDataPath, $"motion_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            try
            {
                Recorder.Save(path);
                Debug.Log($"[Game] Motion track saved: {path} ({Recorder.Samples.Count} samples)");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Game] Could not save the motion track: {e.Message}");
            }
        }

        private IEnumerator ArrivalRoutine()
        {
            yield return _elevator.Open(Balance.ArrivalSeconds);
            _elevator.Hide();
            // The first wave starts like every other one: a few calm seconds, then the portals open.
            EnterState(GameState.Respite);
        }
    }
}
