using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MainGame.P2
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class P2GhostDoorApparitionDirector : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform patrolRoot;
        [SerializeField] private Transform doorApparitionRoot;
        [SerializeField] private NavMeshAgent agent;

        [Header("Patrol")]
        [SerializeField] private bool autoCollectPointsFromChildren = true;
        [SerializeField] private bool pingPongPatrol = true;
        [SerializeField] private Transform[] patrolPoints = Array.Empty<Transform>();
        [SerializeField, Min(0.05f)] private float waypointReachDistance = 0.35f;
        [SerializeField, Min(0.1f)] private float patrolSpeed = 1.6f;
        [SerializeField, Min(0.1f)] private float awakenedSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 2.5f;
        [SerializeField] private bool beginAwakened;

        [Header("Door Apparition")]
        [SerializeField] private bool enableDoorApparitions = true;
        [SerializeField] private bool useDoorApparitionPoints = true;
        [SerializeField, Min(0.5f)] private float minSecondsBetweenApparitions = 12f;
        [SerializeField, Min(0.5f)] private float maxSecondsBetweenApparitions = 24f;
        [SerializeField, Min(0.1f)] private float apparitionVisibleSeconds = 1.4f;
        [SerializeField, Min(0.1f)] private float minDistanceFromPlayer = 3f;
        [SerializeField, Min(0.1f)] private float maxDistanceFromPlayer = 13f;
        [SerializeField, Min(0.1f)] private float doorPointMaxDistance = 2.4f;
        [SerializeField] private bool avoidPlayerCameraView = true;
        [SerializeField] private bool hideVisualBetweenApparitions;

        [Header("Compatibility")]
        [SerializeField] private bool disableOtherGhostAiOnStart = true;

        public bool IsAwakened { get; private set; }

        private readonly List<Transform> reusablePoints = new();
        private Renderer[] visualRenderers = Array.Empty<Renderer>();
        private int patrolIndex;
        private int patrolDirection = 1;
        private int lastPatrolChildCount = -1;
        private int lastApparitionChildCount = -1;
        private Vector3 lastDestination;
        private bool hasDestination;
        private bool apparitionRunning;
        private float nextApparitionTime;

        private void Awake()
        {
            ResolveReferences();
            CacheVisualRenderers();
            RefreshPatrolPoints(true);
            IsAwakened = beginAwakened;
            ScheduleNextApparition();
            ApplyVisualHiddenState();
        }

        private void Start()
        {
            ResolveReferences();
            CacheVisualRenderers();
            RefreshPatrolPoints(true);

            if (disableOtherGhostAiOnStart)
                DisableOtherGhostControllers();
        }

        private void Update()
        {
            ResolveReferences();
            RefreshPatrolPoints(false);

            if (IsGameLocked())
                return;

            if (!apparitionRunning)
            {
                TickPatrol();
                TickDoorApparition();
            }
        }

        public void Awaken()
        {
            IsAwakened = true;
            ScheduleNextApparition(1f, 3f);
        }

        public void ForceApparitionNearPlayer()
        {
            if (!isActiveAndEnabled)
                return;

            StopCoroutine(nameof(ApparitionRoutine));
            StartCoroutine(nameof(ApparitionRoutine), true);
        }

        private void TickPatrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
                return;

            patrolIndex = Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1);
            Transform target = patrolPoints[patrolIndex];
            if (target == null)
            {
                AdvancePatrolPoint();
                return;
            }

            if (MoveTo(target.position, IsAwakened ? awakenedSpeed : patrolSpeed, waypointReachDistance))
                AdvancePatrolPoint();
        }

        private void TickDoorApparition()
        {
            if (!enableDoorApparitions || Time.time < nextApparitionTime)
                return;

            StartCoroutine(nameof(ApparitionRoutine), false);
        }

        private IEnumerator ApparitionRoutine(bool forced)
        {
            if (apparitionRunning)
                yield break;

            apparitionRunning = true;
            if (TryChooseApparitionPoint(out var point))
            {
                WarpTo(point.position, point.rotation);
                FacePlayer();
                SetVisualVisible(true);
                if (agent != null)
                    agent.isStopped = true;

                yield return new WaitForSeconds(apparitionVisibleSeconds);

                if (hideVisualBetweenApparitions)
                    SetVisualVisible(false);
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                    agent.isStopped = false;

                AlignPatrolIndexToNearest();
            }

            ScheduleNextApparition();
            apparitionRunning = false;
        }

        private bool TryChooseApparitionPoint(out Transform point)
        {
            point = null;
            CollectApparitionCandidates(reusablePoints);
            if (reusablePoints.Count == 0)
                return false;

            Transform best = null;
            float bestScore = float.MaxValue;
            Vector3 playerPosition = player != null ? player.position : transform.position;

            for (int i = 0; i < reusablePoints.Count; i++)
            {
                Transform candidate = reusablePoints[i];
                if (candidate == null)
                    continue;

                float distance = Vector3.Distance(playerPosition, candidate.position);
                if (distance < minDistanceFromPlayer || distance > maxDistanceFromPlayer)
                    continue;
                if (avoidPlayerCameraView && IsVisibleFromPlayerCamera(candidate.position))
                    continue;
                if (!NavMesh.SamplePosition(candidate.position, out _, navMeshSampleRadius, NavMesh.AllAreas))
                    continue;

                float doorScore = DistanceToNearestDoor(candidate.position);
                float score = distance + doorScore * 0.35f;
                if (best == null || score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            if (best == null && reusablePoints.Count > 0)
                best = reusablePoints[UnityEngine.Random.Range(0, reusablePoints.Count)];

            point = best;
            return point != null;
        }

        private void CollectApparitionCandidates(List<Transform> points)
        {
            points.Clear();

            if (useDoorApparitionPoints && doorApparitionRoot != null)
            {
                RefreshApparitionRootCache();
                for (int i = 0; i < doorApparitionRoot.childCount; i++)
                {
                    Transform child = doorApparitionRoot.GetChild(i);
                    if (child != null && child.gameObject.activeSelf)
                        points.Add(child);
                }
            }

            if (points.Count == 0 && patrolPoints != null)
            {
                for (int i = 0; i < patrolPoints.Length; i++)
                {
                    Transform waypoint = patrolPoints[i];
                    if (waypoint != null && IsNearAnyDoor(waypoint.position))
                        points.Add(waypoint);
                }
            }
        }

        private void RefreshPatrolPoints(bool force)
        {
            if (!autoCollectPointsFromChildren || patrolRoot == null)
                return;

            if (!force && lastPatrolChildCount == patrolRoot.childCount)
                return;

            patrolPoints = CollectDirectChildren(patrolRoot);
            lastPatrolChildCount = patrolRoot.childCount;
            patrolIndex = Mathf.Clamp(patrolIndex, 0, Mathf.Max(0, patrolPoints.Length - 1));
        }

        private void RefreshApparitionRootCache()
        {
            if (doorApparitionRoot == null || lastApparitionChildCount == doorApparitionRoot.childCount)
                return;

            lastApparitionChildCount = doorApparitionRoot.childCount;
        }

        private Transform[] CollectDirectChildren(Transform root)
        {
            if (root == null || root.childCount == 0)
                return Array.Empty<Transform>();

            var points = new List<Transform>(root.childCount);
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                    points.Add(child);
            }

            return points.ToArray();
        }

        private void AdvancePatrolPoint()
        {
            if (patrolPoints == null || patrolPoints.Length <= 1)
                return;

            if (!pingPongPatrol)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                ClearDestination();
                return;
            }

            if (patrolIndex >= patrolPoints.Length - 1)
                patrolDirection = -1;
            else if (patrolIndex <= 0)
                patrolDirection = 1;

            patrolIndex = Mathf.Clamp(patrolIndex + patrolDirection, 0, patrolPoints.Length - 1);
            ClearDestination();
        }

        private bool MoveTo(Vector3 target, float speed, float reachDistance)
        {
            if (agent == null || !agent.enabled)
                return false;
            if (!agent.isOnNavMesh && !TryWarpToNavMesh(transform.position))
                return false;
            if (!NavMesh.SamplePosition(target, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                return false;

            agent.isStopped = false;
            agent.speed = speed;
            agent.stoppingDistance = Mathf.Min(0.1f, reachDistance * 0.5f);

            Vector3 destination = hit.position;
            if (!hasDestination || Vector3.Distance(lastDestination, destination) > 0.1f)
            {
                if (!agent.SetDestination(destination))
                    return false;

                lastDestination = destination;
                hasDestination = true;
            }

            RotateAlongVelocity();
            return !agent.pathPending && agent.remainingDistance <= reachDistance;
        }

        private void WarpTo(Vector3 position, Quaternion rotation)
        {
            if (NavMesh.SamplePosition(position, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                position = hit.position;

            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh || TryWarpToNavMesh(transform.position))
                    agent.Warp(position);
                else
                    transform.position = position;
            }
            else
            {
                transform.position = position;
            }

            transform.rotation = rotation;
            ClearDestination();
        }

        private bool TryWarpToNavMesh(Vector3 position)
        {
            return agent != null
                && NavMesh.SamplePosition(position, out var hit, navMeshSampleRadius, NavMesh.AllAreas)
                && agent.Warp(hit.position);
        }

        private void RotateAlongVelocity()
        {
            if (agent == null)
                return;

            Vector3 velocity = agent.desiredVelocity.sqrMagnitude > 0.01f ? agent.desiredVelocity : agent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude <= 0.01f)
                return;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(velocity.normalized, Vector3.up),
                Time.deltaTime * 5f);
        }

        private void FacePlayer()
        {
            if (player == null)
                return;

            Vector3 direction = player.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return;

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void AlignPatrolIndexToNearest()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
                return;

            int nearest = patrolIndex;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null)
                    continue;

                float distance = (patrolPoints[i].position - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = distance;
                }
            }

            patrolIndex = nearest;
            ClearDestination();
        }

        private float DistanceToNearestDoor(Vector3 position)
        {
            float nearest = doorPointMaxDistance;
            var doors = FindObjectsByType<FpsHorrorKit.DoorSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] == null)
                    continue;

                nearest = Mathf.Min(nearest, Vector3.Distance(position, doors[i].transform.position));
            }

            return nearest;
        }

        private bool IsNearAnyDoor(Vector3 position)
        {
            return DistanceToNearestDoor(position) <= doorPointMaxDistance;
        }

        private bool IsVisibleFromPlayerCamera(Vector3 position)
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled)
                return false;

            Vector3 viewport = camera.WorldToViewportPoint(position + Vector3.up * 1.1f);
            return viewport.z > camera.nearClipPlane
                && viewport.x > 0.05f
                && viewport.x < 0.95f
                && viewport.y > 0.05f
                && viewport.y < 0.95f;
        }

        private void ResolveReferences()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();
            if (agent == null)
                agent = gameObject.AddComponent<NavMeshAgent>();
            agent.updateRotation = false;

            if (player == null)
            {
                var fps = FindFirstObjectByType<FpsHorrorKit.FpsController>(FindObjectsInactive.Include);
                if (fps != null)
                    player = fps.transform;
            }

            if (visualRoot == null)
            {
                var mada = FindChildRecursive(transform, "mada2");
                visualRoot = mada != null ? mada : transform;
            }

            if (patrolRoot == null)
                patrolRoot = FindSceneTransform("P2_GhostPatrolPoints") ?? FindSceneTransform("P2_GhostWaypoints");
            if (doorApparitionRoot == null)
                doorApparitionRoot = FindSceneTransform("P2_GhostDoorApparitionPoints");
        }

        private void CacheVisualRenderers()
        {
            visualRenderers = visualRoot != null
                ? visualRoot.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
        }

        private void SetVisualVisible(bool visible)
        {
            if (visualRoot != null && visualRoot != transform)
                visualRoot.gameObject.SetActive(true);

            if (visualRenderers == null || visualRenderers.Length == 0)
                CacheVisualRenderers();

            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null)
                    visualRenderers[i].enabled = visible;
            }
        }

        private void ApplyVisualHiddenState()
        {
            if (hideVisualBetweenApparitions)
                SetVisualVisible(false);
        }

        private void DisableOtherGhostControllers()
        {
            var p2Ghost = GetComponent<P2GhostController>();
            if (p2Ghost != null)
                p2Ghost.enabled = false;

            var monsterAi = GetComponent<global::MonsterAI>();
            if (monsterAi != null)
                monsterAi.enabled = false;
        }

        private static bool IsGameLocked()
        {
            var controller = GameController.Instance;
            return controller != null
                && (controller.currentGameState == GameController.GameState.Cutscene
                    || controller.currentGameState == GameController.GameState.Ending
                    || controller.currentGameState == GameController.GameState.Dead);
        }

        private void ScheduleNextApparition()
        {
            ScheduleNextApparition(minSecondsBetweenApparitions, maxSecondsBetweenApparitions);
        }

        private void ScheduleNextApparition(float minSeconds, float maxSeconds)
        {
            float min = Mathf.Max(0.1f, Mathf.Min(minSeconds, maxSeconds));
            float max = Mathf.Max(min, Mathf.Max(minSeconds, maxSeconds));
            nextApparitionTime = Time.time + UnityEngine.Random.Range(min, max);
        }

        private void ClearDestination()
        {
            hasDestination = false;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
                return null;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == childName)
                    return child;
            }

            return null;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            foreach (var candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate != null && candidate.name == objectName && candidate.gameObject.scene.IsValid())
                    return candidate;
            }

            return null;
        }
    }
}
