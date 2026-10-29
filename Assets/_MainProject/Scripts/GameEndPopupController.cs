using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    public sealed class GameEndPopupController : MonoBehaviour
    {
        [Header("Содержимое")]
        [SerializeField] private TMP_Text defeatedEnemiesText;
        [SerializeField] private TMP_Text accumulatedMoneyText;
        [SerializeField] private EventTriggerButton restartButton;
        [SerializeField] private EventTriggerButton upgradeButton;

        [Header("Анимация (секунды)")]
        [SerializeField] private RectTransform popupScaleTarget;
        [SerializeField, Min(0f)] private float popupAnimationTime = 0.3f;
        [SerializeField, Min(0f)] private float itemAnimationTime = 0.12f;
        [SerializeField, Min(0f)] private float pauseBeforeButtons = 0.4f;
        [SerializeField, Min(0f)] private float pauseBetweenButtons = 0.08f;
        [SerializeField, Range(0.1f, 1f)] private float popupStartScale = 0.85f;
        [SerializeField, Range(0.1f, 1f)] private float itemStartScale = 0.8f;

        private sealed class Element
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Vector3 scale;
            public float alpha;
            public bool interactable;
            public bool blocksRaycasts;
            public bool active;
        }

        private readonly List<Element> elements = new List<Element>();
        private Element popup;
        private Action restartAction;
        private Action upgradeAction;
        private EventTrigger restartTrigger;
        private EventTrigger upgradeTrigger;
        private EventTrigger.Entry restartClick;
        private EventTrigger.Entry upgradeClick;
        private EventTrigger.Entry restartSubmit;
        private EventTrigger.Entry upgradeSubmit;
        public bool Ready { get; private set; }

        public IEnumerator Show(int defeated, int coins, Action restart, Action upgrade)
        {
            Ready = false;
            restartAction = restart;
            upgradeAction = upgrade;
            if (defeatedEnemiesText != null) defeatedEnemiesText.text = $"Убито: {defeated}";
            if (accumulatedMoneyText != null) accumulatedMoneyText.text = $"Монет: {coins}";
            popup = Capture(gameObject, popupScaleTarget);
            Element kills = PrepareItem(defeatedEnemiesText != null ? defeatedEnemiesText.gameObject : null);
            Element money = PrepareItem(accumulatedMoneyText != null ? accumulatedMoneyText.gameObject : null);
            Element restartItem = PrepareItem(restartButton != null ? restartButton.gameObject : null);
            Element upgradeItem = PrepareItem(upgradeButton != null ? upgradeButton.gameObject : null);
            BindButton(restartButton, OnRestart, out restartTrigger, out restartClick, out restartSubmit);
            BindButton(upgradeButton, OnUpgrade, out upgradeTrigger, out upgradeClick, out upgradeSubmit);
            popup.group.interactable = false;
            popup.group.blocksRaycasts = false;
            SetProgress(popup, 0f, popupStartScale);
            gameObject.SetActive(true);
            yield return Animate(popup, true, popupAnimationTime, popupStartScale);
            yield return Animate(kills, true, itemAnimationTime, itemStartScale);
            yield return Animate(money, true, itemAnimationTime, itemStartScale);
            if (pauseBeforeButtons > 0f) yield return new WaitForSecondsRealtime(pauseBeforeButtons);
            yield return Animate(restartItem, true, itemAnimationTime, itemStartScale);
            if (pauseBetweenButtons > 0f && restartItem != null && upgradeItem != null)
                yield return new WaitForSecondsRealtime(pauseBetweenButtons);
            yield return Animate(upgradeItem, true, itemAnimationTime, itemStartScale);
            popup.group.interactable = popup.interactable;
            popup.group.blocksRaycasts = popup.blocksRaycasts;
            Ready = true;
        }

        public IEnumerator Hide()
        {
            Ready = false;
            if (popup != null && popup.group != null)
            {
                popup.group.interactable = false;
                popup.group.blocksRaycasts = false;
            }
            yield return Animate(popup, false, popupAnimationTime, popupStartScale);
            ResetView();
        }

        public void ResetView()
        {
            Ready = false;
            UnbindButton(restartTrigger, restartClick);
            UnbindButton(upgradeTrigger, upgradeClick);
            UnbindButton(restartTrigger, restartSubmit);
            UnbindButton(upgradeTrigger, upgradeSubmit);
            restartTrigger = upgradeTrigger = null;
            restartClick = upgradeClick = null;
            restartSubmit = upgradeSubmit = null;
            gameObject.SetActive(false);
            foreach (Element item in elements)
            {
                Restore(item);
                if (item.group != null) item.group.gameObject.SetActive(item.active);
            }
            elements.Clear();
            Restore(popup);
            popup = null;
            restartAction = null;
            upgradeAction = null;
        }

        private void OnRestart() { if (Ready) restartAction?.Invoke(); }
        private void OnUpgrade() { if (Ready) upgradeAction?.Invoke(); }
        public void BlockActions() => Ready = false;

        private static void BindButton(EventTriggerButton button, Action callback,
            out EventTrigger trigger, out EventTrigger.Entry entry, out EventTrigger.Entry submit)
        {
            trigger = null;
            entry = null;
            submit = null;
            if (button == null) return;
            trigger = button.GetComponent<EventTrigger>();
            if (trigger == null) return;
            if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();
            entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(data =>
            {
                if (button != null && button.IsInteractable &&
                    (!(data is PointerEventData pointer) || pointer.button == PointerEventData.InputButton.Left))
                    callback();
            });
            trigger.triggers.Add(entry);
            submit = new EventTrigger.Entry { eventID = EventTriggerType.Submit };
            submit.callback.AddListener(_ => { if (button != null && button.IsInteractable) callback(); });
            trigger.triggers.Add(submit);
        }

        private static void UnbindButton(EventTrigger trigger, EventTrigger.Entry entry)
        {
            if (trigger != null && trigger.triggers != null && entry != null) trigger.triggers.Remove(entry);
        }

        private Element PrepareItem(GameObject target)
        {
            if (target == null || target == gameObject) return null;
            Element item = Capture(target);
            elements.Add(item);
            SetProgress(item, 0f, itemStartScale);
            target.SetActive(true);
            return item;
        }

        private static Element Capture(GameObject target, RectTransform scaleTarget = null)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();
            RectTransform rect = scaleTarget != null ? scaleTarget : target.transform as RectTransform;
            return new Element { rect = rect, group = group, scale = rect != null ? rect.localScale : Vector3.one,
                alpha = group.alpha, interactable = group.interactable, blocksRaycasts = group.blocksRaycasts,
                active = target.activeSelf };
        }

        private static void Restore(Element item)
        {
            if (item == null) return;
            if (item.group != null)
            {
                item.group.alpha = item.alpha;
                item.group.interactable = item.interactable;
                item.group.blocksRaycasts = item.blocksRaycasts;
            }
            if (item.rect != null) item.rect.localScale = item.scale;
        }

        private static void SetProgress(Element item, float progress, float startScale)
        {
            if (item == null || item.group == null) return;
            item.group.alpha = item.alpha * progress;
            if (item.rect != null) item.rect.localScale = item.scale * Mathf.Lerp(startScale, 1f, progress);
        }

        private static IEnumerator Animate(Element item, bool opening, float duration, float startScale)
        {
            if (item == null) yield break;
            float elapsed = 0f;
            duration = Mathf.Max(0f, duration);
            while (elapsed < duration && item.group != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                SetProgress(item, opening ? eased : 1f - eased, startScale);
                yield return null;
            }
            SetProgress(item, opening ? 1f : 0f, startScale);
        }
    }
}
