using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TennisPrototype
{
    public enum UpgradeEffect { Unlock, PaddleDamage, Energy, KillCoins, BonusZone, CoinZone, EnergyZone }

    [Serializable]
    public sealed class UpgradeNodeDefinition
    {
        public string id;
        public string parentId;
        public string title;
        [TextArea] public string description;
        public UpgradeEffect effect;
        public Vector2 position;
        [Min(1)] public int maxLevel = 1;
        [Min(0)] public int firstPrice;
        [Min(0)] public int priceStep;
        [Min(0)] public int amountPerLevel;
    }

    // The graph is data. The view is rebuilt from it when a parent is bought.
    [DisallowMultipleComponent]
    public sealed class UpgradeTreeScreen : MonoBehaviour
    {
        [SerializeField] private List<UpgradeNodeDefinition> nodes = new List<UpgradeNodeDefinition>();
        [SerializeField] private MatchControlState match;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private TMP_FontAsset uiFont;

        private readonly Dictionary<string, int> levels = new Dictionary<string, int>();
        private readonly List<GameObject> graphObjects = new List<GameObject>();
        private Canvas canvas;
        private RectTransform overlay;
        private RectTransform graph;
        private RectTransform tooltip;
        private TMP_Text tooltipText;
        private TMP_Text walletText;
        private TMP_Text statusText;
        private string hoveredId;

        public bool BonusZoneUnlocked => Total(UpgradeEffect.BonusZone) > 0;
        public int ZoneLevel(BonusZoneType type) => Total(type == BonusZoneType.Coins ? UpgradeEffect.CoinZone :
            type == BonusZoneType.Energy ? UpgradeEffect.EnergyZone : UpgradeEffect.BonusZone);
        public int PaddleDamageBonus => Total(UpgradeEffect.PaddleDamage);
        public int EnergyBonus => Total(UpgradeEffect.Energy);
        public int KillCoinBonus => Total(UpgradeEffect.KillCoins);

        public void SetFont(TMP_FontAsset font) => uiFont = font;

        private void Awake()
        {
            if (nodes == null || nodes.Count == 0) CreateDemoGraph();
            if (match == null) match = GetComponent<MatchControlState>();
            if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
        }

        private void Start()
        {
            BuildScreen();
            if (wallet != null) wallet.BalanceChanged += OnBalanceChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            if (wallet != null) wallet.BalanceChanged -= OnBalanceChanged;
        }

        private void OnBalanceChanged(int _)
        {
            // The upgrade screen is hidden during a match. Its graph is rebuilt when
            // the screen opens, so coin rewards must not rebuild it on every hit.
            if (overlay != null && overlay.gameObject.activeInHierarchy) Refresh();
        }

        public void ShowAfterMatch(MatchPhase result)
        {
            if (spawner != null) spawner.ResetForNewMatch();
            overlay.gameObject.SetActive(true);
            statusText.text = result == MatchPhase.Victory
                ? "ПОБЕДА! УЛУЧШИТЕ РАКЕТКУ ПЕРЕД СЛЕДУЮЩИМ МАТЧЕМ"
                : "МАТЧ ОКОНЧЕН. УЛУЧШИТЕ РАКЕТКУ И ПОПРОБУЙТЕ СНОВА";
            Refresh();
        }

        private int Level(string id) => levels.TryGetValue(id, out int level) ? level : 0;

        private int Total(UpgradeEffect effect)
        {
            int total = 0;
            foreach (UpgradeNodeDefinition node in nodes)
                if (node.effect == effect) total += Level(node.id) * node.amountPerLevel;
            return total;
        }

        private bool IsRevealed(UpgradeNodeDefinition node) =>
            string.IsNullOrEmpty(node.parentId) || Level(node.parentId) > 0;

        private int Price(UpgradeNodeDefinition node) =>
            node.firstPrice + Level(node.id) * node.priceStep;

        private void Buy(UpgradeNodeDefinition node)
        {
            if (!IsRevealed(node) || Level(node.id) >= node.maxLevel || wallet == null) return;
            if (!wallet.TrySpend(Price(node))) return;
            levels[node.id] = Level(node.id) + 1;
            Refresh();
        }

        private void StartMatch()
        {
            if (match == null || spawner == null) return;
            overlay.gameObject.SetActive(false);
            match.BeginNewRun();
            spawner.BeginMatch();
        }

        private void Refresh()
        {
            if (graph == null) return;
            foreach (GameObject item in graphObjects) Destroy(item);
            graphObjects.Clear();
            walletText.text = "МОНЕТЫ  " + (wallet != null ? wallet.Balance : 0);

            foreach (UpgradeNodeDefinition node in nodes)
            {
                if (!IsRevealed(node) || string.IsNullOrEmpty(node.parentId)) continue;
                UpgradeNodeDefinition parent = nodes.Find(n => n.id == node.parentId);
                if (parent != null) DrawConnection(parent.position, node.position);
            }
            foreach (UpgradeNodeDefinition node in nodes)
                if (IsRevealed(node)) DrawNode(node);
            if (hoveredId != null)
            {
                UpgradeNodeDefinition hovered = nodes.Find(n => n.id == hoveredId);
                if (hovered != null && IsRevealed(hovered)) ShowTooltip(hovered);
                else HideTooltip();
            }
        }

        private void BuildScreen()
        {
            GameObject canvasObject = new GameObject("Upgrade Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            overlay = Panel("Upgrade Screen", canvas.transform, new Color(0.045f, 0.065f, 0.1f, 0.97f));
            Stretch(overlay);
            Text("ПРОКАЧКА", overlay, new Vector2(-560, 380), new Vector2(500, 70), 44,
                new Color(0.96f, 0.98f, 1f), TextAlignmentOptions.Left);
            statusText = Text("КУПИТЕ УЗЕЛ И НАЧНИТЕ МАТЧ", overlay, new Vector2(-530, 327),
                new Vector2(600, 35), 20, new Color(0.6f, 0.75f, 0.82f), TextAlignmentOptions.Left);
            walletText = Text("", overlay, new Vector2(575, 375), new Vector2(300, 54), 28,
                new Color(1f, 0.84f, 0.45f), TextAlignmentOptions.Right);
            graph = Panel("Tree", overlay, new Color(0, 0, 0, 0));
            Stretch(graph);
            graph.SetAsLastSibling();

            RectTransform bottom = Panel("Bottom Bar", overlay, new Color(0.055f, 0.09f, 0.14f, 0.95f));
            bottom.anchorMin = new Vector2(0, 0);
            bottom.anchorMax = new Vector2(1, 0);
            bottom.pivot = new Vector2(0.5f, 0);
            bottom.sizeDelta = new Vector2(0, 100);
            bottom.anchoredPosition = Vector2.zero;
            Text("Выберите улучшение  •  наведите курсор для подробностей", bottom,
                new Vector2(-335, 50), new Vector2(650, 38), 20,
                new Color(0.65f, 0.76f, 0.83f), TextAlignmentOptions.Left);
            var start = Panel("Start Button", bottom, new Color(0.25f, 0.68f, 0.49f, 1));
            Place(start, new Vector2(570, 50), new Vector2(260, 64));
            var startButton = start.gameObject.AddComponent<UnityEngine.UI.Button>();
            start.gameObject.AddComponent<EventTriggerButton>();
            startButton.targetGraphic = start.GetComponent<UnityEngine.UI.Image>();
            var startTrigger = start.GetComponent<EventTrigger>();
            var startClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            startClick.callback.AddListener(data =>
            {
                if (startButton.IsInteractable() &&
                    (!(data is PointerEventData pointer) || pointer.button == PointerEventData.InputButton.Left))
                    StartMatch();
            });
            startTrigger.triggers.Add(startClick);
            var startSubmit = new EventTrigger.Entry { eventID = EventTriggerType.Submit };
            startSubmit.callback.AddListener(_ => { if (startButton.IsInteractable()) StartMatch(); });
            startTrigger.triggers.Add(startSubmit);
            Text("НАЧАТЬ", start, Vector2.zero, new Vector2(240, 55), 28,
                Color.white, TextAlignmentOptions.Center);

            tooltip = Panel("Node Info", overlay, new Color(0.07f, 0.11f, 0.17f, 0.99f));
            Place(tooltip, new Vector2(500, -145), new Vector2(340, 245));
            var border = tooltip.gameObject.AddComponent<UnityEngine.UI.Outline>();
            border.effectColor = new Color(0.35f, 0.78f, 0.72f);
            border.effectDistance = new Vector2(2, -2);
            tooltipText = Text("", tooltip, Vector2.zero, new Vector2(300, 215), 19,
                Color.white, TextAlignmentOptions.TopLeft);
            tooltip.gameObject.SetActive(false);
        }

        private void DrawConnection(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            RectTransform line = Panel("Link", graph, new Color(0.36f, 0.57f, 0.62f, 0.8f));
            Place(line, (from + to) * 0.5f, new Vector2(delta.magnitude, 4));
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            graphObjects.Add(line.gameObject);
        }

        private void DrawNode(UpgradeNodeDefinition node)
        {
            int level = Level(node.id);
            bool complete = level >= node.maxLevel;
            bool affordable = wallet != null && wallet.Balance >= Price(node);
            Color color = complete ? new Color(0.27f, 0.57f, 0.42f) :
                affordable ? new Color(0.23f, 0.48f, 0.56f) : new Color(0.16f, 0.24f, 0.32f);
            RectTransform tile = Panel(node.title, graph, color);
            Place(tile, node.position, new Vector2(170, 106));
            var outline = tile.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = complete ? new Color(0.68f, 0.94f, 0.59f) : new Color(0.4f, 0.72f, 0.78f);
            outline.effectDistance = new Vector2(2, -2);
            var button = tile.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = tile.GetComponent<UnityEngine.UI.Image>();
            button.interactable = !complete && affordable;
            button.onClick.AddListener(() => Buy(node));
            var hover = tile.gameObject.AddComponent<UpgradeNodeHover>();
            hover.Enter = () => ShowTooltip(node);
            hover.Exit = HideTooltip;
            string symbol = node.effect == UpgradeEffect.PaddleDamage ? "D" :
                node.effect == UpgradeEffect.Energy ? "E" :
                node.effect == UpgradeEffect.KillCoins ? "$" :
                (node.effect == UpgradeEffect.BonusZone || node.effect == UpgradeEffect.CoinZone || node.effect == UpgradeEffect.EnergyZone) ? "◎" : "+";
            Text(symbol, tile, new Vector2(0, 26), new Vector2(150, 45), 32,
                new Color(0.96f, 0.87f, 0.55f), TextAlignmentOptions.Center);
            Text(node.title, tile, new Vector2(0, -7), new Vector2(158, 28), 18,
                Color.white, TextAlignmentOptions.Center);
            Text(complete ? "КУПЛЕНО" : level > 0 ? $"УРОВЕНЬ {level}/{node.maxLevel}  •  {Price(node)}" :
                Price(node) == 0 ? "БЕСПЛАТНО" : $"{Price(node)} МОНЕТ", tile,
                new Vector2(0, -37), new Vector2(158, 25), 15,
                new Color(0.73f, 0.88f, 0.77f), TextAlignmentOptions.Center);
            graphObjects.Add(tile.gameObject);
        }

        private void ShowTooltip(UpgradeNodeDefinition node)
        {
            hoveredId = node.id;
            int level = Level(node.id);
            bool complete = level >= node.maxLevel;
            string value = node.effect == UpgradeEffect.BonusZone ? "Красная зона: увеличивает урон" :
                node.effect == UpgradeEffect.CoinZone ? "Золотая зона: дополнительные монеты" :
                node.effect == UpgradeEffect.EnergyZone ? "Зелёная зона: восстановление энергии" :
                node.effect == UpgradeEffect.Unlock ? "Открывает три ветки улучшений" :
                node.effect == UpgradeEffect.PaddleDamage ? $"Бонус урона: +{PaddleDamageBonus}" +
                    (complete ? "" : $" → +{PaddleDamageBonus + node.amountPerLevel}") :
                node.effect == UpgradeEffect.Energy ? $"Запас энергии: {match.MaxEnergy}" +
                    (complete ? "" : $" → {match.MaxEnergy + node.amountPerLevel}") :
                $"Монеты за убийство: {spawner.BaseCoinsPerKill + KillCoinBonus}" +
                    (complete ? "" : $" → {spawner.BaseCoinsPerKill + KillCoinBonus + node.amountPerLevel}");
            tooltipText.text = $"<b>{node.title.ToUpperInvariant()}</b>\n\n{node.description}\n\n" +
                value +
                $"\n\nУровень: {level}/{node.maxLevel}\n" +
                (complete ? "Максимальный уровень" : $"Цена: {Price(node)} монет");
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
        }

        private void HideTooltip()
        {
            hoveredId = null;
            if (tooltip != null) tooltip.gameObject.SetActive(false);
        }

        private static RectTransform Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = color.a > 0.01f;
            return (RectTransform)go.transform;
        }

        private TMP_Text Text(string value, Transform parent, Vector2 position, Vector2 size,
            float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Place(rect, position, size);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (uiFont != null) text.font = uiFont;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void CreateDemoGraph()
        {
            nodes = new List<UpgradeNodeDefinition>
            {
                Node("root", "", "Старт", "Открывает ветки прокачки ракетки, энергии и наград.", UpgradeEffect.Unlock, -550, 0, 1, 0, 0, 0),
                Node("damage1", "root", "Сильный удар", "Каждое попадание по столу противника наносит больше урона.", UpgradeEffect.PaddleDamage, -270, 220, 3, 5, 5, 1),
                Node("bonuszone", "damage1", "Красная зона", "Открывает зону повышенного урона; следующие уровни усиливают её.", UpgradeEffect.BonusZone, 300, 220, 3, 15, 10, 1),
                Node("damage2", "damage1", "Мощная ракетка", "Дополнительный урон при каждом успешном ударе.", UpgradeEffect.PaddleDamage, 30, 220, 3, 12, 8, 2),
                Node("energyzone", "energy1", "Зелёная зона", "Открывает зону восстановления энергии; следующие уровни усиливают её.", UpgradeEffect.EnergyZone, 300, 0, 3, 15, 10, 1),
                Node("energy1", "root", "Выносливость", "Увеличивает максимальный и стартовый запас энергии.", UpgradeEffect.Energy, -270, 0, 3, 5, 5, 3),
                Node("energy2", "energy1", "Второе дыхание", "Еще больше энергии для длинного матча.", UpgradeEffect.Energy, 30, 0, 3, 12, 8, 5),
                Node("coinzone", "coins1", "Золотая зона", "Открывает зону дополнительных монет; следующие уровни усиливают её.", UpgradeEffect.CoinZone, 300, -220, 3, 15, 10, 1),
                Node("coins1", "root", "Награда за победу", "За убийство противника начисляется больше монет.", UpgradeEffect.KillCoins, -270, -220, 3, 5, 5, 2),
                Node("coins2", "coins1", "Богатый трофей", "Увеличивает доход за каждого побежденного противника.", UpgradeEffect.KillCoins, 30, -220, 3, 12, 8, 4)
            };
        }

        private static UpgradeNodeDefinition Node(string id, string parent, string title, string description,
            UpgradeEffect effect, float x, float y, int maxLevel, int price, int priceStep, int amount) =>
            new UpgradeNodeDefinition { id = id, parentId = parent, title = title, description = description,
                effect = effect, position = new Vector2(x, y), maxLevel = maxLevel,
                firstPrice = price, priceStep = priceStep, amountPerLevel = amount };
    }

    public sealed class UpgradeNodeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Enter;
        public Action Exit;
        public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => Exit?.Invoke();
    }
}
