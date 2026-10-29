using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace TennisPrototype
{
    public enum BonusZoneType { Damage, Coins, Energy }

    [DisallowMultipleComponent]
    public sealed class BonusZoneSystem : MonoBehaviour
    {
        [FormerlySerializedAs("forceEnabled")]
        [SerializeField] private bool forceDamageEnabled;
        [SerializeField] private bool forceCoinsEnabled;
        [SerializeField] private bool forceEnergyEnabled;
        [SerializeField, Range(0f, 100f)] private float spawnChance = 35f;
        [SerializeField, Range(0f, 100f)] private float coinSpawnChance = 25f;
        [SerializeField, Range(0f, 100f)] private float energySpawnChance = 25f;
        [SerializeField, Min(1f)] private float damageMultiplier = 2f;
        [SerializeField, Min(0f)] private float damageMultiplierPerLevel = .25f;
        [SerializeField, Min(0)] private int coinReward = 3;
        [SerializeField, Min(0)] private int coinsPerLevel = 2;
        [SerializeField, Min(0)] private int energyReward = 2;
        [SerializeField, Min(0)] private int energyPerLevel = 1;
        [FormerlySerializedAs("lifetime")]
        [SerializeField, Min(0.1f)] private float damageLifetime = 5f;
        [SerializeField, Min(0.1f)] private float coinLifetime = 5f;
        [SerializeField, Min(0.1f)] private float energyLifetime = 5f;
        [SerializeField, Min(0.01f)] private float animationDuration = .3f;
        [SerializeField, Min(0.05f)] private float hitAnimationDuration = .24f;
        [SerializeField] private Vector2 tableX = new Vector2(-.76f, .76f);
        [SerializeField] private Vector2 tableZ = new Vector2(.05f, 1.37f);
        [SerializeField] private float tableHeight = .761f;
        [SerializeField] private Vector2[] sizes = { new Vector2(.42f,.42f), new Vector2(.56f,.34f), new Vector2(.36f,.56f), new Vector2(.3f,.3f), new Vector2(.48f,.3f), new Vector2(.32f,.48f) };
        [SerializeField] private Vector2[] positions = { new Vector2(.2f,.5f), new Vector2(.8f,.5f), new Vector2(.5f,.75f), new Vector2(.5f,.25f), new Vector2(0f,0f), new Vector2(1f,0f), new Vector2(0f,1f), new Vector2(1f,1f), new Vector2(.5f,.5f) };
        [SerializeField] private Shader zoneShader;

        private sealed class Zone
        {
            public BonusZoneType type;
            public GameObject visual;
            public Material material;
            public Vector2 center, size;
            public float remaining, visibility, hitElapsed;
            public bool active, reacting;
        }
        private readonly List<Zone> zones = new List<Zone>();
        private UpgradeTreeScreen upgrades;
        private MatchControlState match;
        private PlayerWallet wallet;
        private ArcadeBallController ball;

        public static Color ZoneColor(BonusZoneType type) => type == BonusZoneType.Coins
            ? new Color(1f, .72f, .03f) : type == BonusZoneType.Energy
                ? new Color(.05f, 1f, .22f) : new Color(1f, .08f, .025f);

        private void Awake()
        {
            upgrades = FindAnyObjectByType<UpgradeTreeScreen>();
            match = FindAnyObjectByType<MatchControlState>();
            wallet = FindAnyObjectByType<PlayerWallet>();
            if (zoneShader == null) zoneShader = Shader.Find("Tennis/BonusZone");
            if (zoneShader == null) { Debug.LogError("Bonus zone shader is missing.", this); enabled = false; }
        }

        public void BindBall(ArcadeBallController newBall)
        {
            if (ball != null) ball.EnemyReturnedBall -= OnEnemyReturn;
            ball = newBall;
            if (ball != null) ball.EnemyReturnedBall += OnEnemyReturn;
            foreach (Zone zone in zones)
                if (newBall != null) HideImmediately(zone); else zone.active = false;
        }

        private int Level(BonusZoneType type) => upgrades == null ? 0 : upgrades.ZoneLevel(type);
        private void OnEnemyReturn()
        {
            TrySpawn(BonusZoneType.Damage, spawnChance);
            TrySpawn(BonusZoneType.Coins, coinSpawnChance);
            TrySpawn(BonusZoneType.Energy, energySpawnChance);
        }
        private void TrySpawn(BonusZoneType type, float chance)
        {
            bool forced = type == BonusZoneType.Damage ? forceDamageEnabled :
                type == BonusZoneType.Coins ? forceCoinsEnabled : forceEnergyEnabled;
            if ((!forced && Level(type) == 0) || chance <= 0f || Random.value * 100f >= chance ||
                sizes == null || sizes.Length == 0 || positions == null || positions.Length == 0) return;
            // One active zone per type; different types roll independently and may overlap.
            foreach (Zone existing in zones) if (existing.type == type && (existing.active || existing.reacting || existing.visibility > 0f)) return;
            Zone zone = GetZone(type);
            float minX = Mathf.Min(tableX.x, tableX.y), maxX = Mathf.Max(tableX.x, tableX.y);
            float minZ = Mathf.Min(tableZ.x, tableZ.y), maxZ = Mathf.Max(tableZ.x, tableZ.y);
            Vector2 candidate = sizes[Random.Range(0, sizes.Length)];
            zone.size = new Vector2(Mathf.Clamp(candidate.x, .01f, maxX - minX), Mathf.Clamp(candidate.y, .01f, maxZ - minZ));
            Vector2 pos = positions[Random.Range(0, positions.Length)];
            zone.center = new Vector2(Mathf.Lerp(minX + zone.size.x * .5f, maxX - zone.size.x * .5f, Mathf.Clamp01(pos.x)),
                Mathf.Lerp(minZ + zone.size.y * .5f, maxZ - zone.size.y * .5f, Mathf.Clamp01(pos.y)));
            zone.visual.transform.position = new Vector3(zone.center.x, tableHeight + (int)type * .001f, zone.center.y);
            zone.visual.transform.localScale = new Vector3(zone.size.x, zone.size.y, 1f);
            zone.material.SetVector("_ClipRect", new Vector4(minX, minZ, maxX, maxZ));
            zone.material.SetFloat("_Visibility", 0f);
            float lifetime = type == BonusZoneType.Damage ? damageLifetime :
                type == BonusZoneType.Coins ? coinLifetime : energyLifetime;
            zone.remaining = Mathf.Max(.1f, lifetime);
            zone.active = true;
            zone.visual.SetActive(true);
        }
        private Zone GetZone(BonusZoneType type)
        {
            foreach (Zone existing in zones) if (existing.type == type) return existing;
            Zone zone = new Zone { type = type };
            zone.visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            zone.visual.name = "Bonus Zone " + type;
            zone.visual.transform.SetParent(transform, false);
            Destroy(zone.visual.GetComponent<Collider>());
            zone.visual.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            zone.material = new Material(zoneShader);
            zone.material.SetColor("_ZoneColor", ZoneColor(type));
            zone.visual.GetComponent<Renderer>().sharedMaterial = zone.material;
            zone.visual.SetActive(false);
            zones.Add(zone);
            return zone;
        }

        // Apply every overlapping zone before damage, so lethal hits still award coins and energy.
        public float ConsumeMultiplier(Vector3 landing)
        {
            float multiplier = 1f;
            if (landing.x < Mathf.Min(tableX.x, tableX.y) || landing.x > Mathf.Max(tableX.x, tableX.y) ||
                landing.z < Mathf.Min(tableZ.x, tableZ.y) || landing.z > Mathf.Max(tableZ.x, tableZ.y)) return multiplier;
            foreach (Zone zone in zones)
            {
                if (!zone.active || zone.remaining <= 0f || Mathf.Abs(landing.x - zone.center.x) > zone.size.x * .5f ||
                    Mathf.Abs(landing.z - zone.center.y) > zone.size.y * .5f) continue;
                zone.active = false; zone.reacting = true; zone.hitElapsed = 0f; zone.visibility = 1f;
                int extraLevels = Mathf.Max(0, Level(zone.type) - 1);
                switch (zone.type)
                {
                    case BonusZoneType.Damage: multiplier *= Mathf.Max(1f, damageMultiplier + damageMultiplierPerLevel * extraLevels); break;
                    case BonusZoneType.Coins: if (wallet != null) wallet.AddMatchCoins(Mathf.Max(0, coinReward + coinsPerLevel * extraLevels)); break;
                    case BonusZoneType.Energy: if (match != null) match.RestoreEnergy(Mathf.Max(0, energyReward + energyPerLevel * extraLevels)); break;
                }
            }
            return multiplier;
        }
        private void Update()
        {
            bool paused = match != null && match.CurrentPhase == MatchPhase.Pause;
            foreach (Zone zone in zones)
            {
                if (match != null && match.CurrentPhase != MatchPhase.ActiveMatch && !paused) zone.active = false;
                if (paused) continue;
                if (zone.active) { zone.remaining -= Time.deltaTime; if (zone.remaining <= 0f) zone.active = false; }
                if (zone.reacting)
                {
                    zone.hitElapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(zone.hitElapsed / Mathf.Max(.05f, hitAnimationDuration));
                    float expansion = 1f + .16f * Mathf.Sin(progress * Mathf.PI);
                    zone.visual.transform.localScale = new Vector3(zone.size.x * expansion, zone.size.y * expansion, 1f);
                    zone.material.SetFloat("_HitFlash", 1f - progress);
                    zone.material.SetFloat("_Visibility", 1f - progress * progress);
                    if (progress >= 1f) HideImmediately(zone);
                    continue;
                }
                zone.visibility = Mathf.MoveTowards(zone.visibility, zone.active ? 1f : 0f, Time.deltaTime / Mathf.Max(.01f, animationDuration));
                zone.material.SetFloat("_Visibility", Mathf.SmoothStep(0f, 1f, zone.visibility));
                if (!zone.active && zone.visibility <= 0f) zone.visual.SetActive(false);
            }
        }
        private static void HideImmediately(Zone zone)
        {
            zone.active = false; zone.reacting = false; zone.visibility = 0f; zone.remaining = 0f;
            zone.material.SetFloat("_HitFlash", 0f); zone.visual.SetActive(false);
        }
        private void OnDisable() { BindBall(null); foreach (Zone zone in zones) HideImmediately(zone); }
        private void OnDestroy() { foreach (Zone zone in zones) if (zone.material != null) Destroy(zone.material); }
    }
}
