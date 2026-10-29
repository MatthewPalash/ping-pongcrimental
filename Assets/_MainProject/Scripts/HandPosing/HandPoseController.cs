using System;
using System.Linq;
using UnityEngine;

namespace TennisPrototype.HandPosing
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class HandPoseController : MonoBehaviour
    {
        [Serializable]
        public sealed class FingerBinding
        {
            public string name;
            public Transform[] bones = new Transform[3];
            public Quaternion[] rest = new Quaternion[3];
            public Vector3[] curlAxes = new Vector3[3];
            public Vector3 spreadAxis;
        }

        public bool drivePose = true;
        public HandPoseData pose;
        [SerializeField, HideInInspector] private Transform wrist, palm, palmNormal, palmForward, gripSocket;
        [SerializeField, HideInInspector] private Quaternion wristRest;
        [SerializeField, HideInInspector] private Vector3 oppositionAxis;
        [SerializeField, HideInInspector] private FingerBinding[] fingers;
        [NonSerialized] private bool pending = true;
        [NonSerialized] private int lastHash;

        public Transform GripSocket => gripSocket;
        public Transform Palm => palm;
        public bool IsBound => wrist != null && fingers != null && fingers.Length == 5 &&
                               fingers.All(f => f != null && f.bones != null && f.bones.Length == 3 && f.bones.All(b => b != null));
        public Transform[] BoundBones => IsBound ? fingers.SelectMany(f => f.bones).Append(wrist).Append(palm).ToArray() : Array.Empty<Transform>();

        public void BindRig()
        {
            Transform Find(string n) => GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
            wrist = Find("Wrist"); palm = Find("Palm");
            palmNormal = Find("PalmNormal"); palmForward = Find("PalmForward"); gripSocket = Find("GripSocket");
            if (wrist == null || palm == null || palmNormal == null || palmForward == null)
                throw new InvalidOperationException("The hand rig is missing Wrist, Palm or orientation anchors.");
            wristRest = wrist.localRotation;
            Vector3 normal = (palmNormal.position - palm.position).normalized;
            Vector3 forward = (palmForward.position - palm.position).normalized;
            string[] names = { "Thumb", "Index", "Middle", "Ring", "Little" };
            fingers = new FingerBinding[5];
            for (int f = 0; f < names.Length; f++)
            {
                var binding = new FingerBinding { name = names[f] };
                for (int j = 0; j < 3; j++)
                    binding.bones[j] = Find(names[f] + "_" + (j + 1));
                if (binding.bones.Any(b => b == null)) throw new InvalidOperationException("Missing finger bones: " + names[f]);
                for (int j = 0; j < 3; j++)
                {
                    Transform bone = binding.bones[j];
                    Transform end = j < 2 ? binding.bones[j + 1] : Find(names[f] + "Tip");
                    if (end == null) throw new InvalidOperationException("Missing fingertip marker: " + names[f]);
                    Vector3 direction = (end.position - bone.position).normalized;
                    binding.rest[j] = bone.localRotation;
                    binding.curlAxes[j] = bone.InverseTransformDirection(Vector3.Cross(direction, normal).normalized);
                }
                binding.spreadAxis = binding.bones[0].InverseTransformDirection(normal);
                fingers[f] = binding;
            }
            oppositionAxis = fingers[0].bones[0].InverseTransformDirection(forward);
            pending = true;
        }

        public void SetPreset(HandPreset preset) { pose = HandPoseData.Preset(preset); drivePose = true; ApplyPose(); }
        public void LoadPose(HandPoseAsset asset) { if (asset != null) { pose = asset.pose; drivePose = true; ApplyPose(); } }
        public void ApplyPose()
        {
            if (!drivePose || !IsBound) return;
            wrist.localRotation = wristRest * Quaternion.Euler(pose.wristEuler);
            FingerPose[] values = { pose.thumb, pose.index, pose.middle, pose.ring, pose.little };
            for (int f = 0; f < fingers.Length; f++)
            {
                FingerBinding binding = fingers[f]; FingerPose value = values[f];
                float curl = Mathf.Clamp01(value.curl);
                float[] angles = { curl * 70 + value.baseOffset, curl * 95 + value.middleOffset, curl * 70 + value.tipOffset };
                for (int j = 0; j < 3; j++)
                {
                    Quaternion rotation = Quaternion.AngleAxis(angles[j], binding.curlAxes[j]);
                    if (j == 0)
                    {
                        rotation = Quaternion.AngleAxis(value.spread, binding.spreadAxis) * rotation;
                        if (f == 0) rotation = Quaternion.AngleAxis(pose.thumbOpposition, oppositionAxis) * rotation;
                    }
                    binding.bones[j].localRotation = binding.rest[j] * rotation;
                }
            }
            pending = false;
            lastHash = pose.GetHashCode();
        }

        private void OnEnable() { pending = true; }
        private void OnValidate() { pending = true; }
        private void Update() { if (drivePose && (pending || pose.GetHashCode() != lastHash)) ApplyPose(); }
    }
}
