using System.Collections.Generic;
using UnityEngine;
using FNaS.MasterNodes;
using FNaS.Gameplay;
using FNaS.Systems;
using FNaS.Entities;

namespace FNaS.Entities.Stalker {
    public class StalkerEntity : MonoBehaviour {
        [Header("Path (GUID-based)")]
        public LinearPathDefinition pathDef;

        [Range(0, 20)] public int ai = 10;

        [Header("Movement Opportunities")]
        public float opportunityIntervalSeconds = 5f;

        [Header("Freeze Rules")]
        public bool freezeIfSeenOnCamera = true;
        public bool freezeIfSeenInPerson = true;

        [Header("Door Loss")]
        public float doorKillSeconds = 10f;

        [Header("Flashlight Pushback")]
        public bool flashlightIsOnlyPushback = true;
        public float flashlightHoldSecondsToPush = 3f;
        public bool pushBackTwoNodes = false;

        [Header("References")]
        public PlayerNodeController player;
        public FlashlightTool flashlight;
        public GameAttentionState attentionState;
        public BlockerRegistry blockerRegistry;
        public LoseState loseState;

        [Header("Runtime")]
        [SerializeField] private int currentIndex = 0;

        private float opportunityTimer;
        private float doorTimer;
        private float flashlightHoldTimer;

        private readonly List<MasterNode> resolvedPath = new();
        private MasterNode lastBlockedNode;

        public MasterNode CurrentMasterNode => (currentIndex >= 0 && currentIndex < resolvedPath.Count) ? resolvedPath[currentIndex] : null;
        public bool AtDoor => resolvedPath.Count > 0 && currentIndex >= resolvedPath.Count - 1;

        private void Awake() {
            Debug.Log($"StalkerEntity Awake on '{name}' (scene={gameObject.scene.name}) pathDef={(pathDef ? pathDef.name : "NULL")}", this);
            
        }

        private void Start() {
            ResolvePath();
            if (resolvedPath.Count == 0)
                Debug.LogError("StalkerEntity: Resolved path is empty. Check pathDef + registry.");

            UpdateBlocking();
        }

        private void ResolvePath() {
            resolvedPath.Clear();

            if (pathDef == null) {
                Debug.LogError("StalkerEntity: pathDef not assigned.");
                return;
            }

            if (MasterNodeRegistry.Instance == null) {
                Debug.LogError("StalkerEntity: No MasterNodeRegistry in scene.");
                return;
            }

            foreach (var guid in pathDef.nodeGuids) {
                var node = MasterNodeRegistry.Instance.GetOrNull(guid);
                if (node == null) {
                    Debug.LogError($"StalkerEntity: Could not resolve MasterNode GUID '{guid}'.");
                    resolvedPath.Clear();
                    return;
                }
                resolvedPath.Add(node);
            }

            // Clamp index in case definition changed
            if (resolvedPath.Count == 0) currentIndex = 0;
            else currentIndex = Mathf.Clamp(currentIndex, 0, resolvedPath.Count - 1);
            Debug.Log($"StalkerEntity: ResolvedPathCount={resolvedPath.Count}, startIndex={currentIndex}");
        }

        private void Update() {
            if (loseState != null && loseState.hasLost) return;

            UpdateBlocking();

            if (AtDoor) {
                doorTimer += Time.deltaTime;
                if (doorTimer >= doorKillSeconds) {
                    loseState?.TriggerLose("Stalker waited at the door too long.");
                    return;
                }
            }
            else {
                doorTimer = 0f;
            }

            HandleFlashlightPushback();
            HandleMovementOpportunities();
        }

        private void HandleMovementOpportunities() {
            opportunityTimer += Time.deltaTime;
            if (opportunityTimer < opportunityIntervalSeconds) return;
            opportunityTimer = 0f;

            if (IsFrozen()) return;
            if (resolvedPath.Count == 0) return;

            float p = Mathf.Clamp01(ai / 20f);
            if (Random.value <= p) MoveForwardOne();
        }

        private void HandleFlashlightPushback() {
            if (!flashlightIsOnlyPushback) return;
            if (flashlight == null || !flashlight.isOn) { flashlightHoldTimer = 0f; return; }

            if (player == null || player.CurrentMasterNode == null) { flashlightHoldTimer = 0f; return; }
            if (CurrentMasterNode == null) { flashlightHoldTimer = 0f; return; }

            bool inSameMasterNode = (player.CurrentMasterNode == CurrentMasterNode);
            if (!inSameMasterNode) { flashlightHoldTimer = 0f; return; }

            flashlightHoldTimer += Time.deltaTime;
            if (flashlightHoldTimer >= flashlightHoldSecondsToPush) {
                flashlightHoldTimer = 0f;
                PushBack(pushBackTwoNodes ? 2 : 1);
            }
        }

        private bool IsFrozen() {
            bool seenOnCamera = false;
            bool seenInPerson = false;

            if (freezeIfSeenOnCamera && attentionState != null && attentionState.isCameraActive) {
                if (attentionState.activeCameraNode != null && attentionState.activeCameraNode == CurrentMasterNode)
                    seenOnCamera = true;
            }

            if (freezeIfSeenInPerson && player != null && player.CurrentMasterNode != null) {
                if (player.CurrentMasterNode == CurrentMasterNode)
                    seenInPerson = true;
            }

            return seenOnCamera || seenInPerson;
        }

        private void MoveForwardOne() {
            if (resolvedPath.Count == 0) return;
            int next = Mathf.Min(currentIndex + 1, resolvedPath.Count - 1);
            if (next != currentIndex) {
                currentIndex = next;
                Debug.Log($"Stalker moved forward to index {currentIndex} ({CurrentMasterNode?.Id})");
            }
        }

        private void PushBack(int steps) {
            if (resolvedPath.Count == 0) return;
            int next = Mathf.Max(0, currentIndex - Mathf.Max(1, steps));
            if (next != currentIndex) {
                currentIndex = next;
                Debug.Log($"Stalker pushed back to index {currentIndex} ({CurrentMasterNode?.Id})");
            }
        }

        private void UpdateBlocking() {
            if (blockerRegistry == null) return;

            var node = CurrentMasterNode;
            if (node == null) return;

            if (lastBlockedNode != null && lastBlockedNode != node)
                blockerRegistry.SetBlockedForward(lastBlockedNode, false);

            blockerRegistry.SetBlockedForward(node, true);
            lastBlockedNode = node;
        }

        private void OnDisable() {
            if (blockerRegistry != null && CurrentMasterNode != null)
                blockerRegistry.SetBlockedForward(CurrentMasterNode, false);
        }

        private void LateUpdate() {
            if (CurrentMasterNode != null) {
                transform.position = CurrentMasterNode.transform.position;
            }
        }
    }
}