using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Sounds of the tower itself: a deep rumble at every structural drop (75, 50 and 25 %, deeper each time) and a
    /// looping alarm from the critical state until the end of the rescue. The tower is under the player's feet, so
    /// these sounds are not positioned in space.
    /// </summary>
    public class TowerSoundsView : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private GameSounds _sounds;
        [SerializeField] private AudioSource _rumble;
        [SerializeField] private AudioSource _alarm;
        [SerializeField] private float _alarmVolume = 0.35f;

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
            // The core tower is created in GameController.Awake, so it is only safe to subscribe here.
            _game.Tower.OnThresholdReached += HandleThreshold;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Tower != null)
            {
                _game.Tower.OnThresholdReached -= HandleThreshold;
            }
        }

        private void HandleThreshold(int threshold)
        {
            // The first floor that gives way sounds at normal pitch; the next one (and the collapse at 0 %) deeper.
            _rumble.pitch = Mathf.Lerp(0.7f, 1f, threshold / (float)Balance.IntegrityThresholds[0]);
            _rumble.PlayOneShot(_sounds.TowerRumble);
        }

        private void HandleStateChanged(GameState state)
        {
            bool alarm = state == GameState.Critical || state == GameState.Rescue;
            if (alarm && !_alarm.isPlaying)
            {
                _alarm.clip = _sounds.Alarm;
                _alarm.loop = true;
                _alarm.volume = _alarmVolume;
                _alarm.Play();
            }
            else if (!alarm && _alarm.isPlaying)
            {
                _alarm.Stop();
            }
        }
    }
}
