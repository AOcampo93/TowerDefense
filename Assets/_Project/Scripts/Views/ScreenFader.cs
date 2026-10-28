using System.Collections;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Fades the whole view to black and back with a dark shell around the head. The rescue ends in black (comfort
    /// rule: never cut straight from a fall to the real room); the room is switched on behind the black, which then
    /// clears. Lives on the center eye, so the shell always surrounds the player's eyes.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Sphere around the eyes with the ScreenFade material (black, drawn over everything). It must be wider " +
                 "than the camera's near plane at the edges of the view (radius 0.5 m), or the near plane cuts a hole " +
                 "in it and the darkness shows as a dark ball in the middle of the view.")]
        [SerializeField] private Renderer _shell;

        private MaterialPropertyBlock _block;

        /// <summary>0 = clear view, 1 = completely black.</summary>
        public float Alpha { get; private set; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            SetImmediate(0f);
        }

        /// <summary>Coroutine: fades to the given darkness over some seconds.</summary>
        public IEnumerator FadeTo(float alpha, float seconds)
        {
            float start = Alpha;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                Apply(Mathf.Lerp(start, alpha, t / seconds));
                yield return null;
            }
            Apply(alpha);
        }

        /// <summary>Jumps straight to a darkness level.</summary>
        public void SetImmediate(float alpha)
        {
            Apply(alpha);
        }

        private void Apply(float alpha)
        {
            Alpha = alpha;
            // A clear shell is switched off, so it costs nothing while the game is playing.
            _shell.enabled = alpha > 0.001f;
            _block.SetColor(BaseColorId, new Color(0f, 0f, 0f, alpha));
            _shell.SetPropertyBlock(_block);
        }
    }
}
