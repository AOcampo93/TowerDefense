using System;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Greybox explosion: a fireball that swells quickly and fades from yellow to dark red. Pooled by the
    /// ExplosionPool; it calls back when it finishes so the pool can reuse it.
    /// </summary>
    public class ExplosionView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer _fireball;
        [SerializeField] private float _seconds = 0.6f;
        [SerializeField] private Color _startColor = new Color(1f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color _endColor = new Color(0.9f, 0.15f, 0.02f, 0f);

        private MaterialPropertyBlock _block;
        private Action<ExplosionView> _onFinished;
        private float _size;
        private float _elapsed;

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
            Animate(0f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _seconds);
            Animate(t);
            if (t >= 1f)
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
