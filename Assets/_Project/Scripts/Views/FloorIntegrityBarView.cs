using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The tower's integrity painted on the platform floor, right in front of the player's feet: a row of glowing
    /// segments that go out from right to left as the tower takes damage, green, then yellow after the first floor
    /// gives way and red after the second. When the tower is about to give way again the whole bar blinks red, so a
    /// glance down warns the player before the drop.
    /// </summary>
    public class FloorIntegrityBarView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private GameController _game;
        [Tooltip("Segments from left to right.")]
        [SerializeField] private Renderer[] _segments;
        [SerializeField] private Color _healthyColor = new Color(0.2f, 1f, 0.35f);
        [SerializeField] private Color _damagedColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] private Color _criticalColor = new Color(1f, 0.1f, 0.05f);
        [SerializeField] private Color _offColor = new Color(0.08f, 0.08f, 0.1f);
        [Tooltip("The bar blinks red when the tower is this close (integrity points) to giving way again.")]
        [SerializeField] private float _warningMargin = 8f;
        [SerializeField] private float _blinkRate = 3f;

        private MaterialPropertyBlock _block;
        private bool _visible = true;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            SetVisible(false);
        }

        private void OnEnable()
        {
            _game.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            _game.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (!_visible)
            {
                return;
            }
            float integrity = _game.Tower.Integrity;
            Color lit = integrity > Balance.IntegrityThresholds[0] ? _healthyColor
                : integrity > Balance.IntegrityThresholds[1] ? _damagedColor
                : _criticalColor;
            bool warning = NearNextThreshold(integrity);
            bool blinkOff = warning && Mathf.Repeat(Time.time * _blinkRate, 1f) >= 0.5f;
            if (warning)
            {
                lit = _criticalColor;
            }
            int litCount = Mathf.CeilToInt(integrity / Balance.MaxIntegrity * _segments.Length);
            for (int i = 0; i < _segments.Length; i++)
            {
                _block.SetColor(BaseColorId, i < litCount && !blinkOff ? lit : _offColor);
                _segments[i].SetPropertyBlock(_block);
            }
        }

        // True when the next floor (or the final collapse) is only a few hits away.
        private bool NearNextThreshold(float integrity)
        {
            foreach (int threshold in Balance.IntegrityThresholds)
            {
                if (integrity > threshold)
                {
                    return integrity - threshold <= _warningMargin;
                }
            }
            return false;
        }

        private void HandleStateChanged(GameState state)
        {
            SetVisible(_game.InFight);
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            foreach (Renderer segment in _segments)
            {
                segment.enabled = visible;
            }
        }
    }
}
