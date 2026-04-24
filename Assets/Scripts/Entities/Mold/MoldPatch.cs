using System.Collections;
using UnityEngine;

namespace FNaS.Entities.Mold {
    public class MoldPatch : MonoBehaviour {
        [Header("Identity")]
        [SerializeField] private string patchId;
        public MoldSurfaceType surfaceType;

        [Header("State (read-only at runtime)")]
        [SerializeField] private MoldSpreadState spreadState = MoldSpreadState.Clean;
        [SerializeField] private MoldCorruptionPhase corruptionPhase = MoldCorruptionPhase.Normal;
        [SerializeField, Range(0f, 1f)] private float visualFill = 0f;

        [Header("Gameplay Links")]
        public Light linkedLight;
        public GameObject cameraObstruction;
        public Collider floorSlowZone;

        [Header("Visuals")]
        public Renderer[] targetRenderers;
        public Transform[] dripAnchors;
        public GameObject[] bloodDripPrefabs;

        [Header("Shader Properties")]
        public string fillProperty = "_Fill";
        public string bloodBlendProperty = "_BloodBlend";

        [Header("Audio")]
        public AudioSource oneShotSource;
        public AudioSource ambienceSource;

        [Space]
        public AudioClip spreadClip;
        public AudioClip sprayHitClip;
        public AudioClip cleanClip;
        public AudioClip bloodTransitionClip;
        public AudioClip ambienceClip;
        public AudioClip bloodAmbienceClip;

        [Space]
        [Range(0f, 1f)] public float spreadVolume = 1f;
        [Range(0f, 1f)] public float sprayHitVolume = 0.6f;
        [Range(0f, 1f)] public float cleanVolume = 0.8f;
        [Range(0f, 1f)] public float bloodTransitionVolume = 1f;
        [Range(0f, 1f)] public float ambienceVolume = 0.25f;
        [Range(0f, 1f)] public float bloodAmbienceVolume = 0.35f;

        [Min(0.01f)] public float sprayHitCooldown = 0.2f;

        [Header("Debug Colors")]
        public bool useDebugColors = true;
        public Color cleanColor = new Color(0f, 0f, 0f, 0f);
        public Color markedColor = Color.yellow;
        public Color activeColor = new Color(0.12f, 0.25f, 0.08f, 1f);
        public Color isolatedColor = new Color(0f, 0f, 0.8f, 1f);
        public Color bloodTint = new Color(0.35f, 0.03f, 0.03f, 1f);

        private MaterialPropertyBlock mpb;
        private GameObject[] spawnedDrips;

        private float baseLightIntensity;
        private Color baseLightColor;
        private bool cachedLightBase;

        private float lastSprayHitTime = -999f;

        [SerializeField, Range(0f, 1f)] private float visualSuppression01 = 0f;

        private Coroutine bloodCleanRoutine;
        private float bloodBlendOverride = -1f;

        public string PatchId => patchId;
        public MoldSpreadState SpreadState => spreadState;
        public MoldCorruptionPhase CorruptionPhase => corruptionPhase;
        public float VisualFill => visualFill;

        public bool IsMoldPresent =>
            spreadState == MoldSpreadState.Marked ||
            spreadState == MoldSpreadState.Active ||
            spreadState == MoldSpreadState.Isolated;

        public bool CanSpread => spreadState == MoldSpreadState.Active;

        private void OnValidate() {
            patchId = gameObject.name;
        }

        private void Awake() {
            patchId = gameObject.name;
            mpb = new MaterialPropertyBlock();

            if (linkedLight != null) {
                baseLightIntensity = linkedLight.intensity;
                baseLightColor = linkedLight.color;
                cachedLightBase = true;
            }

            EnsureAudioSources();

            ApplyGameplayEffects();
            ApplyVisualsImmediate();
            RefreshAmbience();
        }

        private void EnsureAudioSources() {
            if (oneShotSource == null) {
                oneShotSource = gameObject.AddComponent<AudioSource>();
                oneShotSource.playOnAwake = false;
                oneShotSource.loop = false;
                oneShotSource.spatialBlend = 1f;
                oneShotSource.rolloffMode = AudioRolloffMode.Linear;
                oneShotSource.minDistance = 0.5f;
                oneShotSource.maxDistance = 8f;
            }

            if (ambienceSource == null) {
                ambienceSource = gameObject.AddComponent<AudioSource>();
                ambienceSource.playOnAwake = false;
                ambienceSource.loop = true;
                ambienceSource.spatialBlend = 1f;
                ambienceSource.rolloffMode = AudioRolloffMode.Linear;
                ambienceSource.minDistance = 0.75f;
                ambienceSource.maxDistance = 10f;
            }
        }

