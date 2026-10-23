using AlienInvaders.Core;
using AlienInvaders.Game;
using TMPro;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Big floating titles that tell the story of the match: the game title when the elevator opens at the top,
    /// "WAVE n" with a 5 to 1 countdown (and a beep per number) while the portals open, "DEFEND THE TOWER" when the
    /// ships come out, "WAVE n CLEARED" and "TOWER CRITICAL". The panel follows the head slowly (FollowHeadPanel),
    /// so it reads like a sign in the sky, not a HUD glued to the face.
    /// </summary>
    public class BannerView : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [Tooltip("Canvas root (with a FollowHeadPanel), shown only while a banner is up.")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _subtitle;
        [SerializeField] private TMP_Text _countdown;
        [SerializeField] private GameSounds _sounds;
        [SerializeField] private AudioSource _audio;

        [Header("Colors")]
        [SerializeField] private Color _gameTitleColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] private Color _infoColor = new Color(0.6f, 0.9f, 1f);
        [SerializeField] private Color _goodColor = new Color(0.45f, 1f, 0.55f);
        [SerializeField] private Color _alertColor = new Color(1f, 0.3f, 0.15f);

        [Header("Feel")]
        [Tooltip("Extra size of each countdown number when it appears; it shrinks back in a moment.")]
        [SerializeField] private float _countPunch = 0.4f;
        [SerializeField] private float _punchSeconds = 0.25f;

        private float _hideTime;
        private bool _counting;
        private int _shownCount;
        private float _punchTimer;

        private void Awake()
        {
            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            _game.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            _game.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            // The WaveCycle is created in GameController.Awake, so subscribing is only safe from Start.
            _game.WaveCycle.OnPortalsOpening += HandlePortalsOpening;
            _game.WaveCycle.OnAttackStarted += HandleAttackStarted;
            _game.WaveCycle.OnWaveCleared += HandleWaveCleared;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.WaveCycle != null)
            {
                _game.WaveCycle.OnPortalsOpening -= HandlePortalsOpening;
                _game.WaveCycle.OnAttackStarted -= HandleAttackStarted;
                _game.WaveCycle.OnWaveCleared -= HandleWaveCleared;
            }
        }

        private void Update()
        {
            if (!_panel.activeSelf)
            {
                return;
            }

            if (_counting)
            {
                UpdateCountdown();
            }
            else if (Time.time >= _hideTime)
            {
                _panel.SetActive(false);
            }
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Arrival:
                    // First view of the burning city: the title of the game, like the opening of a film.
                    Show("ALIEN INVADERS", "THE TOWER DEFENSE", _gameTitleColor, Balance.ArrivalSeconds);
                    break;
                case GameState.Critical:
                    Show("TOWER CRITICAL", "IT WILL NOT HOLD MUCH LONGER", _alertColor, 2.5f);
                    break;
                case GameState.Calibration:
                case GameState.Ascent:
                case GameState.Rescue:
                case GameState.Results:
                    // No titles outside the fight (inside the closed cabin they would be hidden by the walls anyway).
                    _counting = false;
                    _panel.SetActive(false);
                    break;
            }
        }

        private void HandlePortalsOpening(WaveConfig wave)
        {
            Show($"WAVE {wave.Number}", "PORTALS OPENING", _infoColor, Balance.PortalOpeningSeconds);
            _counting = true;
            _shownCount = -1;
            _countdown.gameObject.SetActive(true);
        }

        private void HandleAttackStarted(WaveConfig wave)
        {
            _counting = false;
            string hint = wave.Number == 1 ? "SHOOT THEM WHILE THEY AIM" : string.Empty;
            Show("DEFEND THE TOWER", hint, _alertColor, 1.5f);
            _audio.pitch = 1f;
            _audio.PlayOneShot(_sounds.Go);
        }

        private void HandleWaveCleared(WaveConfig wave)
        {
            if (_game.InFight)
            {
                Show($"WAVE {wave.Number} CLEARED", string.Empty, _goodColor, 2f);
            }
        }

        // One number per second of the portal phase: 5, 4, 3, 2, 1. Each new number pops in bigger and beeps.
        private void UpdateCountdown()
        {
            int count = Mathf.CeilToInt(_game.WaveCycle.PhaseTimeLeft);
            if (count != _shownCount && count > 0)
            {
                _shownCount = count;
                _countdown.text = count.ToString();
                _punchTimer = _punchSeconds;
                _audio.pitch = count == 1 ? 1.5f : 1f;
                _audio.PlayOneShot(_sounds.Beep);
            }

            _punchTimer = Mathf.Max(0f, _punchTimer - Time.deltaTime);
            float scale = 1f + _countPunch * (_punchTimer / _punchSeconds);
            _countdown.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Show(string title, string subtitle, Color color, float seconds)
        {
            _title.text = title;
            _title.color = color;
            _subtitle.text = subtitle;
            _subtitle.gameObject.SetActive(subtitle.Length > 0);
            _countdown.gameObject.SetActive(false);
            _hideTime = Time.time + seconds;
            // Activating the panel places it in front of the player (FollowHeadPanel snaps on enable).
            _panel.SetActive(true);
        }
    }
}
