using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FNaS.Systems;
using FNaS.Gameplay;
using FNaS.MasterNodes;
using FNaS.Settings;

namespace FNaS.Entities.Mimic {
    public class MimicEntity : MonoBehaviour, IRuntimeSettingsConsumer {

        public enum Phase {
            Dormant,
            Active,
            Cooldown,
            Punishing
        }

        [System.Serializable]
        public class SpawnChoice {
            public Transform anchor;
            public GameObject variant;
        }

        [Header("AI")]
        [Range(0, 20)] public int ai = 10;

        [Header("Spawn")]
        [Tooltip("How many global AI ticks pass before the Mimic attempts to spawn.")]
        public int spawnEveryTicks = 2;

        [Tooltip("Cooldown after banish or punish.")]
        public float cooldownSeconds = 10f;

        [Tooltip("If true, the Mimic begins the game in Cooldown instead of Dormant.")]
        public bool startInCooldown = true;

        [Header("Danger Timer")]
        [Tooltip("Danger time at AI 20.")]
        public float minDangerSeconds = 15f;

        [Tooltip("Danger time at AI 1.")]
        public float maxDangerSeconds = 30f;

        [Header("Flashlight")]
        [Tooltip("How long the flashlight must stay on the Mimic to banish it.")]
        public float holdToBanishSeconds = 1.5f;

        [Header("Punish")]
        [Range(0f, 1f)] public float batteryDrainPercent = 0.10f;
        public int punishPulseCount = 4;
        public float punishPulseSpacing = 0.25f;
        public float punishVisibleSeconds = 4.35f;

        [Header("References")]
        public PlayerWaypointController playerMovement;
        public FlashlightTool flashlight;
        public ScreenFader screenFader;

        [Tooltip("Player child transform used to place the Mimic for the punish scare.")]
        public Transform punishAnchor;

        [Header("Variants")]
        [Tooltip("All child Mimic models. Only one is enabled at a time.")]
        public GameObject[] allMimicVariants;

        [Header("Jumpscare Render")]
        [Tooltip("Usually this Mimic object, or a child visual root if you have one.")]
        public GameObject mimicVisualRoot;

        [Tooltip("Separate jumpscare camera, same idea as Stalker.")]
        public Camera jumpscareCamera;

        [Tooltip("Layer used by the JumpscareCamera.")]
        public string jumpscareLayerName = "Jumpscare";

        [Header("Audio")]
        [Tooltip("Use this for laughs / spawn / banish / punish one-shots.")]
        public AudioSource audioSource;

        public AudioClip spawnClip;
        public AudioClip laughClip;
        public AudioClip banishClip;
        public AudioClip punishClip;

        [Range(0f, 1f)] public float spawnVolume = 1f;
        [Range(0f, 1f)] public float laughVolume = 1f;
        [Range(0f, 1f)] public float banishVolume = 1f;
        [Range(0f, 1f)] public float punishVolume = 1f;

        [Header("Debug")]
        public bool verboseLogging = false;

        [Header("Runtime (read-only)")]
        [SerializeField] private Phase phase = Phase.Dormant;
        [SerializeField] private int dormantTickCounter;
        [SerializeField] private float dangerTimer;
        [SerializeField] private float dangerSeconds;
        [SerializeField] private float banishTimer;
        [SerializeField] private float cooldownTimer;
        [SerializeField] private float laughTimer;
        [SerializeField] private Transform currentAnchor;
        [SerializeField] private GameObject currentVariant;

        private GlobalAIScheduler scheduler;
        private EntityAnchorSet[] anchorSets;
        private Coroutine punishCoroutine;
        private bool subscribed;

        private Vector3 startPosition;
        private Quaternion startRotation;

        private int jumpscareLayer = -1;
        private int originalLayer = -1;

