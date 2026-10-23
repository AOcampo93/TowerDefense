using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Keeps the radar watch on the player's real left wrist. With body tracking it sits on the forearm, just behind
    /// the tracked wrist joint; without tracking data it falls back to its place relative to the controller.
    /// </summary>
    public class WristMount : MonoBehaviour
    {
        [SerializeField] private OVRSkeleton _skeleton;
        [SerializeField] private OVRSkeleton.BoneId _wrist = OVRSkeleton.BoneId.FullBody_LeftHandWrist;
        [SerializeField] private OVRSkeleton.BoneId _elbow = OVRSkeleton.BoneId.FullBody_LeftArmLower;
        [Tooltip("Meters from the wrist joint towards the elbow (where a watch is worn).")]
        [SerializeField] private float _alongForearm = 0.05f;
        [Tooltip("Meters above the forearm, so the watch rests on it instead of inside it.")]
        [SerializeField] private float _aboveForearm = 0.045f;

        private Vector3 _fallbackLocalPosition;
        private Transform _wristBone;
        private Transform _elbowBone;

        private void Awake()
        {
            _fallbackLocalPosition = transform.localPosition;
        }

        private void LateUpdate()
        {
            if (_skeleton.IsInitialized && _skeleton.IsDataValid && FindBones())
            {
                Vector3 wrist = _wristBone.position;
                Vector3 towardElbow = (_elbowBone.position - wrist).normalized;
                transform.position = wrist + towardElbow * _alongForearm + Vector3.up * _aboveForearm;
            }
            else
            {
                transform.localPosition = _fallbackLocalPosition;
            }
        }

        // Looks the two joints up once, after the skeleton exists.
        private bool FindBones()
        {
            if (_wristBone != null && _elbowBone != null)
            {
                return true;
            }
            foreach (OVRBone bone in _skeleton.Bones)
            {
                if (bone.Id == _wrist)
                {
                    _wristBone = bone.Transform;
                }
                else if (bone.Id == _elbow)
                {
                    _elbowBone = bone.Transform;
                }
            }
            return _wristBone != null && _elbowBone != null;
        }
    }
}
