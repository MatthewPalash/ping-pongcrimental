using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TennisPrototype
{
    public enum MatchPhase
    {
        ActiveMatch,
        Pause,
        Victory,
        Defeat,
        Upgrade,
        OpponentTransition
    }

    [DisallowMultipleComponent]
    public sealed class MatchControlState : MonoBehaviour
    {
        [SerializeField] private FixedPlayerCamera playerCamera;
        [SerializeField] private PaddleController paddle;
        [SerializeField] private MatchPhase startingPhase = MatchPhase.ActiveMatch;
        [Header("Player energy")]
        [SerializeField, Min(1)] private int initialEnergy = 15;
        [Header("Game end UI")]
        [SerializeField] private GameObject defeatFinalText;
        [SerializeField] private GameObject gameEndPopup;
        [SerializeField, Min(0f)] private float defeatTextDisplayTime = 0.5f;
        [SerializeField, Min(0f)] private float defeatTextFadeTime = 0.25f;
        [SerializeField, Min(0f)] private float delayBeforeGameEndPopup = 0.5f;

        public event Action<int, int> EnergyChanged;
        public event Action PlayerDamaged;
        public event Action GameEndedEvent;
        public int CurrentEnergy { get; private set; }
        public int MaxEnergy => Mathf.Max(1, initialEnergy + (upgrades != null ? upgrades.EnergyBonus : 0));
        public float ContinuousEnergy => Mathf.Max(0f, CurrentEnergy - energyTimer);
        public bool GameEnded => CurrentPhase == MatchPhase.Defeat || CurrentPhase == MatchPhase.Victory;
        private float energyTimer;
        private UpgradeTreeScreen upgrades;
        private Coroutine gameEndSequence;
        private CanvasGroup defeatTextGroup;
        private float originalTextAlpha;
        private RectTransform defeatTextRect;
        private Vector3 originalTextScale;
        private Vector2 originalTextPosition;
        private readonly List<RectTransform> animatedCanvasChildren = new List<RectTransform>();
        private readonly List<Vector3> canvasChildScales = new List<Vector3>();
        private readonly List<Vector2> canvasChildPositions = new List<Vector2>();
        private EnemySpawner spawner;
        private PlayerWallet wallet;
        private GameEndPopupController popupController;

        public MatchPhase CurrentPhase { get; private set; }

        public void Configure(FixedPlayerCamera camera, PaddleController controller)
        {
            playerCamera = camera;
            paddle = controller;
        }

        private void Start()
        {
            upgrades = GetComponent<UpgradeTreeScreen>();
            if (gameEndPopup != null) popupController = gameEndPopup.GetComponent<GameEndPopupController>();
            spawner = GetComponent<EnemySpawner>();
            if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
            wallet = paddle != null ? paddle.GetComponent<PlayerWallet>() : null;
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            BeginNewRun();
            SetPhase(upgrades != null ? MatchPhase.Upgrade : startingPhase);
        }

        private void Update()
        {
            if (GameEnded || CurrentPhase == MatchPhase.Upgrade || CurrentPhase == MatchPhase.Pause) return;
            energyTimer += Time.deltaTime;
            while (energyTimer >= 1f && !GameEnded)
            {
                energyTimer -= 1f;
                SpendEnergy(1, false);
            }
        }

        public void BeginNewRun()
        {
            PixelBloodEmitter.ClearMatchBlood();
            StopGameEndSequence();
            if (wallet != null) wallet.ResetMatchEarnings();
            CurrentPhase = MatchPhase.ActiveMatch;
            CurrentEnergy = MaxEnergy;
            energyTimer = 0f;
            if (defeatFinalText != null) defeatFinalText.SetActive(false);
            if (gameEndPopup != null) gameEndPopup.SetActive(false);
            EnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
            if (playerCamera != null) playerCamera.ResetToStartPosition();
            if (paddle != null) paddle.ResetToStartPosition();
            SetPhase(MatchPhase.ActiveMatch);
        }

        public void SetPhase(MatchPhase phase)
        {
            if (GameEnded) return;
            CurrentPhase = phase;
            if (paddle != null)
                paddle.SetControlEnabled(phase == MatchPhase.ActiveMatch ||
                    phase == MatchPhase.OpponentTransition);
            if (GameEnded)
            {
                GameEndedEvent?.Invoke();
                if (phase == MatchPhase.Defeat)
                    gameEndSequence = StartCoroutine(ShowDefeatSequence());
                else
                    gameEndSequence = StartCoroutine(ShowGameEndPopupSequence());
            }
        }

        private IEnumerator ShowDefeatSequence()
        {
            if (gameEndPopup != null) gameEndPopup.SetActive(false);
            if (defeatFinalText != null)
            {
                defeatTextGroup = defeatFinalText.GetComponent<CanvasGroup>();
                if (defeatTextGroup == null) defeatTextGroup = defeatFinalText.AddComponent<CanvasGroup>();
                originalTextAlpha = defeatTextGroup.alpha;
                defeatTextRect = defeatFinalText.transform as RectTransform;
                Canvas targetCanvas = defeatFinalText.GetComponent<Canvas>();
                animatedCanvasChildren.Clear();
                canvasChildScales.Clear();
                canvasChildPositions.Clear();
                if (targetCanvas != null && targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    foreach (Transform child in defeatFinalText.transform)
                    {
                        if (child is not RectTransform rect) continue;
                        animatedCanvasChildren.Add(rect);
                        canvasChildScales.Add(rect.localScale);
                        canvasChildPositions.Add(rect.anchoredPosition);
                        rect.localScale *= 0.35f;
                    }
                    if (animatedCanvasChildren.Count > 0) defeatTextRect = null;
                }
                if (defeatTextRect != null)
                {
                    originalTextScale = defeatTextRect.localScale;
                    if (originalTextScale.sqrMagnitude < 0.000001f)
                        originalTextScale = Vector3.one;
                    originalTextPosition = defeatTextRect.anchoredPosition;
                    defeatTextRect.localScale = originalTextScale * 0.35f;
                }
                defeatTextGroup.alpha = 0f;
                defeatFinalText.SetActive(true);
                yield return AnimateDefeatText(true);
                if (defeatTextDisplayTime > 0f)
                    yield return new WaitForSecondsRealtime(defeatTextDisplayTime);
                yield return AnimateDefeatText(false);
                HideDefeatText();
            }
            if (delayBeforeGameEndPopup > 0f)
                yield return new WaitForSecondsRealtime(delayBeforeGameEndPopup);
            yield return ShowGameEndPopup();
            if (popupController == null || popupController.Ready) gameEndSequence = null;
        }

        private IEnumerator ShowGameEndPopupSequence()
        {
            yield return ShowGameEndPopup();
            if (popupController == null || popupController.Ready) gameEndSequence = null;
        }

        private IEnumerator ShowGameEndPopup()
        {
            if (popupController == null)
            {
                Debug.LogError("Game End Popup needs a GameEndPopupController component.", this);
                yield break;
            }
            yield return popupController.Show(spawner != null ? spawner.DefeatedCount : 0,
                wallet != null ? wallet.Balance : 0, RestartMatch, OpenUpgrades);
        }

        public void RestartMatch()
        {
            if (!GameEnded || popupController == null || !popupController.Ready || spawner == null) return;
            popupController.BlockActions();
            gameEndSequence = StartCoroutine(CloseGameEndPopup(true));
        }

        public void OpenUpgrades()
        {
            if (!GameEnded || popupController == null || !popupController.Ready || upgrades == null) return;
            popupController.BlockActions();
            gameEndSequence = StartCoroutine(CloseGameEndPopup(false));
        }

        private IEnumerator CloseGameEndPopup(bool restart)
        {
            MatchPhase result = CurrentPhase;
            yield return popupController.Hide();
            gameEndSequence = null;
            if (restart)
            {
                spawner.ResetForNewMatch();
                BeginNewRun();
                spawner.BeginMatch();
            }
            else
            {
                CurrentPhase = MatchPhase.Upgrade;
                upgrades.ShowAfterMatch(result);
            }
        }

        private IEnumerator AnimateDefeatText(bool entering)
        {
            float duration = Mathf.Max(0f, defeatTextFadeTime);
            float elapsed = 0f;
            while (elapsed < duration && defeatTextGroup != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = t * t * (3f - 2f * t);
                defeatTextGroup.alpha = originalTextAlpha * (entering ? Mathf.Clamp01(t * 2f) : 1f - smooth);
                float phase = entering ? (t < 0.55f ? t / 0.55f : (t - 0.55f) / 0.45f) : 0f;
                float ease = phase * phase * (3f - 2f * phase);
                float scale = entering ? (t < 0.55f ? Mathf.Lerp(0.35f, 1.28f, ease) : Mathf.Lerp(1.28f, 1f, ease)) : Mathf.Lerp(1f, 0.85f, smooth);
                Vector2 shake = entering ? UnityEngine.Random.insideUnitCircle * (10f * (1f - t)) : Vector2.zero;
                for (int i = 0; i < animatedCanvasChildren.Count; i++)
                {
                    if (animatedCanvasChildren[i] == null) continue;
                    animatedCanvasChildren[i].localScale = canvasChildScales[i] * scale;
                    animatedCanvasChildren[i].anchoredPosition = canvasChildPositions[i] + shake;
                }
                if (defeatTextRect != null)
                {
                    defeatTextRect.localScale = originalTextScale * scale;
                    defeatTextRect.anchoredPosition = originalTextPosition + shake;
                }
                yield return null;
            }
            if (defeatTextGroup != null) defeatTextGroup.alpha = entering ? originalTextAlpha : 0f;
            for (int i = 0; i < animatedCanvasChildren.Count; i++)
            {
                if (animatedCanvasChildren[i] == null) continue;
                animatedCanvasChildren[i].localScale = canvasChildScales[i] * (entering ? 1f : 0.85f);
                animatedCanvasChildren[i].anchoredPosition = canvasChildPositions[i];
            }
            if (defeatTextRect != null)
            {
                defeatTextRect.localScale = originalTextScale * (entering ? 1f : 0.85f);
                defeatTextRect.anchoredPosition = originalTextPosition;
            }
        }

        private void HideDefeatText()
        {
            if (defeatFinalText != null) defeatFinalText.SetActive(false);
            if (defeatTextGroup != null) defeatTextGroup.alpha = originalTextAlpha;
            for (int i = 0; i < animatedCanvasChildren.Count; i++)
            {
                if (animatedCanvasChildren[i] == null) continue;
                animatedCanvasChildren[i].localScale = canvasChildScales[i];
                animatedCanvasChildren[i].anchoredPosition = canvasChildPositions[i];
            }
            animatedCanvasChildren.Clear();
            canvasChildScales.Clear();
            canvasChildPositions.Clear();
            if (defeatTextRect != null)
            {
                defeatTextRect.localScale = originalTextScale;
                defeatTextRect.anchoredPosition = originalTextPosition;
            }
            defeatTextGroup = null;
            defeatTextRect = null;
        }

        private void StopGameEndSequence()
        {
            if (gameEndSequence != null)
            {
                StopCoroutine(gameEndSequence);
                gameEndSequence = null;
            }
            HideDefeatText();
            if (popupController != null) popupController.ResetView();
        }

        // Assign this method to the OK button in Game End Popup.
        public void ConfirmGameEnd()
        {
            OpenUpgrades();
        }

        public void RestoreEnergy(int amount)
        {
            if (amount <= 0 || GameEnded || CurrentEnergy >= MaxEnergy) return;
            CurrentEnergy += Mathf.Min(amount, MaxEnergy - CurrentEnergy);
            EnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
        }

        public void TakePlayerDamage(int amount)
        {
            SpendEnergy(amount, true);
        }

        private void SpendEnergy(int amount, bool isDamage)
        {
            if (amount <= 0 || GameEnded) return;
            CurrentEnergy = Mathf.Max(0, CurrentEnergy - amount);
            if (isDamage)
            {
                if (playerCamera != null) playerCamera.ShowDamageFeedback();
                PlayerDamaged?.Invoke();
            }
            EnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
            if (CurrentEnergy == 0) SetPhase(MatchPhase.Defeat);
        }

        public void PrepareNextOpponent()
        {
            SetPhase(MatchPhase.OpponentTransition);
        }
    }
}
