using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FNaS.Systems;
using FNaS.Settings;

namespace FNaS.Entities.Mold {
    public class MoldManager : MonoBehaviour, IRuntimeSettingsConsumer {
        public static MoldManager Instance { get; private set; }

        [Serializable]
        public class MoldEdge {
            public MoldPatch a;
            public MoldPatch b;
        }

        [Serializable]
        public class RoomGroup {
            public string roomName = "Room";
            public Transform roomRoot;
            public List<MoldEdge> edges = new();
        }

        [Header("Patch Collection")]
        [Tooltip("Direct children of this root are treated as room objects. All MoldPatch descendants under them are auto-collected.")]
        public Transform patchRoot;

        [Header("Sources")]
        [Tooltip("A patch is a source only if it is listed here.")]
        public List<MoldPatch> sourcePatches = new();

        [Header("Grouped Edge Data")]
        [Tooltip("Edges are authored centrally here, grouped by room for organization only.")]
        public List<RoomGroup> roomGroups = new();

        [Header("AI / Spread")]
        [Range(0, 20)] public int ai = 8;
        [Min(1)] public int marksPerSuccessfulSpread = 1;
        [Min(0.01f)] public float markedToActiveSeconds = 1.5f;

        [Header("Surface Weights")]
        [Min(0)] public int ceilingWeight = 5;
        [Min(0)] public int wallWeight = 3;
        [Min(0)] public int floorWeight = 1;
        [Min(0)] public int lightWeight = 4;
        [Min(0)] public int cameraWeight = 4;

        [Header("Special Proximity Bias")]
        public bool useSpecialProximityBias = true;
        [Min(0)] public int dist0Bonus = 4;
        [Min(0)] public int dist1Bonus = 2;
        [Min(0)] public int dist2Bonus = 1;

        [Header("Cleansing")]
        [Min(0.01f)] public float cleanseTimeConnected = 1.5f;
        [Min(0.01f)] public float cleanseTimeIsolated = 0.5f;

        [Header("Spray Spread Lockout")]
        [Min(0.01f)] public float spraySpreadBlockSeconds = 5f;

        [Header("Spray Visual Suppression")]
        [Min(0.01f)] public float sprayVisualContactHoldTime = 0.10f;
        [Min(0f)] public float sprayVisualRegrowDelay = 0.75f;
        [Min(0.01f)] public float sprayVisualRegrowSpeedMultiplier = 2.5f;

        [Header("Blood Escalation")]
        public bool allowBloodPhase = true;
        [Min(0.01f)] public float bloodTransitionSeconds = 30f;

        [Header("Gizmos")]
        public bool drawAdjacencyGizmos = true;
        public bool drawPatchSpheres = true;
        public Color adjacencyColor = Color.cyan;
        public Color sourceEdgeColor = Color.yellow;
        public Color isolatedEdgeColor = Color.gray;
        [Min(0f)] public float gizmoSphereRadius = 0.04f;

        [Header("Graph Data (read-only at runtime)")]
        [SerializeField] private List<MoldPatch> allPatches = new();

        [Header("Debug")]
        public bool verboseLogging = false;

        [Header("Runtime (read-only)")]
        [SerializeField] private int activePatchCount;
        [SerializeField] private int isolatedPatchCount;
        [SerializeField] private int bloodPatchCount;
        [SerializeField] private int sprayBlockedPatchCount;

        public IReadOnlyList<MoldPatch> AllPatches => allPatches;

        private GlobalAIScheduler scheduler;
        private bool subscribed;
        private bool aiDisabled;

        private readonly Dictionary<MoldPatch, List<MoldPatch>> adj = new();
        private readonly HashSet<MoldPatch> connectedSet = new();
        private readonly Dictionary<MoldPatch, int> distToNearestLight = new();
        private readonly Dictionary<MoldPatch, int> distToNearestCamera = new();
        private readonly Dictionary<MoldPatch, float> bloodTimerByPatch = new();
        private readonly Dictionary<MoldPatch, float> sprayVisualSuppressionByPatch = new();
        private readonly Dictionary<MoldPatch, float> sprayVisualLastContactTimeByPatch = new();
        private readonly List<MoldPatch> sprayVisualKeysBuffer = new();

