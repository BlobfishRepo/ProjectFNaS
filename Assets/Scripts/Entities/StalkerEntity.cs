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

        [Header("Player Collision Rule")]
        [Tooltip("If true, the stalker is allowed to enter the player's current MasterNode (share the same node).")]
        public bool allowShareNodeWithPlayer = false;

        [Header("Door Loss")]
        public float doorKillSeconds = 10f;

        [Header("Stun")]
        public float stunSecondsAfterPushback = 5f;

        [Header("Flashlight Pushback")]
        public bool flashlightIsOnlyPushback = true;
        public float flashlightHoldSecondsToPush = 3f;
        public bool pushBackTwoNodes = false;

        [Header("Flashlight Vanish")]
        public bool flashlightCausesVanish = true;
        public float vanishSeconds = 10f;
        [Tooltip("Reappear only in the first N nodes of the path (typically 2).")]
        public int reappearFirstNNodes = 2;

        [Header("Spatial Placement")]
        [Tooltip("Teleport to a per-node anchor (EntityAnchorSet) on node changes.")]
        public bool teleportToAnchors = true;

        [Tooltip("If null, uses this GameObject's transform.")]
        public Transform teleportTarget;

        [Tooltip("If no stalker anchors exist on the node, snap to the node transform.")]
        public bool fallbackToNodeTransform = true;

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

        [Header("Flashlight Burning SFX")]
        public AudioClip burningLoopClip;
        [Range(0f, 1f)] public float burningVolume = 0.8f;

        private AudioSource burningSource;

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
        public PlayerWaypointController player;
        public FlashlightTool flashlight;
        public ViewController viewController;
        public GameAttentionState attentionState;
        public BlockerRegistry blockerRegistry;
        public LoseState loseState;

        [Header("Groan when sharing node")]
        public bool groanWhenSameNode = true;
        public float sameNodeGroanCooldownSeconds = 6f;

        [Header("Runtime")]
        [SerializeField] private int currentIndex = 0;

        private readonly List<MasterNode> resolvedPath = new();

        private MasterNode lastBlockedNode;
        private MasterNode lastCameraPulseNode;

        private float opportunityTimer;
        private float doorTimer;
        private float stunTimer;
        private float flashlightHoldTimer;

        // Random groans
        private float nextGroanTime;
        private bool doorGroanPlayedOnce;
        private float doorGroanTimer;

        // Same node groan
        private bool wasSameNodeLastFrame;
        private float sameNodeGroanCooldownTimer;

        private bool isVanished;
        private float vanishTimer;

        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;

        // Teleport guard
        private int lastTeleportedIndex = int.MinValue;
        private bool freezeWhilePlayerMoving;

        // Current anchor slot (spatial placement only)
        private EntityAnchorSet.StalkerAnchorSlot currentAnchorSlot;

        public MasterNode CurrentMasterNode =>
            (currentIndex >= 0 && currentIndex < resolvedPath.Count) ? resolvedPath[currentIndex] : null;

        public bool AtDoor =>
            resolvedPath.Count > 0 && currentIndex >= resolvedPath.Count - 1;

        private void Awake() {
            if (teleportTarget == null) teleportTarget = transform;
            cachedRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            cachedColliders = GetComponentsInChildren<Collider>(includeInactive: true);
            SetVisible(true);
        }

        private void Start() {
            if (viewController == null) viewController = FindFirstObjectByType<ViewController>();

            if (viewController != null) {
                viewController.OnBeginMove += OnPlayerBeginMove;
                viewController.OnEnteredWaypointOrView += OnPlayerEndMoveOrEnter;
            }

            ResolvePath();
            if (resolvedPath.Count == 0) {
                Debug.LogError("StalkerEntity: Resolved path is empty. Check pathDef + registry.", this);
            }

            EnsureAudioSource();
            UpdateBlocking();
            lastCameraPulseNode = CurrentMasterNode;

            TeleportToCurrentNodeIfNeeded(force: true);
            ScheduleNextRandomGroan();
        }

        private void Update() {
            if (loseState != null && loseState.hasLost) return;

            if (isVanished) {
                vanishTimer -= Time.deltaTime;
                if (vanishTimer <= 0f) ReappearAtStart();
                return;
            }

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

            TeleportToCurrentNodeIfNeeded();
        }

        private void OnPlayerBeginMove() => freezeWhilePlayerMoving = true;
        private void OnPlayerEndMoveOrEnter() => freezeWhilePlayerMoving = false;

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

            currentIndex = resolvedPath.Count == 0 ? 0 : Mathf.Clamp(currentIndex, 0, resolvedPath.Count - 1);
        }

        private void EnsureAudioSource() {
            if (audioSource == null) {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        private void HandleMovementOpportunities() {
            if (freezeWhilePlayerMoving) { opportunityTimer = 0f; return; }
            if (stunTimer > 0f) { opportunityTimer = 0f; return; }
            if (IsFrozen()) { opportunityTimer = 0f; return; }

            opportunityTimer += Time.deltaTime;
            if (opportunityTimer < opportunityIntervalSeconds) return;
            opportunityTimer = 0f;

            if (resolvedPath.Count == 0) return;

            float p = Mathf.Clamp01(ai / 20f);
            if (Random.value <= p) MoveForwardOne();
        }

        private void HandleFlashlightPushback() {
            if (!flashlightIsOnlyPushback) { flashlightHoldTimer = 0f; SetBurning(false); return; }
            if (isVanished) { flashlightHoldTimer = 0f; SetBurning(false); return; }
            if (flashlight == null || !flashlight.isOn) { flashlightHoldTimer = 0f; SetBurning(false); return; }

            // Cone-only LOS (no wall blocking)
            bool affecting = flashlight.IsIlluminating(transform);

            SetBurning(affecting);

            if (!affecting) {
                flashlightHoldTimer = 0f;
                return;
            }

            flashlightHoldTimer += Time.deltaTime;

            if (flashlightHoldTimer >= flashlightHoldSecondsToPush) {
                flashlightHoldTimer = 0f;

                if (flashlightCausesVanish) {
                    VanishForSeconds(vanishSeconds);
                }
                else {
                    PushBack(pushBackTwoNodes ? 2 : 1);
                    stunTimer = Mathf.Max(stunTimer, stunSecondsAfterPushback);
                }

                playerFader?.Pulse();
            }
        }

        private bool IsFrozen() {
            if (CurrentMasterNode == null) return false;

            if (freezeIfSeenOnCamera && attentionState != null && attentionState.isCameraActive) {
                if (attentionState.activeCameraNode != null && attentionState.activeCameraNode == CurrentMasterNode)
                    return true;
            }

            if (freezeIfSeenInPerson && player != null && player.CurrentMasterNode != null) {
                if (player.CurrentMasterNode == CurrentMasterNode)
                    return true;
            }

            return false;
        }

        private void MoveForwardOne() {
            if (freezeWhilePlayerMoving) return;
            if (resolvedPath.Count == 0) return;

            int next = Mathf.Min(currentIndex + 1, resolvedPath.Count - 1);
            if (next == currentIndex) return;

            if (!allowShareNodeWithPlayer && player != null && player.CurrentMasterNode != null) {
                var nextNode = resolvedPath[next];
                if (nextNode == player.CurrentMasterNode) return;
            }

            currentIndex = next;
            OnChangedNode();
        }

        private void PushBack(int steps) {
            if (resolvedPath.Count == 0) return;

            int next = Mathf.Max(0, currentIndex - Mathf.Max(1, steps));
            if (next == currentIndex) return;

            currentIndex = next;
            OnChangedNode();
        }

        private void OnChangedNode() {
            PlayFootstep();

            if (!AtDoor) {
                doorGroanPlayedOnce = false;
                doorGroanTimer = 0f;
            }

            PulseIfEnteredActiveCamera();
            ScheduleNextRandomGroan();

            TeleportToCurrentNodeIfNeeded(force: true);
        }

        private void TeleportToCurrentNodeIfNeeded(bool force = false) {
            if (!teleportToAnchors) return;
            if (!force && currentIndex == lastTeleportedIndex) return;

            lastTeleportedIndex = currentIndex;

            var node = CurrentMasterNode;
            if (node == null || teleportTarget == null) return;

            currentAnchorSlot = null;

            Transform anchor = null;
            var anchorSet = node.GetComponent<EntityAnchorSet>();
            if (anchorSet != null) {
                currentAnchorSlot = anchorSet.GetRandomStalkerAnchor();
                anchor = currentAnchorSlot != null ? currentAnchorSlot.anchor : null;
            }

            if (anchor != null) {
                teleportTarget.SetPositionAndRotation(anchor.position, anchor.rotation);
                return;
            }

            if (fallbackToNodeTransform) {
                currentAnchorSlot = null;
                teleportTarget.SetPositionAndRotation(node.transform.position, node.transform.rotation);
            }
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
            if (AtDoor) {
                AudioClip clip = (doorGroanClip != null) ? doorGroanClip : groanClip;

                if (doorGroanRepeatSeconds <= 0f) {
                    if (!doorGroanPlayedOnce) {
                        PlayGroan(clip);
                        doorGroanPlayedOnce = true;
                    }
                }
                else {
                    doorGroanTimer += Time.deltaTime;
                    if (doorGroanTimer >= doorGroanRepeatSeconds) {
                        doorGroanTimer = 0f;
                        PlayGroan(clip);
                    }
                }

                if (disableRandomGroansAtDoor) return;
            }

            if (groanClip == null || audioSource == null) return;

            if (Time.time >= nextGroanTime) {
                PlayGroan(groanClip);
                ScheduleNextRandomGroan();
            }
        }

        private void EnsureBurningSource() {
            if (burningSource != null) return;

            burningSource = gameObject.AddComponent<AudioSource>();
            burningSource.playOnAwake = false;
            burningSource.loop = true;
            burningSource.spatialBlend = 0f; // 2D; set to 1f if you want 3D
            burningSource.volume = burningVolume;
        }

        private void SetBurning(bool on) {
            EnsureBurningSource();

            if (burningLoopClip == null) {
                if (burningSource.isPlaying) burningSource.Stop();
                return;
            }

            if (on) {
                if (burningSource.clip != burningLoopClip) burningSource.clip = burningLoopClip;
                burningSource.volume = burningVolume;
                if (!burningSource.isPlaying) burningSource.Play();
            }
            else {
                if (burningSource.isPlaying) burningSource.Stop();
            }
        }

        private void PulseIfEnteredActiveCamera() {
            if (attentionState == null || !attentionState.isCameraActive) return;
            if (attentionState.activeCameraNode == null) return;

            var now = CurrentMasterNode;
            if (now == null) return;

            if (attentionState.activeCameraNode == now && lastCameraPulseNode != now) {
                cameraFader?.Pulse();
            }

            lastCameraPulseNode = now;
        }

        private void UpdateBlocking() {
            if (blockerRegistry == null) return;

            var node = CurrentMasterNode;
            if (node == null) return;

            if (lastBlockedNode != null && lastBlockedNode != node) {
                blockerRegistry.SetBlockedForward(lastBlockedNode, false);
            }

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

            if (same && !wasSameNodeLastFrame && sameNodeGroanCooldownTimer <= 0f) {
                PlayGroan(groanClip);
                sameNodeGroanCooldownTimer = Mathf.Max(0f, sameNodeGroanCooldownSeconds);
            }

            wasSameNodeLastFrame = same;
        }

        private void OnDisable() {
            if (viewController != null) {
                viewController.OnBeginMove -= OnPlayerBeginMove;
                viewController.OnEnteredWaypointOrView -= OnPlayerEndMoveOrEnter;
            }

            if (blockerRegistry != null && CurrentMasterNode != null) {
                blockerRegistry.SetBlockedForward(CurrentMasterNode, false);
            }
            SetBurning(false);
        }

        private void SetVisible(bool visible) {
            if (cachedRenderers != null)
                foreach (var r in cachedRenderers) r.enabled = visible;

            if (cachedColliders != null)
                foreach (var c in cachedColliders) c.enabled = visible;
        }

        private void VanishForSeconds(float seconds) {
            if (isVanished) return;

            isVanished = true;
            vanishTimer = seconds;

            stunTimer = 0f;
            doorTimer = 0f;
            opportunityTimer = 0f;
            flashlightHoldTimer = 0f;

            SetVisible(false);
            SetBurning(false);

            if (blockerRegistry != null && lastBlockedNode != null) {
                blockerRegistry.SetBlockedForward(lastBlockedNode, false);
                lastBlockedNode = null;
            }
        }

        private void ReappearAtStart() {
            isVanished = false;

            int max = Mathf.Clamp(reappearFirstNNodes, 1, Mathf.Max(1, resolvedPath.Count));
            int newIndex = Random.Range(0, max);

            currentIndex = Mathf.Clamp(newIndex, 0, resolvedPath.Count - 1);

            lastTeleportedIndex = int.MinValue;
            TeleportToCurrentNodeIfNeeded(force: true);

            SetVisible(true);

            UpdateBlocking();
            stunTimer = 0.5f;

            if (audioSource != null && footstepClip != null)
                audioSource.PlayOneShot(footstepClip, sfxVolume);
        }
    }
}