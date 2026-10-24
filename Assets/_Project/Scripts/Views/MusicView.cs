using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Background music: synthwave from the moment the elevator starts rising until the tower falls. The tracks play
    /// one after another (and start over after the last one); the music fades in with the ascent and fades out when
    /// the rescue starts, so the scores in the real room are quiet. The tracks come from a store pack that is not in
    /// the repository: with no tracks the game simply has no music.
    /// </summary>
    public class MusicView : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [Tooltip("Plain stereo source (not placed in the world), with the highest priority so effects never cut it.")]
        [SerializeField] private AudioSource _music;
        [Tooltip("Played in this order every match; the first one plays during the ride up.")]
        [SerializeField] private AudioClip[] _tracks;
        [Tooltip("Kept low, under the shots and explosions.")]
        [SerializeField] private float _volume = 0.35f;
        [Tooltip("Volume change per second when the music starts and ends.")]
        [SerializeField] private float _fadeSpeed = 0.15f;

        // A streamed track takes a moment to start: until then it does not count as ended.
        private const float StartMargin = 1f;

        private int _next;
        private bool _wanted;
        private float _startedAt;

        private void Start()
        {
            if (_tracks == null || _tracks.Length == 0)
            {
                enabled = false;
                return;
            }
            _music.loop = false;
            _music.volume = 0f;
        }

        private void Update()
        {
            bool wanted = WantsMusic(_game.State);
            if (wanted && !_wanted)
            {
                // A new match: the playlist starts again from its first track.
                _next = 0;
                PlayNext();
            }
            _wanted = wanted;

            _music.volume = Mathf.MoveTowards(_music.volume, wanted ? _volume : 0f, _fadeSpeed * Time.deltaTime);
            if (!wanted && _music.volume <= 0f)
            {
                _music.Stop();
            }
            else if (wanted && !_music.isPlaying && Time.unscaledTime - _startedAt > StartMargin)
            {
                // The track ended: the next one follows.
                PlayNext();
            }
        }

        private void PlayNext()
        {
            AudioClip track = _tracks[_next % _tracks.Length];
            _next++;
            _startedAt = Time.unscaledTime;
            if (track == null)
            {
                return;
            }
            _music.clip = track;
            _music.Play();
        }

        private static bool WantsMusic(GameState state)
        {
            switch (state)
            {
                case GameState.Ascent:
                case GameState.Arrival:
                case GameState.Defense:
                case GameState.Respite:
                case GameState.Critical:
                    return true;
                default:
                    return false;
            }
        }
    }
}
