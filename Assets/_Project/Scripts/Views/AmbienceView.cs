using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Wind at the top of the tower. Silent in the real room, muffled inside the closed elevator and full once the
    /// cabin opens 60 m above the city, with slow gusts so the loop never sounds like a loop. Wind is one of the
    /// strongest height cues for the ears.
    /// </summary>
    public class AmbienceView : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private GameSounds _sounds;
        [SerializeField] private AudioSource _wind;
        [SerializeField] private float _openAirVolume = 0.7f;
        [SerializeField] private float _insideCabinVolume = 0.12f;
        [Tooltip("Volume change per second when moving between the room, the cabin and the open air.")]
        [SerializeField] private float _fadeSpeed = 0.35f;
        [SerializeField] private float _gustStrength = 0.35f;
        [SerializeField] private float _gustSpeed = 0.15f;

        private float _level;

        private void Start()
        {
            if (_sounds.Wind == null)
            {
                enabled = false;
                return;
            }
            _wind.clip = _sounds.Wind;
            _wind.loop = true;
            _wind.volume = 0f;
            _wind.Play();
        }

        private void Update()
        {
            _level = Mathf.MoveTowards(_level, TargetLevel(_game.State), _fadeSpeed * Time.deltaTime);
            float gust = 1f - _gustStrength + _gustStrength * 2f * Mathf.PerlinNoise(Time.time * _gustSpeed, 0.5f);
            _wind.volume = _level * gust;
        }

        private float TargetLevel(GameState state)
        {
            switch (state)
            {
                case GameState.Ascent:
                case GameState.Rescue:
                    return _insideCabinVolume;
                case GameState.Arrival:
                case GameState.Defense:
                case GameState.Respite:
                case GameState.Critical:
                    return _openAirVolume;
                default:
                    return 0f;
            }
        }
    }
}
