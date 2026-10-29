using UnityEngine;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PaddleShapeTrail))]
    public sealed class EnemyPaddleController : MonoBehaviour
    {
        [SerializeField] private Vector3 restPosition = new Vector3(0f, 1.26f, 2.02f);
        [SerializeField, Min(0.1f)] private float followSpeed = 10f;
        [SerializeField, HideInInspector] private float swingAngle = 20f;
        [SerializeField, HideInInspector] private float swingRecovery = 12f;
        [SerializeField] private Transform paddleRoot;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform paddleMesh;

        [Header("Side switching")]
        [SerializeField, Min(0f)] private float turnHalfWidth = 0.04f;
        [SerializeField, Range(0f, 180f)] private float turnAngle = 180f;
        [SerializeField, Min(1f)] private float turnSpeedDegrees = 540f;

        [Header("Soft visual motion")]
        [SerializeField, Min(1f)] private float visualSpringStiffness = 125f;
        [SerializeField, Min(0f)] private float visualSpringDamping = 14f;
        [SerializeField, Range(0f, 20f)] private float velocityTiltDegrees = 4f;

        [Header("Hit pulse")]
        [SerializeField, Min(0.01f)] private float pulseDuration = 0.18f;
        [SerializeField, Range(0f, 0.2f)] private float pulseScaleBoost = 0.05f;

        [Header("Normal strike")]
        [SerializeField, Min(0f)] private float normalWindupDistance = 0.05f;
        [SerializeField, Min(0f)] private float normalStrikeDistance = 0.05f;
        [SerializeField, Min(0.01f)] private float normalWindupDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float normalStrikeDuration = 0.07f;

        [Header("Strong strike")]
        [SerializeField, Min(0f)] private float strongWindupDistance = 0.32f;
        [SerializeField, Min(0f)] private float strongStrikeDistance = 0.20f;
        [SerializeField, Min(0.01f)] private float strongWindupDuration = 0.55f;
        [SerializeField, Min(0.01f)] private float strongStrikeDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float strikeReturnDuration = 0.25f;

        private Vector3 targetPosition;
        private float strikeOffset;
        private float returnElapsed = -1f;
        private float returnStartOffset;
        private bool preparedStrongStrike;
        private bool entranceControlled;
        private bool poseCached;
        private bool rightSideActive;
        private float currentTurnAngle;
        private float pulseElapsed = -1f;
        private Vector3 visualAngles;
        private Vector3 visualAngularVelocity;
        private Quaternion visualBaseRotation;
        private Quaternion meshBaseRotation;
        private Vector3 meshBaseScale;
        private Vector3 visualBasePosition;

        public Vector3 RestPosition => restPosition;
        public Vector3 ServePoint => Paddle.position + new Vector3(0f, 0.04f, -0.12f + strikeOffset);
        public float StrikePreparationDuration => preparedStrongStrike
            ? strongWindupDuration + strongStrikeDuration : normalWindupDuration + normalStrikeDuration;
        public float StrikeForwardDistance => preparedStrongStrike ? strongStrikeDistance : normalStrikeDistance;
        public Transform PaddleTransform => Paddle;
        public bool IsHitPulseActive => pulseElapsed >= 0f;
        public Transform PaddleMeshTransform
        {
            get
            {
                FindPaddleMesh();
                return paddleMesh;
            }
        }

        private Transform Paddle => paddleRoot != null ? paddleRoot : transform.Find("Paddle");

        public void Configure(Transform paddle, Transform visual)
        {
            paddleRoot = paddle;
            visualRoot = visual;
            poseCached = false;
            ResetPaddle();
        }

        public void ApplyConfig(EnemyConfig config)
        {
            if (config == null) return;
            followSpeed = config.PaddleFollowSpeed;
        }

        private void Awake() => ResetPaddle();

        private void Update()
        {
            if (Paddle == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Vector3 before = Paddle.position;
            if (!entranceControlled)
                Paddle.position = Vector3.Lerp(Paddle.position, targetPosition,
                    1f - Mathf.Exp(-followSpeed * dt));
            if (returnElapsed >= 0f)
            {
                returnElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(returnElapsed / Mathf.Max(0.01f, strikeReturnDuration));
                strikeOffset = Mathf.Lerp(returnStartOffset, 0f, Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f) returnElapsed = -1f;
            }
            UpdateVisualMotion(dt, (Paddle.position - before) / Mathf.Max(dt, 0.001f));
        }

        public Vector3 AimReturn(float landingX)
        {
            targetPosition = new Vector3(Mathf.Clamp(landingX, -0.55f, 0.55f), restPosition.y, restPosition.z);
            return targetPosition + new Vector3(0f, 0.04f, -0.12f);
        }

        public Vector3 GetReturnPoint(float landingX) =>
            new Vector3(Mathf.Clamp(landingX, -0.55f, 0.55f), restPosition.y + 0.04f, restPosition.z - 0.12f);

        public void Swing()
        {
            strikeOffset = -StrikeForwardDistance;
            returnStartOffset = strikeOffset;
            returnElapsed = 0f;
            pulseElapsed = 0f;
        }

        public void PrepareStrike(bool strong)
        {
            preparedStrongStrike = strong;
            returnElapsed = -1f;
            strikeOffset = 0f;
        }

        public void CancelStrike()
        {
            returnStartOffset = strikeOffset;
            returnElapsed = 0f;
        }

        public void SetStrikeProgress(float progress)
        {
            float windup = preparedStrongStrike ? strongWindupDuration : normalWindupDuration;
            float split = windup / Mathf.Max(0.02f, StrikePreparationDuration);
            float t = Mathf.Clamp01(progress);
            float back = preparedStrongStrike ? strongWindupDistance : normalWindupDistance;
            float forward = StrikeForwardDistance;
            strikeOffset = t < split
                ? Mathf.Lerp(0f, back, Mathf.SmoothStep(0f, 1f, t / split))
                : Mathf.Lerp(back, -forward, Mathf.SmoothStep(0f, 1f, (t - split) / (1f - split)));
        }

        public void HideForEntrance()
        {
            entranceControlled = true;
            if (Paddle != null) Paddle.gameObject.SetActive(false);
        }

        public void BeginHandRise(Vector3 battlePosition, float depth)
        {
            restPosition = battlePosition;
            targetPosition = restPosition;
            entranceControlled = true;
            if (Paddle == null) return;
            Paddle.position = restPosition - Vector3.up * depth;
            Paddle.gameObject.SetActive(true);
        }

        public void SetHandRiseProgress(float progress, float depth)
        {
            if (Paddle != null)
                Paddle.position = restPosition - Vector3.up * (depth * (1f - progress));
        }

        public void FinishHandRise()
        {
            if (Paddle != null) Paddle.position = restPosition;
            entranceControlled = false;
        }

        public void HidePaddle()
        {
            entranceControlled = true;
            if (Paddle != null) Paddle.gameObject.SetActive(false);
        }

        public void ResetPaddle()
        {
            CacheVisualPose();
            targetPosition = restPosition;
            entranceControlled = false;
            if (Paddle != null) Paddle.position = restPosition;
            strikeOffset = 0f;
            returnElapsed = -1f;
            preparedStrongStrike = false;
            visualAngles = Vector3.zero;
            visualAngularVelocity = Vector3.zero;
            rightSideActive = restPosition.x > turnHalfWidth;
            currentTurnAngle = rightSideActive ? turnAngle : 0f;
            pulseElapsed = -1f;
            if (visualRoot != null)
            {
                visualRoot.localPosition = visualBasePosition;
                visualRoot.localRotation = Quaternion.AngleAxis(currentTurnAngle, Vector3.up) *
                    visualBaseRotation;
            }
            if (paddleMesh != null)
            {
                paddleMesh.localRotation = meshBaseRotation;
                paddleMesh.localScale = meshBaseScale;
            }
        }

        private void FindPaddleMesh()
        {
            if (paddleMesh != null || visualRoot == null) return;
            Transform namedMesh = visualRoot.Find("TableTennisPaddle");
            if (namedMesh != null && namedMesh.GetComponent<MeshFilter>() != null)
                paddleMesh = namedMesh;
            else
            {
                MeshFilter filter = visualRoot.GetComponentInChildren<MeshFilter>(true);
                if (filter != null) paddleMesh = filter.transform;
            }
        }

        private void CacheVisualPose()
        {
            if (poseCached) return;
            FindPaddleMesh();
            visualBaseRotation = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
            visualBasePosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
            meshBaseRotation = paddleMesh != null ? paddleMesh.localRotation : Quaternion.identity;
            meshBaseScale = paddleMesh != null ? paddleMesh.localScale : Vector3.one;
            poseCached = true;
        }

        private void UpdateVisualMotion(float dt, Vector3 movementVelocity)
        {
            CacheVisualPose();
            Vector3 targetAngles = entranceControlled ? Vector3.zero : new Vector3(
                0f,
                Mathf.Clamp(movementVelocity.x * velocityTiltDegrees * 0.4f, -8f, 8f),
                Mathf.Clamp(-movementVelocity.x * velocityTiltDegrees, -14f, 14f));
            visualAngularVelocity += (targetAngles - visualAngles) * visualSpringStiffness * dt;
            visualAngularVelocity *= Mathf.Exp(-visualSpringDamping * dt);
            visualAngles += visualAngularVelocity * dt;

            if (rightSideActive)
            {
                if (Paddle.position.x < -turnHalfWidth) rightSideActive = false;
            }
            else if (Paddle.position.x > turnHalfWidth)
                rightSideActive = true;
            currentTurnAngle = Mathf.MoveTowards(currentTurnAngle,
                rightSideActive ? turnAngle : 0f, turnSpeedDegrees * dt);
            if (visualRoot != null)
            {
                visualRoot.localPosition = visualBasePosition + Paddle.InverseTransformVector(Vector3.forward * strikeOffset);
                visualRoot.localRotation = Quaternion.AngleAxis(currentTurnAngle, Vector3.up) *
                    visualBaseRotation *
                    Quaternion.Euler(visualAngles.x, visualAngles.y, visualAngles.z);
            }

            if (paddleMesh == null) return;
            float pulse = 0f;
            if (pulseElapsed >= 0f)
            {
                pulseElapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(pulseElapsed / Mathf.Max(0.01f, pulseDuration));
                pulse = Mathf.Sin(progress * Mathf.PI) * pulseScaleBoost;
                if (progress >= 1f) pulseElapsed = -1f;
            }
            paddleMesh.localScale = meshBaseScale * (1f + pulse);
        }

        private void OnValidate()
        {
            followSpeed = Mathf.Max(0.1f, followSpeed);
            swingRecovery = Mathf.Max(0.1f, swingRecovery);
            turnHalfWidth = Mathf.Max(0f, turnHalfWidth);
            turnSpeedDegrees = Mathf.Max(1f, turnSpeedDegrees);
            visualSpringStiffness = Mathf.Max(1f, visualSpringStiffness);
            visualSpringDamping = Mathf.Max(0f, visualSpringDamping);
            pulseDuration = Mathf.Max(0.01f, pulseDuration);
            normalWindupDistance = Mathf.Max(0f, normalWindupDistance);
            normalStrikeDistance = Mathf.Max(0f, normalStrikeDistance);
            normalWindupDuration = Mathf.Max(0.01f, normalWindupDuration);
            normalStrikeDuration = Mathf.Max(0.01f, normalStrikeDuration);
            strongWindupDistance = Mathf.Max(0f, strongWindupDistance);
            strongStrikeDistance = Mathf.Max(0f, strongStrikeDistance);
            strongWindupDuration = Mathf.Max(0.01f, strongWindupDuration);
            strongStrikeDuration = Mathf.Max(0.01f, strongStrikeDuration);
            strikeReturnDuration = Mathf.Max(0.01f, strikeReturnDuration);
        }
    }
}
