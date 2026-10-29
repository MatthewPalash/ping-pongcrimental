using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EventTrigger))]
    public sealed class EventTriggerButton : MonoBehaviour
    {
        [Header("Масштаб")]
        [SerializeField] private RectTransform animationTarget;
        [SerializeField, Range(0.5f, 1f)] private float hoverScale = 0.95f;
        [SerializeField, Min(0.01f)] private float hoverSpeed = 12f;
        [SerializeField, Range(0f, 0.5f)] private float pressStrength = 0.16f;
        [SerializeField, Min(0.05f)] private float springDuration = 0.45f;

        [Header("Цвет текста при наведении")]
        [SerializeField] private bool changeTextColor;
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private Color hoverTextColor = Color.white;

        private EventTrigger eventTrigger;
        private UnityEngine.UI.Button button;
        private readonly List<EventTrigger.Entry> entries = new List<EventTrigger.Entry>();
        private Vector3 baseScale;
        private Color baseTextColor;
        private bool activeAnimation;
        private bool hovered;
        private float hoverAmount;
        private float springElapsed;
        private bool springActive;

        private void Awake()
        {
            eventTrigger = GetComponent<EventTrigger>();
            button = GetComponent<UnityEngine.UI.Button>();
            if (animationTarget == null) animationTarget = transform as RectTransform;
            if (targetText == null) targetText = GetComponentInChildren<TMP_Text>(true);
        }

        private void OnEnable()
        {
            AddTrigger(EventTriggerType.PointerEnter, OnPointerEnter);
            AddTrigger(EventTriggerType.PointerExit, OnPointerExit);
            AddTrigger(EventTriggerType.PointerDown, OnPointerDown);
        }

        private void OnDisable()
        {
            if (eventTrigger != null && eventTrigger.triggers != null)
                foreach (EventTrigger.Entry entry in entries) eventTrigger.triggers.Remove(entry);
            entries.Clear();
            Restore();
        }

        private void AddTrigger(EventTriggerType type, UnityAction<BaseEventData> callback)
        {
            if (eventTrigger.triggers == null) eventTrigger.triggers = new List<EventTrigger.Entry>();
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            eventTrigger.triggers.Add(entry);
            entries.Add(entry);
        }

        public bool IsInteractable => isActiveAndEnabled && (button == null || button.IsActive() && button.IsInteractable());
        private bool CanInteract() => IsInteractable;

        private void Capture()
        {
            if (activeAnimation) return;
            if (animationTarget != null) baseScale = animationTarget.localScale;
            if (targetText != null) baseTextColor = targetText.color;
            activeAnimation = true;
        }

        private void OnPointerEnter(BaseEventData eventData)
        {
            if (!CanInteract()) return;
            Capture();
            hovered = true;
        }

        private void OnPointerExit(BaseEventData eventData) => hovered = false;

        private void OnPointerDown(BaseEventData eventData)
        {
            if (!CanInteract() || eventData is PointerEventData pointer && pointer.button != PointerEventData.InputButton.Left) return;
            Capture();
            springElapsed = 0f;
            springActive = true;
        }

        private void Update()
        {
            if (!activeAnimation) return;
            if (!CanInteract()) { Restore(); return; }
            float dt = Time.unscaledDeltaTime;
            hoverAmount = Mathf.MoveTowards(hoverAmount, hovered ? 1f : 0f, Mathf.Max(0.01f, hoverSpeed) * dt);
            float pulse = 0f;
            if (springActive)
            {
                springElapsed += dt;
                float t = Mathf.Clamp01(springElapsed / Mathf.Max(0.05f, springDuration));
                pulse = -pressStrength * Mathf.Exp(-5f * t) * Mathf.Cos(t * Mathf.PI * 3f);
                if (t >= 1f) { springActive = false; pulse = 0f; }
            }
            if (animationTarget != null)
                animationTarget.localScale = baseScale * (Mathf.Lerp(1f, hoverScale, hoverAmount) + pulse);
            if (changeTextColor && targetText != null)
                targetText.color = Color.Lerp(baseTextColor, hoverTextColor, hoverAmount);
            if (!hovered && hoverAmount <= 0f && !springActive) Restore();
        }

        private void Restore()
        {
            if (activeAnimation)
            {
                if (animationTarget != null) animationTarget.localScale = baseScale;
                if (changeTextColor && targetText != null) targetText.color = baseTextColor;
            }
            activeAnimation = false;
            hovered = false;
            hoverAmount = 0f;
            springActive = false;
        }
    }
}
