using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TennisPrototype
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Level")]
        [SerializeField, Min(1)] private int enemiesToDefeat = 5;
        [SerializeField] private List<EnemyController> enemyPrefabs = new List<EnemyController>();
        [SerializeField] private ArcadeBallController ballPrefab;

        [Header("Rewards")]
        [SerializeField, Min(0)] private int coinsPerHit = 1;
        [SerializeField, Min(0)] private int coinsPerKill = 5;

        [Header("Entrance")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform battlePoint;
        [SerializeField, Min(0.01f)] private float approachDuration = 0.55f;
        [SerializeField, Min(0f)] private float handRiseDepth = 0.55f;
        [SerializeField, Min(0.01f)] private float handRiseDuration = 0.3f;

        [Header("Opponent change")]
        [SerializeField, Min(0f)] private float nextEnemyDelay = 1f;

        [Header("Exit")]
        [SerializeField, Min(0f)] private float deathExitDelay = 2f;
        [SerializeField, Min(0.01f)] private float deathExitDuration = 0.55f;
        [SerializeField, Min(0f)] private float deathExitDistance = 12f;

        [Header("Scene references")]
        [SerializeField] private MatchControlState match;
        [SerializeField] private PaddleController playerPaddle;
        [SerializeField] private FixedPlayerCamera playerCamera;
        [SerializeField] private PaddleImpactFeedback impactFeedback;
        [SerializeField] private EnemyUI enemyUI;
        [SerializeField] private PlayerWallet playerWallet;

        public event Action<int, int> ProgressChanged;
        public event Action LevelCompleted;

        public int DefeatedCount { get; private set; }
        public int EnemiesToDefeat => enemiesToDefeat;
        public int BaseCoinsPerKill => coinsPerKill;
        public EnemyController CurrentEnemy { get; private set; }

        private EnemyController lastPrefab;
        private ArcadeBallController currentBall;
        private UpgradeTreeScreen upgrades;
        private BonusZoneSystem bonusZone;
        private readonly List<EnemyController> spawnedEnemies = new List<EnemyController>();

        public void Configure(List<EnemyController> prefabs, ArcadeBallController ball,
            Transform entrance, Transform destination, MatchControlState matchState,
            PaddleController player, FixedPlayerCamera camera,
            PaddleImpactFeedback feedback, EnemyUI ui)
        {
            enemyPrefabs = prefabs;
            ballPrefab = ball;
            spawnPoint = entrance;
            battlePoint = destination;
            match = matchState;
            playerPaddle = player;
            playerCamera = camera;
            impactFeedback = feedback;
            enemyUI = ui;
            playerWallet = player != null ? player.GetComponent<PlayerWallet>() : null;
        }

        private void Start()
        {
            upgrades = match != null ? match.GetComponent<UpgradeTreeScreen>() : null;
            bonusZone = FindAnyObjectByType<BonusZoneSystem>();
            if (match != null) match.GameEndedEvent += OnGameEnded;
            if (playerWallet == null && playerPaddle != null)
                playerWallet = playerPaddle.GetComponent<PlayerWallet>();
            if (match == null || playerPaddle == null || spawnPoint == null ||
                battlePoint == null || ballPrefab == null || playerWallet == null ||
                ValidPrefabCount() == 0)
            {
                Debug.LogError("EnemySpawner needs match, player paddle and wallet, spawn/battle points, ball prefab and at least one enemy prefab.", this);
                enabled = false;
                return;
            }

            if (enemyUI != null) enemyUI.SetEnemy(null);
            if (playerCamera != null) playerCamera.SetBall(null);
            if (impactFeedback != null) impactFeedback.Configure(null, playerPaddle);
            ProgressChanged?.Invoke(0, enemiesToDefeat);
            if (upgrades == null) BeginMatch();
        }

        public void BeginMatch()
        {
            if (!enabled || match == null || match.GameEnded || CurrentEnemy != null) return;
            DefeatedCount = 0;
            ProgressChanged?.Invoke(0, enemiesToDefeat);
            match.SetPhase(MatchPhase.OpponentTransition);
            StartCoroutine(SpawnNext());
        }

        public void ResetForNewMatch()
        {
            if (bonusZone != null) bonusZone.BindBall(null);
            StopAllCoroutines();
            foreach (EnemyController enemy in spawnedEnemies)
            {
                if (enemy == null) continue;
                enemy.Died -= OnCurrentEnemyDied;
                Destroy(enemy.gameObject);
            }
            spawnedEnemies.Clear();
            CurrentEnemy = null;
            if (currentBall != null)
            {
                currentBall.EnemyTableHit -= OnPlayerScoredHit;
                Destroy(currentBall.gameObject);
                currentBall = null;
            }
            if (enemyUI != null) enemyUI.SetEnemy(null);
            if (playerCamera != null) playerCamera.SetBall(null);
            if (impactFeedback != null) impactFeedback.Configure(null, playerPaddle);
        }

        private IEnumerator SpawnNext()
        {
            if (match == null || match.GameEnded) yield break;
            EnemyController prefab = ChoosePrefab();
            if (prefab == null) yield break;
            lastPrefab = prefab;

            EnemyController enemy = Instantiate(prefab, spawnPoint.position, battlePoint.rotation);
            spawnedEnemies.Add(enemy);
            CurrentEnemy = enemy;
            enemy.BindRuntime(null, match);
            enemy.Died += OnCurrentEnemyDied;
            if (enemyUI != null) enemyUI.SetEnemy(enemy);

            EnemyPaddleController paddle = enemy.GetComponent<EnemyPaddleController>();
            if (paddle == null)
            {
                Debug.LogError("Enemy prefab needs EnemyPaddleController.", enemy);
                yield break;
            }
            paddle.HideForEntrance();

            Vector3 start = spawnPoint.position;
            float elapsed = 0f;
            while (elapsed < approachDuration)
            {
                if (enemy == null || match.GameEnded) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / approachDuration);
                enemy.transform.position = Vector3.LerpUnclamped(start, battlePoint.position,
                    1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }
            enemy.transform.position = battlePoint.position;

            paddle.BeginHandRise(battlePoint.position, handRiseDepth);
            bool firstStrongHit = enemy.RollStrongHit();
            paddle.PrepareStrike(firstStrongHit);
            elapsed = 0f;
            while (elapsed < handRiseDuration)
            {
                if (enemy == null || match.GameEnded) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / handRiseDuration);
                paddle.SetHandRiseProgress(t * t * (3f - 2f * t), handRiseDepth);
                float preparationTime = Mathf.Min(handRiseDuration, paddle.StrikePreparationDuration);
                paddle.SetStrikeProgress(1f - Mathf.Clamp01((handRiseDuration - elapsed) /
                    Mathf.Max(0.02f, preparationTime)));
                yield return null;
            }
            paddle.FinishHandRise();
            paddle.SetStrikeProgress(1f);
            if (match.GameEnded || CurrentEnemy != enemy || enemy.IsDead) yield break;
            BeginAttack(enemy, paddle, firstStrongHit);
        }

        private void BeginAttack(EnemyController enemy, EnemyPaddleController paddle, bool firstStrongHit)
        {
            if (match.GameEnded) return;
            currentBall = Instantiate(ballPrefab, paddle.ServePoint, Quaternion.identity);
            BallPaddleContact contact = currentBall.GetComponent<BallPaddleContact>();
            TrailRenderer trail = currentBall.GetComponent<TrailRenderer>();
            Transform visual = currentBall.transform.Find("Ball Visual");
            currentBall.Configure(match, playerPaddle, paddle, contact, visual, trail, true, true, firstStrongHit);
            currentBall.EnemyTableHit += OnPlayerScoredHit;
            enemy.BindRuntime(currentBall, match);
            if (bonusZone != null) bonusZone.BindBall(currentBall);
            if (playerCamera != null) playerCamera.SetBall(currentBall);
            if (impactFeedback != null) impactFeedback.Configure(currentBall, playerPaddle);
            match.SetPhase(MatchPhase.ActiveMatch);
        }

        private void OnCurrentEnemyDied()
        {
            if (bonusZone != null) bonusZone.BindBall(null);
            EnemyController defeated = CurrentEnemy;
            if (defeated == null) return;
            playerWallet.AddMatchCoins(coinsPerKill + (upgrades != null ? upgrades.KillCoinBonus : 0));
            defeated.Died -= OnCurrentEnemyDied;
            defeated.BindRuntime(null, match);
            CurrentEnemy = null;

            if (playerCamera != null) playerCamera.SetBall(null);
            if (impactFeedback != null) impactFeedback.Configure(null, playerPaddle);
            if (currentBall != null)
            {
                currentBall.EnemyTableHit -= OnPlayerScoredHit;
                Destroy(currentBall.gameObject);
            }
            currentBall = null;

            EnemyPaddleController paddle = defeated.GetComponent<EnemyPaddleController>();
            if (paddle != null) paddle.HidePaddle();
            StartCoroutine(ExitAfterDeath(defeated));

            DefeatedCount++;
            ProgressChanged?.Invoke(DefeatedCount, enemiesToDefeat);
            if (match.GameEnded) return;
            if (DefeatedCount >= enemiesToDefeat)
            {
                match.SetPhase(MatchPhase.Victory);
                LevelCompleted?.Invoke();
                return;
            }

            match.PrepareNextOpponent();
            StartCoroutine(SpawnAfterDelay(defeated.DeathKnockbackDuration));
        }

        private IEnumerator SpawnAfterDelay(float deathKnockbackDuration)
        {
            float delay = deathKnockbackDuration + nextEnemyDelay;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (match.GameEnded) yield break;
            yield return SpawnNext();
        }

        private void OnGameEnded()
        {
            if (match.CurrentPhase == MatchPhase.Defeat && CurrentEnemy != null)
                CurrentEnemy.CelebrateVictory();
        }

        private IEnumerator ExitAfterDeath(EnemyController defeated)
        {
            yield return new WaitForSecondsRealtime(defeated.DeathKnockbackDuration + deathExitDelay);
            if (defeated == null) yield break;
            Vector3 start = defeated.transform.position;
            Vector3 end = start + Vector3.forward * deathExitDistance;
            float elapsed = 0f;
            while (elapsed < deathExitDuration)
            {
                if (defeated == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / deathExitDuration);
                defeated.transform.position = Vector3.LerpUnclamped(start, end, t * t);
                yield return null;
            }
            if (defeated != null) Destroy(defeated.gameObject);
            spawnedEnemies.Remove(defeated);
        }

        private void OnPlayerScoredHit(Vector3 landing)
        {
            if (CurrentEnemy != null && !CurrentEnemy.IsDead)
                playerWallet.AddMatchCoins(coinsPerHit);
        }

        private EnemyController ChoosePrefab()
        {
            List<EnemyController> choices = new List<EnemyController>();
            foreach (EnemyController prefab in enemyPrefabs)
                if (prefab != null && !choices.Contains(prefab)) choices.Add(prefab);
            if (choices.Count > 1) choices.Remove(lastPrefab);
            return choices.Count == 0 ? null : choices[UnityEngine.Random.Range(0, choices.Count)];
        }

        private int ValidPrefabCount()
        {
            foreach (EnemyController prefab in enemyPrefabs)
                if (prefab != null) return 1;
            return 0;
        }

        private void OnDestroy()
        {
            if (match != null) match.GameEndedEvent -= OnGameEnded;
            if (CurrentEnemy != null) CurrentEnemy.Died -= OnCurrentEnemyDied;
            if (currentBall != null) currentBall.EnemyTableHit -= OnPlayerScoredHit;
        }

        private void OnValidate()
        {
            enemiesToDefeat = Mathf.Max(1, enemiesToDefeat);
            coinsPerHit = Mathf.Max(0, coinsPerHit);
            coinsPerKill = Mathf.Max(0, coinsPerKill);
        }
    }
}
