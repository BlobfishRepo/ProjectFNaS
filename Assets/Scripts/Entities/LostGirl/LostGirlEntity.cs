using System.Collections.Generic;
using UnityEngine;
using FNaS.Systems;

namespace FNaS.Entities.LostGirl {
    public class LostGirlEntity : MonoBehaviour {
        public enum LostGirlPhase {
            PreSpawn,
            InGlass,
            Emerging,
            Chasing
        }

        [Header("Core AI")]
        [Range(0, 20)] public int ai = 10;

        [Header("Scheduler")]
        [Min(1)] public int preSpawnOpportunityTicks = 2;
        [Min(1)] public int glassProgressOpportunityTicks = 2;

        [Header("Glass Stages")]
        [Tooltip("Visible glass stages are 0..maxGlassStage.")]
        public int maxGlassStage = 3;

        [Header("Flashlight")]
        public float flashlightHoldSecondsToPushback = 2f;

        [Header("Emerging")]
        public float emergeLingerSeconds = 2f;

        [Header("Chase")]
        public LostGirlMovement.ActiveMoveMode activeMoveMode = LostGirlMovement.ActiveMoveMode.ChaseCurrentPlayer;
        public float activeChaseDurationSeconds = 5f;
        public float killDistance = 1.25f;

        [Header("Audio")]
        public AudioSource audioSource;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        public AudioClip spawnInClip;
        public AudioClip stageProgressClip;
        public AudioClip activationClip;
        public AudioClip runStartClip;
        public AudioClip runningLoopClip;
        [Range(0f, 1f)] public float runningLoopVolume = 0.9f;

        [Header("References")]
        public Transform playerTarget;
        public FlashlightTool flashlight;
        public LoseState loseState;
        public LostGirlMovement movement;
        public GameObject activeVisualRoot;

        [Header("Glass Anchors")]
        public List<LostGirlGlassAnchor> anchors = new();

        [Header("Runtime (read-only)")]
        [SerializeField] private LostGirlPhase phase = LostGirlPhase.PreSpawn;
        [SerializeField] private int glassStage;
        [SerializeField] private LostGirlGlassAnchor currentAnchor;

        private bool subscribedToScheduler;
        private bool flashlightTouchedSinceLastEligibleTick;
        private bool aiDisabled;

        private float flashlightHoldTimer;
        private float emergeTimer;
        private float activeTimer;

        private AudioSource runningLoopSource;

        public LostGirlPhase Phase => phase;
        public int GlassStage => glassStage;
        public LostGirlGlassAnchor CurrentAnchor => currentAnchor;

        private void Awake() {
            if (movement == null) movement = GetComponent<LostGirlMovement>();

            if (activeVisualRoot != null) {
                activeVisualRoot.SetActive(false);
            }

            movement?.StopMovement();
            HideAllAnchorStages();
            StopRunningLoop();
        }

        private void OnEnable() {
            TrySubscribeScheduler();
        }

        private void Start() {
            EnsureAudioSource();
            TrySubscribeScheduler();
            ResetToPreSpawn();

            if (ai <= 0) {
                EnterAIDisabledState();
            }
        }

        private void Update() {
            if (HandleAIDisabledState()) return;
            if (loseState != null && loseState.hasLost) return;

            if (phase == LostGirlPhase.InGlass) {
                TrackGlassFlashlightContact();
                return;
            }

            if (phase == LostGirlPhase.Emerging) {
                UpdateEmerging();
                return;
            }

            if (phase == LostGirlPhase.Chasing) {
                UpdateChasing();
            }
        }

        private void OnDisable() {
            TryUnsubscribeScheduler();
            StopRunningLoop();
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
            ResetToPreSpawn();
        }

