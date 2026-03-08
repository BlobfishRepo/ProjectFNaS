using UnityEngine;
using FNaS.Gameplay;
using FNaS.MasterNodes;
using FNaS.Systems;
using FNaS.Settings;

namespace FNaS.Entities.Stalker {
    public class StalkerEntity : MonoBehaviour {
        [Header("Core AI")]
        [Range(0, 20)] public int ai = 10;
        public float opportunityIntervalSeconds = 5f;

        [Header("Freeze Rules")]
        public bool freezeIfSeenOnCamera = true;
        public bool freezeIfSeenInPerson = true;
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

        private StalkerMovementBase movement;

        // Runtime timers/state
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

        private MasterNode lastCameraPulseNode;
        
        public MasterNode CurrentMasterNode => movement != null ? movement.CurrentMasterNode : null;
        public bool AtDoor => movement != null && movement.AtDoor;
        private bool IsPlayerMoving() => player != null && player.IsMoving;

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

        private void Start() {
            movement.Initialize();
            lastCameraPulseNode = movement.CurrentMasterNode;

            EnsureAudioSource();
            ScheduleNextRandomGroan();
        }

        private void Update() {
            if (loseState != null && loseState.hasLost) return;

            if (isVanished) {
                vanishTimer -= Time.deltaTime;
                if (vanishTimer <= 0f) ReappearAtStart();
                return;
            }

            movement.RefreshOccupancy();

            if (stunTimer > 0f) stunTimer -= Time.deltaTime;

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

            movement.TickMovementVisuals();
        }

        private void OnDisable() {
            SetBurning(false);
        }

        private StalkerMovementBase ResolveMovementFromSettings() {
            var settings = GameSettingsManager.Instance;

            StalkerMovementMode mode = settings != null
                ? settings.StalkerMovementMode
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

        private void HandleMovementOpportunities() {
            if (movement == null) return;
            if (IsPlayerMoving()) { opportunityTimer = 0f; return; }
            if (stunTimer > 0f) { opportunityTimer = 0f; return; }
            if (IsFrozen()) { opportunityTimer = 0f; return; }

            opportunityTimer += Time.deltaTime;
            if (opportunityTimer < opportunityIntervalSeconds) return;
            opportunityTimer = 0f;

            float p = Mathf.Clamp01(ai / 20f);
            if (Random.value <= p) {
                MasterNode playerNode = player != null ? player.CurrentMasterNode : null;
                bool moved = movement.TryAdvance(playerNode, allowShareNodeWithPlayer);
                if (moved) OnChangedNode();
            }
        }

        private void HandleFlashlightPushback() {
            if (!flashlightIsOnlyPushback) { flashlightHoldTimer = 0f; SetBurning(false); return; }
            if (isVanished) { flashlightHoldTimer = 0f; SetBurning(false); return; }
            if (flashlight == null || !flashlight.isOn) { flashlightHoldTimer = 0f; SetBurning(false); return; }

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
                    bool moved = movement != null && movement.PushBack(pushBackTwoNodes ? 2 : 1);
                    if (moved) {
                        stunTimer = Mathf.Max(stunTimer, stunSecondsAfterPushback);
                        OnChangedNode();
                    }
                }

                playerFader?.Pulse();
            }
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

        private void OnChangedNode() {
            PlayFootstep();

            if (!AtDoor) {
                doorGroanPlayedOnce = false;
                doorGroanTimer = 0f;
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

            movement?.ClearOccupancy();
        }

        private void ReappearAtStart() {
            isVanished = false;

            movement?.ReappearInFirstNNodes(reappearFirstNNodes);

            SetVisible(true);

            stunTimer = 0.5f;

            if (audioSource != null && footstepClip != null)
                audioSource.PlayOneShot(footstepClip, sfxVolume);
        }
    }
}