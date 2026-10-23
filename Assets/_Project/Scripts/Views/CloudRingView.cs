using System.Collections.Generic;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// A ring of huge storm clouds (cumulonimbus) all around the city, of different shapes and sizes, so the line
    /// between the ground and the sky disappears behind them in every direction. Each cloud is one big sprite (one of
    /// four shapes) rising out of the cloud sea, sometimes with smaller ones at its feet; they are placed once from a
    /// fixed seed and drawn by a particle system that never moves them; the whole ring only turns with the sky.
    /// Clouds facing the setting sun are tinted pink, the others violet.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class CloudRingView : MonoBehaviour
    {
        [SerializeField] private SkyController _sky;
        [SerializeField] private int _seed = 31;
        [SerializeField] private int _clouds = 40;
        [Tooltip("Distance range from the tower (meters).")]
        [SerializeField] private Vector2 _distance = new Vector2(620f, 880f);
        [Tooltip("Height range of the cloud bases, relative to the player (the street is at -200).")]
        [SerializeField] private Vector2 _baseHeight = new Vector2(-200f, -150f);
        [Tooltip("Size range of the main clouds (meters).")]
        [SerializeField] private Vector2 _cloudSize = new Vector2(300f, 650f);
        [Tooltip("Compass direction of the sun before the sky turns (degrees from +Z towards +X), to light the clouds facing it.")]
        [SerializeField] private float _sunAzimuth = 117.4f;
        [SerializeField] private Color _litColor = new Color(1f, 0.74f, 0.78f);
        [SerializeField] private Color _shadeColor = new Color(0.72f, 0.62f, 0.92f);

        private ParticleSystem _system;
        private ParticleSystem.Particle[] _sprites;

        private void Awake()
        {
            _system = GetComponent<ParticleSystem>();
            _sprites = BuildSprites();
        }

        private void OnEnable()
        {
            // Turning the object off clears its particles, so the clouds are put back every time it turns on.
            _system.Play();
            _system.SetParticles(_sprites, _sprites.Length);
            // Paused particles keep rendering but never age or move: the clouds stay where they were placed.
            _system.Pause();
        }

        private void LateUpdate()
        {
            // The sun turns with the sky, so the ring turns too and its lit side keeps facing the sun.
            transform.localRotation = Quaternion.Euler(0f, -_sky.Turn, 0f);
        }

        private ParticleSystem.Particle[] BuildSprites()
        {
            var random = new System.Random(_seed);
            float Next() => (float)random.NextDouble();
            float Lerp(float a, float b, float t) => a + (b - a) * t;

            var sprites = new List<ParticleSystem.Particle>();
            for (int c = 0; c < _clouds; c++)
            {
                float azimuth = (c + Lerp(-0.45f, 0.45f, Next())) * 360f / _clouds;
                float distance = Lerp(_distance.x, _distance.y, Next());
                float baseY = Lerp(_baseHeight.x, _baseHeight.y, Next());
                float size = Lerp(_cloudSize.x, _cloudSize.y, Next());
                // One big storm cloud, sometimes with one or two smaller ones at its feet.
                AddSprite(sprites, azimuth, distance, baseY, size, (uint)random.Next());
                int companions = random.Next(0, 3);
                for (int k = 0; k < companions; k++)
                {
                    float side = (Next() < 0.5f ? -1f : 1f) * Lerp(0.25f, 0.45f, Next()) * size;
                    float companionSize = size * Lerp(0.4f, 0.7f, Next());
                    float companionAzimuth = azimuth + side / distance * Mathf.Rad2Deg;
                    AddSprite(sprites, companionAzimuth, distance - Lerp(20f, 60f, Next()), baseY - Lerp(0f, 20f, Next()),
                        companionSize, (uint)random.Next());
                }
            }
            return sprites.ToArray();
        }

        // A cloud sprite standing on the cloud sea: its flat base (12 percent up the picture) sits at baseY.
        private void AddSprite(List<ParticleSystem.Particle> sprites, float azimuth, float distance, float baseY,
            float size, uint seed)
        {
            float radians = azimuth * Mathf.Deg2Rad;
            var outward = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            // Clouds facing the low sun are lit warm, the others stay in violet shade.
            float facing = Mathf.Clamp01(Mathf.Cos((azimuth - _sunAzimuth) * Mathf.Deg2Rad));
            sprites.Add(new ParticleSystem.Particle
            {
                position = outward * distance + Vector3.up * (baseY + size * 0.38f),
                startSize = size,
                startColor = Color.Lerp(_shadeColor, _litColor, facing * facing),
                startLifetime = 1e6f,
                remainingLifetime = 1e6f,
                rotation = 0f,
                // The particle's random seed also picks which of the four cloud shapes it shows.
                randomSeed = seed
            });
        }
    }
}
