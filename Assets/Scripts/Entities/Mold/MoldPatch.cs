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

            ApplyGameplayEffects();
            ApplyVisualsImmediate();
        }

        public void SetSpreadState(MoldSpreadState newState) {
            spreadState = newState;

            switch (spreadState) {
                case MoldSpreadState.Clean:
                    visualFill = 0f;
                    break;
                case MoldSpreadState.Active:
                case MoldSpreadState.Isolated:
                    visualFill = 1f;
                    break;
            }

            ApplyGameplayEffects();
            ApplyVisualsImmediate();
        }

        public void SetCorruptionPhase(MoldCorruptionPhase newPhase) {
            corruptionPhase = newPhase;

            ApplyGameplayEffects();
            ApplyVisualsImmediate();

            if (corruptionPhase == MoldCorruptionPhase.Blood && spreadState != MoldSpreadState.Clean) {
                EnsureBloodDrips();
            }
            else {
                DisableBloodDrips();
            }
        }

        public void SetMarkedFill(float fill01) {
            visualFill = Mathf.Clamp01(fill01);
            ApplyVisualsImmediate();
        }

        public void ForceClean() {
            spreadState = MoldSpreadState.Clean;
            corruptionPhase = MoldCorruptionPhase.Normal;
            visualFill = 0f;

            ApplyGameplayEffects();
            ApplyVisualsImmediate();
            DisableBloodDrips();
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
                bool show =
                    spreadState == MoldSpreadState.Marked ||
                    spreadState == MoldSpreadState.Active ||
                    spreadState == MoldSpreadState.Isolated;

                cameraObstruction.SetActive(show);
            }

            if (floorSlowZone != null) {
                bool active =
                    spreadState == MoldSpreadState.Marked ||
                    spreadState == MoldSpreadState.Active ||
                    spreadState == MoldSpreadState.Isolated;

                floorSlowZone.enabled = active;
            }
        }

        private void ApplyVisualsImmediate() {
            if (targetRenderers == null) return;

            if (mpb == null) {
                mpb = new MaterialPropertyBlock();
            }

            foreach (var rend in targetRenderers) {
                if (rend == null) continue;

                rend.GetPropertyBlock(mpb);

                if (!string.IsNullOrWhiteSpace(fillProperty)) {
                    mpb.SetFloat(fillProperty, visualFill);
                }

                if (!string.IsNullOrWhiteSpace(bloodBlendProperty)) {
                    float bloodBlend =
                        (corruptionPhase == MoldCorruptionPhase.Blood && spreadState != MoldSpreadState.Clean) ? 1f : 0f;
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

                    if (corruptionPhase == MoldCorruptionPhase.Blood && spreadState != MoldSpreadState.Clean) {
                        c = Color.Lerp(c, bloodTint, 0.85f);
                    }

                    mpb.SetColor("_BaseColor", c);
                    mpb.SetColor("_Color", c);
                }

                rend.SetPropertyBlock(mpb);
            }
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