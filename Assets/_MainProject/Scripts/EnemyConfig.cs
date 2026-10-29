using UnityEngine;

namespace TennisPrototype
{
    [CreateAssetMenu(fileName = "Enemy Config", menuName = "Tennis Prototype/Enemy Config")]
    public sealed class EnemyConfig : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int maxHealth = 5;
        [SerializeField, Min(1)] private int damagePerTableHit = 1;
        [SerializeField, Min(1)] private int attackDamage = 1;
        [Header("Strong hit")]
        [SerializeField] private bool strongHitEnabled;
        [SerializeField, Range(0f, 100f), Tooltip("Chance of a strong hit, in percent.")]
        private float strongHitChance = 25f;
        [SerializeField, Min(1f), Tooltip("Ball speed multiplier for a strong hit.")]
        private float strongHitSpeedMultiplier = 2f;
        [SerializeField, Min(0.01f)] private float hurtDuration = 1f;
        [SerializeField, Min(0.01f)] private float wonDuration = 1f;
        [Header("Visual feedback")]
        [SerializeField] private Color visualTint = Color.white;
        [SerializeField, Min(0.01f)] private float stateTransitionDuration = 0.28f;
        [SerializeField, Min(0.01f)] private float hurtFlashDuration = 0.18f;
        [SerializeField, Min(0f)] private float hurtFlashIntensity = 4f;
        [SerializeField, Min(0.01f)] private float hurtKnockbackDuration = 0.32f;
        [SerializeField, Min(0f)] private float hurtKnockbackDistance = 0.18f;
        [SerializeField, Min(0.01f)] private float deathKnockbackDuration = 0.42f;
        [SerializeField, Min(0f)] private float deathKnockbackDistance = 0.65f;
        [SerializeField, Min(0.1f)] private float paddleFollowSpeed = 10f;
        [SerializeField, HideInInspector] private float paddleSwingAngle = 20f;
        [SerializeField, HideInInspector] private float paddleSwingRecovery = 12f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int DamagePerTableHit => Mathf.Max(1, damagePerTableHit);
        public int AttackDamage => Mathf.Max(1, attackDamage);
        public bool StrongHitEnabled => strongHitEnabled;
        public float StrongHitChance => Mathf.Clamp(strongHitChance, 0f, 100f);
        public float StrongHitSpeedMultiplier => Mathf.Max(1f, strongHitSpeedMultiplier);
        public float HurtDuration => Mathf.Max(0.01f, hurtDuration);
        public float WonDuration => Mathf.Max(0.01f, wonDuration);
        public float StateTransitionDuration => Mathf.Max(0.01f, stateTransitionDuration);
        public Color VisualTint => visualTint;
        public float HurtFlashDuration => Mathf.Max(0.01f, hurtFlashDuration);
        public float HurtFlashIntensity => Mathf.Max(0f, hurtFlashIntensity);
        public float HurtKnockbackDuration => Mathf.Max(0.01f, hurtKnockbackDuration);
        public float HurtKnockbackDistance => Mathf.Max(0f, hurtKnockbackDistance);
        public float DeathKnockbackDuration => Mathf.Max(0.01f, deathKnockbackDuration);
        public float DeathKnockbackDistance => Mathf.Max(0f, deathKnockbackDistance);
        public float PaddleFollowSpeed => Mathf.Max(0.1f, paddleFollowSpeed);
        public float PaddleSwingAngle => Mathf.Clamp(paddleSwingAngle, 0f, 35f);
        public float PaddleSwingRecovery => Mathf.Max(0.1f, paddleSwingRecovery);

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            damagePerTableHit = Mathf.Max(1, damagePerTableHit);
            attackDamage = Mathf.Max(1, attackDamage);
            strongHitChance = Mathf.Clamp(strongHitChance, 0f, 100f);
            strongHitSpeedMultiplier = Mathf.Max(1f, strongHitSpeedMultiplier);
            hurtDuration = Mathf.Max(0.01f, hurtDuration);
            wonDuration = Mathf.Max(0.01f, wonDuration);
            stateTransitionDuration = Mathf.Max(0.01f, stateTransitionDuration);
            hurtFlashDuration = Mathf.Max(0.01f, hurtFlashDuration);
            hurtFlashIntensity = Mathf.Max(0f, hurtFlashIntensity);
            hurtKnockbackDuration = Mathf.Max(0.01f, hurtKnockbackDuration);
            hurtKnockbackDistance = Mathf.Max(0f, hurtKnockbackDistance);
            deathKnockbackDuration = Mathf.Max(0.01f, deathKnockbackDuration);
            deathKnockbackDistance = Mathf.Max(0f, deathKnockbackDistance);
            paddleFollowSpeed = Mathf.Max(0.1f, paddleFollowSpeed);
            paddleSwingAngle = Mathf.Clamp(paddleSwingAngle, 0f, 35f);
            paddleSwingRecovery = Mathf.Max(0.1f, paddleSwingRecovery);
        }
    }
}
