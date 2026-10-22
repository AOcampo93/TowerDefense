using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Keeps a world-space panel floating in front of the player and follows the head slowly.
    /// It lags behind on purpose: a panel glued to the face is uncomfortable in VR (design rule: no HUD),
    /// while a slow follow reads like an object hanging in the room.
    /// </summary>
    public class FollowHeadPanel : MonoBehaviour
    {
        [SerializeField] private Transform _head;
        [SerializeField] private float _distance = 1.2f;
        [SerializeField] private float _heightOffset = -0.1f;
        [Tooltip("Higher values catch up with the head faster.")]
        [SerializeField] private float _followSpeed = 2f;

        private void OnEnable()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            GetTarget(out Vector3 position, out Quaternion rotation);
            // Exponential smoothing: the same feel at any frame rate.
            float blend = 1f - Mathf.Exp(-_followSpeed * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, position, blend),
                Quaternion.Slerp(transform.rotation, rotation, blend));
        }

        /// <summary>Places the panel in front of the head immediately (when it appears).</summary>
        public void SnapToTarget()
        {
            GetTarget(out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);
        }

        private void GetTarget(out Vector3 position, out Quaternion rotation)
        {
            // Only the horizontal direction counts, so the panel stays upright when the player looks down.
            Vector3 forward = _head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            position = _head.position + forward * _distance + Vector3.up * _heightOffset;
            // A world-space canvas is readable from behind its forward axis, so it looks the same way as the player.
            rotation = Quaternion.LookRotation(forward);
        }
    }
}
