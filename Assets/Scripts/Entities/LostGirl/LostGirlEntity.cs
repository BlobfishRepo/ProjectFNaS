using System.Collections.Generic;
using UnityEngine;
using FNaS.Systems;
using FNaS.Settings;

namespace FNaS.Entities.LostGirl {
    public class LostGirlEntity : MonoBehaviour, IRuntimeSettingsConsumer {
        public enum LostGirlPhase {
            PreSpawn,
            InGlass,
            Queued,
            Emerging,
            Chasing,
            Jumpscare
        }

        [Header("Core AI")]
        [Range(0, 20)] public int ai = 10;
        [Min(1)] public int preSpawnOpportunityTicks = 2;
        [Min(1)] public int glassProgressOpportunityTicks = 2;

        [Header("Glass")]
        public int maxGlassStage = 3;

        [Header("Observation")]
        public float observeHoldSecondsToPushback = 1.25f;
        public float observationCheckInterval = 0.10f;
        [Range(0f, 180f)] public float directObserveAngleDegrees = 25f;
        public float directObserveMaxDistance = 25f;
        public LayerMask observationBlockers = ~0;
        public Transform playerViewOrigin;
        public GameAttentionState attentionState;

        [Header("Emerging")]
        public float emergeLingerSeconds = 0.2f;

        [Header("Chase")]
        public float activeChaseDurationSeconds = 8f;
        public float killDistance = 1.25f;
        public bool requireLineOfSightForKill = true;

        [Header("Open Door Kill")]
        public bool instantKillIfAnyDoorOpen = true;
        public List<Door> trackedDoors = new();

        [Header("Spawn / Region")]
        public bool requireReachableRegionToSpawn = true;
        public Transform playerTarget;
        public Transform playerRegionProbe;
        public List<LostGirlRegionVolume> playerRegionVolumes = new();
        public List<LostGirlGlassAnchor> anchors = new();

        [Header("References")]
        public LoseState loseState;
        public LostGirlMovement movement;
        public GameObject activeVisualRoot;
        public LostGirlJumpscareController jumpscareController;

        [Header("Audio")]
        public AudioSource audioSource;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        public AudioClip spawnInClip;
        public AudioClip stageProgressClip;
        public AudioClip activationClip;
        public AudioClip runStartClip;
        public AudioClip runningLoopClip;
        [Range(0f, 1f)] public float runningLoopVolume = 0.9f;

        [Header("Debug")]
        public bool verboseLogging = false;

        [Header("Runtime")]
        [SerializeField] private LostGirlPhase phase = LostGirlPhase.PreSpawn;
        [SerializeField] private int glassStage;
        [SerializeField] private LostGirlGlassAnchor currentAnchor;
        [SerializeField] private LostGirlRegionId currentPlayerRegion = LostGirlRegionId.None;

        private bool subscribedToScheduler;
        private bool aiDisabled;
        private bool reachedMaxStageThisCycle;
        private float emergeTimer;
        private float activeTimer;
        private float observedHoldTimer;
        private float nextObservationCheckTime;
        private AudioSource runningLoopSource;
        private LostGirlGlassAnchor lastUsedAnchor;

