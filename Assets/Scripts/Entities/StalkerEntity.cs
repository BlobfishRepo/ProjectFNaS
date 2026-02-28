using System.Collections.Generic;
using UnityEngine;
using FNaS.MasterNodes;
using FNaS.Gameplay;
using FNaS.Systems;

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

        [Header("Stun")]
        public float stunSecondsAfterPushback = 5f;
        private float stunTimer;

        [Header("Flashlight Pushback")]
        public bool flashlightIsOnlyPushback = true;
        public float flashlightHoldSecondsToPush = 3f;
        public bool pushBackTwoNodes = false;

        [Header("Camera FX")]
        [Tooltip("Pulse a black fade when the stalker ENTERS the currently active camera node.")]
        public ScreenFader cameraFader;

        [Header("Player POV FX")]
        [Tooltip("Pulse a black fade on the player's main view (HUD canvas).")]
        public ScreenFader playerFader;

        [Header("Audio")]
        [Tooltip("One AudioSource is enough. Use 2D or 3D depending on what you want.")]
        public AudioSource audioSource;

        [Tooltip("Played once each time the stalker changes nodes (forward or pushback).")]
        public AudioClip footstepClip;

        [Tooltip("Random ambient groan (rare).")]
        public AudioClip groanClip;

        [Tooltip("Groan used when at the final node (door). If null, falls back to groanClip.")]
        public AudioClip doorGroanClip;

        [Range(0f, 1f)] public float sfxVolume = 0.9f;

        [Header("Random Groan Tuning")]
        [Tooltip("Min seconds between random groans while roaming.")]
        public float groanMinInterval = 18f;

        [Tooltip("Max seconds between random groans while roaming.")]
        public float groanMaxInterval = 35f;

        [Tooltip("If true, random groans stop once AtDoor.")]
        public bool disableRandomGroansAtDoor = true;

        [Header("Door Groan Tuning")]
        [Tooltip("If >0, repeat door groan while at door every N seconds. If 0, play once when entering door.")]
        public float doorGroanRepeatSeconds = 0f;

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

        // Used to ensure we only pulse once per node change
        private MasterNode lastCameraPulseNode;

        // Audio timers/state
        private float nextGroanTime;
        private bool doorGroanPlayedOnce;
        private float doorGroanTimer;

        [Header("Groan when sharing node")]
        public bool groanWhenSameNode = true;
        public float sameNodeGroanCooldownSeconds = 6f;

        private bool wasSameNodeLastFrame;
        private float sameNodeGroanCooldownTimer;

        public MasterNode CurrentMasterNode =>
            (currentIndex >= 0 && currentIndex < resolvedPath.Count) ? resolvedPath[currentIndex] : null;

        public bool AtDoor =>
            resolvedPath.Count > 0 && currentIndex >= resolvedPath.Count - 1;

        private void Awake() {
            Debug.Log($"StalkerEntity Awake on '{name}' (scene={gameObject.scene.name}) pathDef={(pathDef ? pathDef.name : "NULL")}", this);
        }

        private void Start() {
            ResolvePath();
            if (resolvedPath.Count == 0)
                Debug.LogError("StalkerEntity: Resolved path is empty. Check pathDef + registry.", this);

            UpdateBlocking();
            lastCameraPulseNode = CurrentMasterNode;

            // If audioSource wasn't assigned, try to find/create one (safe fallback)
            if (audioSource == null) {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Good defaults (you can override in Inspector)
            // If you want positional audio, set spatialBlend = 1 in Inspector.
            audioSource.playOnAwake = false;
            audioSource.loop = false;

            ScheduleNextRandomGroan();
        }

        private void ResolvePath() {
            resolvedPath.Clear();

            if (pathDef == null) {
                Debug.LogError("StalkerEntity: pathDef not assigned.", this);
                return;
            }

            if (MasterNodeRegistry.Instance == null) {
                Debug.LogError("StalkerEntity: No MasterNodeRegistry in scene.", this);
                return;
            }

            foreach (var guid in pathDef.nodeGuids) {
                var node = MasterNodeRegistry.Instance.GetOrNull(guid);
                if (node == null) {
                    Debug.LogError($"StalkerEntity: Could not resolve MasterNode GUID '{guid}'.", this);
                    resolvedPath.Clear();
                    return;
                }
                resolvedPath.Add(node);
            }

            if (resolvedPath.Count == 0) currentIndex = 0;
            else currentIndex = Mathf.Clamp(currentIndex, 0, resolvedPath.Count - 1);

            Debug.Log($"StalkerEntity: ResolvedPathCount={resolvedPath.Count}, startIndex={currentIndex}", this);
        }

        private void Update() {
            if (loseState != null && loseState.hasLost) return;

            UpdateBlocking();

            if (stunTimer > 0f) stunTimer -= Time.deltaTime;

            // Door loss timer
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
            HandleGroans();
            HandleSameNodeGroan();
        }

        private void HandleMovementOpportunities() {
            if (stunTimer > 0f) return;

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
                stunTimer = Mathf.Max(stunTimer, stunSecondsAfterPushback);

                playerFader?.Pulse();
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
            if (next == currentIndex) return;

            // NEW: don't move onto the player's node
            MasterNode nextNode = resolvedPath[next];
            if (player != null && player.CurrentMasterNode != null && nextNode != null) {
                if (nextNode == player.CurrentMasterNode) {
                    return;
                }
            }

            currentIndex = next;
            OnChangedNode();
        }

        private void PushBack(int steps) {
            if (resolvedPath.Count == 0) return;

            int next = Mathf.Max(0, currentIndex - Mathf.Max(1, steps));
            if (next == currentIndex) return;

            currentIndex = next;
            Debug.Log($"Stalker pushed back to index {currentIndex} ({CurrentMasterNode?.Id})", this);

            OnChangedNode();
        }

        // --------- Audio hooks / camera pulse hooks ----------
        private void OnChangedNode() {
            PlayFootstep();

            // If we backed away from door, allow door groan again next time we re-enter
            if (!AtDoor) {
                doorGroanPlayedOnce = false;
                doorGroanTimer = 0f;
            }

            PulseIfEnteredActiveCamera();

            // Reset random groan schedule so it doesn't instantly fire after a move
            ScheduleNextRandomGroan();
        }

        private void PlayFootstep() {
            if (audioSource == null || footstepClip == null) return;
            audioSource.PlayOneShot(footstepClip, sfxVolume);
        }

        private void PlayGroan(AudioClip clip) {
            if (audioSource == null || clip == null) return;
            audioSource.PlayOneShot(clip, sfxVolume);
        }

        private void ScheduleNextRandomGroan() {
            float min = Mathf.Max(0.1f, groanMinInterval);
            float max = Mathf.Max(min, groanMaxInterval);
            nextGroanTime = Time.time + Random.Range(min, max);
        }

        private void HandleGroans() {
            // Door groan behavior
            if (AtDoor) {
                AudioClip clip = (doorGroanClip != null) ? doorGroanClip : groanClip;

                if (doorGroanRepeatSeconds <= 0f) {
                    // Play once upon arriving at door
                    if (!doorGroanPlayedOnce) {
                        PlayGroan(clip);
                        doorGroanPlayedOnce = true;
                    }
                }
                else {
                    // Repeat every N seconds while at door
                    doorGroanTimer += Time.deltaTime;
                    if (doorGroanTimer >= doorGroanRepeatSeconds) {
                        doorGroanTimer = 0f;
                        PlayGroan(clip);
                    }
                }

                if (disableRandomGroansAtDoor) return;
            }

            // Random roaming groans
            if (groanClip == null || audioSource == null) return;
            if (Time.time >= nextGroanTime) {
                PlayGroan(groanClip);
                ScheduleNextRandomGroan();
            }
        }

        private void PulseIfEnteredActiveCamera() {
            if (attentionState == null || !attentionState.isCameraActive) return;
            if (attentionState.activeCameraNode == null) return;

            var now = CurrentMasterNode;
            if (now == null) return;

            // Only pulse when we ENTER the active camera node
            if (attentionState.activeCameraNode == now && lastCameraPulseNode != now) {
                cameraFader?.Pulse();
            }

            lastCameraPulseNode = now;
        }

        // --------- Blocking / position ----------
        private void UpdateBlocking() {
            if (blockerRegistry == null) return;

            var node = CurrentMasterNode;
            if (node == null) return;

            if (lastBlockedNode != null && lastBlockedNode != node)
                blockerRegistry.SetBlockedForward(lastBlockedNode, false);

            blockerRegistry.SetBlockedForward(node, true);
            lastBlockedNode = node;
        }

        private void HandleSameNodeGroan() {
            if (!groanWhenSameNode) return;
            if (audioSource == null || groanClip == null) return;
            if (player == null || player.CurrentMasterNode == null || CurrentMasterNode == null) return;

            if (sameNodeGroanCooldownTimer > 0f)
                sameNodeGroanCooldownTimer -= Time.deltaTime;

            bool same = (player.CurrentMasterNode == CurrentMasterNode);

            // Trigger once on entering the same node
            if (same && !wasSameNodeLastFrame && sameNodeGroanCooldownTimer <= 0f) {
                PlayGroan(groanClip);
                sameNodeGroanCooldownTimer = Mathf.Max(0f, sameNodeGroanCooldownSeconds);
            }

            wasSameNodeLastFrame = same;
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