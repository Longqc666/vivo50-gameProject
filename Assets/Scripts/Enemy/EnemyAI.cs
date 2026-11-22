using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(EnemyPerception), typeof(EnemyCatch))]
public sealed class EnemyAI : MonoBehaviour
{
    public enum EnemyState { Patrol, Investigate, Chase, Return }

    [Header("场景引用")]
    [SerializeField] private PlayerHiding target;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private Transform facingMarker;
    [SerializeField, Min(0)] private int initialPatrolIndex;

    [Header("行为参数")]
    [SerializeField, Min(0f)] private float patrolSpeed = 2.1f;
    [SerializeField, Min(0f)] private float chaseSpeed = 4.1f;
    [SerializeField, Min(0f)] private float recognitionTime = 0.35f;
    [SerializeField, Min(0f)] private float searchDuration = 3f;
    [SerializeField, Min(0.01f)] private float arrivalDistance = 0.15f;

    [Header("运行时观察")]
    [SerializeField] private EnemyState currentState;
    [SerializeField] private int facingDirection = 1;
    [SerializeField] private Vector2 investigationPoint;
    [SerializeField] private float searchTimer;

    private Rigidbody2D body;
    private EnemyPerception perception;
    private EnemyCatch catcher;
    private Collider2D targetCollider;
    private PlayerDetection targetDetection;
    private SpriteRenderer visual;
    private Vector2 spawnPosition;
    private int spawnFacing;
    private int patrolIndex;
    private int patrolStep = 1;
    private Vector2 lastSeenPosition;
    private float recognitionTimer;
    private float noProgressTimer;
    private float previousInvestigationDistance;
    private bool simulationActive = true;

    public EnemyState State => currentState;
    public Vector2 InvestigationPoint => investigationPoint;
    public bool CanAct => isActiveAndEnabled && simulationActive && Time.timeScale > 0f;
    public event Action<EnemyState> StateChanged;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        perception = GetComponent<EnemyPerception>();
        catcher = GetComponent<EnemyCatch>();
        visual = GetComponent<SpriteRenderer>();
        spawnPosition = body.position;
        spawnFacing = facingDirection < 0 ? -1 : 1;
        if (target == null || patrolPoints == null || patrolPoints.Length == 0 || Array.Exists(patrolPoints, p => p == null))
        {
            Debug.LogError("[EnemyAI] Assign a player and ordered patrol points on the scene instance.", this);
            enabled = false;
            return;
        }

