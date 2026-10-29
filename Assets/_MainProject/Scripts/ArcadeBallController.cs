using System;
using UnityEngine;

namespace TennisPrototype
{
    public enum ArcadeBallPhase
    {
        WaitingToServe,
        ToPlayerTable,
        ToPlayerPaddle,
        ToEnemyTable,
        ToEnemyPaddle
    }

    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(BallPaddleContact))]
    public sealed class ArcadeBallController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchControlState match;
        [SerializeField] private PaddleController playerPaddle;
        [SerializeField] private EnemyPaddleController enemyPaddle;
        [SerializeField] private BallPaddleContact paddleContact;
        [SerializeField] private Transform visual;
        [SerializeField] private TrailRenderer trail;

        [Header("Ball and flight")]
        [SerializeField, Min(0.015f)] private float radius = 0.035f;
        [SerializeField, Min(0.1f)] private float enemyBallSpeed = 3.6f;
        [SerializeField, Min(0.1f)] private float playerBallSpeed = 5f;
        [SerializeField, Min(0.05f)] private float mainArcHeight = 0.28f;
        [SerializeField, Min(0.02f)] private float bounceHeight = 0.08f;
        [SerializeField, Min(0.05f)] private float bounceDuration = 0.52f;
        [SerializeField, Min(1f)] private float playerBounceSpeedMultiplier = 1.25f;
        [SerializeField, Min(0f)] private float netClearance = 0.04f;
        [SerializeField, Min(0f)] private float serveDelay = 1f;
        [SerializeField, Min(0f)] private float visualSpinPerMetre = 200f;

        [Header("Safe table targets")]
        [SerializeField] private float tableTop = 0.785f;
        [SerializeField] private float netTop = 1f;
        [SerializeField] private Vector2 playerLandingX = new Vector2(-0.54f, 0.54f);
        [SerializeField] private Vector2 playerLandingZ = new Vector2(-1.12f, -0.42f);
        [SerializeField] private Vector2 enemyLandingX = new Vector2(-0.54f, 0.54f);
        [SerializeField] private Vector2 enemyLandingZ = new Vector2(0.42f, 1.12f);
        [SerializeField] private float playerMissZ = -2.8f;
        [SerializeField] private float playerMissY = 0.9f;

        [Header("Player aim")]
        [SerializeField, Min(0f)] private float paddleYawInfluence = 0.9f;
        [SerializeField, Min(0f)] private float lateralMotionInfluence = 0.055f;
        [SerializeField, Min(0f)] private float contactPointInfluence = 0.15f;
        [SerializeField, Min(0f)] private float depthMotionInfluence = 0.08f;
        [SerializeField, Min(0f)] private float arcMotionInfluence = 0.055f;
        [SerializeField, Min(0.01f)] private float swingAimThreshold = 0.15f;

        [Header("Contact feedback")]
        [SerializeField, Min(0f)] private float contactDepthTolerance = 0.1f;
        [SerializeField, Min(0f)] private float contactAreaPadding = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool drawTrajectoryGizmos = true;

        public event Action<Vector3> BallServed;
        public event Action<Vector3, Vector3> PlayerHitBall;
        public event Action<Vector3> EnemyTableHit;
        public event Action<Vector3> PlayerMissedBall;
        public event Action EnemyReturnedBall;

        public ArcadeBallPhase Phase { get; private set; }
        public Vector3 LastPlayerLanding { get; private set; }
        public Vector3 LastEnemyLanding { get; private set; }
        public Vector3 LastPlayerAim { get; private set; }
        public int ServeCount { get; private set; }
        public int PlayerHitCount { get; private set; }
        public int EnemyHitCount { get; private set; }

        private Rigidbody body;
        private SphereCollider sphere;
        private Vector3 simulatedPosition;
        private Vector3 previousPaddlePosition;
        private bool hasPreviousPaddle;
        private Vector3 segmentStart;
        private Vector3 segmentEnd;
        private float segmentDuration;
        private float segmentElapsed;
        private float segmentArc;
        private float currentEnemyShotSpeed;
        private float serveTimer;
        private float strikeTimelineElapsed;
        private float strikeTimelineDelay;
        private float strikeTimelineDuration;
        private bool shotPrepared;
        private bool preparedStrongHit;
        private float appliedRadius = -1f;
        private bool finished;
        private bool immediateAttack;
        private bool usingDefeatPhysics;
        private PhysicsMaterial defeatPhysicsMaterial;

        public void Configure(MatchControlState matchState, PaddleController player,
            EnemyPaddleController opponent, BallPaddleContact contact, Transform visualRoot,
            TrailRenderer trailRenderer, bool attackImmediately = false,
            bool initialShotPrepared = false, bool initialStrongHit = false)
        {
            if (Application.isPlaying && paddleContact != null)
                paddleContact.HitByPaddle -= OnPaddleTrigger;
            if (Application.isPlaying && match != null)
                match.GameEndedEvent -= OnGameEnded;
            match = matchState;
            playerPaddle = player;
            enemyPaddle = opponent;
            paddleContact = contact;
            visual = visualRoot;
            trail = trailRenderer;
            immediateAttack = attackImmediately;
            shotPrepared = initialShotPrepared;
            preparedStrongHit = initialStrongHit;
            if (Application.isPlaying && isActiveAndEnabled && paddleContact != null)
                paddleContact.HitByPaddle += OnPaddleTrigger;
            if (Application.isPlaying && isActiveAndEnabled && match != null)
                match.GameEndedEvent += OnGameEnded;
            ApplyRadius();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            sphere = GetComponent<SphereCollider>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            ApplyRadius();
            if (paddleContact != null) paddleContact.HitByPaddle += OnPaddleTrigger;
            if (match != null) match.GameEndedEvent += OnGameEnded;
        }

        private void OnDestroy()
        {
            if (paddleContact != null) paddleContact.HitByPaddle -= OnPaddleTrigger;
            if (match != null) match.GameEndedEvent -= OnGameEnded;
            if (defeatPhysicsMaterial != null) Destroy(defeatPhysicsMaterial);
        }

        private void OnGameEnded()
        {
            if (shotPrepared && enemyPaddle != null) enemyPaddle.CancelStrike();
            shotPrepared = false;
            if (match == null || match.CurrentPhase != MatchPhase.Defeat || usingDefeatPhysics)
                return;

            usingDefeatPhysics = true;
            finished = true;
            StopAllCoroutines();
            Vector3 velocity = Vector3.zero;
            if (Phase != ArcadeBallPhase.WaitingToServe && segmentDuration > 0f)
            {
                float t = Mathf.Clamp01(segmentElapsed / segmentDuration);
                velocity = (segmentEnd - segmentStart +
                    Vector3.up * (4f * segmentArc * (1f - 2f * t))) / segmentDuration;
            }
            if (velocity.sqrMagnitude < 0.01f)
                velocity = new Vector3(0f, 0.5f, -1f);

            defeatPhysicsMaterial = new PhysicsMaterial("Ball defeat bounce")
            {
                bounciness = 0.7f,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            sphere.sharedMaterial = defeatPhysicsMaterial;
            body.position = simulatedPosition;
            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            if (trail != null) trail.emitting = false;
        }

        private void Start()
        {
            if (enemyPaddle == null)
            {
                finished = true;
                if (trail != null) { trail.emitting = false; trail.Clear(); }
                return;
            }
            if (!shotPrepared) enemyPaddle.ResetPaddle();
            simulatedPosition = enemyPaddle.ServePoint;
            body.position = simulatedPosition;
            serveTimer = immediateAttack ? 0f : serveDelay;
            if (!shotPrepared && serveTimer > 0f) PrepareEnemyShot();
            Phase = ArcadeBallPhase.WaitingToServe;
            if (trail != null) { trail.emitting = false; trail.Clear(); }
        }

        private void Update()
        {
            if (!Mathf.Approximately(appliedRadius, radius)) ApplyRadius();
        }

        private void FixedUpdate()
        {
            if (finished) return;
            if (match != null && match.CurrentPhase != MatchPhase.ActiveMatch) return;
            Vector3 currentPaddlePosition = playerPaddle.GetComponent<Rigidbody>().position;
            if (!hasPreviousPaddle)
            {
                previousPaddlePosition = currentPaddlePosition;
                hasPreviousPaddle = true;
            }

            if (Phase == ArcadeBallPhase.WaitingToServe)
            {
                serveTimer -= Time.fixedDeltaTime;
                if (shotPrepared && serveTimer > 0f)
                    enemyPaddle.SetStrikeProgress(1f - Mathf.Clamp01(serveTimer /
                        Mathf.Max(0.02f, enemyPaddle.StrikePreparationDuration)));
                if (serveTimer <= 0f)
                {
                    if (!shotPrepared) PrepareEnemyShot();
                    enemyPaddle.SetStrikeProgress(1f);
                    simulatedPosition = enemyPaddle.ServePoint;
                    body.MovePosition(simulatedPosition);
                    enemyPaddle.Swing();
                    LaunchEnemyShot();
                }
                previousPaddlePosition = currentPaddlePosition;
                return;
            }

            Vector3 ballBefore = simulatedPosition;
            float previousSegmentElapsed = segmentElapsed;
            segmentElapsed = Mathf.Min(segmentElapsed + Time.fixedDeltaTime, segmentDuration);
            simulatedPosition = Evaluate(segmentElapsed / segmentDuration);
            if (shotPrepared && (Phase == ArcadeBallPhase.ToEnemyTable || Phase == ArcadeBallPhase.ToEnemyPaddle))
            {
                strikeTimelineElapsed += segmentElapsed - previousSegmentElapsed;
                enemyPaddle.SetStrikeProgress((strikeTimelineElapsed - strikeTimelineDelay) /
                    Mathf.Max(0.02f, strikeTimelineDuration));
            }
            body.MovePosition(simulatedPosition);
            if (visual != null)
                visual.Rotate(Vector3.right,
                    Vector3.Distance(ballBefore, simulatedPosition) * visualSpinPerMetre, Space.Self);

            if (Phase == ArcadeBallPhase.ToPlayerPaddle &&
                TrySweptPaddleHit(ballBefore, simulatedPosition, previousPaddlePosition,
                    currentPaddlePosition, out Vector3 contact, out Vector3 ballAtContact))
            {
                RegisterPlayerHit(contact, ballAtContact);
                previousPaddlePosition = currentPaddlePosition;
                return;
            }

            previousPaddlePosition = currentPaddlePosition;
            if (segmentElapsed < segmentDuration) return;

            switch (Phase)
            {
                case ArcadeBallPhase.ToPlayerTable:
                    BeginPlayerBounce();
                    break;
                case ArcadeBallPhase.ToPlayerPaddle:
                    Miss();
                    break;
                case ArcadeBallPhase.ToEnemyTable:
                    EnemyHitCount++;
                    EnemyTableHit?.Invoke(LastEnemyLanding);
                    if (match != null && match.CurrentPhase != MatchPhase.ActiveMatch)
                    {
                        finished = true;
                        if (trail != null) trail.emitting = false;
                        return;
                    }
                    BeginEnemyBounce();
                    break;
                case ArcadeBallPhase.ToEnemyPaddle:
                    enemyPaddle.Swing();
                    EnemyReturnedBall?.Invoke();
                    LaunchEnemyShot();
                    break;
            }
        }

        private void LaunchEnemyShot()
        {
            EnemyController enemy = enemyPaddle != null
                ? enemyPaddle.GetComponent<EnemyController>() : null;
            currentEnemyShotSpeed = enemyBallSpeed *
                (preparedStrongHit && enemy != null ? enemy.StrongHitSpeedMultiplier : 1f);
            if (preparedStrongHit && enemy != null) enemy.ShowStrongHitText();
            shotPrepared = false;
            LastPlayerLanding = new Vector3(
                UnityEngine.Random.Range(playerLandingX.x, playerLandingX.y),
                tableTop + radius,
                UnityEngine.Random.Range(playerLandingZ.x, playerLandingZ.y));
            Phase = ArcadeBallPhase.ToPlayerTable;
            BeginSegment(simulatedPosition, LastPlayerLanding, currentEnemyShotSpeed, mainArcHeight, true);
            ServeCount++;
            BallServed?.Invoke(LastPlayerLanding);
            if (trail != null) { trail.Clear(); trail.emitting = true; }
        }

        private void BeginPlayerBounce()
        {
            Vector3 target = new Vector3(LastPlayerLanding.x, playerMissY, playerMissZ);
            Phase = ArcadeBallPhase.ToPlayerPaddle;
            BeginSegment(simulatedPosition, target,
                currentEnemyShotSpeed * playerBounceSpeedMultiplier, bounceHeight, false);
        }

        private void RegisterPlayerHit(Vector3 contact, Vector3 ballAtContact)
        {
            if (Phase != ArcadeBallPhase.ToPlayerPaddle) return;
            simulatedPosition = ballAtContact;
            body.position = ballAtContact;
            PaddleHitDetector surface = playerPaddle.HitDetector;
            float normalizedContact = surface.NormalizedHorizontalPosition(contact);
            Vector3 paddleForward = playerPaddle.transform.forward;
            Vector2 aimMotion = playerPaddle.RecentAimVelocity;
            LastPlayerAim = CalculatePlayerAim(ballAtContact, normalizedContact,
                paddleForward, aimMotion, playerPaddle.TargetPosition.y);
            LastEnemyLanding = new Vector3(
                Mathf.Clamp(LastPlayerAim.x, enemyLandingX.x, enemyLandingX.y),
                tableTop + radius,
                Mathf.Clamp(LastPlayerAim.z, enemyLandingZ.x, enemyLandingZ.y));
            Phase = ArcadeBallPhase.ToEnemyTable;
            BeginSegment(ballAtContact, LastEnemyLanding, playerBallSpeed, LastPlayerAim.y, true);
            PrepareEnemyShot();
            Vector3 expectedContact = enemyPaddle.GetReturnPoint(LastEnemyLanding.x);
            float bounceTime = CalculateSegmentDuration(LastEnemyLanding, expectedContact,
                playerBallSpeed, Mathf.Max(0.35f, bounceDuration * 0.85f));
            ScheduleStrike(segmentDuration + bounceTime);
            PlayerHitCount++;
            PlayerHitBall?.Invoke(contact, LastEnemyLanding);
        }

        private Vector3 CalculatePlayerAim(Vector3 ballAtContact, float normalizedContact,
            Vector3 paddleForward, Vector2 aimMotion, float paddleDepth)
        {
            float sideReach = Mathf.Max(Mathf.Abs(enemyLandingX.x), Mathf.Abs(enemyLandingX.y));
            float landingX;
            if (Mathf.Abs(aimMotion.x) >= swingAimThreshold)
            {
                // An active left/right stroke must choose that side of the opponent's table.
                float strokeAmount = Mathf.Max(Mathf.Abs(aimMotion.x * lateralMotionInfluence),
                    sideReach * 0.3f);
                landingX = Mathf.Sign(aimMotion.x) * Mathf.Min(strokeAmount, sideReach);
            }
            else if (Mathf.Abs(aimMotion.y) >= swingAimThreshold)
            {
                // A mostly forward/back stroke aims along the table rather than to a side.
                landingX = Mathf.Clamp(ballAtContact.x * 0.15f +
                    normalizedContact * contactPointInfluence * 0.15f,
                    -sideReach * 0.3f, sideReach * 0.3f);
            }
            else
            {
                float poseAim = Mathf.Clamp(paddleForward.x * paddleYawInfluence,
                    -sideReach * 0.35f, sideReach * 0.35f);
                landingX = ballAtContact.x * 0.45f + poseAim +
                    normalizedContact * contactPointInfluence;
            }

            float depth = Mathf.InverseLerp(playerPaddle.DepthLimits.x,
                playerPaddle.DepthLimits.y, paddleDepth);
            float landingZ = Mathf.Lerp(enemyLandingZ.x, enemyLandingZ.y, depth) +
                aimMotion.y * depthMotionInfluence;
            float arc = Mathf.Clamp(mainArcHeight + aimMotion.y * arcMotionInfluence,
                0.15f, 1.2f);
            return new Vector3(landingX, arc, landingZ);
        }

        private void BeginEnemyBounce()
        {
            bool preparedBeforeBounce = shotPrepared;
            if (!preparedBeforeBounce) PrepareEnemyShot();
            Vector3 contact = enemyPaddle.AimReturn(LastEnemyLanding.x);
            Phase = ArcadeBallPhase.ToEnemyPaddle;
            BeginSegment(simulatedPosition, contact, playerBallSpeed, bounceHeight, false,
                Mathf.Max(0.35f, bounceDuration * 0.85f));
            segmentEnd += Vector3.back * enemyPaddle.StrikeForwardDistance;
            if (!preparedBeforeBounce) ScheduleStrike(segmentDuration);
        }

        private void PrepareEnemyShot()
        {
            EnemyController enemy = enemyPaddle != null
                ? enemyPaddle.GetComponent<EnemyController>() : null;
            preparedStrongHit = enemy != null && enemy.RollStrongHit();
            shotPrepared = true;
            enemyPaddle.PrepareStrike(preparedStrongHit);
        }

        private void ScheduleStrike(float availableTime)
        {
            strikeTimelineElapsed = 0f;
            strikeTimelineDuration = Mathf.Min(availableTime, enemyPaddle.StrikePreparationDuration);
            strikeTimelineDelay = Mathf.Max(0f, availableTime - strikeTimelineDuration);
        }

        private void Miss()
        {
            finished = true;
            PlayerMissedBall?.Invoke(simulatedPosition);
            if (trail != null) trail.emitting = false;
            EnemyController enemy = enemyPaddle != null
                ? enemyPaddle.GetComponent<EnemyController>() : null;
            if (match != null) match.TakePlayerDamage(enemy != null ? enemy.AttackDamage : 1);
            if (match == null || !match.GameEnded)
                StartCoroutine(ServeAfterMiss(enemy != null ? enemy.WonDuration : 1f));
        }

        private System.Collections.IEnumerator ServeAfterMiss(float delay)
        {
            PrepareEnemyShot();
            float elapsed = 0f;
            while (elapsed < delay)
            {
                if (match != null && match.GameEnded) yield break;
                elapsed += Time.unscaledDeltaTime;
                float remaining = Mathf.Max(0f, delay - elapsed);
                enemyPaddle.SetStrikeProgress(1f - Mathf.Clamp01(remaining /
                    Mathf.Max(0.02f, enemyPaddle.StrikePreparationDuration)));
                yield return null;
            }
            if (match != null && match.GameEnded) yield break;
            if (enemyPaddle == null) yield break;
            simulatedPosition = enemyPaddle.ServePoint;
            body.position = simulatedPosition;
            serveTimer = 0f;
            Phase = ArcadeBallPhase.WaitingToServe;
            if (trail != null) trail.Clear();
            finished = false;
        }

        private void OnPaddleTrigger(PaddleHitDetector surface, Vector3 contact)
        {
            if (finished || usingDefeatPhysics) return;
            RegisterPlayerHit(contact, simulatedPosition);
        }

        private void BeginSegment(Vector3 start, Vector3 end, float speed, float arc,
            bool crossesNet, float minimumDuration = 0f)
        {
            segmentStart = start;
            segmentEnd = end;
            segmentElapsed = 0f;
            segmentDuration = CalculateSegmentDuration(start, end, speed, minimumDuration);
            segmentArc = crossesNet ? Mathf.Max(arc, RequiredNetArc(start, end)) : arc;
        }

        private static float CalculateSegmentDuration(Vector3 start, Vector3 end, float speed, float minimumDuration)
        {
            float distance = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z));
            return Mathf.Max(minimumDuration, distance / Mathf.Max(speed, 0.1f), 0.02f);
        }

        private Vector3 Evaluate(float fraction)
        {
            float t = Mathf.Clamp01(fraction);
            Vector3 point = Vector3.Lerp(segmentStart, segmentEnd, t);
            point.y += 4f * segmentArc * t * (1f - t);
            return point;
        }

        private float RequiredNetArc(Vector3 start, Vector3 end)
        {
            if (start.z * end.z >= 0f) return 0f;
            float t = -start.z / (end.z - start.z);
            float factor = 4f * t * (1f - t);
            if (factor < 0.01f) return 0f;
            float linearHeight = Mathf.Lerp(start.y, end.y, t);
            return Mathf.Max(0f, (netTop + radius + netClearance - linearHeight) / factor + 0.02f);
        }

        private bool TrySweptPaddleHit(Vector3 ballBefore, Vector3 ballAfter,
            Vector3 paddleBefore, Vector3 paddleAfter, out Vector3 contact, out Vector3 ballAtContact)
        {
            contact = default;
            ballAtContact = default;
            PaddleHitDetector surface = playerPaddle.HitDetector;
            if (surface == null) return false;
            MeshCollider collider = surface.Collider;
            if (collider == null || !collider.enabled || collider.sharedMesh == null) return false;

            float reach = radius + Mathf.Max(contactDepthTolerance, contactAreaPadding);
            float reachSquared = reach * reach;
            Vector3 relativeMovement = (ballAfter - ballBefore) - (paddleAfter - paddleBefore);
            int steps = Mathf.Clamp(Mathf.CeilToInt(relativeMovement.magnitude /
                Mathf.Max(radius * 0.5f, 0.02f)), 1, 64);
            Physics.SyncTransforms();
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 paddleAtSample = Vector3.Lerp(paddleBefore, paddleAfter, t);
                Vector3 ballAtSample = Vector3.Lerp(ballBefore, ballAfter, t);
                Vector3 queryPoint = ballAtSample + paddleAfter - paddleAtSample;
                Vector3 nearest = collider.ClosestPoint(queryPoint);
                if ((queryPoint - nearest).sqrMagnitude > reachSquared) continue;
                contact = nearest;
                ballAtContact = ballAtSample;
                return true;
            }
            return false;
        }

        private void ApplyRadius()
        {
            if (sphere == null) sphere = GetComponent<SphereCollider>();
            if (sphere != null) sphere.radius = radius;
            if (visual != null) visual.localScale = Vector3.one * radius * 2f;
            if (trail != null) trail.startWidth = radius * 1.2f;
            appliedRadius = radius;
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.015f, radius);
            enemyBallSpeed = Mathf.Max(0.1f, enemyBallSpeed);
            playerBallSpeed = Mathf.Max(0.1f, playerBallSpeed);
            playerBounceSpeedMultiplier = Mathf.Max(1f, playerBounceSpeedMultiplier);
            swingAimThreshold = Mathf.Max(0.01f, swingAimThreshold);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawTrajectoryGizmos) return;
            Gizmos.color = Color.yellow;
            Vector3 previous = segmentStart;
            for (int i = 1; i <= 24; i++)
            {
                Vector3 next = Evaluate(i / 24f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(LastPlayerLanding, 0.065f);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(LastEnemyLanding, 0.065f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.16f,
                Phase.ToString() + " / Aim " + LastPlayerAim.ToString("F2"));
#endif
        }
    }
}
