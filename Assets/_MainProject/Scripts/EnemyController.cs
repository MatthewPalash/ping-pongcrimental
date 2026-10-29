using System;
using System.Collections;
using DamageNumbersPro;
using UnityEngine;

namespace TennisPrototype
{
    public enum EnemyState
    {
        Idle,
        Hurt,
        Dead,
        Won
    }

    [DisallowMultipleComponent]
    public sealed class EnemyController : MonoBehaviour
    {
        [Header("Characteristics")]
        [SerializeField] private EnemyConfig config;

        [Header("Match references")]
        [SerializeField] private ArcadeBallController ball;
        [SerializeField] private MatchControlState match;

        [Header("State objects (direct children)")]
        [SerializeField] private GameObject idleObject;
        [SerializeField] private GameObject hurtObject;
        [SerializeField] private GameObject deadObject;
        [SerializeField] private GameObject wonObject;

        [Header("Damage number")]
        [SerializeField] private DamageNumber damageNumberPrefab;
        [SerializeField] private Vector3 damageNumberOffset = new Vector3(0f, -0.1f, -0.2f);
        [SerializeField] private float damageNumberRandomXMin = -0.2f;
        [SerializeField] private float damageNumberRandomXMax = 0.2f;

        [Header("Strong hit text")]
        [SerializeField] private DamageNumber strongHitTextPrefab;
        [SerializeField] private Vector3 strongHitTextOffset = new Vector3(0f, 0.2f, -0.2f);

        public event Action<int, int> HealthChanged;
        public event Action<EnemyState> StateChanged;
        public event Action Died;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => config != null ? config.MaxHealth : 1;
        public string DisplayName => config != null ? config.DisplayName : name;
        public float HealthNormalized => (float)CurrentHealth / MaxHealth;
        public float WonDuration => config != null ? config.WonDuration : 1f;
        public int AttackDamage => config != null ? config.AttackDamage : 1;
        public float StrongHitSpeedMultiplier => config != null ? config.StrongHitSpeedMultiplier : 1f;
        public bool RollStrongHit()
        {
            if (config == null || !config.StrongHitEnabled) return false;
            float chance = config.StrongHitChance;
            return chance >= 100f || (chance > 0f && UnityEngine.Random.value < chance / 100f);
        }

        public void ShowStrongHitText()
        {
            if (strongHitTextPrefab != null)
                strongHitTextPrefab.Spawn(transform.position + strongHitTextOffset, "STRONG");
        }
        public EnemyState CurrentState { get; private set; }
        public bool IsDead => CurrentState == EnemyState.Dead;
        public float DeathKnockbackDuration => config != null ? config.DeathKnockbackDuration : 0.42f;

        private Coroutine returnToIdle;
        private EnemyVisualEffects visualEffects;
        private UpgradeTreeScreen upgrades;
        private BonusZoneSystem bonusZone;

        public void Configure(EnemyConfig settings, ArcadeBallController ballController,
            MatchControlState matchState, GameObject idle, GameObject hurt,
            GameObject dead, GameObject won)
        {
            config = settings;
            ball = ballController;
            match = matchState;
            idleObject = idle;
            hurtObject = hurt;
            deadObject = dead;
            wonObject = won;
        }

        public void BindRuntime(ArcadeBallController ballController, MatchControlState matchState)
        {
            if (isActiveAndEnabled && ball != null)
            {
                ball.EnemyTableHit -= OnEnemyTableHit;
                ball.PlayerMissedBall -= OnPlayerMissedBall;
            }
            ball = ballController;
            match = matchState;
            if (isActiveAndEnabled && ball != null)
            {
                ball.EnemyTableHit += OnEnemyTableHit;
                ball.PlayerMissedBall += OnPlayerMissedBall;
            }
        }

        private void Awake()
        {
            upgrades = FindAnyObjectByType<UpgradeTreeScreen>();
            bonusZone = FindAnyObjectByType<BonusZoneSystem>();
            CurrentHealth = MaxHealth;
            GetComponent<EnemyPaddleController>()?.ApplyConfig(config);
            visualEffects = GetComponent<EnemyVisualEffects>();
            if (visualEffects != null)
                visualEffects.Initialize(idleObject, hurtObject, deadObject, wonObject, config);
            SetState(EnemyState.Idle);
        }

        private void OnEnable()
        {
            if (ball != null)
            {
                ball.EnemyTableHit += OnEnemyTableHit;
                ball.PlayerMissedBall += OnPlayerMissedBall;
            }
        }

        private void OnDisable()
        {
            if (ball != null)
            {
                ball.EnemyTableHit -= OnEnemyTableHit;
                ball.PlayerMissedBall -= OnPlayerMissedBall;
            }
            CancelReturnToIdle();
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead || CurrentHealth <= 0) return;

            int previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (damageNumberPrefab != null)
            {
                float randomX = UnityEngine.Random.Range(
                    Mathf.Min(damageNumberRandomXMin, damageNumberRandomXMax),
                    Mathf.Max(damageNumberRandomXMin, damageNumberRandomXMax));
                Vector3 position = transform.position + damageNumberOffset + Vector3.right * randomX;
                damageNumberPrefab.Spawn(position, previousHealth - CurrentHealth);
            }
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            if (CurrentHealth == 0)
            {
                CancelReturnToIdle();
                SetState(EnemyState.Dead);
                Died?.Invoke();
                return;
            }

            ShowTemporaryState(EnemyState.Hurt, config != null ? config.HurtDuration : 1f);
        }

        private void OnEnemyTableHit(Vector3 landing)
        {
            if (config == null) return;
            int damage = config.DamagePerTableHit + (upgrades != null ? upgrades.PaddleDamageBonus : 0);
            float multiplier = bonusZone != null ? bonusZone.ConsumeMultiplier(landing) : 1f;
            TakeDamage(Mathf.RoundToInt(damage * multiplier));
        }

        private void OnPlayerMissedBall(Vector3 position)
        {
            if (!IsDead && (match == null || !match.GameEnded))
                ShowTemporaryState(EnemyState.Won, WonDuration);
        }

        public void CelebrateVictory()
        {
            if (IsDead) return;
            CancelReturnToIdle();
            SetState(EnemyState.Won);
        }

        private void ShowTemporaryState(EnemyState state, float duration)
        {
            CancelReturnToIdle();
            SetState(state);
            returnToIdle = StartCoroutine(ReturnToIdleAfter(duration));
        }

        private IEnumerator ReturnToIdleAfter(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            returnToIdle = null;
            if (!IsDead) SetState(EnemyState.Idle);
        }

        private void CancelReturnToIdle()
        {
            if (returnToIdle == null) return;
            StopCoroutine(returnToIdle);
            returnToIdle = null;
        }

        private void SetState(EnemyState state)
        {
            CurrentState = state;
            if (visualEffects != null) visualEffects.Show(state);
            else
            {
                if (idleObject != null) idleObject.SetActive(state == EnemyState.Idle);
                if (hurtObject != null) hurtObject.SetActive(state == EnemyState.Hurt);
                if (deadObject != null) deadObject.SetActive(state == EnemyState.Dead);
                if (wonObject != null) wonObject.SetActive(state == EnemyState.Won);
            }
            StateChanged?.Invoke(state);
        }
    }
}