        targetCollider = target.GetComponent<Collider2D>();
        targetDetection = target.GetComponent<PlayerDetection>();
        ResetToSpawn();
    }

    private void OnEnable() => PlayerNoise.NoiseEmitted += OnNoiseEmitted;

    private void OnDisable()
    {
        PlayerNoise.NoiseEmitted -= OnNoiseEmitted;
        if (body != null)
            StopMoving();
    }

    private void FixedUpdate()
    {
        if (!CanAct || catcher.HasCaught || target == null || target.IsCatchLocked)
        {
            StopMoving();
            return;
        }

        bool canSee = perception.CanSee(target, facingDirection);
        recognitionTimer = canSee ? recognitionTimer + Time.fixedDeltaTime : 0f;
        if (canSee && (currentState == EnemyState.Chase || recognitionTimer >= recognitionTime))
        {
            lastSeenPosition = target.transform.position;
            ChangeState(EnemyState.Chase);
        }
        else if (currentState == EnemyState.Chase)
        {
            BeginInvestigation(lastSeenPosition);
        }

        switch (currentState)
        {
            case EnemyState.Patrol:
                FollowPatrol();
                break;
            case EnemyState.Investigate:
                Investigate();
                break;
            case EnemyState.Chase:
                if (catcher.TryCatch(target, targetCollider))
                    StopMoving();
                else
                    MoveTo(lastSeenPosition, chaseSpeed);
                break;
            case EnemyState.Return:
                Vector2 routePoint = patrolPoints[patrolIndex].position;
                if (HorizontalDistance(routePoint) <= arrivalDistance)
                    ChangeState(EnemyState.Patrol);
                MoveTo(routePoint, patrolSpeed);
                break;
        }
    }

    private void OnNoiseEmitted(GameObject source, Vector2 position, float radius)
    {
        if (target == null || source != target.gameObject || target.IsHidden || target.IsCatchLocked
            || !CanAct || currentState == EnemyState.Chase || radius <= 0f
            || (body.position - position).sqrMagnitude > radius * radius)
            return;

        if (currentState != EnemyState.Investigate || Vector2.Distance(investigationPoint, position) > arrivalDistance)
            BeginInvestigation(position);
    }

    private void BeginInvestigation(Vector2 position)
    {
        investigationPoint = position;
        searchTimer = 0f;
        noProgressTimer = 0f;
        previousInvestigationDistance = HorizontalDistance(position);
        ChangeState(EnemyState.Investigate);
    }

    private void Investigate()
    {
        float distance = HorizontalDistance(investigationPoint);
        noProgressTimer = previousInvestigationDistance - distance > 0.002f ? 0f : noProgressTimer + Time.fixedDeltaTime;
        previousInvestigationDistance = distance;

        // 固定路点无法绕开实体墙。受阻时在可达侧搜索，避免卡在调查态。
        if (distance <= arrivalDistance || noProgressTimer >= 0.6f)
        {
            StopMoving();
            searchTimer += Time.fixedDeltaTime;
            if (searchTimer >= searchDuration)
            {
                patrolIndex = FindNearestPatrolPoint();
                ChangeState(EnemyState.Return);
            }
            return;
        }

        MoveTo(investigationPoint, patrolSpeed);
    }

    private void FollowPatrol()
    {
        if (patrolPoints.Length > 1 && HorizontalDistance(patrolPoints[patrolIndex].position) <= arrivalDistance)
        {
            if (patrolIndex == 0) patrolStep = 1;
            else if (patrolIndex == patrolPoints.Length - 1) patrolStep = -1;
            patrolIndex += patrolStep;
        }
        MoveTo(patrolPoints[patrolIndex].position, patrolSpeed);
    }

    private int FindNearestPatrolPoint()
    {
        int nearest = patrolIndex;
        float nearestDistance = float.PositiveInfinity;
        for (int index = 0; index < patrolPoints.Length; index++)
        {
            Vector2 point = patrolPoints[index].position;
            float distance = HorizontalDistance(point);
            if (distance >= nearestDistance || !perception.HasFreePathTo(point.x, target.transform))
                continue;
            nearest = index;
            nearestDistance = distance;
        }
        return nearest;
    }

    private float HorizontalDistance(Vector2 position) => Mathf.Abs(position.x - body.position.x);

    private void MoveTo(Vector2 position, float speed)
    {
        float offsetX = position.x - body.position.x;
        float velocityX = Mathf.Abs(offsetX) <= arrivalDistance ? 0f : Mathf.Clamp(offsetX / Time.fixedDeltaTime, -speed, speed);
        body.velocity = new Vector2(velocityX, body.velocity.y);
        if (Mathf.Abs(velocityX) > 0.01f)
        {
            facingDirection = velocityX > 0f ? 1 : -1;
            RefreshMarker();
        }
    }

    private void StopMoving() => body.velocity = new Vector2(0f, body.velocity.y);

    public void SetSimulationActive(bool active)
    {
        simulationActive = active;
        if (!active && body != null)
            StopMoving();
    }

    public void ResetToSpawn()
    {
        body.position = spawnPosition;
        transform.position = new Vector3(spawnPosition.x, spawnPosition.y, transform.position.z);
        body.velocity = Vector2.zero;
        recognitionTimer = 0f;
        searchTimer = 0f;
        noProgressTimer = 0f;
        previousInvestigationDistance = 0f;
        lastSeenPosition = spawnPosition;
        investigationPoint = spawnPosition;
        patrolIndex = Mathf.Clamp(initialPatrolIndex, 0, patrolPoints.Length - 1);
        patrolStep = 1;
        facingDirection = spawnFacing;
        catcher.ResetLatch();
        RefreshMarker();
        ChangeState(EnemyState.Patrol);
        RefreshColor();
    }

    private void ChangeState(EnemyState next)
    {
        if (currentState == next) return;
        currentState = next;
        RefreshColor();
        if (next == EnemyState.Chase && targetDetection != null)
            targetDetection.NotifyDetected();
        StateChanged?.Invoke(next);
    }

    private void RefreshColor()
    {
        if (visual == null) return;
        switch (currentState)
        {
            case EnemyState.Chase: visual.color = new Color(0.96f, 0.30f, 0.35f); break;
            case EnemyState.Investigate: visual.color = new Color(1f, 0.79f, 0.36f); break;
            case EnemyState.Return: visual.color = new Color(0.33f, 0.84f, 0.92f); break;
            default: visual.color = new Color(0.67f, 0.42f, 1f); break;
        }
    }

    private void RefreshMarker()
    {
        if (facingMarker != null)
            facingMarker.localPosition = new Vector3(facingDirection * 0.65f, 0.22f, -0.1f);
    }

    private void OnDrawGizmosSelected()
    {
        if (patrolPoints == null) return;
        Gizmos.color = Color.cyan;
        for (int index = 0; index < patrolPoints.Length; index++)
        {
            Transform point = patrolPoints[index];
            if (point == null) continue;
            Gizmos.DrawWireSphere(point.position, 0.2f);
            if (index > 0 && patrolPoints[index - 1] != null)
                Gizmos.DrawLine(patrolPoints[index - 1].position, point.position);
        }
    }
}
