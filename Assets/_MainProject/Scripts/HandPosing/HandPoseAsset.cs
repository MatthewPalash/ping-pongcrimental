using UnityEngine;

namespace TennisPrototype.HandPosing
{
    [System.Serializable]
    public struct FingerPose
    {
        [Range(0, 1)] public float curl;
        [Range(-25, 105)] public float baseOffset;
        [Range(-25, 115)] public float middleOffset;
        [Range(-25, 100)] public float tipOffset;
        [Range(-45, 45)] public float spread;

        public override int GetHashCode()
        {
            unchecked { int h = curl.GetHashCode(); h = h * 397 ^ baseOffset.GetHashCode(); h = h * 397 ^ middleOffset.GetHashCode(); h = h * 397 ^ tipOffset.GetHashCode(); return h * 397 ^ spread.GetHashCode(); }
        }
    }

    public enum HandPreset { Open, Relaxed, Fist, PaddleGrip, Point }

    [System.Serializable]
    public struct HandPoseData
    {
        public Vector3 wristEuler;
        [Range(-35, 80)] public float thumbOpposition;
        public FingerPose thumb, index, middle, ring, little;

        public override int GetHashCode()
        {
            unchecked { int h = wristEuler.GetHashCode(); h = h * 397 ^ thumbOpposition.GetHashCode(); h = h * 397 ^ thumb.GetHashCode(); h = h * 397 ^ index.GetHashCode(); h = h * 397 ^ middle.GetHashCode(); h = h * 397 ^ ring.GetHashCode(); return h * 397 ^ little.GetHashCode(); }
        }

        public static HandPoseData Preset(HandPreset preset)
        {
            var p = new HandPoseData();
            switch (preset)
            {
                case HandPreset.Open:
                    break;
                case HandPreset.Relaxed:
                    p.index.curl = .18f; p.middle.curl = .24f; p.ring.curl = .30f; p.little.curl = .36f;
                    p.thumb.curl = .12f; p.thumbOpposition = 10;
                    break;
                case HandPreset.Fist:
                    p.index.curl = p.middle.curl = p.ring.curl = p.little.curl = 1;
                    p.thumb.curl = .35f; p.thumbOpposition = 45; p.thumb.spread = -35;
                    break;
                case HandPreset.PaddleGrip:
                    p.index.curl = .82f; p.middle.curl = .88f; p.ring.curl = .91f; p.little.curl = .94f;
                    p.thumb.curl = .20f; p.thumbOpposition = 35; p.thumb.spread = -35;
                    p.index.spread = 3; p.little.spread = -4;
                    break;
                case HandPreset.Point:
                    p.middle.curl = p.ring.curl = p.little.curl = 1;
                    p.thumb.curl = .25f; p.thumbOpposition = 30; p.thumb.spread = -25;
                    break;
            }
            return p;
        }
    }

    [CreateAssetMenu(menuName = "Tennis/Hand Pose", fileName = "HandPose")]
    public sealed class HandPoseAsset : ScriptableObject
    {
        public HandPoseData pose;
    }
}
