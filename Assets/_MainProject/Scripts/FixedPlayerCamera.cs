using UnityEngine;
using UnityEngine.InputSystem;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class FixedPlayerCamera : MonoBehaviour
    {
        [Header("Base view")]
        [SerializeField] private Transform tableReference;
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 1.22f, -2.48f);
        [SerializeField] private Vector3 cameraRotation = new Vector3(6f, 0f, 0f);
        [SerializeField, Range(35f, 100f)] private float fieldOfView = 44f;
        [SerializeField] private Camera paddleCamera;
        [SerializeField] private PaddleController paddle;
        [SerializeField] private ArcadeBallController ball;

        [Header("Mouse sway")]
        [SerializeField, Min(0f)] private float horizontalDegreesPerPixel = 0.035f;
        [SerializeField, Min(0f)] private float verticalDegreesPerPixel = 0.025f;
        [SerializeField, Range(0f, 10f)] private float maximumYaw = 2.5f;
        [SerializeField, Range(0f, 10f)] private float maximumPitch = 1.5f;
        [SerializeField, Min(0f)] private float swayFollowSpeed = 18f;
        [SerializeField, Min(0f)] private float swayReturnSpeed = 5f;

        [Header("Hit shake")]
        [SerializeField, Min(0f)] private float shakeDuration = 0.16f;
        [SerializeField, Min(0f)] private float shakePositionAmplitude = 0.008f;
        [SerializeField, Min(0f)] private float shakeRotationAmplitude = 0.35f;

        [Header("Damage feedback")]
        [SerializeField, Min(0.01f)] private float damageFlashDuration = 0.22f;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0f, 0f, 0.3f);
        [SerializeField, Min(0f)] private float damageShakeDuration = 0.25f;
        [SerializeField, Min(0f)] private float damageShakePosition = 0.015f;
        [SerializeField, Min(0f)] private float damageShakeRotation = 0.7f;

        private Vector2 swayTarget;
        private Vector2 currentSway;
        private float shakeRemaining;
        private bool wasAcceptingInput;
        private float damageFlashRemaining;
        private float damageShakeRemaining;
        private UnityEngine.UI.Image damageOverlay;

        public void Configure(Transform reference, Vector3 position, Vector3 rotation, float fov)
        {
            tableReference = reference;
            cameraPosition = position;
            cameraRotation = rotation;
            fieldOfView = fov;
            ResetToStartPosition();
        }

        public void SetPaddleCamera(Camera overlayCamera)
        {
            paddleCamera = overlayCamera;
            ApplyPose(Vector3.zero, Vector3.zero);
        }

        public void SetPaddleController(PaddleController controller) => paddle = controller;

        public void SetBall(ArcadeBallController controller)
        {
            if (isActiveAndEnabled && ball != null) ball.PlayerHitBall -= OnPlayerHit;
            ball = controller;
            if (Application.isPlaying && isActiveAndEnabled && ball != null)
                ball.PlayerHitBall += OnPlayerHit;
        }

        public void ResetToStartPosition()
        {
            swayTarget = Vector2.zero;
            currentSway = Vector2.zero;
            shakeRemaining = 0f;
            damageShakeRemaining = 0f;
            damageFlashRemaining = 0f;
            if (damageOverlay != null) damageOverlay.color = Color.clear;
            wasAcceptingInput = false;
            ApplyPose(Vector3.zero, Vector3.zero);
        }

        private void Awake() => ResetToStartPosition();

        private void OnEnable()
        {
            if (Application.isPlaying && ball != null)
                ball.PlayerHitBall += OnPlayerHit;
        }

        private void OnDisable()
        {
            if (ball != null) ball.PlayerHitBall -= OnPlayerHit;
            damageShakeRemaining = 0f;
            damageFlashRemaining = 0f;
            if (damageOverlay != null) damageOverlay.color = Color.clear;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            bool acceptingInput = Application.isPlaying && Application.isFocused &&
                paddle != null && paddle.ControlEnabled;
            Vector2 delta = acceptingInput && wasAcceptingInput && Mouse.current != null
                ? Mouse.current.delta.ReadValue() : Vector2.zero;
            wasAcceptingInput = acceptingInput;
            AdvanceMotion(delta, acceptingInput, dt);
            if (damageOverlay != null && damageFlashRemaining > 0f)
            {
                damageFlashRemaining = Mathf.Max(0f, damageFlashRemaining - Time.unscaledDeltaTime);
                Color color = damageFlashColor;
                color.a *= damageFlashRemaining / Mathf.Max(0.01f, damageFlashDuration);
                damageOverlay.color = color;
            }
        }

        private void AdvanceMotion(Vector2 delta, bool acceptingInput, float dt)
        {
            if (acceptingInput) ApplyMouseDelta(delta);

            if (!acceptingInput) swayTarget = Vector2.zero;
            swayTarget = Vector2.Lerp(swayTarget, Vector2.zero,
                1f - Mathf.Exp(-swayReturnSpeed * dt));
            currentSway = Vector2.Lerp(currentSway, swayTarget,
                1f - Mathf.Exp(-swayFollowSpeed * dt));

            Vector3 shakePosition = Vector3.zero;
            Vector3 shakeAngles = Vector3.zero;
            if (acceptingInput && shakeRemaining > 0f && shakeDuration > 0f)
            {
                float age = shakeDuration - shakeRemaining;
                float strength = shakeRemaining / shakeDuration;
                float horizontal = Mathf.Sin(age * 85f);
                float vertical = Mathf.Sin(age * 117f + 1f);
                shakePosition = new Vector3(horizontal, vertical, 0f) *
                    (shakePositionAmplitude * strength);
                shakeAngles = new Vector3(vertical, horizontal, horizontal - vertical) *
                    (shakeRotationAmplitude * strength);
                shakeRemaining = Mathf.Max(0f, shakeRemaining - dt);
            }
            else if (!acceptingInput)
            {
                shakeRemaining = 0f;
            }

            if (damageShakeRemaining > 0f && damageShakeDuration > 0f)
            {
                float age = damageShakeDuration - damageShakeRemaining;
                float strength = damageShakeRemaining / damageShakeDuration;
                float horizontal = Mathf.Sin(age * 85f);
                float vertical = Mathf.Sin(age * 117f + 1f);
                shakePosition += new Vector3(horizontal, vertical, 0f) * (damageShakePosition * strength);
                shakeAngles += new Vector3(vertical, horizontal, horizontal - vertical) *
                    (damageShakeRotation * strength);
                damageShakeRemaining = Mathf.Max(0f, damageShakeRemaining - dt);
            }

            ApplyPose(shakePosition,
                new Vector3(currentSway.x, currentSway.y, 0f) + shakeAngles);
        }

        private void ApplyMouseDelta(Vector2 delta)
        {
            delta = Vector2.ClampMagnitude(delta, 80f);
            swayTarget.x = Mathf.Clamp(swayTarget.x - delta.y * verticalDegreesPerPixel,
                -maximumPitch, maximumPitch);
            swayTarget.y = Mathf.Clamp(swayTarget.y + delta.x * horizontalDegreesPerPixel,
                -maximumYaw, maximumYaw);
        }

        private void OnPlayerHit(Vector3 contact, Vector3 destination)
        {
            shakeRemaining = shakeDuration;
        }

        public void ShowDamageFeedback()
        {
            if (!isActiveAndEnabled) return;
            if (damageOverlay == null) CreateDamageOverlay();
            damageFlashRemaining = Mathf.Max(0.01f, damageFlashDuration);
            damageShakeRemaining = Mathf.Max(0f, damageShakeDuration);
            damageOverlay.color = damageFlashColor;
        }

        private void CreateDamageOverlay()
        {
            GameObject canvasObject = new GameObject("Damage Flash Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            GameObject overlayObject = new GameObject("Damage Flash", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            overlayObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = overlayObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            damageOverlay = overlayObject.GetComponent<UnityEngine.UI.Image>();
            damageOverlay.raycastTarget = false;
            damageOverlay.color = Color.clear;
        }

        private void ApplyPose(Vector3 shakePosition, Vector3 extraAngles)
        {
            Vector3 basePosition = tableReference != null
                ? tableReference.TransformPoint(cameraPosition) : cameraPosition;
            Quaternion baseRotation = (tableReference != null
                ? tableReference.rotation : Quaternion.identity) * Quaternion.Euler(cameraRotation);
            transform.SetPositionAndRotation(basePosition + baseRotation * shakePosition,
                baseRotation * Quaternion.Euler(extraAngles));
            GetComponent<Camera>().fieldOfView = fieldOfView;
            if (paddleCamera != null) paddleCamera.fieldOfView = fieldOfView;
        }

        private void OnValidate()
        {
            fieldOfView = Mathf.Clamp(fieldOfView, 35f, 100f);
            if (!Application.isPlaying && gameObject.scene.IsValid())
                ApplyPose(Vector3.zero, Vector3.zero);
        }
    }
}
