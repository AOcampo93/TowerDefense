using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// An explosion with a 3D boom: a fireball that swells quickly and fades from yellow to dark red, and over it the
    /// particle effects of a store pack when it is in the project (a flash, a shock ring, smoke and sparks), all scaled
    /// to the size of each explosion. Pooled by the ExplosionPool; it calls back when the fireball, the effects and the
    /// sound are over, so the pool can reuse it.
    /// </summary>
    public class ExplosionView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>A store particle effect and the color its own colors are multiplied by.</summary>
        [Serializable]
        private struct Effect
        {
            public GameObject Prefab;
            public Color Tint;
        }

        [Tooltip("Particle effects played together over the fireball (optional store assets). The tint multiplies " +
                 "their colors: the pack's explosion alone is pale against the pink sky.")]
        [SerializeField] private Effect[] _effectPrefabs;
        [Tooltip("Diameter of the effects at scale 1 (meters), to fit them to the size of each explosion.")]
        [SerializeField] private float _effectDiameter = 4f;
        [SerializeField] private Renderer _fireball;
        [SerializeField] private float _seconds = 0.6f;
        [SerializeField] private Color _startColor = new Color(1f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color _endColor = new Color(0.9f, 0.15f, 0.02f, 0f);
        [SerializeField] private GameSounds _sounds;
        [SerializeField] private AudioSource _audio;
        [Tooltip("Explosions this size (meters) or bigger play at full volume; smaller ones are quieter and higher.")]
        [SerializeField] private float _fullVolumeSize = 6f;

        private readonly List<ParticleEffect> _effects = new List<ParticleEffect>();
        private MaterialPropertyBlock _block;
        private Action<ExplosionView> _onFinished;
        private float _elapsed;
        private float _lifetime;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_effectPrefabs == null)
            {
                return;
            }
            foreach (Effect entry in _effectPrefabs)
            {
                ParticleEffect effect = ParticleEffect.Attach(entry.Prefab, transform, 1f, false, entry.Tint);
                if (effect != null)
                {
                    _effects.Add(effect);
                }
            }
        }

        /// <summary>Starts the explosion at a world position. Size is the final diameter in meters.</summary>
        public void Play(Vector3 position, float size, Action<ExplosionView> onFinished)
        {
            transform.position = position;
            _elapsed = 0f;
            _onFinished = onFinished;
            // The effects are made at a diameter of _effectDiameter; the fireball is scaled inside the same space.
            transform.localScale = Vector3.one * (size / _effectDiameter);
            _fireball.enabled = true;
            Animate(0f);
            float effectSeconds = _seconds;
            foreach (ParticleEffect effect in _effects)
            {
                effect.Restart();
                effectSeconds = Mathf.Max(effectSeconds, effect.Duration);
            }

            AudioClip boom = _sounds.Explosion;
            float strength = Mathf.Clamp01(size / _fullVolumeSize);
            _audio.pitch = UnityEngine.Random.Range(0.85f, 1.15f) * Mathf.Lerp(1.5f, 1f, strength);
            _audio.PlayOneShot(boom, Mathf.Lerp(0.4f, 1f, strength));
            // The object stays alive until both the effect and the sound end.
            _lifetime = Mathf.Max(effectSeconds, boom.length / _audio.pitch);
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
            _fireball.transform.localScale = Vector3.one * (_effectDiameter * Mathf.Lerp(0.2f, 1f, swell));
            _block.SetColor(BaseColorId, Color.Lerp(_startColor, _endColor, t));
            _fireball.SetPropertyBlock(_block);
        }
    }
}