        public void SetSpreadState(MoldSpreadState newState) {
            MoldSpreadState previousState = spreadState;
            spreadState = newState;

            switch (spreadState) {
                case MoldSpreadState.Clean:
                    visualFill = 0f;
                    visualSuppression01 = 0f;
                    corruptionPhase = MoldCorruptionPhase.Normal;
                    bloodBlendOverride = -1f;
                    break;

                case MoldSpreadState.Active:
                case MoldSpreadState.Isolated:
                    visualFill = 1f;
                    break;
            }

            ApplyGameplayEffects();
            ApplyVisualsImmediate();

            if (previousState == MoldSpreadState.Clean &&
                spreadState != MoldSpreadState.Clean) {
                PlayOneShot(spreadClip, spreadVolume);
            }

            RefreshAmbience();
        }

        public void SetCorruptionPhase(MoldCorruptionPhase newPhase) {
            MoldCorruptionPhase previousPhase = corruptionPhase;
            corruptionPhase = newPhase;
            bloodBlendOverride = -1f;

            ApplyGameplayEffects();
            ApplyVisualsImmediate();

            if (corruptionPhase == MoldCorruptionPhase.Blood && spreadState != MoldSpreadState.Clean) {
                EnsureBloodDrips();

                if (previousPhase != MoldCorruptionPhase.Blood) {
                    PlayOneShot(bloodTransitionClip, bloodTransitionVolume);
                }
            }
            else {
                DisableBloodDrips();
            }

            RefreshAmbience();
        }

        public void SetMarkedFill(float fill01) {
            visualFill = Mathf.Clamp01(fill01);
            ApplyVisualsImmediate();
        }

        public void ForceClean() {
            bool wasMoldPresent = IsMoldPresent;

            spreadState = MoldSpreadState.Clean;
            corruptionPhase = MoldCorruptionPhase.Normal;
            visualFill = 0f;
            visualSuppression01 = 0f;
            bloodBlendOverride = -1f;

            ApplyGameplayEffects();
            ApplyVisualsImmediate();
            DisableBloodDrips();
            RefreshAmbience();

            if (wasMoldPresent) {
                PlayOneShot(cleanClip, cleanVolume);
            }
        }

        public void SetVisualSuppression01(float value) {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(visualSuppression01, clamped)) return;

            visualSuppression01 = clamped;
            ApplyVisualsImmediate();
        }

        public void NotifySprayContact() {
            if (!IsMoldPresent) return;
            if (sprayHitClip == null) return;

            if (Time.time < lastSprayHitTime + sprayHitCooldown) {
                return;
            }

            lastSprayHitTime = Time.time;
            PlayOneShot(sprayHitClip, sprayHitVolume);
        }

        private void ApplyGameplayEffects() {
            if (linkedLight != null && cachedLightBase) {
                float intensityScale = 1f;
                Color targetColor = baseLightColor;

                switch (spreadState) {
                    case MoldSpreadState.Clean:
                        intensityScale = 1f;
                        break;
                    case MoldSpreadState.Marked:
                        intensityScale = 0.9f;
                        break;
                    case MoldSpreadState.Active:
                        intensityScale = 0.7f;
                        break;
                    case MoldSpreadState.Isolated:
                        intensityScale = 0.82f;
                        break;
                }

                if (corruptionPhase == MoldCorruptionPhase.Blood && spreadState != MoldSpreadState.Clean) {
                    intensityScale *= 0.8f;
                    targetColor = Color.Lerp(baseLightColor, new Color(1f, 0.45f, 0.45f), 0.35f);
                }

                linkedLight.intensity = baseLightIntensity * intensityScale;
                linkedLight.color = targetColor;
            }

            if (cameraObstruction != null) {
                cameraObstruction.SetActive(IsMoldPresent);
            }

            if (floorSlowZone != null) {
                floorSlowZone.enabled = IsMoldPresent;
            }
        }

