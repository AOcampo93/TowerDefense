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

        /// <summary>Current state of the session.</summary>
        public GameState State { get; private set; }

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
        }

        private void OnEnable()
        {
            _calibration.OnCalibrated += HandleCalibrated;
        }

        private void OnDisable()
        {
            _calibration.OnCalibrated -= HandleCalibrated;
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
                    _elevator.Hide();
                    _passthrough.SetImmediate(1f);
                    _calibration.Begin();
                    break;

                case GameState.Ascent:
                    StartCoroutine(AscentRoutine());
                    break;

                case GameState.Arrival:
                    StartCoroutine(ArrivalRoutine());
                    break;

                case GameState.Defense:
                    // The weapon and the waves start here once they are built (next phases).
                    break;

                case GameState.Respite:
                case GameState.Critical:
                case GameState.Rescue:
                case GameState.Results:
                    // Not reachable yet: added together with waves, tower collapse and records.
                    break;
            }

            OnStateChanged?.Invoke(next);
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

        private IEnumerator ArrivalRoutine()
        {
            yield return _elevator.Open(Balance.ArrivalSeconds);
            _elevator.Hide();
            EnterState(GameState.Defense);
        }
    }
}