        public LostGirlPhase Phase => phase;
        public int GlassStage => glassStage;
        public LostGirlGlassAnchor CurrentAnchor => currentAnchor;

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) return;
            ai = settings.GetInt("lostGirl.ai");
        }

        private void Awake() {
            if (movement == null) movement = GetComponent<LostGirlMovement>();
            if (activeVisualRoot != null) activeVisualRoot.SetActive(false);
            movement?.StopMovement();
            StopRunningLoop();
        }

        private void OnEnable() {
            TrySubscribeScheduler();

            if (movement != null) {
                movement.OnMovementFailed += HandleMovementFailed;
                movement.OnReachedOpenDoor += HandleReachedOpenDoor;
                movement.verboseLogging = verboseLogging;
            }
        }

        private void OnDisable() {
            TryUnsubscribeScheduler();

            if (movement != null) {
                movement.OnMovementFailed -= HandleMovementFailed;
                movement.OnReachedOpenDoor -= HandleReachedOpenDoor;
            }

            StopRunningLoop();
        }

        private void OnDestroy() {
            TryUnsubscribeScheduler();

            if (movement != null) {
                movement.OnMovementFailed -= HandleMovementFailed;
                movement.OnReachedOpenDoor -= HandleReachedOpenDoor;
            }
        }

        private void Start() {
            EnsureAudioSource();
            ResetToPreSpawn();
            if (ai <= 0) EnterAIDisabledState();
        }

        private void Update() {
            if (phase == LostGirlPhase.Jumpscare) return;
            if (loseState != null && loseState.hasLost) return;
            if (HandleAIDisabledState()) return;

            currentPlayerRegion = GetCurrentPlayerRegion();

            if (phase == LostGirlPhase.InGlass || phase == LostGirlPhase.Queued) {
                UpdateObservation();
            }

            switch (phase) {
                case LostGirlPhase.Queued:
                    if (!IsObserved() && IsCurrentAnchorReachable()) {
                        ActivateLostGirl();
                    }
                    break;

                case LostGirlPhase.Emerging:
                    emergeTimer += Time.deltaTime;
                    if (emergeTimer >= emergeLingerSeconds) {
                        StartChasing();
                    }
                    break;

                case LostGirlPhase.Chasing:
                    UpdateChasing();
                    break;
            }
        }

        private void HandleOpportunityTick(int tick) {
            if (aiDisabled || ai <= 0) return;
            if (loseState != null && loseState.hasLost) return;

            if (phase == LostGirlPhase.PreSpawn) {
                if (tick % Mathf.Max(1, preSpawnOpportunityTicks) != 0) return;
                if (Random.Range(0, 21) <= ai) {
                    SpawnIntoRandomGlass();
                }
                return;
            }

            if (phase == LostGirlPhase.InGlass) {
                if (tick % Mathf.Max(1, glassProgressOpportunityTicks) != 0) return;
                if (IsObserved()) return;

                if (glassStage < maxGlassStage) {
                    glassStage++;
                    reachedMaxStageThisCycle = glassStage >= maxGlassStage;
                    ShowCurrentGlassStage();
                    PlayOneShot(stageProgressClip);
                    return;
                }

                if (reachedMaxStageThisCycle) {
                    reachedMaxStageThisCycle = false;
                    phase = LostGirlPhase.Queued;
                    observedHoldTimer = 0f;
                }
            }
        }

        private void SpawnIntoRandomGlass() {
            LostGirlGlassAnchor next = GetRandomAnchorAvoidingLast();
            if (next == null) return;

            phase = LostGirlPhase.InGlass;
            currentAnchor = next;
            glassStage = 0;
            observedHoldTimer = 0f;
            emergeTimer = 0f;
            activeTimer = 0f;
            reachedMaxStageThisCycle = false;

            movement?.StopMovement();
            if (activeVisualRoot != null) activeVisualRoot.SetActive(false);

            HideAllAnchorStages();
            ShowCurrentGlassStage();
            StopRunningLoop();
            PlayOneShot(spawnInClip);
        }

        private void ActivateLostGirl() {
            if (currentAnchor == null || currentAnchor.activeSpawnPoint == null) {
                ResetToPreSpawn();
                return;
            }

            bool warped = movement != null && movement.WarpToSpawnPoint(currentAnchor.activeSpawnPoint);
            if (!warped) {
                ResetToPreSpawn();
                return;
            }

            phase = LostGirlPhase.Emerging;
            emergeTimer = 0f;
            activeTimer = 0f;
            observedHoldTimer = 0f;

            currentAnchor.HideAllStages();
            if (activeVisualRoot != null) activeVisualRoot.SetActive(true);

            movement?.StopMovement();
            StopRunningLoop();
            PlayOneShot(activationClip);
        }

        private void StartChasing() {
            phase = LostGirlPhase.Chasing;
            activeTimer = 0f;

            if (movement != null) {
                movement.BeginMovement(playerTarget, movement.moveMode);
            }

            PlayOneShot(runStartClip);
            StartRunningLoop();
        }

        private void UpdateChasing() {
            activeTimer += Time.deltaTime;

            if (CheckOpenDoorAutoKill()) return;
            if (CheckPlayerKill()) return;

            if (activeTimer >= activeChaseDurationSeconds) {
                ResetToPreSpawn();
            }
        }

        private bool CheckOpenDoorAutoKill() {
            if (!instantKillIfAnyDoorOpen) return false;

            for (int i = 0; i < trackedDoors.Count; i++) {
                Door door = trackedDoors[i];
                if (door != null && door.isOpen) {
                    TriggerJumpscare("The Lost Girl reached the open door.");
                    return true;
                }
            }

            return false;
        }

        private bool CheckPlayerKill() {
            if (playerTarget == null) return false;

            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = playerTarget.position; b.y = 0f;

            if (Vector3.Distance(a, b) > killDistance) return false;
            if (requireLineOfSightForKill && !HasLineOfSight(GetKillOrigin(), GetKillTarget())) return false;

            TriggerJumpscare("The Lost Girl caught the player.");
            return true;
        }

        private void TriggerJumpscare(string reason) {
            if (phase == LostGirlPhase.Jumpscare) return;

            phase = LostGirlPhase.Jumpscare;
            movement?.StopMovement();
            StopRunningLoop();

            if (jumpscareController != null) {
                jumpscareController.PlayJumpscare(reason);
            }
            else {
                loseState?.TriggerLose(reason);
            }
        }

        private void UpdateObservation() {
            if (Time.time < nextObservationCheckTime) return;
            nextObservationCheckTime = Time.time + Mathf.Max(0.02f, observationCheckInterval);

            if (!IsObserved()) {
                observedHoldTimer = 0f;
                return;
            }

            if (phase != LostGirlPhase.InGlass) return;

            observedHoldTimer += observationCheckInterval;
            if (observedHoldTimer < observeHoldSecondsToPushback) return;

            observedHoldTimer = 0f;
            if (glassStage > 0) {
                glassStage--;
                reachedMaxStageThisCycle = false;
                ShowCurrentGlassStage();
            }
        }

        private bool IsObserved() {
            return IsObservedByCamera() || IsObservedInPerson();
        }

        private bool IsObservedByCamera() {
            if (attentionState == null) return false;
            if (!attentionState.isMonitorInUse) return false;
            if (!attentionState.isCameraActive) return false;
            if (currentAnchor == null) return false;
            if (currentAnchor.cameraNode == null) return false;

            return attentionState.activeCameraNode == currentAnchor.cameraNode;
        }

        private bool IsObservedInPerson() {
            if (playerViewOrigin == null) return false;

            Transform target = GetObservationTarget();
            if (target == null) return false;

            Vector3 toTarget = target.position - playerViewOrigin.position;
            float dist = toTarget.magnitude;
            if (dist > Mathf.Max(0.01f, directObserveMaxDistance)) return false;
            if (dist > 0.001f && Vector3.Angle(playerViewOrigin.forward, toTarget) > directObserveAngleDegrees) return false;

            return HasLineOfSight(playerViewOrigin.position, target.position);
        }

        private bool HasLineOfSight(Vector3 from, Vector3 to) {
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len <= 0.001f) return true;

            if (!Physics.Raycast(from, dir.normalized, out RaycastHit hit, len, observationBlockers, QueryTriggerInteraction.Ignore)) {
                return true;
            }

            Transform target = GetObservationTarget();
            return target != null && (hit.transform == target || hit.transform.IsChildOf(target));
        }

        private Transform GetObservationTarget() {
            if (currentAnchor == null) return null;
            return currentAnchor.flashlightTarget != null ? currentAnchor.flashlightTarget : currentAnchor.transform;
        }

        private bool IsCurrentAnchorReachable() {
            if (!requireReachableRegionToSpawn) return true;
            if (currentAnchor == null) return false;

            LostGirlSpawnGate gate = currentAnchor.GetComponentInParent<LostGirlSpawnGate>();
            return gate != null && gate.Allows(currentPlayerRegion);
        }

        private LostGirlRegionId GetCurrentPlayerRegion() {
            Vector3 probePos =
                playerRegionProbe != null ? playerRegionProbe.position :
                playerTarget != null ? playerTarget.position :
                Vector3.positiveInfinity;

            if (!float.IsFinite(probePos.x)) return LostGirlRegionId.None;

            for (int i = 0; i < playerRegionVolumes.Count; i++) {
                LostGirlRegionVolume v = playerRegionVolumes[i];
                if (v != null && v.Contains(probePos)) return v.Region;
            }

            return LostGirlRegionId.None;
        }

        private LostGirlGlassAnchor GetRandomAnchorAvoidingLast() {
            if (anchors == null || anchors.Count == 0) return null;

            List<LostGirlGlassAnchor> valid = new();
            for (int i = 0; i < anchors.Count; i++) {
                if (anchors[i] != null && anchors[i] != lastUsedAnchor) {
                    valid.Add(anchors[i]);
                }
            }

            if (valid.Count == 0) {
                for (int i = 0; i < anchors.Count; i++) {
                    if (anchors[i] != null) valid.Add(anchors[i]);
                }
            }

            return valid.Count == 0 ? null : valid[Random.Range(0, valid.Count)];
        }

        private void HandleMovementFailed(LostGirlMovement.FailureReason reason, Door door) {
            ResetToPreSpawn();
        }

        private void HandleReachedOpenDoor(Door door) {
            TriggerJumpscare("The Lost Girl reached the open door.");
        }

        public void ResetToPreSpawn() {
            if (currentAnchor != null) {
                lastUsedAnchor = currentAnchor;
            }

            phase = LostGirlPhase.PreSpawn;
            glassStage = 0;
            currentAnchor = null;
            currentPlayerRegion = LostGirlRegionId.None;
            observedHoldTimer = 0f;
            emergeTimer = 0f;
            activeTimer = 0f;
            reachedMaxStageThisCycle = false;

            movement?.StopMovement();

            if (activeVisualRoot != null) activeVisualRoot.SetActive(false);

            HideAllAnchorStages();
            StopRunningLoop();
        }

        private bool HandleAIDisabledState() {
            if (ai <= 0) {
                if (!aiDisabled) EnterAIDisabledState();
                return true;
            }

            if (aiDisabled) ExitAIDisabledState();
            return false;
        }

        private void EnterAIDisabledState() {
            aiDisabled = true;
            ResetToPreSpawn();
        }

        private void ExitAIDisabledState() {
            aiDisabled = false;
            ResetToPreSpawn();
        }

        private void TrySubscribeScheduler() {
            if (subscribedToScheduler || GlobalAIScheduler.Instance == null) return;
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

        private void ShowCurrentGlassStage() {
            if (currentAnchor != null) currentAnchor.ShowStage(glassStage);
        }

        private void HideAllAnchorStages() {
            if (anchors == null) return;
            for (int i = 0; i < anchors.Count; i++) {
                if (anchors[i] != null) anchors[i].HideAllStages();
            }
        }

        private void EnsureAudioSource() {
            if (audioSource != null) return;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        private void PlayOneShot(AudioClip clip) {
            if (audioSource == null || clip == null) return;
            audioSource.PlayOneShot(clip, sfxVolume);
        }

        private void StartRunningLoop() {
            if (runningLoopClip == null) return;

            if (runningLoopSource == null) {
                runningLoopSource = gameObject.AddComponent<AudioSource>();
                runningLoopSource.playOnAwake = false;
                runningLoopSource.loop = true;
                runningLoopSource.spatialBlend = 0f;
            }

            runningLoopSource.clip = runningLoopClip;
            runningLoopSource.volume = runningLoopVolume;

            if (!runningLoopSource.isPlaying) {
                runningLoopSource.Play();
            }
        }

        private void StopRunningLoop() {
            if (runningLoopSource != null && runningLoopSource.isPlaying) {
                runningLoopSource.Stop();
            }
        }

        private Vector3 GetKillOrigin() {
            return (activeVisualRoot != null ? activeVisualRoot.transform.position : transform.position) + Vector3.up * 1.2f;
        }

        private Vector3 GetKillTarget() {
            return playerTarget != null ? playerTarget.position + Vector3.up * 1.2f : transform.position;
        }
    }
}