        private readonly Dictionary<MoldPatch, float> sprayBlockTimerByPatch = new();
        private readonly List<MoldPatch> expiredSprayBlocksBuffer = new();

        private readonly List<MoldPatch> frontierBuffer = new List<MoldPatch>();
        private readonly HashSet<MoldPatch> frontierSet = new HashSet<MoldPatch>();
        private readonly List<int> weightBuffer = new List<int>();
        private readonly List<MoldPatch> sprayBlockKeysBuffer = new();

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) return;
            ai = settings.GetInt("mold.ai");
        }

        private void Awake() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("Duplicate MoldManager found. Destroying extra instance.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            SyncRoomsFromHierarchy();
            CollectAllPatchesFromRooms();
            RuntimeCleanup();
            BuildAdjacency();
            PrecomputeSpecialDistances();
            RecomputeConnectivity();
            RefreshAllPatchStatesAfterConnectivity();
            RecountDebugStats();
        }

        private void OnEnable() {
            TrySubscribeToScheduler();
        }

        private void Start() {
            TrySubscribeToScheduler();

            if (ai <= 0) {
                EnterAIDisabledState();
            }
            else {
                ExitAIDisabledState();
            }
        }

        private void Update() {
            if (!subscribed) {
                TrySubscribeToScheduler();
            }

            HandleAIDisabledState();
            UpdateSprayBlockTimers();
            UpdateSprayVisualSuppression();
            UpdateBloodTimers();
        }

        private bool HandleAIDisabledState() {
            if (ai <= 0) {
                if (!aiDisabled) {
                    EnterAIDisabledState();
                }
                return true;
            }

            if (aiDisabled) {
                ExitAIDisabledState();
            }

            return false;
        }

        private void OnDisable() {
            TryUnsubscribeScheduler();
        }

        private void OnDestroy() {
            TryUnsubscribeScheduler();

            if (Instance == this) {
                Instance = null;
            }
        }

        private void OnValidate() {
            if (patchRoot != null) {
                SyncRoomsFromHierarchy();
                CollectAllPatchesFromRooms();
            }
        }

        private void TrySubscribeToScheduler() {
            if (subscribed) return;

            if (scheduler == null) {
                scheduler = GlobalAIScheduler.Instance;
            }

            if (scheduler == null) return;

            scheduler.OnOpportunityTick -= HandleOpportunityTick;
            scheduler.OnOpportunityTick += HandleOpportunityTick;
            subscribed = true;

            if (verboseLogging) {
                Debug.Log("MoldManager subscribed to GlobalAIScheduler.", this);
            }
        }

        private void TryUnsubscribeScheduler() {
            if (!subscribed) return;

            if (scheduler != null) {
                scheduler.OnOpportunityTick -= HandleOpportunityTick;
            }

            subscribed = false;
        }

        private void EnterAIDisabledState() {
            aiDisabled = true;
            StopAllCoroutines();
            ResetAllMoldToClean();
        }

        private void ExitAIDisabledState() {
            aiDisabled = false;
            StopAllCoroutines();
            ResetToSourceOnlyState();
        }

        private void ResetAllMoldToClean() {
            sprayBlockTimerByPatch.Clear();
            bloodTimerByPatch.Clear();
            sprayVisualSuppressionByPatch.Clear();
            sprayVisualLastContactTimeByPatch.Clear();

            for (int i = 0; i < allPatches.Count; i++) {
                MoldPatch patch = allPatches[i];
                if (patch == null) continue;

                patch.ForceClean(playCleanSound: false);
            }

            connectedSet.Clear();
            RecountDebugStats();
        }

        private void ResetToSourceOnlyState() {
            sprayBlockTimerByPatch.Clear();
            bloodTimerByPatch.Clear();
            sprayVisualSuppressionByPatch.Clear();
            sprayVisualLastContactTimeByPatch.Clear();

            for (int i = 0; i < allPatches.Count; i++) {
                MoldPatch patch = allPatches[i];
                if (patch == null) continue;

                patch.ForceClean(playCleanSound: false);
            }

            for (int i = 0; i < sourcePatches.Count; i++) {
                MoldPatch src = sourcePatches[i];
                if (src == null) continue;

                src.SetSpreadState(MoldSpreadState.Active);
                src.SetCorruptionPhase(MoldCorruptionPhase.Normal);
            }

            RecomputeConnectivity();
            RefreshAllPatchStatesAfterConnectivity();
            RecountDebugStats();
        }

        private void HandleOpportunityTick(int tick) {
            if (aiDisabled || ai <= 0) return;
            if (sourcePatches == null || sourcePatches.Count == 0) return;

            for (int i = 0; i < sourcePatches.Count; i++) {
                TrySpreadFromSource(sourcePatches[i]);
            }

            RecomputeConnectivity();
            RefreshAllPatchStatesAfterConnectivity();
            UpdateBloodTimers();
            RecountDebugStats();
        }

        private void UpdateSprayVisualSuppression() {
            if (allPatches == null || allPatches.Count == 0) {
                return;
            }

            sprayVisualKeysBuffer.Clear();

            for (int i = 0; i < allPatches.Count; i++) {
                MoldPatch patch = allPatches[i];
                if (patch == null) continue;

                sprayVisualKeysBuffer.Add(patch);
            }

            for (int i = 0; i < sprayVisualKeysBuffer.Count; i++) {
                MoldPatch patch = sprayVisualKeysBuffer[i];
                if (patch == null) continue;

                // Clean or non-cleansable patches should never stay visually suppressed
                if (!CanPatchBeCleansed(patch)) {
                    if (sprayVisualSuppressionByPatch.ContainsKey(patch)) {
                        sprayVisualSuppressionByPatch.Remove(patch);
                    }

                    if (sprayVisualLastContactTimeByPatch.ContainsKey(patch)) {
                        sprayVisualLastContactTimeByPatch.Remove(patch);
                    }

                    patch.SetVisualSuppression01(0f);
                    continue;
                }

                float suppression = 0f;
                sprayVisualSuppressionByPatch.TryGetValue(patch, out suppression);

                bool hasRecentContact = sprayVisualLastContactTimeByPatch.TryGetValue(patch, out float lastContactTime);
                bool beingSprayedNow = hasRecentContact && Time.time <= lastContactTime + sprayVisualContactHoldTime;

                float cleanseDuration = Mathf.Max(0.01f, GetCleanseDuration(patch));

                if (beingSprayedNow) {
                    // Shrink visually using the SAME duration as the real cleanse
                    suppression = Mathf.MoveTowards(
                        suppression,
                        1f,
                        Time.deltaTime / cleanseDuration
                    );
                }
                else if (hasRecentContact && Time.time >= lastContactTime + sprayVisualRegrowDelay) {
                    // Grow back faster than it was hidden
                    suppression = Mathf.MoveTowards(
                        suppression,
                        0f,
                        Time.deltaTime * sprayVisualRegrowSpeedMultiplier / cleanseDuration
                    );
                }

                if (suppression <= 0.0001f) {
                    suppression = 0f;

                    // If there's no recent contact anymore, clean up bookkeeping
                    if (!hasRecentContact || Time.time > lastContactTime + sprayVisualRegrowDelay + 1f) {
                        sprayVisualSuppressionByPatch.Remove(patch);
                        sprayVisualLastContactTimeByPatch.Remove(patch);
                    }
                }
                else {
                    sprayVisualSuppressionByPatch[patch] = suppression;
                }

                patch.SetVisualSuppression01(suppression);
            }
        }

        private void SyncRoomsFromHierarchy() {
            roomGroups ??= new List<RoomGroup>();

            if (patchRoot == null) return;

            var existingByRoot = new Dictionary<Transform, RoomGroup>();
            foreach (var group in roomGroups) {
                if (group != null && group.roomRoot != null && !existingByRoot.ContainsKey(group.roomRoot)) {
                    existingByRoot.Add(group.roomRoot, group);
                }
            }

            List<RoomGroup> newGroups = new List<RoomGroup>();

            for (int i = 0; i < patchRoot.childCount; i++) {
                Transform roomRoot = patchRoot.GetChild(i);
                if (roomRoot == null) continue;

                RoomGroup group;
                if (!existingByRoot.TryGetValue(roomRoot, out group) || group == null) {
                    group = new RoomGroup {
                        roomRoot = roomRoot,
                        roomName = roomRoot.name,
                        edges = new List<MoldEdge>()
                    };
                }

                group.roomRoot = roomRoot;
                group.roomName = roomRoot.name;
                group.edges ??= new List<MoldEdge>();

                newGroups.Add(group);
            }

            roomGroups = newGroups;
        }

        private void CollectAllPatchesFromRooms() {
            allPatches.Clear();

            if (patchRoot == null) return;

            for (int i = 0; i < patchRoot.childCount; i++) {
                Transform roomRoot = patchRoot.GetChild(i);
                if (roomRoot == null) continue;

                List<MoldPatch> roomPatches = roomRoot
                    .GetComponentsInChildren<MoldPatch>(true)
                    .Where(p => p != null)
                    .Distinct()
                    .ToList();

                foreach (var patch in roomPatches) {
                    if (!allPatches.Contains(patch)) {
                        allPatches.Add(patch);
                    }
                }
            }
        }

        private void RuntimeCleanup() {
            sourcePatches = sourcePatches.Where(p => p != null).Distinct().ToList();
            allPatches = allPatches.Where(p => p != null).Distinct().ToList();

            if (roomGroups == null) {
                roomGroups = new List<RoomGroup>();
                return;
            }

            foreach (var group in roomGroups) {
                if (group == null) continue;

                if (group.roomRoot != null) {
                    group.roomName = group.roomRoot.name;
                }

                group.edges ??= new List<MoldEdge>();
                group.edges = group.edges
                    .Where(e => e != null && e.a != null && e.b != null && e.a != e.b)
                    .ToList();
            }
        }

        private bool IsSourcePatch(MoldPatch patch) {
            return patch != null && sourcePatches.Contains(patch);
        }

        public void BuildAdjacency() {
            adj.Clear();

            foreach (var patch in allPatches) {
                if (patch == null) continue;
                if (!adj.ContainsKey(patch)) {
                    adj.Add(patch, new List<MoldPatch>());
                }
            }

            if (roomGroups == null) return;

            foreach (var group in roomGroups) {
                if (group == null || group.edges == null) continue;

                foreach (var edge in group.edges) {
                    if (edge == null || edge.a == null || edge.b == null || edge.a == edge.b) continue;

                    if (!adj.ContainsKey(edge.a)) adj[edge.a] = new List<MoldPatch>();
                    if (!adj.ContainsKey(edge.b)) adj[edge.b] = new List<MoldPatch>();

                    if (!adj[edge.a].Contains(edge.b)) adj[edge.a].Add(edge.b);
                    if (!adj[edge.b].Contains(edge.a)) adj[edge.b].Add(edge.a);
                }
            }
        }

        public void NotifyPatchSprayed(MoldPatch patch) {
            if (patch == null) return;

            sprayBlockTimerByPatch[patch] = spraySpreadBlockSeconds;
            patch.NotifySprayContact();

            // NEW: manager-owned visual suppression tracking
            if (CanPatchBeCleansed(patch)) {
                sprayVisualLastContactTimeByPatch[patch] = Time.time;

                if (!sprayVisualSuppressionByPatch.ContainsKey(patch)) {
                    sprayVisualSuppressionByPatch[patch] = 0f;
                }
            }

            if (verboseLogging) {
                Debug.Log($"Spray block refreshed on patch: {patch.name}", this);
            }
        }

        private void UpdateSprayBlockTimers() {
            if (sprayBlockTimerByPatch.Count == 0) {
                sprayBlockedPatchCount = 0;
                return;
            }

            expiredSprayBlocksBuffer.Clear();
            sprayBlockKeysBuffer.Clear();

            foreach (var patch in sprayBlockTimerByPatch.Keys) {
                sprayBlockKeysBuffer.Add(patch);
            }

            for (int i = 0; i < sprayBlockKeysBuffer.Count; i++) {
                MoldPatch patch = sprayBlockKeysBuffer[i];
                if (patch == null) {
                    expiredSprayBlocksBuffer.Add(patch);
                    continue;
                }

                float next = sprayBlockTimerByPatch[patch] - Time.deltaTime;
                if (next <= 0f) {
                    expiredSprayBlocksBuffer.Add(patch);
                }
                else {
                    sprayBlockTimerByPatch[patch] = next;
                }
            }

            for (int i = 0; i < expiredSprayBlocksBuffer.Count; i++) {
                sprayBlockTimerByPatch.Remove(expiredSprayBlocksBuffer[i]);
            }

            sprayBlockedPatchCount = sprayBlockTimerByPatch.Count;
        }

        private bool IsPatchSprayBlocked(MoldPatch patch) {
            if (patch == null) return false;
            return sprayBlockTimerByPatch.TryGetValue(patch, out float timeRemaining) && timeRemaining > 0f;
        }

        private void TrySpreadFromSource(MoldPatch sourcePatch) {
            if (sourcePatch == null) return;
            if (!IsSourcePatch(sourcePatch)) return;

            int roll = UnityEngine.Random.Range(1, 21);
            if (roll > ai) return;

            List<MoldPatch> connectedRegion = GetConnectedMoldRegionFromSource(sourcePatch);
            if (connectedRegion.Count == 0) return;

            List<MoldPatch> frontier = GatherLegalFrontier(connectedRegion);
            if (frontier.Count == 0) return;

            int markCount = Mathf.Min(marksPerSuccessfulSpread, frontier.Count);

            for (int i = 0; i < markCount; i++) {
                MoldPatch chosen = ChooseWeightedPatch(frontier);
                if (chosen == null) break;

                frontier.Remove(chosen);

                if (chosen.SpreadState == MoldSpreadState.Clean) {
                    chosen.SetSpreadState(MoldSpreadState.Marked);
                    StartCoroutine(CoGrowMarkedToActive(chosen));
                }
            }
        }

        private IEnumerator CoGrowMarkedToActive(MoldPatch patch) {
            if (patch == null) yield break;

            float t = 0f;
            while (t < markedToActiveSeconds) {
                if (patch == null || patch.SpreadState != MoldSpreadState.Marked) yield break;

                t += Time.deltaTime;
                float fill = Mathf.Clamp01(t / markedToActiveSeconds);
                patch.SetMarkedFill(fill);
                yield return null;
            }

            if (patch != null && patch.SpreadState == MoldSpreadState.Marked) {
                patch.SetSpreadState(MoldSpreadState.Active);
                RecomputeConnectivity();
                RefreshAllPatchStatesAfterConnectivity();
                UpdateBloodTimers();
                RecountDebugStats();
            }
        }

        public bool CanPatchBeCleansed(MoldPatch patch) {
            if (patch == null) return false;
            if (IsSourcePatch(patch)) return false;
            return patch.SpreadState != MoldSpreadState.Clean;
        }

        public float GetCleanseDuration(MoldPatch patch) {
            if (patch == null) return cleanseTimeConnected;
            return patch.SpreadState == MoldSpreadState.Isolated ? cleanseTimeIsolated : cleanseTimeConnected;
        }

        public void CleansePatch(MoldPatch patch) {
            if (!CanPatchBeCleansed(patch)) return;

            patch.ForceClean();
            bloodTimerByPatch.Remove(patch);
            sprayVisualSuppressionByPatch.Remove(patch);
            sprayVisualLastContactTimeByPatch.Remove(patch);

            RecomputeConnectivity();
            RefreshAllPatchStatesAfterConnectivity();
            RecountDebugStats();
        }

        private void RecomputeConnectivity() {
            connectedSet.Clear();

            Queue<MoldPatch> q = new Queue<MoldPatch>();

            foreach (var src in sourcePatches) {
                if (src == null) continue;

                if (connectedSet.Add(src)) {
                    q.Enqueue(src);
                }
            }

            while (q.Count > 0) {
                MoldPatch current = q.Dequeue();

                if (!adj.TryGetValue(current, out List<MoldPatch> neighbors)) continue;

                foreach (var next in neighbors) {
                    if (next == null || connectedSet.Contains(next)) continue;

                    bool traversable =
                        IsSourcePatch(next) ||
                        next.SpreadState == MoldSpreadState.Marked ||
                        next.SpreadState == MoldSpreadState.Active ||
                        next.SpreadState == MoldSpreadState.Isolated;

                    if (!traversable) continue;

                    connectedSet.Add(next);
                    q.Enqueue(next);
                }
            }
        }

        private void RefreshAllPatchStatesAfterConnectivity() {
            foreach (var patch in allPatches) {
                if (patch == null) continue;

                if (IsSourcePatch(patch)) {
                    if (!aiDisabled) {
                        patch.SetSpreadState(MoldSpreadState.Active);
                    }
                    continue;
                }

                switch (patch.SpreadState) {
                    case MoldSpreadState.Clean:
                        break;

                    case MoldSpreadState.Marked:
                        break;

                    case MoldSpreadState.Active:
                    case MoldSpreadState.Isolated:
                        bool connected = connectedSet.Contains(patch);
                        patch.SetSpreadState(connected ? MoldSpreadState.Active : MoldSpreadState.Isolated);
                        break;
                }
            }
        }

        private List<MoldPatch> GetConnectedMoldRegionFromSource(MoldPatch sourcePatch) {
            List<MoldPatch> result = new List<MoldPatch>();
            if (sourcePatch == null) return result;

            HashSet<MoldPatch> visited = new HashSet<MoldPatch>();
            Queue<MoldPatch> q = new Queue<MoldPatch>();

            visited.Add(sourcePatch);
            q.Enqueue(sourcePatch);

            while (q.Count > 0) {
                MoldPatch current = q.Dequeue();
                result.Add(current);

                if (!adj.TryGetValue(current, out List<MoldPatch> neighbors)) continue;

                foreach (var next in neighbors) {
                    if (next == null || visited.Contains(next)) continue;

                    bool traversable = IsSourcePatch(next) || next.SpreadState == MoldSpreadState.Active;
                    if (!traversable) continue;

                    visited.Add(next);
                    q.Enqueue(next);
                }
            }

            return result;
        }

        private List<MoldPatch> GatherLegalFrontier(List<MoldPatch> connectedRegion) {
            frontierBuffer.Clear();
            frontierSet.Clear();

            for (int i = 0; i < connectedRegion.Count; i++) {
                var patch = connectedRegion[i];
                if (patch == null) continue;

                if (!adj.TryGetValue(patch, out List<MoldPatch> neighbors)) continue;

                for (int j = 0; j < neighbors.Count; j++) {
                    var next = neighbors[j];
                    if (next == null) continue;
                    if (IsSourcePatch(next)) continue;
                    if (next.SpreadState != MoldSpreadState.Clean) continue;
                    if (IsPatchSprayBlocked(next)) continue;

                    if (frontierSet.Add(next)) {
                        frontierBuffer.Add(next);
                    }
                }
            }

            return frontierBuffer;
        }

        private MoldPatch ChooseWeightedPatch(List<MoldPatch> candidates) {
            if (candidates == null || candidates.Count == 0) return null;

            weightBuffer.Clear();

            int total = 0;

            for (int i = 0; i < candidates.Count; i++) {
                int w = Mathf.Max(0, GetPatchWeight(candidates[i]));
                weightBuffer.Add(w);
                total += w;
            }

            if (total <= 0) {
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            int roll = UnityEngine.Random.Range(0, total);
            int running = 0;

            for (int i = 0; i < candidates.Count; i++) {
                running += weightBuffer[i];
                if (roll < running) {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private int GetPatchWeight(MoldPatch patch) {
            if (patch == null) return 0;

            int weight = patch.surfaceType switch {
                MoldSurfaceType.Ceiling => ceilingWeight,
                MoldSurfaceType.Wall => wallWeight,
                MoldSurfaceType.Floor => floorWeight,
                MoldSurfaceType.Light => lightWeight,
                MoldSurfaceType.Camera => cameraWeight,
                _ => 1
            };

            if (useSpecialProximityBias) {
                if (distToNearestLight.TryGetValue(patch, out int dLight)) {
                    weight += DistanceBonus(dLight);
                }

                if (distToNearestCamera.TryGetValue(patch, out int dCam)) {
                    weight += DistanceBonus(dCam);
                }
            }

            return Mathf.Max(0, weight);
        }

        private int DistanceBonus(int dist) {
            return dist switch {
                0 => dist0Bonus,
                1 => dist1Bonus,
                2 => dist2Bonus,
                _ => 0
            };
        }

        private void PrecomputeSpecialDistances() {
            distToNearestLight.Clear();
            distToNearestCamera.Clear();

            MultiSourceBfsDistance(
                allPatches.Where(p => p != null && p.surfaceType == MoldSurfaceType.Light).ToList(),
                distToNearestLight
            );

            MultiSourceBfsDistance(
                allPatches.Where(p => p != null && p.surfaceType == MoldSurfaceType.Camera).ToList(),
                distToNearestCamera
            );
        }

        private void MultiSourceBfsDistance(List<MoldPatch> starts, Dictionary<MoldPatch, int> outDistances) {
            Queue<MoldPatch> q = new Queue<MoldPatch>();
            HashSet<MoldPatch> visited = new HashSet<MoldPatch>();

            foreach (var s in starts) {
                if (s == null) continue;
                visited.Add(s);
                outDistances[s] = 0;
                q.Enqueue(s);
            }

            while (q.Count > 0) {
                MoldPatch current = q.Dequeue();
                int baseDist = outDistances[current];

                if (!adj.TryGetValue(current, out List<MoldPatch> neighbors)) continue;

                foreach (var next in neighbors) {
                    if (next == null || visited.Contains(next)) continue;

                    visited.Add(next);
                    outDistances[next] = baseDist + 1;
                    q.Enqueue(next);
                }
            }
        }

        private void UpdateBloodTimers() {
            if (!allowBloodPhase) {
                ClearAllBlood();
                return;
            }

            if (ai <= 0) {
                ClearAllBlood();
                return;
            }

            for (int i = 0; i < allPatches.Count; i++) {
                MoldPatch patch = allPatches[i];
                if (patch == null) continue;

                if (patch.SpreadState == MoldSpreadState.Clean) {
                    bloodTimerByPatch.Remove(patch);

                    if (patch.CorruptionPhase != MoldCorruptionPhase.Normal) {
                        patch.SetCorruptionPhase(MoldCorruptionPhase.Normal);
                    }

                    continue;
                }

                bool shouldAccumulate = HasMarkedOrActiveNeighbor(patch);

                if (!shouldAccumulate) {
                    bloodTimerByPatch.Remove(patch);

                    if (patch.CorruptionPhase != MoldCorruptionPhase.Normal) {
                        patch.SetCorruptionPhase(MoldCorruptionPhase.Normal);
                    }

                    continue;
                }

                float timer = 0f;
                bloodTimerByPatch.TryGetValue(patch, out timer);
                timer += Time.deltaTime;
                bloodTimerByPatch[patch] = timer;

                if (timer >= bloodTransitionSeconds) {
                    if (patch.CorruptionPhase != MoldCorruptionPhase.Blood) {
                        patch.SetCorruptionPhase(MoldCorruptionPhase.Blood);
                    }
                }
                else {
                    if (patch.CorruptionPhase != MoldCorruptionPhase.Normal) {
                        patch.SetCorruptionPhase(MoldCorruptionPhase.Normal);
                    }
                }
            }
        }

        private bool HasMarkedOrActiveNeighbor(MoldPatch patch) {
            if (patch == null) return false;
            if (!adj.TryGetValue(patch, out List<MoldPatch> neighbors)) return false;

            for (int i = 0; i < neighbors.Count; i++) {
                MoldPatch n = neighbors[i];
                if (n == null) continue;

                if (n.SpreadState == MoldSpreadState.Marked ||
                    n.SpreadState == MoldSpreadState.Active) {
                    return true;
                }
            }

            return false;
        }

        private void ClearAllBlood() {
            bloodTimerByPatch.Clear();

            for (int i = 0; i < allPatches.Count; i++) {
                MoldPatch patch = allPatches[i];
                if (patch == null) continue;
                if (patch.SpreadState == MoldSpreadState.Clean) continue;

                if (patch.CorruptionPhase != MoldCorruptionPhase.Normal) {
                    patch.SetCorruptionPhase(MoldCorruptionPhase.Normal);
                }
            }
        }

        private void RecountDebugStats() {
            activePatchCount = 0;
            isolatedPatchCount = 0;
            bloodPatchCount = 0;
            sprayBlockedPatchCount = 0;

            for (int i = 0; i < allPatches.Count; i++) {
                MoldPatch p = allPatches[i];
                if (p == null) continue;

                if (p.SpreadState == MoldSpreadState.Active) {
                    activePatchCount++;
                }

                if (p.SpreadState == MoldSpreadState.Isolated) {
                    isolatedPatchCount++;
                }

                if (p.CorruptionPhase == MoldCorruptionPhase.Blood &&
                    p.SpreadState != MoldSpreadState.Clean) {
                    bloodPatchCount++;
                }

                if (IsPatchSprayBlocked(p)) {
                    sprayBlockedPatchCount++;
                }
            }
        }

        private Vector3 GetPatchDrawPosition(MoldPatch patch) {
            if (patch == null) return Vector3.zero;

            if (patch.targetRenderers != null) {
                for (int i = 0; i < patch.targetRenderers.Length; i++) {
                    Renderer r = patch.targetRenderers[i];
                    if (r != null) {
                        return r.bounds.center;
                    }
                }
            }

            return patch.transform.position;
        }

        private void OnDrawGizmosSelected() {
            if (!drawAdjacencyGizmos) return;

            if (adj.Count == 0) {
                CollectAllPatchesFromRooms();
                BuildAdjacency();
            }

            HashSet<(MoldPatch, MoldPatch)> drawn = new HashSet<(MoldPatch, MoldPatch)>();

            foreach (var kvp in adj) {
                MoldPatch a = kvp.Key;
                if (a == null) continue;

                Vector3 aPos = GetPatchDrawPosition(a);

                if (drawPatchSpheres) {
                    Gizmos.color = connectedSet.Contains(a)
                        ? (IsSourcePatch(a) ? sourceEdgeColor : adjacencyColor)
                        : isolatedEdgeColor;

                    Gizmos.DrawSphere(aPos, gizmoSphereRadius);
                }

                foreach (var b in kvp.Value) {
                    if (b == null) continue;

                    var k1 = (a, b);
                    var k2 = (b, a);
                    if (drawn.Contains(k1) || drawn.Contains(k2)) continue;

                    drawn.Add(k1);

                    Vector3 bPos = GetPatchDrawPosition(b);

                    bool eitherSource = IsSourcePatch(a) || IsSourcePatch(b);
                    bool bothConnected = connectedSet.Contains(a) && connectedSet.Contains(b);

                    if (eitherSource) {
                        Gizmos.color = sourceEdgeColor;
                    }
                    else if (bothConnected) {
                        Gizmos.color = adjacencyColor;
                    }
                    else {
                        Gizmos.color = isolatedEdgeColor;
                    }

                    Gizmos.DrawLine(aPos, bPos);
                }
            }
        }

        [ContextMenu("Mold/Sync Rooms From Hierarchy")]
        private void DebugSyncRoomsFromHierarchy() {
            SyncRoomsFromHierarchy();
            CollectAllPatchesFromRooms();

            Debug.Log($"[MoldManager] Synced {roomGroups.Count} room groups and collected {allPatches.Count} patches.", this);
        }

        [ContextMenu("Mold/Rebuild Graph")]
        private void DebugRebuildGraph() {
            CollectAllPatchesFromRooms();
            RuntimeCleanup();
            BuildAdjacency();
            PrecomputeSpecialDistances();
            RecomputeConnectivity();
            RefreshAllPatchStatesAfterConnectivity();
            RecountDebugStats();

            Debug.Log($"[MoldManager] Rebuilt adjacency for {adj.Count} patches.", this);
        }

        [ContextMenu("Mold/Recompute Connectivity")]
        private void DebugRecomputeConnectivity() {
            if (aiDisabled || ai <= 0) {
                ResetAllMoldToClean();
                Debug.Log("[MoldManager] AI disabled; forced all mold clean.", this);
                return;
            }

            RecomputeConnectivity();
            RefreshAllPatchStatesAfterConnectivity();
            RecountDebugStats();

            Debug.Log($"[MoldManager] Recomputed connectivity. Connected = {connectedSet.Count}", this);
        }

        [ContextMenu("Mold/Force Opportunity Tick")]
        private void DebugForceOpportunityTick() {
            HandleOpportunityTick(scheduler != null ? scheduler.CurrentTick : 0);
        }
    }
}