        private void ApplyVisualsImmediate() {
            if (targetRenderers == null) return;

            if (mpb == null) {
                mpb = new MaterialPropertyBlock();
            }

            float shaderFill = visualFill * (1f - visualSuppression01);

            foreach (var rend in targetRenderers) {
                if (rend == null) continue;

                rend.GetPropertyBlock(mpb);

                if (!string.IsNullOrWhiteSpace(fillProperty)) {
                    mpb.SetFloat(fillProperty, shaderFill);
                }

                if (!string.IsNullOrWhiteSpace(bloodBlendProperty)) {
                    float bloodBlend =
                        bloodBlendOverride >= 0f
                            ? bloodBlendOverride
                            : (corruptionPhase == MoldCorruptionPhase.Blood && spreadState != MoldSpreadState.Clean ? 1f : 0f);

                    mpb.SetFloat(bloodBlendProperty, bloodBlend);
                }

                if (useDebugColors) {
                    Color c = cleanColor;

                    switch (spreadState) {
                        case MoldSpreadState.Clean: c = cleanColor; break;
                        case MoldSpreadState.Marked: c = markedColor; break;
                        case MoldSpreadState.Active: c = activeColor; break;
                        case MoldSpreadState.Isolated: c = isolatedColor; break;
                    }

                    float debugBloodBlend =
                        bloodBlendOverride >= 0f
                            ? bloodBlendOverride
                            : (corruptionPhase == MoldCorruptionPhase.Blood && spreadState != MoldSpreadState.Clean ? 1f : 0f);

                    if (debugBloodBlend > 0f && spreadState != MoldSpreadState.Clean) {
                        c = Color.Lerp(c, bloodTint, 0.85f * debugBloodBlend);
                    }

                    mpb.SetColor("_BaseColor", c);
                    mpb.SetColor("_Color", c);
                }

                rend.SetPropertyBlock(mpb);
            }
        }

        private void RefreshAmbience() {
            EnsureAudioSources();

            if (!IsMoldPresent) {
                if (ambienceSource.isPlaying) {
                    ambienceSource.Stop();
                }

                ambienceSource.clip = null;
                return;
            }

            AudioClip targetClip =
                (corruptionPhase == MoldCorruptionPhase.Blood && bloodAmbienceClip != null)
                ? bloodAmbienceClip
                : ambienceClip;

            if (targetClip == null) {
                if (ambienceSource.isPlaying) {
                    ambienceSource.Stop();
                }

                ambienceSource.clip = null;
                return;
            }

            float targetVolume =
                (corruptionPhase == MoldCorruptionPhase.Blood && bloodAmbienceClip != null)
                ? bloodAmbienceVolume
                : ambienceVolume;

            bool clipChanged = ambienceSource.clip != targetClip;
            ambienceSource.volume = targetVolume;

            if (clipChanged) {
                ambienceSource.Stop();
                ambienceSource.clip = targetClip;
            }

            if (!ambienceSource.isPlaying) {
                ambienceSource.Play();
            }
        }

        private void PlayOneShot(AudioClip clip, float volume) {
            if (clip == null) return;
            EnsureAudioSources();
            oneShotSource.PlayOneShot(clip, volume);
        }

        private void EnsureBloodDrips() {
            if (bloodDripPrefabs == null || bloodDripPrefabs.Length == 0) return;
            if (dripAnchors == null || dripAnchors.Length == 0) return;

            if (spawnedDrips == null || spawnedDrips.Length != dripAnchors.Length) {
                spawnedDrips = new GameObject[dripAnchors.Length];
            }

            for (int i = 0; i < dripAnchors.Length; i++) {
                Transform anchor = dripAnchors[i];
                if (anchor == null) continue;

                if (spawnedDrips[i] == null) {
                    GameObject prefab = bloodDripPrefabs[Random.Range(0, bloodDripPrefabs.Length)];
                    if (prefab == null) continue;

                    spawnedDrips[i] = Instantiate(prefab, anchor.position, anchor.rotation, anchor);
                }

                if (spawnedDrips[i] != null) {
                    spawnedDrips[i].SetActive(true);
                }
            }
        }

        private void DisableBloodDrips() {
            if (spawnedDrips == null) return;

            for (int i = 0; i < spawnedDrips.Length; i++) {
                if (spawnedDrips[i] != null) {
                    spawnedDrips[i].SetActive(false);
                }
            }
        }
    }
}