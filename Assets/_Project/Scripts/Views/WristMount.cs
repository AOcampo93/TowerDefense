using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Wears the radar like a real watch on the back of the left wrist: it sits on the forearm and its face looks out
    /// of the back of the hand, so the player turns the wrist to read it (RadarView keeps "ahead" at the top of the
    /// picture). With body tracking it follows the tracked wrist and hand; without it, the controller held in that
    /// hand.
    /// </summary>
    public class WristMount : MonoBehaviour
    {
        [SerializeField] private OVRSkeleton _skeleton;
        [Tooltip("Left controller anchor, used when there is no body tracking.")]
        [SerializeField] private Transform _controller;
        [Tooltip("Meters from the wrist joint towards the elbow (where a watch is worn).")]
        [SerializeField] private float _alongForearm = 0.06f;
        [Tooltip("Meters from the middle of the forearm out to the back of the wrist, so the watch rests on it.")]
        [SerializeField] private float _aboveWrist = 0.04f;

        [Header("Without body tracking")]
        [Tooltip("Wrist position in the controller's own space (behind the grip).")]
        [SerializeField] private Vector3 _controllerWrist = new Vector3(-0.015f, -0.01f, -0.1f);
        [Tooltip("Direction the back of the left hand faces, in the controller's own space.")]
        [SerializeField] private Vector3 _controllerBackOfHand = new Vector3(-0.75f, 0.65f, 0f);

        private Transform _wristBone;
        private Transform _elbowBone;
        private Transform _indexBone;
        private Transform _littleBone;

        private void LateUpdate()
        {
            Vector3 wrist = Vector3.zero;
            Vector3 towardElbow = Vector3.zero;
            Vector3 backOfHand = Vector3.zero;
            if (_skeleton.IsInitialized && _skeleton.IsDataValid && FindBones())
            {
                wrist = _wristBone.position;
                towardElbow = (_elbowBone.position - wrist).normalized;
                // For a left hand, little x index (both measured from the wrist) points out of the back of the hand.
                backOfHand = Vector3.Cross(_littleBone.position - wrist, _indexBone.position - wrist).normalized;
            }
            // Right after the headset is put back on, the tracked joints can sit on one point for a few frames and
            // give no direction (normalized zero vectors): the watch follows the controller meanwhile.
            if (towardElbow.sqrMagnitude < 0.5f || backOfHand.sqrMagnitude < 0.5f)
            {
                wrist = _controller.TransformPoint(_controllerWrist);
                towardElbow = -_controller.forward;
                backOfHand = _controller.TransformDirection(_controllerBackOfHand.normalized);
            }

            // Fingers direction, kept square to the face so the dial is not skewed.
            Vector3 fingers = Vector3.ProjectOnPlane(-towardElbow, backOfHand).normalized;
            transform.position = wrist + towardElbow * _alongForearm + backOfHand * _aboveWrist;
            // The dial's visible side is its local -Z (it faces whoever looks along +Z), so +Z goes into the wrist.
            transform.rotation = Quaternion.LookRotation(-backOfHand, fingers);
        }

        // Looks the joints up once, after the skeleton exists.
        private bool FindBones()
        {
            if (_wristBone != null && _elbowBone != null && _indexBone != null && _littleBone != null)
            {
                return true;
            }
            foreach (OVRBone bone in _skeleton.Bones)
            {
                switch (bone.Id)
                {
                    case OVRSkeleton.BoneId.FullBody_LeftHandWrist: _wristBone = bone.Transform; break;
                    case OVRSkeleton.BoneId.FullBody_LeftArmLower: _elbowBone = bone.Transform; break;
                    case OVRSkeleton.BoneId.FullBody_LeftHandIndexProximal: _indexBone = bone.Transform; break;
                    case OVRSkeleton.BoneId.FullBody_LeftHandLittleProximal: _littleBone = bone.Transform; break;
                }
            }
            return _wristBone != null && _elbowBone != null && _indexBone != null && _littleBone != null;
        }
    }
}
