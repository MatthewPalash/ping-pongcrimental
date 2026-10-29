using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TennisPrototype
{
    [DefaultExecutionOrder(90)]
    [DisallowMultipleComponent]
    public sealed class PaddleShapeTrail : MonoBehaviour
    {
        [Header("Shape trail")]
        [SerializeField] private bool trailEnabled = true;
        [SerializeField] private Color trailColor = new Color(1f, 0.32f, 0.35f, 0.28f);
        [SerializeField, Min(0.01f)] private float lifetime = 0.16f;
        [SerializeField, Range(1, 24)] private int maxGhosts = 8;
        [SerializeField, Min(0.005f)] private float sampleInterval = 0.025f;
        [SerializeField, Min(0.001f)] private float sampleDistance = 0.025f;
        [SerializeField, Min(0.1f)] private float sampleAngle = 4f;

        private struct Ghost
        {
            public Matrix4x4 Matrix;
            public float CreatedAt;
        }

        private readonly List<Ghost> ghosts = new List<Ghost>(12);
        private MaterialPropertyBlock properties;
        [Header("Mesh reference")]
        [SerializeField] private Transform paddleMesh;
        [SerializeField] private Material ghostMaterial;
        private Mesh mesh;
        private EnemyPaddleController enemyPaddle;
        private Matrix4x4 lastMatrix;
        private Vector3 lastPosition;
        private Vector3 lastScale;
        private Quaternion lastRotation;
        private float lastSampleTime;
        private bool hasLastSample;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            enemyPaddle = GetComponent<EnemyPaddleController>();
            FindPaddleMesh();
        }

        private void OnEnable()
        {
            hasLastSample = false;
            ghosts.Clear();
        }

        private void OnDisable()
        {
            ghosts.Clear();
            hasLastSample = false;
        }

        private void Update()
        {
            if (!trailEnabled || (enemyPaddle != null && enemyPaddle.IsHitPulseActive))
            {
                ghosts.Clear();
                hasLastSample = false;
                return;
            }

            if (paddleMesh == null || mesh == null) FindPaddleMesh();
            if (paddleMesh == null || mesh == null || ghostMaterial == null) return;
            if (!paddleMesh.gameObject.activeInHierarchy)
            {
                ghosts.Clear();
                hasLastSample = false;
                return;
            }
            if (properties == null) properties = new MaterialPropertyBlock();

            float now = Time.unscaledTime;
            for (int i = ghosts.Count - 1; i >= 0; i--)
                if (now - ghosts[i].CreatedAt >= lifetime) ghosts.RemoveAt(i);

            Vector3 currentPosition = paddleMesh.position;
            Quaternion currentRotation = paddleMesh.rotation;
            Vector3 currentScale = paddleMesh.lossyScale;
            Matrix4x4 currentMatrix = paddleMesh.localToWorldMatrix;
            if (!hasLastSample)
            {
                RememberCurrent(currentMatrix, currentPosition, currentRotation, currentScale, now);
                return;
            }

            bool moved = Vector3.Distance(currentPosition, lastPosition) >= sampleDistance ||
                Quaternion.Angle(currentRotation, lastRotation) >= sampleAngle ||
                Vector3.Distance(currentScale, lastScale) >= 0.01f;
            if (moved && now - lastSampleTime >= sampleInterval)
            {
                ghosts.Add(new Ghost { Matrix = lastMatrix, CreatedAt = now });
                if (ghosts.Count > maxGhosts) ghosts.RemoveAt(0);
                RememberCurrent(currentMatrix, currentPosition, currentRotation, currentScale, now);
            }

            for (int i = 0; i < ghosts.Count; i++)
            {
                Ghost ghost = ghosts[i];
                float remaining = 1f - (now - ghost.CreatedAt) / lifetime;
                Color color = trailColor;
                color.a *= remaining * remaining;
                properties.Clear();
                properties.SetColor("_BaseColor", color);
                var render = new RenderParams(ghostMaterial)
                {
                    layer = paddleMesh.gameObject.layer,
                    matProps = properties,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false
                };
                Graphics.RenderMesh(render, mesh, 0, ghost.Matrix);
            }
        }

        private void RememberCurrent(Matrix4x4 matrix, Vector3 position,
            Quaternion rotation, Vector3 scale, float time)
        {
            lastMatrix = matrix;
            lastPosition = position;
            lastRotation = rotation;
            lastScale = scale;
            lastSampleTime = time;
            hasLastSample = true;
        }

        private void FindPaddleMesh()
        {
            if (paddleMesh == null)
            {
                var controller = GetComponent<PaddleController>();
                PaddleHitDetector detector = controller != null ? controller.HitDetector : null;
                if (detector != null) paddleMesh = detector.transform;
                else
                {
                    var enemyPaddle = GetComponent<EnemyPaddleController>();
                    if (enemyPaddle != null) paddleMesh = enemyPaddle.PaddleMeshTransform;
                }
            }
            MeshFilter filter = paddleMesh != null ? paddleMesh.GetComponent<MeshFilter>() : null;
            mesh = filter != null ? filter.sharedMesh : null;
        }

        private void OnValidate()
        {
            lifetime = Mathf.Max(0.01f, lifetime);
            sampleInterval = Mathf.Max(0.005f, sampleInterval);
            sampleDistance = Mathf.Max(0.001f, sampleDistance);
            sampleAngle = Mathf.Max(0.1f, sampleAngle);
            maxGhosts = Mathf.Clamp(maxGhosts, 1, 24);
        }
    }
}