        public Phase CurrentPhase => phase;
        public bool IsActive => phase == Phase.Active;

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) return;
            ai = settings.GetInt("mimic.ai");
        }

        private void Awake() {
            startPosition = transform.position;
            startRotation = transform.rotation;

            anchorSets = FindObjectsOfType<EntityAnchorSet>(true);
            HideAllVariants();

            jumpscareLayer = LayerMask.NameToLayer(jumpscareLayerName);

            if (mimicVisualRoot == null) {
                mimicVisualRoot = gameObject;
            }

            if (mimicVisualRoot != null) {
                originalLayer = mimicVisualRoot.layer;
            }

            if (jumpscareCamera != null) {
                jumpscareCamera.enabled = false;
            }
        }

        private void OnEnable() {
            TrySubscribeToScheduler();
        }

        private void Start() {
            ResetInitialState();
            TrySubscribeToScheduler();
        }

        private void OnDisable() {
            if (scheduler != null) {
                scheduler.OnOpportunityTick -= OnOpportunityTick;
            }

            subscribed = false;

            if (jumpscareCamera != null) {
                jumpscareCamera.enabled = false;
            }
        }

        private void Update() {
            if (!subscribed) {
                TrySubscribeToScheduler();
            }

            switch (phase) {
                case Phase.Active:
                    UpdateActive();
                    break;

                case Phase.Cooldown:
                    UpdateCooldown();
                    break;
            }
        }

        private void TrySubscribeToScheduler() {
            if (subscribed) return;

            if (scheduler == null) {
                scheduler = GlobalAIScheduler.Instance;
            }

            if (scheduler == null) return;

            scheduler.OnOpportunityTick -= OnOpportunityTick;
            scheduler.OnOpportunityTick += OnOpportunityTick;
            subscribed = true;

            if (verboseLogging) {
                Debug.Log("Mimic subscribed to GlobalAIScheduler.", this);
            }
        }

        private void OnOpportunityTick(int tick) {
            if (ai <= 0) return;
            if (phase != Phase.Dormant) return;

            dormantTickCounter++;

            if (verboseLogging) {
                Debug.Log($"Mimic tick {tick}, dormant counter = {dormantTickCounter}", this);
            }

            if (dormantTickCounter >= Mathf.Max(1, spawnEveryTicks)) {
                if (TrySpawn()) {
                    dormantTickCounter = 0;
                }
            }
        }

        private bool TrySpawn() {
            SpawnChoice choice = GetRandomValidSpawnChoice();
            if (choice == null || choice.anchor == null || choice.variant == null) {
                if (verboseLogging) {
                    Debug.LogWarning("Mimic spawn failed: no valid anchor/variant found.", this);
                }
                return false;
            }

            transform.position = choice.anchor.position;
            transform.rotation = choice.anchor.rotation;

            HideAllVariants();
            choice.variant.SetActive(true);

            currentAnchor = choice.anchor;
            currentVariant = choice.variant;

            banishTimer = 0f;
            dangerTimer = 0f;
            dangerSeconds = GetDangerSeconds();
            laughTimer = GetNextLaughInterval();
            phase = Phase.Active;

            PlayOneShot(spawnClip, spawnVolume);

            if (verboseLogging) {
                Debug.Log($"Mimic spawned at {choice.anchor.name} using {choice.variant.name}", this);
            }

            return true;
        }

        private void UpdateActive() {
            bool illuminated =
                flashlight != null &&
                currentVariant != null &&
                currentVariant.activeInHierarchy &&
                flashlight.IsIlluminating(currentVariant.transform);

            if (illuminated) {
                banishTimer += Time.deltaTime;

                if (banishTimer >= Mathf.Max(0.01f, holdToBanishSeconds)) {
                    Banish();
                    return;
                }
            }
            else {
                banishTimer = 0f;
                dangerTimer += Time.deltaTime;

                laughTimer -= Time.deltaTime;
                if (laughTimer <= 0f) {
                    PlayOneShot(laughClip, laughVolume);
                    laughTimer = GetNextLaughInterval();
                }

                if (dangerTimer >= dangerSeconds) {
                    TriggerPunish();
                }
            }
        }

        private void Banish() {
            PlayOneShot(banishClip, banishVolume);
            HideAllVariants();

            currentAnchor = null;
            currentVariant = null;
            banishTimer = 0f;
            dangerTimer = 0f;
            laughTimer = 0f;

            phase = Phase.Cooldown;
            cooldownTimer = Mathf.Max(0f, cooldownSeconds);

            transform.position = startPosition;
            transform.rotation = startRotation;

            if (verboseLogging) {
                Debug.Log("Mimic banished.", this);
            }
        }

        private void TriggerPunish() {
            if (phase == Phase.Punishing) return;
            if (punishCoroutine != null) return;

            punishCoroutine = StartCoroutine(PunishRoutine());
        }

        private IEnumerator PunishRoutine() {
            phase = Phase.Punishing;

            if (punishAnchor != null) {
                transform.position = punishAnchor.position;
                transform.rotation = punishAnchor.rotation;
            }

            if (currentVariant == null) {
                foreach (var go in allMimicVariants) {
                    if (go != null) {
                        currentVariant = go;
                        break;
                    }
                }
            }

            HideAllVariants();
            if (currentVariant != null) {
                currentVariant.SetActive(true);
            }

            if (mimicVisualRoot != null && jumpscareLayer != -1) {
                SetLayerRecursively(mimicVisualRoot, jumpscareLayer);
            }

            if (jumpscareCamera != null) {
                jumpscareCamera.enabled = true;
            }

            PlayOneShot(punishClip, punishVolume);

            if (flashlight != null) {
                flashlight.DrainPercent(batteryDrainPercent);
            }

            float visible = Mathf.Max(0.01f, punishVisibleSeconds);
            float spacing = Mathf.Max(0.01f, punishPulseSpacing);
            int pulses = Mathf.Max(1, punishPulseCount);

            float elapsed = 0f;
            int pulsesPlayed = 0;

            while (elapsed < visible) {
                if (screenFader != null && pulsesPlayed < pulses) {
                    screenFader.Pulse();
                    pulsesPlayed++;
                }

                yield return new WaitForSeconds(spacing);
                elapsed += spacing;
            }

            if (mimicVisualRoot != null && originalLayer != -1) {
                SetLayerRecursively(mimicVisualRoot, originalLayer);
            }

            if (jumpscareCamera != null) {
                jumpscareCamera.enabled = false;
            }

            HideAllVariants();

            currentAnchor = null;
            currentVariant = null;
            banishTimer = 0f;
            dangerTimer = 0f;
            laughTimer = 0f;

            phase = Phase.Cooldown;
            cooldownTimer = Mathf.Max(0f, cooldownSeconds);

            transform.position = startPosition;
            transform.rotation = startRotation;

            punishCoroutine = null;

            if (verboseLogging) {
                Debug.Log("Mimic punished player.", this);
            }
        }

        private void UpdateCooldown() {
            cooldownTimer -= Time.deltaTime;

            if (cooldownTimer <= 0f) {
                cooldownTimer = 0f;
                phase = Phase.Dormant;

                if (verboseLogging) {
                    Debug.Log("Mimic returned to Dormant.", this);
                }
            }
        }

        private void ResetInitialState() {
            HideAllVariants();

            dormantTickCounter = 0;
            dangerTimer = 0f;
            dangerSeconds = 0f;
            banishTimer = 0f;
            laughTimer = 0f;
            currentAnchor = null;
            currentVariant = null;

            transform.position = startPosition;
            transform.rotation = startRotation;

            if (startInCooldown) {
                phase = Phase.Cooldown;
                cooldownTimer = Mathf.Max(0f, cooldownSeconds);
            }
            else {
                phase = Phase.Dormant;
                cooldownTimer = 0f;
            }
        }

        private SpawnChoice GetRandomValidSpawnChoice() {
            if (anchorSets == null || anchorSets.Length == 0) return null;

            MasterNode playerNode = playerMovement != null ? playerMovement.CurrentMasterNode : null;
            List<SpawnChoice> valid = new List<SpawnChoice>();

            foreach (var set in anchorSets) {
                if (set == null) continue;

                MasterNode node = set.GetComponent<MasterNode>();
                if (playerNode != null && node == playerNode) {
                    continue;
                }

                if (set.mimicAnchors == null) continue;

                foreach (var slot in set.mimicAnchors) {
                    if (slot == null || slot.anchor == null || slot.mimicVariant == null) continue;

                    valid.Add(new SpawnChoice {
                        anchor = slot.anchor,
                        variant = slot.mimicVariant
                    });
                }
            }

            if (verboseLogging) {
                Debug.Log($"Mimic found {valid.Count} valid spawn choices.", this);
            }

            if (valid.Count == 0) return null;
            return valid[Random.Range(0, valid.Count)];
        }

        private float GetDangerSeconds() {
            if (ai <= 0) return float.PositiveInfinity;

            float t = Mathf.Clamp01((ai - 1f) / 19f);
            return Mathf.Lerp(maxDangerSeconds, minDangerSeconds, t);
        }

        private float GetNextLaughInterval() {
            float remaining = dangerSeconds - dangerTimer;

            if (dangerTimer < dangerSeconds * 0.5f) {
                return Random.Range(8f, 12f);
            }

            if (remaining > 10f) {
                return Random.Range(4f, 6f);
            }

            if (remaining > 5f) {
                return Random.Range(2f, 3f);
            }

            return Random.Range(0.8f, 1.5f);
        }

        private void HideAllVariants() {
            if (allMimicVariants == null) return;

            foreach (var go in allMimicVariants) {
                if (go != null) {
                    go.SetActive(false);
                }
            }
        }

        private void PlayOneShot(AudioClip clip, float volume) {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip, volume);
        }

        private void SetLayerRecursively(GameObject obj, int layer) {
            if (obj == null) return;

            obj.layer = layer;
            foreach (Transform child in obj.transform) {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        [ContextMenu("Refresh Anchor Cache")]
        public void RefreshAnchorCache() {
            anchorSets = FindObjectsOfType<EntityAnchorSet>(true);

            if (verboseLogging) {
                Debug.Log($"Mimic refreshed anchor cache: {anchorSets.Length} sets found.", this);
            }
        }
    }
}