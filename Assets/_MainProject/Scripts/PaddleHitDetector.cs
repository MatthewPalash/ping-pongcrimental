using System;
using UnityEngine;

namespace TennisPrototype
{
    public interface IPaddleHitReceiver
    {
        void OnPaddleHit(PaddleHitDetector paddle, Vector3 contactPoint);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
    public sealed class PaddleHitDetector : MonoBehaviour
    {
        public event Action<IPaddleHitReceiver, Vector3> Hit;
        public MeshCollider Collider => GetComponent<MeshCollider>();

        public Vector2 SurfaceSize
        {
            get
            {
                Mesh mesh = Collider.sharedMesh;
                if (mesh == null) return Vector2.zero;
                Vector3 scale = transform.lossyScale;
                return new Vector2(mesh.bounds.size.x * Mathf.Abs(scale.x),
                    mesh.bounds.size.y * Mathf.Abs(scale.y));
            }
        }

        public float NormalizedHorizontalPosition(Vector3 worldPoint)
        {
            Mesh mesh = Collider.sharedMesh;
            if (mesh == null || mesh.bounds.extents.x < 0.000001f) return 0f;
            float localX = transform.InverseTransformPoint(worldPoint).x;
            return Mathf.Clamp((localX - mesh.bounds.center.x) / mesh.bounds.extents.x, -1f, 1f);
        }

        public void SetSurfaceSize(float width, float height)
        {
            Vector2 current = SurfaceSize;
            if (current.x < 0.0001f || current.y < 0.0001f) return;
            Vector3 scale = transform.localScale;
            scale.x *= Mathf.Max(0.05f, width) / current.x;
            scale.y *= Mathf.Max(0.05f, height) / current.y;
            transform.localScale = scale;
        }

        private void Reset() => ConfigureCollider();
        private void Awake() => ConfigureCollider();

        private void ConfigureCollider()
        {
            MeshCollider collider = Collider;
            if (collider.sharedMesh == null && TryGetComponent(out MeshFilter filter))
                collider.sharedMesh = filter.sharedMesh;
            collider.convex = true;
            collider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            foreach (var behaviour in other.GetComponentsInParent<MonoBehaviour>())
            {
                if (behaviour is not IPaddleHitReceiver receiver || !behaviour.isActiveAndEnabled)
                    continue;

                Vector3 point = Collider.ClosestPoint(other.bounds.center);
                receiver.OnPaddleHit(this, point);
                Hit?.Invoke(receiver, point);
                break;
            }
        }
    }
}
