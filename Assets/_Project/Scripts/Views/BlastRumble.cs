using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The thunder of a bomb on the city. Light comes first: the player sees the blast, and a moment later (the time
    /// sound takes to travel that far) hears a deep boom from its direction and feels the platform jolt. The bombs land
    /// far away, so the boom is not faded by distance like the other sounds: it is meant to be felt. The jolt goes to
    /// the tower's motion model, so the camera, the controllers and a future motion platform all feel it.
    /// </summary>
    public class BlastRumble : MonoBehaviour
    {
        private const float SpeedOfSound = 343f;
        // Bombs waiting for their boom to arrive (several bombers can drop at once).
        private const int MaxPending = 6;

        [SerializeField] private GameController _game;
        [SerializeField] private GameSounds _sounds;
        [Tooltip("The player's head: the boom comes from the direction of the blast as seen from here.")]
        [SerializeField] private Transform _head;
        [Tooltip("A few 3D sources, used in turn. Each one is placed in the direction of its blast.")]
        [SerializeField] private AudioSource[] _voices;
        [Tooltip("The boom is placed this far from the head, towards the blast (meters).")]
        [SerializeField] private float _voiceDistance = 20f;
        [Tooltip("The explosion sound played this much lower, so it rumbles.")]
        [SerializeField] private float _pitch = 0.5f;
        [SerializeField] private float _volume = 1f;

        private readonly float[] _arrivals = new float[MaxPending];
        private int _pending;
        private int _nextVoice;

        /// <summary>A bomb went off at a world position: its boom and jolt reach the player after the sound's delay.</summary>
        public void Play(Vector3 blast)
        {
            Vector3 toBlast = blast - _head.position;
            float delay = toBlast.magnitude / SpeedOfSound;

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.transform.position = _head.position + toBlast.normalized * _voiceDistance;
            voice.clip = _sounds.Explosion;
            voice.pitch = _pitch * Random.Range(0.92f, 1.08f);
            voice.volume = _volume;
            voice.PlayDelayed(delay);

            if (_pending < MaxPending)
            {
                _arrivals[_pending++] = Time.time + delay;
            }
        }

        private void Update()
        {
            for (int i = _pending - 1; i >= 0; i--)
            {
                if (Time.time < _arrivals[i])
                {
                    continue;
                }
                if (_game.InFight)
                {
                    _game.TowerMotion.Jolt(Balance.BombJolt);
                }
                _arrivals[i] = _arrivals[--_pending];
            }
        }
    }
}
