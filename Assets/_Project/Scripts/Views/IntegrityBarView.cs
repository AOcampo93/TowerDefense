using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The tower's only "life" bar, floating in front of the player at waist height (diegetic, no HUD). It drains
    /// smoothly after each hit, turns from green to yellow to red, and blinks at 2 Hz in the critical state. Notches
    /// mark the 75, 50 and 25 % thresholds where the tower drops.
    /// </summary>
    public class IntegrityBarView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private GameController _game;
        [Tooltip("Root of the bar meshes, hidden outside the fight.")]
        [SerializeField] private GameObject _bar;
        [Tooltip("Pivot at the left end of the bar; its X scale is the integrity.")]
        [SerializeField] private Transform _fillPivot;
        [SerializeField] private Renderer _fill;

        [Header("Feel")]
        [SerializeField] private Color _healthyColor = new Color(0.2f, 0.95f, 0.3f);
        [SerializeField] private Color _damagedColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] private Color _criticalColor = new Color(1f, 0.1f, 0.05f);
        [SerializeField] private Color _blinkOffColor = new Color(0.25f, 0.02f, 0.02f);
        [SerializeField] private float _blinkRate = 2f;
        [Tooltip("Integrity points per second the bar drains towards the real value.")]
        [SerializeField] private float _drainSpeed = 30f;

        private MaterialPropertyBlock _block;
        private float _shown = Balance.MaxIntegrity;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _bar.SetActive(false);
        }

        private void OnEnable()
        {
            _game.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            _game.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            bool visible = state == GameState.Arrival || state == GameState.Defense ||
                           state == GameState.Respite || state == GameState.Critical;
            if (visible && !_bar.activeSelf)
            {
                // A new match starts full.
                _shown = _game.Tower.Integrity;
            }
            _bar.SetActive(visible);
        }

        private void Update()
        {
            if (!_bar.activeSelf)
            {
                return;
            }

            _shown = Mathf.MoveTowards(_shown, _game.Tower.Integrity, _drainSpeed * Time.deltaTime);
            float fraction = _shown / Balance.MaxIntegrity;
            Vector3 scale = _fillPivot.localScale;
            scale.x = Mathf.Max(0.0001f, fraction);
            _fillPivot.localScale = scale;

            bool blinkOff = _game.Tower.IsCritical && Mathf.Repeat(Time.time * _blinkRate, 1f) >= 0.5f;
            _block.SetColor(BaseColorId, blinkOff ? _blinkOffColor : ColorFor(_shown));
            _fill.SetPropertyBlock(_block);
        }

        // Green above 75 %, fading to yellow at 50 % and to red at 25 %.
        private Color ColorFor(float integrity)
        {
            if (integrity >= 50f)
            {
                return Color.Lerp(_damagedColor, _healthyColor, Mathf.InverseLerp(50f, 75f, integrity));
            }
            return Color.Lerp(_criticalColor, _damagedColor, Mathf.InverseLerp(25f, 50f, integrity));
        }
    }
}
