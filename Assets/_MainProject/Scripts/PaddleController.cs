using UnityEngine;
using UnityEngine.InputSystem;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PaddleController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform tableReference;

        [Header("Movement on the table plane")]
        [SerializeField] private Vector2 horizontalLimits = new Vector2(-0.58f, 0.58f);
        [SerializeField] private Vector2 depthLimits = new Vector2(-1.8f, -1.1f);

        [Header("Start position (Table local coordinates)")]
        [SerializeField] private float startX = 0.28f;
        [SerializeField] private float startZ = -1.5f;

        [Header("Mouse sensitivity")]
        [SerializeField, Min(0.1f)] private float horizontalSensitivity = 1f;
        [SerializeField, Min(0.1f)] private float depthSensitivity = 1f;

        [Header("Shot direction input")]
        [SerializeField, Range(0.02f, 0.3f)] private float aimMotionMemory = 0.15f;

        [Header("Extreme poses at Start Depth (Table local space)")]
        [SerializeField] private Vector3 leftExtremeLocalPosition = new Vector3(-0.46f, 0.97f, -1.37f);
        [SerializeField] private Vector3 leftExtremeLocalEuler = new Vector3(0f, -30f, -18f);
        [SerializeField] private Vector3 rightExtremeLocalPosition = new Vector3(0.44f, 0.97f, -1.63f);
        [SerializeField] private Vector3 rightExtremeLocalEuler = new Vector3(0f, 150f, -18f);

        [Header("Side switching")]
        [SerializeField, Min(0.01f), Tooltip("Switch to the right pose only beyond +this X; switch back to the left pose only beyond -this X.")]
        private float wristTurnHalfWidth = 0.12f;
        [SerializeField, Min(1f), Tooltip("How fast the paddle rotates toward the selected side, in degrees per second.")]
        private float wristTurnSpeedDegrees = 540f;

        [Header("Soft visual motion")]
        [SerializeField, Min(1f)] private float visualSpringStiffness = 125f;
        [SerializeField, Min(0f)] private float visualSpringDamping = 14f;
        [SerializeField, Range(0f, 20f)] private float velocityTiltDegrees = 4f;

        private Rigidbody body;
        private PaddleHitDetector hitDetector;
        private Transform visualRoot;
        private Vector2 targetPosition;
        private Vector3 previousWorldPosition;
        private Vector3 visualAngles;
        private Vector3 visualAngularVelocity;
        private Quaternion visualBaseRotation;
        private bool controlEnabled;
        private bool hasFocus = true;
        private bool wasAcceptingInput;
        private bool skipNextMouseDelta = true;
        private bool rightPoseActive;
        private Quaternion currentLocalPose;
        private Vector2 recentAimVelocity;
        private float lastHorizontalAimInputTime = float.NegativeInfinity;
        private float lastDepthAimInputTime = float.NegativeInfinity;

        public bool ControlEnabled => controlEnabled;
        public Vector2 TargetPosition => targetPosition;
        public Vector3 MovementVelocity { get; private set; }
        public Vector2 RecentAimVelocity => new Vector2(
            Time.unscaledTime - lastHorizontalAimInputTime <= aimMotionMemory ? recentAimVelocity.x : 0f,
            Time.unscaledTime - lastDepthAimInputTime <= aimMotionMemory ? recentAimVelocity.y : 0f);
        public PaddleHitDetector HitDetector
        {
            get
            {
                if (hitDetector == null) FindPaddleMesh();
                return hitDetector;
            }
        }
        public Vector2 DepthLimits => depthLimits;

        public void Configure(Camera camera, Transform table = null)
        {
            playerCamera = camera;
            tableReference = table;
            FindPaddleMesh();
            ResetToStartPosition();
        }

        private void FindPaddleMesh()
        {
            hitDetector = GetComponentInChildren<PaddleHitDetector>(true);
            visualRoot = hitDetector != null ? hitDetector.transform : null;
            visualBaseRotation = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            FindPaddleMesh();
            ResetToStartPosition();
        }

        private void OnEnable()
        {
            skipNextMouseDelta = true;
            ApplyCursorState();
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            skipNextMouseDelta = true;
            MovementVelocity = Vector3.zero;
            recentAimVelocity = Vector2.zero;
            lastHorizontalAimInputTime = float.NegativeInfinity;
            lastDepthAimInputTime = float.NegativeInfinity;
            ApplyCursorState();
        }

        private void Update()
        {
            bool acceptingInput = controlEnabled && hasFocus && Application.isFocused;
            if (acceptingInput != wasAcceptingInput)
            {
                wasAcceptingInput = acceptingInput;
                skipNextMouseDelta = true;
                MovementVelocity = Vector3.zero;
                ApplyCursorState();
            }

            if (acceptingInput && Mouse.current != null)
            {
                float verticalDelta = skipNextMouseDelta ? 0f : Mouse.current.delta.ReadValue().y;
                skipNextMouseDelta = false;
                ApplyMousePosition(Mouse.current.position.ReadValue(), verticalDelta);
            }
            else
                MovementVelocity = Vector3.zero;

            if (visualRoot == null) FindPaddleMesh();
            if (visualRoot == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Vector3 targetAngles = new Vector3(
                Mathf.Clamp(-MovementVelocity.z * velocityTiltDegrees, -14f, 14f),
                Mathf.Clamp(MovementVelocity.x * velocityTiltDegrees * 0.4f, -8f, 8f),
                Mathf.Clamp(-MovementVelocity.x * velocityTiltDegrees, -14f, 14f));
            visualAngularVelocity += (targetAngles - visualAngles) * visualSpringStiffness * dt;
            visualAngularVelocity *= Mathf.Exp(-visualSpringDamping * dt);
            visualAngles += visualAngularVelocity * dt;
            visualRoot.localRotation = visualBaseRotation * Quaternion.Euler(visualAngles);
        }

        public void SetControlEnabled(bool enabled)
        {
            controlEnabled = enabled;
            skipNextMouseDelta = true;
            MovementVelocity = Vector3.zero;
            recentAimVelocity = Vector2.zero;
            lastHorizontalAimInputTime = float.NegativeInfinity;
            lastDepthAimInputTime = float.NegativeInfinity;
            ApplyCursorState();
        }

        public void ResetToStartPosition()
        {
            targetPosition = Clamp(new Vector2(startX, startZ));
            skipNextMouseDelta = true;
            rightPoseActive = targetPosition.x >= 0f;
            currentLocalPose = ExtremePoseRotation(rightPoseActive);
            MovementVelocity = Vector3.zero;
            recentAimVelocity = Vector2.zero;
            lastHorizontalAimInputTime = float.NegativeInfinity;
            lastDepthAimInputTime = float.NegativeInfinity;
            visualAngles = Vector3.zero;
            visualAngularVelocity = Vector3.zero;
            Vector3 position = ToWorld(targetPosition);
            Quaternion rotation = PoseRotation();
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
            }
            previousWorldPosition = position;
            if (visualRoot != null) visualRoot.localRotation = visualBaseRotation;
        }

        public void SetPlayingSurfaceSize(float width, float height)
        {
            if (HitDetector == null) return;
            hitDetector.SetSurfaceSize(width, height);
            targetPosition = Clamp(targetPosition);
        }

        private void ApplyMousePosition(Vector2 cursorPosition, float verticalDeltaPixels)
        {
            Vector2 previousTarget = targetPosition;
            float normalizedX = Mathf.Clamp01(cursorPosition.x / Mathf.Max(1f, Screen.width - 1f));
            normalizedX = Mathf.Clamp01(0.5f + (normalizedX - 0.5f) * horizontalSensitivity);
            float depthPerPixel = (depthLimits.y - depthLimits.x) /
                Mathf.Max(1f, Screen.height - 1f);
            targetPosition = new Vector2(
                Mathf.Lerp(horizontalLimits.x, horizontalLimits.y, normalizedX),
                Mathf.Clamp(targetPosition.y + verticalDeltaPixels * depthPerPixel * depthSensitivity,
                    depthLimits.x, depthLimits.y));
            Vector2 inputDelta = targetPosition - previousTarget;
            float inputDeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            if (Mathf.Abs(inputDelta.x) > 0.0001f)
            {
                recentAimVelocity.x = inputDelta.x / inputDeltaTime;
                lastHorizontalAimInputTime = Time.unscaledTime;
            }
            if (Mathf.Abs(inputDelta.y) > 0.0001f)
            {
                recentAimVelocity.y = inputDelta.y / inputDeltaTime;
                lastDepthAimInputTime = Time.unscaledTime;
            }
            MoveImmediately(targetPosition);
        }

        private void MoveImmediately(Vector2 point)
        {
            UpdatePoseSide(point.x);
            AdvancePose(Time.unscaledDeltaTime);
            Vector3 worldPosition = ToWorld(point);
            MovementVelocity = Vector3.ClampMagnitude(
                (worldPosition - previousWorldPosition) / Mathf.Max(Time.unscaledDeltaTime, 0.001f), 10f);
            previousWorldPosition = worldPosition;
            Quaternion rotation = PoseRotation();
            transform.SetPositionAndRotation(worldPosition, rotation);
            if (body != null)
            {
                body.position = worldPosition;
                body.rotation = rotation;
            }
        }

        private void UpdatePoseSide(float horizontalPosition)
        {
            if (rightPoseActive)
            {
                if (horizontalPosition < -wristTurnHalfWidth) rightPoseActive = false;
            }
            else if (horizontalPosition > wristTurnHalfWidth)
            {
                rightPoseActive = true;
            }
        }

        private void AdvancePose(float deltaTime)
        {
            currentLocalPose = Quaternion.RotateTowards(currentLocalPose,
                ExtremePoseRotation(rightPoseActive),
                wristTurnSpeedDegrees * Mathf.Max(0f, deltaTime));
        }

        private Quaternion ExtremePoseRotation(bool right)
        {
            return Quaternion.Euler(right ? rightExtremeLocalEuler : leftExtremeLocalEuler);
        }

        private Vector2 Clamp(Vector2 point)
        {
            return new Vector2(
                Mathf.Clamp(point.x, horizontalLimits.x, horizontalLimits.y),
                Mathf.Clamp(point.y, depthLimits.x, depthLimits.y));
        }

        private Vector3 ToWorld(Vector2 point)
        {
            float horizontal = Mathf.InverseLerp(horizontalLimits.x, horizontalLimits.y, point.x);
            Vector3 localRoot = Vector3.Lerp(leftExtremeLocalPosition, rightExtremeLocalPosition, horizontal);
            localRoot.z += point.y - startZ;
            return tableReference != null ? tableReference.TransformPoint(localRoot) : localRoot;
        }

        private Quaternion PoseRotation()
        {
            Quaternion local = LocalPoseRotation();
            return tableReference != null ? tableReference.rotation * local : local;
        }

        private Quaternion LocalPoseRotation()
        {
            return currentLocalPose;
        }

        private void ApplyCursorState()
        {
            bool active = controlEnabled && hasFocus && Application.isFocused;
            Cursor.lockState = active ? CursorLockMode.Confined : CursorLockMode.None;
            Cursor.visible = !active;
        }

        private void OnValidate()
        {
            horizontalSensitivity = Mathf.Max(0.1f, horizontalSensitivity);
            depthSensitivity = Mathf.Max(0.1f, depthSensitivity);
            aimMotionMemory = Mathf.Clamp(aimMotionMemory, 0.02f, 0.3f);
            wristTurnHalfWidth = Mathf.Max(0.01f, wristTurnHalfWidth);
            wristTurnSpeedDegrees = Mathf.Max(1f, wristTurnSpeedDegrees);
            if (horizontalLimits.x > horizontalLimits.y) horizontalLimits = new Vector2(horizontalLimits.y, horizontalLimits.x);
            if (depthLimits.x > depthLimits.y) depthLimits = new Vector2(depthLimits.y, depthLimits.x);
            startX = Mathf.Clamp(startX, horizontalLimits.x, horizontalLimits.y);
            startZ = Mathf.Clamp(startZ, depthLimits.x, depthLimits.y);
        }

