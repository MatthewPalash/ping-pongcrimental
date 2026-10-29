using UnityEngine;

namespace TennisPrototype
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PaddleController))]
    public sealed class PaddleImpactFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ArcadeBallController ball;
        [SerializeField] private PaddleController paddle;

        [Header("Paddle impact")]
        [SerializeField, Min(0.01f)] private float visualDuration = 0.18f;
        [SerializeField] private bool shakeEnabled = true;
        [SerializeField, Range(0f, 0.05f)] private float shakeDistance = 0.012f;
        [SerializeField, Range(0f, 10f)] private float shakeAngle = 3f;
        [SerializeField] private bool scalePulseEnabled = true;
        [SerializeField, Range(0f, 0.2f)] private float scaleBoost = 0.05f;

        [Header("Hit slow motion")]
        [SerializeField] private bool slowMotionEnabled = true;
        [SerializeField, Range(0.05f, 1f)] private float slowMotionScale = 0.35f;
        [SerializeField, Min(0.01f)] private float slowMotionDuration = 0.09f;

        private Transform paddleMesh;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private Quaternion lastControlRotation;
        private float visualElapsed;
        private bool visualActive;
        private float slowMotionElapsed;
        private float previousTimeScale;
        private float appliedTimeScale;
        private bool slowMotionActive;

        public void Configure(ArcadeBallController ballController, PaddleController controller)
        {
            if (Application.isPlaying && isActiveAndEnabled && ball != null)
                ball.PlayerHitBall -= OnPlayerHit;
            ball = ballController;
            paddle = controller;
            FindPaddleMesh();
            if (Application.isPlaying && isActiveAndEnabled && ball != null)
                ball.PlayerHitBall += OnPlayerHit;
        }

        private void Awake()
        {
            if (paddle == null) paddle = GetComponent<PaddleController>();
            FindPaddleMesh();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (ball == null) ball = FindFirstObjectByType<ArcadeBallController>();
            if (ball != null) ball.PlayerHitBall += OnPlayerHit;
        }

        private void OnDisable()
        {
            if (ball != null) ball.PlayerHitBall -= OnPlayerHit;
            RestoreVisual();
            RestoreTimeScale();
        }

        private void Update()
        {
            if (paddleMesh == null) FindPaddleMesh();
            if (paddleMesh != null)
            {
                if (visualActive) UpdateVisual();
                else
                {
                    baseLocalPosition = paddleMesh.localPosition;
                    baseLocalScale = paddleMesh.localScale;
                    lastControlRotation = paddleMesh.localRotation;
                }
            }

            if (!slowMotionActive) return;
            slowMotionElapsed += Time.unscaledDeltaTime;
            if (!slowMotionEnabled || slowMotionElapsed >= slowMotionDuration)
                RestoreTimeScale();
        }

        private void FindPaddleMesh()
        {
            if (paddle == null) paddle = GetComponent<PaddleController>();
            PaddleHitDetector detector = paddle != null ? paddle.HitDetector : null;
            paddleMesh = detector != null ? detector.transform : null;
            if (paddleMesh == null) return;
            baseLocalPosition = paddleMesh.localPosition;
            baseLocalScale = paddleMesh.localScale;
            lastControlRotation = paddleMesh.localRotation;
        }

        private void OnPlayerHit(Vector3 contact, Vector3 destination)
        {
            if (shakeEnabled || scalePulseEnabled)
            {
                if (paddleMesh == null) FindPaddleMesh();
                visualActive = paddleMesh != null;
                visualElapsed = 0f;
            }

            if (!slowMotionEnabled) return;
            if (!slowMotionActive) previousTimeScale = Time.timeScale;
            slowMotionElapsed = 0f;
            appliedTimeScale = Mathf.Min(Time.timeScale, slowMotionScale);
            Time.timeScale = appliedTimeScale;
            slowMotionActive = true;
        }

        private void UpdateVisual()
        {
            visualElapsed += Time.unscaledDeltaTime;
            float fraction = Mathf.Clamp01(visualElapsed / visualDuration);
            float fade = 1f - fraction;
            lastControlRotation = paddleMesh.localRotation;

            if (shakeEnabled)
            {
                float wobble = visualElapsed * 75f;
                paddleMesh.localPosition = baseLocalPosition + new Vector3(
                    Mathf.Sin(wobble) * shakeDistance * fade,
                    Mathf.Cos(wobble * 1.3f) * shakeDistance * 0.5f * fade,
                    0f);
                paddleMesh.localRotation = lastControlRotation * Quaternion.Euler(
                    Mathf.Sin(wobble) * shakeAngle * fade, 0f,
                    Mathf.Cos(wobble * 0.8f) * shakeAngle * fade);
            }

            float pulse = scalePulseEnabled ? Mathf.Sin(fraction * Mathf.PI) * scaleBoost : 0f;
            paddleMesh.localScale = baseLocalScale * (1f + pulse);

            if (fraction >= 1f) RestoreVisual();
        }

        private void RestoreVisual()
        {
            if (!visualActive) return;
            visualActive = false;
            if (paddleMesh == null) return;
            paddleMesh.localPosition = baseLocalPosition;
            paddleMesh.localScale = baseLocalScale;
            paddleMesh.localRotation = lastControlRotation;
        }

        private void RestoreTimeScale()
        {
            if (!slowMotionActive) return;
            slowMotionActive = false;
            if (Mathf.Approximately(Time.timeScale, appliedTimeScale))
                Time.timeScale = previousTimeScale;
        }

        private void OnValidate()
        {
            visualDuration = Mathf.Max(0.01f, visualDuration);
            slowMotionDuration = Mathf.Max(0.01f, slowMotionDuration);
            slowMotionScale = Mathf.Clamp(slowMotionScale, 0.05f, 1f);
        }
    }
}
