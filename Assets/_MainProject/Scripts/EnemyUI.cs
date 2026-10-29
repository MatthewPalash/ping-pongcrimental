using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    public sealed class EnemyUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyController enemy;
        [SerializeField] private Slider healthBar;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image delayedHealthFill;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text enemyNameText;

        [Header("Health animation")]
        [SerializeField, Min(0.01f)] private float healthFillSpeed = 2f;
        [SerializeField, Min(0f)] private float delayedFillPause = 0.2f;
        [SerializeField, Min(0.01f)] private float delayedFillSpeed = 1f;

        [Header("Shake on damage")]
        [SerializeField] private RectTransform shakeTarget;
        [SerializeField, Min(0f)] private float shakeDuration = 0.25f;
        [SerializeField, Min(0f)] private float shakeDistance = 8f;

        [Header("Health text pulse")]
        [SerializeField, Min(0f)] private float healthTextPulseDuration = 0.25f;
        [SerializeField, Min(1f)] private float healthTextPulseScale = 1.2f;

        private float targetFill;
        private float displayedFill;
        private float delayedFill;
        private float delayedPauseRemaining;
        private int lastHealth = -1;
        private float shakeRemaining;
        private float pulseRemaining;
        private Vector2 shakeOrigin;
        private Vector3 textOriginScale;
        private RectTransform activeShakeTarget;

        private void OnEnable()
        {
            textOriginScale = healthText != null ? healthText.transform.localScale : Vector3.one;
            if (enemy != null) enemy.HealthChanged += OnHealthChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (enemy != null) enemy.HealthChanged -= OnHealthChanged;
            RestoreShake();
            RestoreTextScale();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            displayedFill = Mathf.MoveTowards(displayedFill, targetFill,
                Mathf.Max(0.01f, healthFillSpeed) * dt);
            ApplyMainFill(displayedFill);

            if (delayedFill < displayedFill)
                delayedFill = displayedFill;
            else if (Mathf.Abs(displayedFill - targetFill) <= 0.0001f && delayedFill > targetFill)
            {
                delayedPauseRemaining -= dt;
                if (delayedPauseRemaining <= 0f)
                    delayedFill = Mathf.MoveTowards(delayedFill, targetFill,
                        Mathf.Max(0.01f, delayedFillSpeed) * dt);
            }
            if (delayedHealthFill != null) delayedHealthFill.fillAmount = delayedFill;

            UpdateShake(dt);
            UpdateTextPulse(dt);
        }

        public void SetEnemy(EnemyController controller)
        {
            if (isActiveAndEnabled && enemy != null) enemy.HealthChanged -= OnHealthChanged;
            enemy = controller;
            if (isActiveAndEnabled && enemy != null) enemy.HealthChanged += OnHealthChanged;
            Refresh();
        }

        public void Refresh()
        {
            lastHealth = enemy != null ? enemy.CurrentHealth : -1;
            float value = enemy != null && enemy.MaxHealth > 0
                ? Mathf.Clamp01((float)enemy.CurrentHealth / enemy.MaxHealth) : 0f;
            targetFill = displayedFill = delayedFill = value;
            delayedPauseRemaining = 0f;
            ApplyMainFill(value);
            if (delayedHealthFill != null) delayedHealthFill.fillAmount = value;
            UpdateTexts();
            RestoreShake();
            RestoreTextScale();
        }

        private void OnHealthChanged(int current, int maximum)
        {
            float value = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
            if (lastHealth >= 0 && current != lastHealth)
            {
                StartTextPulse();
                if (current < lastHealth) StartShake();
            }
            lastHealth = current;
            targetFill = value;
            if (value < displayedFill)
                delayedPauseRemaining = Mathf.Max(0f, delayedFillPause);
            else if (value > displayedFill)
                delayedFill = Mathf.Max(delayedFill, value);
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            if (healthText != null)
                healthText.text = enemy != null ? $"{enemy.CurrentHealth}/{enemy.MaxHealth}" : string.Empty;
            if (enemyNameText != null)
                enemyNameText.text = enemy != null ? enemy.DisplayName : string.Empty;
        }

        private void ApplyMainFill(float value)
        {
            if (healthBar != null)
            {
                healthBar.minValue = 0f;
                healthBar.maxValue = 1f;
                healthBar.SetValueWithoutNotify(value);
            }
            if (healthFill != null) healthFill.fillAmount = value;
        }

        private void StartShake()
        {
            if (shakeDuration <= 0f || shakeDistance <= 0f) return;
            RectTransform target = shakeTarget != null ? shakeTarget
                : healthBar != null ? healthBar.transform as RectTransform
                : healthFill != null ? healthFill.rectTransform : null;
            if (target == null) return;
            if (activeShakeTarget != target)
            {
                RestoreShake();
                activeShakeTarget = target;
                shakeOrigin = target.anchoredPosition;
            }
            shakeRemaining = shakeDuration;
        }

        private void UpdateShake(float dt)
        {
            if (shakeRemaining <= 0f || activeShakeTarget == null) return;
            shakeRemaining = Mathf.Max(0f, shakeRemaining - dt);
            float strength = shakeDistance * (shakeRemaining / Mathf.Max(0.001f, shakeDuration));
            activeShakeTarget.anchoredPosition = shakeRemaining > 0f
                ? shakeOrigin + Random.insideUnitCircle * strength : shakeOrigin;
        }

        private void RestoreShake()
        {
            if (activeShakeTarget != null) activeShakeTarget.anchoredPosition = shakeOrigin;
            activeShakeTarget = null;
            shakeRemaining = 0f;
        }

        private void StartTextPulse()
        {
            if (healthText == null || healthTextPulseDuration <= 0f) return;
            if (pulseRemaining <= 0f) textOriginScale = healthText.transform.localScale;
            pulseRemaining = healthTextPulseDuration;
        }

        private void UpdateTextPulse(float dt)
        {
            if (pulseRemaining <= 0f || healthText == null) return;
            pulseRemaining = Mathf.Max(0f, pulseRemaining - dt);
            float progress = 1f - pulseRemaining / Mathf.Max(0.001f, healthTextPulseDuration);
            float scale = 1f + (Mathf.Max(1f, healthTextPulseScale) - 1f) * Mathf.Sin(progress * Mathf.PI);
            healthText.transform.localScale = textOriginScale * scale;
            if (pulseRemaining <= 0f) RestoreTextScale();
        }

        private void RestoreTextScale()
        {
            if (healthText != null)
                healthText.transform.localScale = textOriginScale;
            pulseRemaining = 0f;
        }
    }
}
