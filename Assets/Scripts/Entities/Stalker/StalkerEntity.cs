using UnityEngine;
using FNaS.Gameplay;
using FNaS.MasterNodes;
using FNaS.Systems;
using FNaS.Settings;

namespace FNaS.Entities.Stalker {
    public class StalkerEntity : MonoBehaviour, IRuntimeSettingsConsumer {
        [Header("Core AI")]
        [Range(0, 20)] public int ai = 10;
        [Min(1)] public int opportunityIntervalTicks = 1;

        [Header("Freeze Rules")]
        public bool freezeIfSeenOnCamera = true;
        public bool freezeIfSeenInPerson = true;
        [Tooltip("If true, the stalker is allowed to enter the player's current MasterNode (share the same node).")]
        public bool allowShareNodeWithPlayer = false;

        [Header("Door Loss")]
        [Min(1)] public int doorKillTicks = 2;

        [Header("Stun")]
        [Min(1)] public int stunTicksAfterPushback = 1;

        [Header("Flashlight Pushback")]
        public bool flashlightIsOnlyPushback = true;
        public float flashlightHoldSecondsToPush = 3f;
        public bool pushBackTwoNodes = false;

        [Header("Flashlight Vanish")]
        public bool flashlightCausesVanish = true;
        [Min(1)] public int vanishTicks = 2;
        [Tooltip("Reappear only in the first N nodes of the path (typically 2).")]
        public int reappearFirstNNodes = 2;

        [Header("Audio")]
        [Range(0f, 1f)] public float sfxVolume = 0.9f;
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

        [Header("Random Groaning")]
        [Tooltip("Min seconds between random groans while roaming.")]
        public float groanMinInterval = 18f;
        [Tooltip("Max seconds between random groans while roaming.")]
        public float groanMaxInterval = 35f;
        [Tooltip("If true, random groans stop once AtDoor.")]
        public bool disableRandomGroansAtDoor = true;
        [Tooltip("If >0, repeat door groan while at door every N seconds. If 0, play once when entering door.")]
        public float doorGroanRepeatSeconds = 0f;

        [Header("Groan when sharing node")]
        public bool groanWhenSameNode = true;
        public float sameNodeGroanCooldownSeconds = 6f;

        [Header("Screen FX")]
        public ScreenFader cameraFader;
        public ScreenFader playerFader;

        [Header("References")]
        public PlayerWaypointController player;
        public FlashlightTool flashlight;
        public GameAttentionState attentionState;
        public LoseState loseState;

        [Header("Movement Drivers")]
        [SerializeField] private StalkerNodeMovement nodeMovement;
        [SerializeField] private StalkerRoamMovement roamMovement;

        [Header("Jumpscare")]
        [SerializeField] private StalkerJumpscareController jumpscareController;

        private StalkerMovementBase movement;

        // Local runtime
        private float flashlightHoldTimer;

        // Tick-based runtime state
        private int stunnedUntilTick = -1;
        private int doorEnterTick = -1;
        private int reappearOnOrAfterTick = -1;

        // Shared flashlight stall mechanic
        private bool flashlightTouchedSinceLastEligibleTick;

        // Random groans
        private float nextGroanTime;
        private bool doorGroanPlayedOnce;
        private float doorGroanTimer;

        // Same node groan
        private bool wasSameNodeLastFrame;
        private float sameNodeGroanCooldownTimer;

        private bool isVanished;
        private bool aiDisabled;
        private bool subscribedToScheduler;

        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;

        private MasterNode lastCameraPulseNode;

        public bool IsThreatActive => isActiveAndEnabled && !aiDisabled && !isVanished && ai > 0;

        public MasterNode CurrentMasterNode => IsThreatActive && movement != null ? movement.CurrentMasterNode : null;
        public bool AtDoor => IsThreatActive && movement != null && movement.AtDoor;
        private bool IsPlayerMoving() => player != null && player.IsMoving;

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) return;