#if UNITY_EDITOR
        public void PreviewStart()
        {
            UnityEditor.Undo.RecordObject(transform, "Preview paddle start position");
            FindPaddleMesh();
            ResetToStartPosition();
            UnityEditor.EditorUtility.SetDirty(transform);
        }

        public void CaptureCurrentPose(bool right)
        {
            UnityEditor.Undo.RecordObject(this, "Capture paddle extreme pose");
            Vector3 localPosition = tableReference != null
                ? tableReference.InverseTransformPoint(transform.position) : transform.position;
            Quaternion localRotation = tableReference != null
                ? Quaternion.Inverse(tableReference.rotation) * transform.rotation : transform.rotation;
            if (right)
            {
                rightExtremeLocalPosition = localPosition;
                rightExtremeLocalEuler = localRotation.eulerAngles;
            }
            else
            {
                leftExtremeLocalPosition = localPosition;
                leftExtremeLocalEuler = localRotation.eulerAngles;
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void PreviewExtreme(bool right)
        {
            UnityEditor.Undo.RecordObject(transform, "Preview paddle extreme pose");
            FindPaddleMesh();
            rightPoseActive = right;
            currentLocalPose = ExtremePoseRotation(right);
            targetPosition = new Vector2(right ? horizontalLimits.y : horizontalLimits.x,
                startZ);
            MoveImmediately(targetPosition);
            UnityEditor.EditorUtility.SetDirty(transform);
        }
#endif

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 a = leftExtremeLocalPosition + Vector3.forward * (depthLimits.x - startZ);
            Vector3 b = rightExtremeLocalPosition + Vector3.forward * (depthLimits.x - startZ);
            Vector3 c = rightExtremeLocalPosition + Vector3.forward * (depthLimits.y - startZ);
            Vector3 d = leftExtremeLocalPosition + Vector3.forward * (depthLimits.y - startZ);
            if (tableReference != null)
            {
                a = tableReference.TransformPoint(a);
                b = tableReference.TransformPoint(b);
                c = tableReference.TransformPoint(c);
                d = tableReference.TransformPoint(d);
            }
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        }
    }
}
