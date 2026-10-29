using TMPro;
using UnityEngine;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    public sealed class PlayerEnergyUI : MonoBehaviour
    {
        [SerializeField] private MatchControlState match;
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private UnityEngine.UI.Slider energyBar;
        [SerializeField] private UnityEngine.UI.Image energyFill;
        [SerializeField] private UnityEngine.UI.Image delayedEnergyFill;

        [Header("Energy animation")]
        [SerializeField, Min(0.01f)] private float energyFillSpeed = 2f;
        [SerializeField, Min(0f)] private float delayedFillPause = 0.2f;
        [SerializeField, Min(0.01f)] private float delayedFillSpeed = 1f;

        [Header("Shake on damage")]
        [SerializeField] private RectTransform shakeTarget;
        [SerializeField, Min(0f)] private float shakeDuration = 0.25f;
        [SerializeField, Min(0f)] private float shakeDistance = 8f;

        private float targetFill;
        private float displayedFill;
        private float delayedFill;
        private float delayedPauseRemaining;
        private bool damageTrailActive;
        private float shakeRemaining;
        private Vector2 shakeOrigin;
        private RectTransform activeShakeTarget;

        private void OnEnable()
        {
            if (match != null) { match.EnergyChanged += UpdateEnergy; match.PlayerDamaged += OnDamage; }
            Refresh();
        }

        private void Start() => Refresh();

        private void OnDisable()
        {
            if (match != null) { match.EnergyChanged -= UpdateEnergy; match.PlayerDamaged -= OnDamage; }
            RestoreShake();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (match != null)
                targetFill = Mathf.Clamp01(match.ContinuousEnergy / match.MaxEnergy);
            displayedFill = Mathf.MoveTowards(displayedFill, targetFill,
                Mathf.Max(0.01f, energyFillSpeed) * dt);
            ApplyMainFill(displayedFill);

            if (!damageTrailActive)
                delayedFill = displayedFill;
            else if (delayedFill < displayedFill)
                delayedFill = displayedFill;
            else if (Mathf.Abs(displayedFill - targetFill) <= 0.0001f && delayedFill > targetFill)
            {
                delayedPauseRemaining -= dt;
                if (delayedPauseRemaining <= 0f)
                    delayedFill = Mathf.MoveTowards(delayedFill, targetFill,
                        Mathf.Max(0.01f, delayedFillSpeed) * dt);
            }

            if (delayedEnergyFill != null)
                delayedEnergyFill.fillAmount = delayedFill;
            if (damageTrailActive && delayedFill <= displayedFill)
                damageTrailActive = false;
            UpdateShake(dt);
        }

        public void SetMatch(MatchControlState matchState)
        {
            if (isActiveAndEnabled && match != null)
            { match.EnergyChanged -= UpdateEnergy; match.PlayerDamaged -= OnDamage; }

            match = matchState;

            if (isActiveAndEnabled && match != null)
            { match.EnergyChanged += UpdateEnergy; match.PlayerDamaged += OnDamage; }

            Refresh();
        }

        public void Refresh()
        {
            int current = match != null ? match.CurrentEnergy : 0;
            int maximum = match != null ? match.MaxEnergy : 1;
            float value = match != null ? Mathf.Clamp01(match.ContinuousEnergy / maximum) : 0f;
            targetFill = displayedFill = delayedFill = value;
            delayedPauseRemaining = 0f;
            damageTrailActive = false;
            RestoreShake();
            ApplyMainFill(value);
            if (delayedEnergyFill != null) delayedEnergyFill.fillAmount = value;
            UpdateText(current, maximum);
        }

        private void UpdateEnergy(int current, int maximum)
        {
            UpdateText(current, maximum);
        }

        private void OnDamage()
        {
            damageTrailActive = true;
            delayedFill = Mathf.Max(delayedFill, displayedFill);
            delayedPauseRemaining = Mathf.Max(0f, delayedFillPause);
            if (shakeDuration <= 0f || shakeDistance <= 0f) return;
            RectTransform target = shakeTarget != null ? shakeTarget
                : energyBar != null ? energyBar.transform as RectTransform
                : energyFill != null ? energyFill.rectTransform : null;
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

        private void UpdateText(int current, int maximum)
        {
            if (energyText != null)
                energyText.text = $"{current}/{maximum}";
        }

        private void ApplyMainFill(float progress)
        {
            if (energyBar != null)
            {
                energyBar.minValue = 0f;
                energyBar.maxValue = 1f;
                energyBar.SetValueWithoutNotify(progress);
            }

            if (energyFill != null)
                energyFill.fillAmount = progress;
        }
    }
}