            ai = settings.GetInt("stalker.ai");
            freezeIfSeenOnCamera = settings.GetBool("stalker.freezeIfSeenOnCamera");
            freezeIfSeenInPerson = settings.GetBool("stalker.freezeIfSeenInPerson");
            allowShareNodeWithPlayer = settings.GetBool("stalker.allowShareNodeWithPlayer");
        }

        private void Awake() {
            if (nodeMovement == null) nodeMovement = GetComponent<StalkerNodeMovement>();
            if (roamMovement == null) roamMovement = GetComponent<StalkerRoamMovement>();

            movement = ResolveMovementFromSettings();

            if (movement == null) {
                Debug.LogError("StalkerEntity: No valid movement driver found.", this);
                enabled = false;
                return;
            }

            cachedRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            cachedColliders = GetComponentsInChildren<Collider>(includeInactive: true);
            SetVisible(true);
        }

        private void OnEnable() {
            TrySubscribeScheduler();
        }

        private void Start() {
            movement.Initialize();
            lastCameraPulseNode = movement.CurrentMasterNode;

            EnsureAudioSource();
            ScheduleNextRandomGroan();
            TrySubscribeScheduler();

            if (ai <= 0) {
                EnterAIDisabledState();
            }
        }

        private void Update() {
            if (HandleAIDisabledState()) return;
            if (loseState != null && loseState.hasLost) return;

            if (!IsThreatActive) {
                movement?.ClearOccupancy();
                SetBurning(false);
                return;
            }

            movement.RefreshOccupancy();

            HandleDoorLoss();
            if (loseState != null && loseState.hasLost) return;

            HandleFlashlightPushback();
            HandleGroans();
            HandleSameNodeGroan();

            movement.TickMovementVisuals();
        }

        private void OnDisable() {
            TryUnsubscribeScheduler();
            SetBurning(false);
        }

        private void OnDestroy() {
            TryUnsubscribeScheduler();
        }

        private bool HandleAIDisabledState() {
            if (ai <= 0) {
                if (!aiDisabled) EnterAIDisabledState();
                return true;
            }

            if (aiDisabled) {
                ExitAIDisabledState();
            }

            return false;
        }

        private void EnterAIDisabledState() {
            aiDisabled = true;
            isVanished = false;

            flashlightHoldTimer = 0f;
            flashlightTouchedSinceLastEligibleTick = false;
            stunnedUntilTick = -1;
            doorEnterTick = -1;
            reappearOnOrAfterTick = -1;

            SetVisible(false);
            SetBurning(false);
            movement?.ClearOccupancy();
        }

        private void ExitAIDisabledState() {
            aiDisabled = false;

            SetVisible(true);
            doorEnterTick = -1;
            stunnedUntilTick = -1;
            flashlightTouchedSinceLastEligibleTick = false;

            movement?.RefreshOccupancy();
            lastCameraPulseNode = movement != null ? movement.CurrentMasterNode : null;
        }

        private void TrySubscribeScheduler() {
            if (subscribedToScheduler) return;
            if (GlobalAIScheduler.Instance == null) return;

            GlobalAIScheduler.Instance.OnOpportunityTick += HandleOpportunityTick;
            subscribedToScheduler = true;
        }

        private void TryUnsubscribeScheduler() {
            if (!subscribedToScheduler) return;

            if (GlobalAIScheduler.Instance != null) {
                GlobalAIScheduler.Instance.OnOpportunityTick -= HandleOpportunityTick;
            }

            subscribedToScheduler = false;
        }

        private StalkerMovementBase ResolveMovementFromSettings() {
            var settings = RuntimeGameSettings.Instance;

            StalkerMovementMode mode = settings != null
                ? settings.GetStalkerMovementMode()
                : StalkerMovementMode.NodeBased;

            return mode switch {
                StalkerMovementMode.RoamTest => roamMovement != null ? roamMovement : nodeMovement,
                _ => nodeMovement != null ? nodeMovement : roamMovement
            };
        }

        private void EnsureAudioSource() {
            if (audioSource == null) {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        private void HandleOpportunityTick(int tick) {
            if (aiDisabled || ai <= 0) return;
            if (loseState != null && loseState.hasLost) return;
            if (movement == null) return;

            if (isVanished) {
                if (reappearOnOrAfterTick >= 0 && tick >= reappearOnOrAfterTick) {
                    ReappearAtStart(tick);
                }
                return;
            }

            if (opportunityIntervalTicks < 1) opportunityIntervalTicks = 1;
            if (tick % opportunityIntervalTicks != 0) return;

            if (IsPlayerMoving()) return;
            if (IsStunnedAtTick(tick)) return;
            if (IsFrozen()) return;

            if (flashlightTouchedSinceLastEligibleTick) {
                flashlightTouchedSinceLastEligibleTick = false;
                return;
            }

            int roll = Random.Range(0, 21);
            if (roll > ai) return;

            MasterNode playerNode = player != null ? player.CurrentMasterNode : null;
            bool moved = movement.TryAdvance(playerNode, allowShareNodeWithPlayer);
            if (moved) OnChangedNode(tick);
        }

        private void HandleDoorLoss() {
            if (!IsThreatActive) {
                doorEnterTick = -1;
                return;
            }

            var scheduler = GlobalAIScheduler.Instance;
            if (scheduler == null) return;

            if (AtDoor) {
                if (doorEnterTick < 0) {
                    doorEnterTick = scheduler.CurrentTick;
                }

                if (doorKillTicks < 1) doorKillTicks = 1;

                if (scheduler.CurrentTick - doorEnterTick >= doorKillTicks) {
                    if (jumpscareController != null)
                        jumpscareController.PlayJumpscare("Stalker waited at the door too long.");
                    else
                        loseState?.TriggerLose("Stalker waited at the door too long.");
                }
            }
            else {
                doorEnterTick = -1;
            }
        }

        private bool IsStunnedAtTick(int tick) {
            return stunnedUntilTick >= 0 && tick < stunnedUntilTick;
        }

        private void HandleFlashlightPushback() {
            if (aiDisabled) {
                flashlightHoldTimer = 0f;
                SetBurning(false);
                return;
            }

            if (!flashlightIsOnlyPushback) {
                flashlightHoldTimer = 0f;
                SetBurning(false);
                return;
            }

            if (isVanished) {
                flashlightHoldTimer = 0f;
                SetBurning(false);
                return;
            }

            if (flashlight == null || !flashlight.isOn) {
                flashlightHoldTimer = 0f;
                SetBurning(false);
                return;
            }

            bool affecting = flashlight.IsIlluminating(transform);

            if (affecting) {
                flashlightTouchedSinceLastEligibleTick = true;
            }

            SetBurning(affecting);

            if (!affecting) {
                flashlightHoldTimer = 0f;
                return;
            }

            flashlightHoldTimer += Time.deltaTime;

            if (flashlightHoldTimer >= flashlightHoldSecondsToPush) {
                flashlightHoldTimer = 0f;

                if (flashlightCausesVanish) {
                    VanishForTicks(vanishTicks);
                }
                else {
                    bool moved = movement != null && movement.PushBack(pushBackTwoNodes ? 2 : 1);
                    if (moved) {
                        ApplyStunTicks(stunTicksAfterPushback);
                        OnChangedNode(GlobalAIScheduler.Instance != null ? GlobalAIScheduler.Instance.CurrentTick : 0);
                    }
                }

                playerFader?.Pulse();
            }
        }

        private void ApplyStunTicks(int ticks) {
            var scheduler = GlobalAIScheduler.Instance;
            if (scheduler == null) return;

            ticks = Mathf.Max(1, ticks);
            int currentTick = scheduler.CurrentTick;
            stunnedUntilTick = Mathf.Max(stunnedUntilTick, currentTick + ticks + 1);
        }

        private bool IsFrozen() {
            var currentNode = CurrentMasterNode;
            if (currentNode == null) return false;

            if (freezeIfSeenOnCamera && attentionState != null && attentionState.isCameraActive) {
                if (attentionState.activeCameraNode != null && attentionState.activeCameraNode == currentNode)
                    return true;
            }

            if (freezeIfSeenInPerson && player != null && player.CurrentMasterNode != null) {
                if (player.CurrentMasterNode == currentNode)
                    return true;
            }

            return false;
        }

        private void OnChangedNode(int tick) {
            PlayFootstep();

            if (!AtDoor) {
                doorGroanPlayedOnce = false;
                doorGroanTimer = 0f;
                doorEnterTick = -1;
            }
            else if (doorEnterTick < 0) {
                doorEnterTick = tick;
            }

            PulseIfEnteredActiveCamera();
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
            burningSource.spatialBlend = 0f;
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

        private void SetVisible(bool visible) {
            if (cachedRenderers != null)
                foreach (var r in cachedRenderers) r.enabled = visible;

            if (cachedColliders != null)
                foreach (var c in cachedColliders) c.enabled = visible;
        }

        private void VanishForTicks(int ticks) {
            if (isVanished || aiDisabled) return;

            var scheduler = GlobalAIScheduler.Instance;
            if (scheduler == null) return;

            ticks = Mathf.Max(1, ticks);

            isVanished = true;
            flashlightHoldTimer = 0f;
            flashlightTouchedSinceLastEligibleTick = false;
            stunnedUntilTick = -1;
            doorEnterTick = -1;

            reappearOnOrAfterTick = scheduler.CurrentTick + ticks;

            SetVisible(false);
            SetBurning(false);
            movement?.ClearOccupancy();
        }

        private void ReappearAtStart(int tick) {
            if (aiDisabled || ai <= 0) return;

            isVanished = false;
            reappearOnOrAfterTick = -1;

            movement?.ReappearInFirstNNodes(reappearFirstNNodes);
            SetVisible(true);

            stunnedUntilTick = tick + 1;
            doorEnterTick = -1;

            if (audioSource != null && footstepClip != null)
                audioSource.PlayOneShot(footstepClip, sfxVolume);
        }
    }
}