using UnityEngine;
using UnityEngine.Rendering;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyBloodVfx : MonoBehaviour
    {
        [Header("Blood assets")]
        [SerializeField] private Material dropletMaterial;
        [SerializeField] private Material[] splatMaterials;

        [Header("Burst")]
        [SerializeField] private Vector3 emissionOffset = new Vector3(0f, 0.18f, -0.22f);
        [SerializeField, Range(2, 6)] private int jetCount = 3;
        [SerializeField, Range(1, 30)] private int dropletsPerJet = 16;
        [SerializeField, Min(0f)] private float lateralSpeed = 0.85f;
        [SerializeField, Min(0f)] private float forwardSpeed = 2.6f;
        [SerializeField, Min(0f)] private float upwardSpeed = 0.9f;
        [SerializeField, Min(0f)] private float velocityJitter = 0.45f;
        [SerializeField, Min(0f)] private float gravityMultiplier = 0.55f;
        [SerializeField] private Vector2 dropletSize = new Vector2(0.045f, 0.085f);
        [SerializeField] private Vector2 dropletLifetime = new Vector2(0.8f, 1.5f);

        [Header("Permanent stains")]
        [SerializeField] private LayerMask stainSurfaces = 1;
        [SerializeField] private Vector2 stainSize = new Vector2(0.08f, 0.19f);
        [SerializeField, Range(0f, 1f)] private float stainChance = 0.85f;

        private EnemyController enemy;
        private bool fired;

        private void OnEnable()
        {
            enemy = GetComponent<EnemyController>();
            fired = false;
            enemy.Died += SpawnBurst;
        }

        private void OnDisable()
        {
            if (enemy != null) enemy.Died -= SpawnBurst;
        }

        private void SpawnBurst()
        {
            if (fired) return;
            fired = SpawnBlood();
        }

        public bool SpawnBlood()
        {
            if (dropletMaterial == null || splatMaterials == null || splatMaterials.Length == 0)
                return false;

            GameObject effect = new GameObject("Blood Burst");
            effect.transform.position = transform.TransformPoint(emissionOffset);
            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startSize = 0.06f;
            main.startLifetime = 1.5f;
            main.gravityModifier = gravityMultiplier;
            main.maxParticles = Mathf.Max(128, jetCount * dropletsPerJet + 8);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            ParticleSystem.CollisionModule collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.collidesWith = stainSurfaces;
            collision.sendCollisionMessages = true;
            collision.bounce = 0f;
            collision.dampen = 1f;
            collision.lifetimeLoss = 1f;
            collision.radiusScale = 0.3f;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = dropletMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            PixelBloodEmitter receiver = effect.AddComponent<PixelBloodEmitter>();
            receiver.Configure(transform, splatMaterials, stainSize, stainChance);

            particles.Play();
            int jets = Mathf.Max(2, jetCount);
            for (int jet = 0; jet < jets; jet++)
            {
                float side = Mathf.Lerp(-lateralSpeed, lateralSpeed, jet / (float)(jets - 1));
                Vector3 direction = -transform.forward * forwardSpeed +
                    transform.right * side + Vector3.up * upwardSpeed;
                for (int drop = 0; drop < dropletsPerJet; drop++)
                {
                    ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
                    {
                        velocity = direction + Random.insideUnitSphere * velocityJitter,
                        startSize = Random.Range(Mathf.Min(dropletSize.x, dropletSize.y),
                            Mathf.Max(dropletSize.x, dropletSize.y)),
                        startLifetime = Random.Range(Mathf.Min(dropletLifetime.x, dropletLifetime.y),
                            Mathf.Max(dropletLifetime.x, dropletLifetime.y)),
                        startColor = Color.white
                    };
                    particles.Emit(particle, 1);
                }
            }

            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(effect, Mathf.Max(dropletLifetime.x, dropletLifetime.y) + 1f);
            return true;
        }
    }
}
