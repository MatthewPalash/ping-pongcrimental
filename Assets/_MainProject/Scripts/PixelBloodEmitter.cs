using System.Collections.Generic;
using UnityEngine;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    public sealed class PixelBloodEmitter : MonoBehaviour
    {
        private static Mesh quad;
        private static readonly List<GameObject> stains = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => stains.Clear();

        public static void ClearMatchBlood()
        {
            foreach (PixelBloodEmitter emitter in FindObjectsByType<PixelBloodEmitter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                emitter.enabled = false;
                Destroy(emitter.gameObject);
            }
            foreach (GameObject stain in stains)
                if (stain != null) Destroy(stain);
            stains.Clear();
        }

        private readonly List<ParticleCollisionEvent> collisions = new List<ParticleCollisionEvent>();
        private ParticleSystem particles;
        private Transform sourceEnemy;
        private Material[] splatMaterials;
        private Vector2 stainSize;
        private float stainChance;

        public void Configure(Transform source, Material[] materials, Vector2 size, float chance)
        {
            particles = GetComponent<ParticleSystem>();
            sourceEnemy = source;
            splatMaterials = materials;
            stainSize = size;
            stainChance = chance;
        }

        private void OnParticleCollision(GameObject surface)
        {
            if (particles == null || splatMaterials == null || surface == null) return;
            if (sourceEnemy != null && surface.transform.IsChildOf(sourceEnemy)) return;
            if (surface.GetComponentInParent<PaddleController>() != null) return;

            int count = particles.GetCollisionEvents(surface, collisions);
            for (int i = 0; i < count; i++)
            {
                ParticleCollisionEvent hit = collisions[i];
                if (hit.normal.sqrMagnitude < 0.5f || Random.value > stainChance) continue;
                PlaceStain(surface.transform, hit.intersection, hit.normal);
            }
        }

        private void PlaceStain(Transform surface, Vector3 point, Vector3 normal)
        {
            Material material = splatMaterials[Random.Range(0, splatMaterials.Length)];
            if (material == null) return;

            GameObject stain = new GameObject("Blood Stain");
            stains.Add(stain);
            stain.transform.SetPositionAndRotation(point + normal * 0.006f,
                Quaternion.LookRotation(normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            float size = Random.Range(Mathf.Min(stainSize.x, stainSize.y),
                Mathf.Max(stainSize.x, stainSize.y));
            stain.transform.localScale = Vector3.one * size;
            stain.transform.SetParent(surface, true);
            stain.AddComponent<MeshFilter>().sharedMesh = GetQuad();
            stain.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh GetQuad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "Blood Quad" };
            quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f),
                new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f)
            };
            quad.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 0f)
            };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return quad;
        }
    }
}
