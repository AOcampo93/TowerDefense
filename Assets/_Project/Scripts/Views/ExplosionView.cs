using System;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Greybox explosion: a fireball that swells quickly and fades from yellow to dark red, with a 3D boom. Pooled by
    /// the ExplosionPool; it calls back when both the fireball and the sound are over, so the pool can reuse it.
    /// </summary>
    public class ExplosionView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer _fireball;
        [SerializeField] private float _seconds = 0.6f;
        [SerializeField] private Color _startColor = new Color(1f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color _endColor = new Color(0.9f, 0.15f, 0.02f, 0f);
        [SerializeField] private AudioSource _audio;
        [Tooltip("Explosions this size (meters) or bigger play at full volume; smaller ones are quieter and higher.")]
        [SerializeField] private float _fullVolumeSize = 6f;

        private MaterialPropertyBlock _block;
        private Action<ExplosionView> _onFinished;
        private float _size;
        private float _elapsed;
        private float _lifetime;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        /// <summary>Starts the explosion at a world position. Size is the final diameter in meters.</summary>
        public void Play(Vector3 position, float size, Action<ExplosionView> onFinished)
        {
            transform.position = position;
            _size = size;
            _elapsed = 0f;
            _onFinished = onFinished;
            _fireball.enabled = true;
            Animate(0f);

            AudioClip boom = PlaceholderSounds.Explosion;
            float strength = Mathf.Clamp01(size / _fullVolumeSize);
            _audio.pitch = UnityEngine.Random.Range(0.85f, 1.15f) * Mathf.Lerp(1.5f, 1f, strength);
            _audio.PlayOneShot(boom, Mathf.Lerp(0.4f, 1f, strength));
            // The object stays alive until the sound ends; the fireball alone is shorter.
            _lifetime = Mathf.Max(_seconds, boom.length / _audio.pitch);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_fireball.enabled)
            {
                float t = Mathf.Clamp01(_elapsed / _seconds);
                Animate(t);
                _fireball.enabled = t < 1f;
            }
            if (_elapsed >= _lifetime)
            {
                _onFinished?.Invoke(this);
            }
        }

        private void Animate(float t)
        {
            // Fast swell at the start (ease out), slow fade at the end.
            float swell = 1f - (1f - t) * (1f - t) * (1f - t);
            transform.localScale = Vector3.one * (_size * Mathf.Lerp(0.2f, 1f, swell));
            _block.SetColor(BaseColorId, Color.Lerp(_startColor, _endColor, t));
            _fireball.SetPropertyBlock(_block);
        }
    }
}