        private void ExitAIDisabledState() {
            aiDisabled = false;
            ResetToPreSpawn();
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

        private void EnsureAudioSource() {
            if (audioSource == null) {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        private void EnsureRunningLoopSource() {
            if (runningLoopSource != null) return;

            runningLoopSource = gameObject.AddComponent<AudioSource>();
            runningLoopSource.playOnAwake = false;
            runningLoopSource.loop = true;
            runningLoopSource.spatialBlend = 0f;
            runningLoopSource.volume = runningLoopVolume;
        }

        private void HandleOpportunityTick(int tick) {
            if (aiDisabled || ai <= 0) return;
            if (loseState != null && loseState.hasLost) return;

            switch (phase) {
                case LostGirlPhase.PreSpawn:
                    HandlePreSpawnOpportunity(tick);
                    break;

                case LostGirlPhase.InGlass:
                    HandleGlassOpportunity(tick);
                    break;
            }
        }

        private void HandlePreSpawnOpportunity(int tick) {
            if (preSpawnOpportunityTicks < 1) preSpawnOpportunityTicks = 1;
            if (tick % preSpawnOpportunityTicks != 0) return;

            int roll = Random.Range(0, 21);
            if (roll > ai) return;

            SpawnIntoRandomGlass();
        }

        private void HandleGlassOpportunity(int tick) {
            if (glassProgressOpportunityTicks < 1) glassProgressOpportunityTicks = 1;
            if (tick % glassProgressOpportunityTicks != 0) return;

            if (flashlightTouchedSinceLastEligibleTick) {
                flashlightTouchedSinceLastEligibleTick = false;
                return;
            }

            if (glassStage < maxGlassStage) {
                glassStage++;
                ShowCurrentGlassStage();
                PlayOneShot(stageProgressClip);
                return;
            }

            ActivateLostGirl();
        }

        private void TrackGlassFlashlightContact() {
            if (flashlight == null || !flashlight.isOn) {
                flashlightHoldTimer = 0f;
                return;
            }

            if (currentAnchor == null || currentAnchor.flashlightTarget == null) {
                flashlightHoldTimer = 0f;
                return;
            }

            bool affecting = flashlight.IsIlluminating(currentAnchor.flashlightTarget);

            if (!affecting) {
                flashlightHoldTimer = 0f;
                return;
            }

            // Shared stall rule
            flashlightTouchedSinceLastEligibleTick = true;

            // Restored pushback rule
            flashlightHoldTimer += Time.deltaTime;
            if (flashlightHoldTimer >= flashlightHoldSecondsToPushback) {
                flashlightHoldTimer = 0f;
                RevertOneGlassStage();
            }
        }

        private void RevertOneGlassStage() {
            int oldStage = glassStage;
            glassStage = Mathf.Max(0, glassStage - 1);

            if (glassStage != oldStage) {
                ShowCurrentGlassStage();
            }

            // Keep the next eligible progression stalled too.
            flashlightTouchedSinceLastEligibleTick = true;
        }

        private void SpawnIntoRandomGlass() {
            LostGirlGlassAnchor next = GetRandomAnchorExcluding(null);
            if (next == null) return;

            phase = LostGirlPhase.InGlass;
            currentAnchor = next;
            glassStage = 0;
            flashlightTouchedSinceLastEligibleTick = false;
            flashlightHoldTimer = 0f;
            emergeTimer = 0f;
            activeTimer = 0f;

            movement?.StopMovement();

            if (activeVisualRoot != null) {
                activeVisualRoot.SetActive(false);
            }

            StopRunningLoop();
            HideAllAnchorStages();
            ShowCurrentGlassStage();
            PlayOneShot(spawnInClip);
        }

        private void ActivateLostGirl() {
            phase = LostGirlPhase.Emerging;
            flashlightTouchedSinceLastEligibleTick = false;
            flashlightHoldTimer = 0f;
            emergeTimer = 0f;
            activeTimer = 0f;

            currentAnchor?.HideAllStages();

            Transform spawnPoint = null;
            if (currentAnchor != null) {
                spawnPoint = currentAnchor.activeSpawnPoint != null
                    ? currentAnchor.activeSpawnPoint
                    : currentAnchor.transform;
            }

            if (spawnPoint != null) {
                transform.position = spawnPoint.position;
                transform.rotation = spawnPoint.rotation;
            }

            if (activeVisualRoot != null) {
                activeVisualRoot.SetActive(true);
            }

            movement?.StopMovement();
            StopRunningLoop();
            PlayOneShot(activationClip);
        }

        private void UpdateEmerging() {
            emergeTimer += Time.deltaTime;
            if (emergeTimer < emergeLingerSeconds) return;

            StartChasing();
        }

        private void StartChasing() {
            phase = LostGirlPhase.Chasing;
            activeTimer = 0f;

            movement?.BeginMovement(playerTarget, activeMoveMode);

            PlayOneShot(runStartClip);
            StartRunningLoop();
        }

        private void UpdateChasing() {
            activeTimer += Time.deltaTime;

            if (CheckPlayerKill()) {
                return;
            }

            if (activeTimer >= activeChaseDurationSeconds) {
                OnMissedPlayer();
            }
        }

        private bool CheckPlayerKill() {
            if (playerTarget == null) return false;

            Vector3 a = transform.position;
            Vector3 b = playerTarget.position;
            a.y = 0f;
            b.y = 0f;

            float dist = Vector3.Distance(a, b);
            if (dist > killDistance) return false;

            loseState?.TriggerLose("The Lost Girl caught the player.");
            return true;
        }

        private void OnMissedPlayer() {
            ResetToPreSpawn();
        }

        public void ResetToPreSpawn() {
            phase = LostGirlPhase.PreSpawn;
            glassStage = 0;
            currentAnchor = null;
            flashlightTouchedSinceLastEligibleTick = false;
            flashlightHoldTimer = 0f;
            emergeTimer = 0f;
            activeTimer = 0f;

            movement?.StopMovement();

            if (activeVisualRoot != null) {
                activeVisualRoot.SetActive(false);
            }

            HideAllAnchorStages();
            StopRunningLoop();
        }

        private void ShowCurrentGlassStage() {
            if (currentAnchor != null) {
                currentAnchor.ShowStage(glassStage);
            }
        }

        private void HideAllAnchorStages() {
            if (anchors == null) return;

            for (int i = 0; i < anchors.Count; i++) {
                if (anchors[i] != null) {
                    anchors[i].HideAllStages();
                }
            }
        }

        private LostGirlGlassAnchor GetRandomAnchorExcluding(LostGirlGlassAnchor exclude) {
            if (anchors == null || anchors.Count == 0) return null;

            List<LostGirlGlassAnchor> valid = new();
            for (int i = 0; i < anchors.Count; i++) {
                var a = anchors[i];
                if (a == null) continue;
                if (a == exclude) continue;
                valid.Add(a);
            }

            if (valid.Count == 0) {
                return exclude;
            }

            return valid[Random.Range(0, valid.Count)];
        }

        private void PlayOneShot(AudioClip clip) {
            if (audioSource == null || clip == null) return;
            audioSource.PlayOneShot(clip, sfxVolume);
        }

        private void StartRunningLoop() {
            EnsureRunningLoopSource();

            if (runningLoopClip == null) {
                StopRunningLoop();
                return;
            }

            if (runningLoopSource.clip != runningLoopClip) {
                runningLoopSource.clip = runningLoopClip;
            }

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
    }
}