using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FNaS.Entities.Mold {
    public class MoldSprayTool : MonoBehaviour {
        [Header("References")]
        public Camera aimCamera;
        public ParticleSystem sprayParticles;
        public AudioSource sprayLoopSource;

        [Header("Spray")]
        [Min(0.1f)] public float range = 10f;
        [Min(0.05f)] public float radius = 1f;

        [Tooltip("Optional start point for the spray. Falls back to camera if null.")]
        public Transform sprayOrigin;

        [Header("Particles")]
        [Tooltip("Rotate the spray visuals so they aim toward the mouse ray.")]
        public bool orientParticlesToMouse = true;

        [Tooltip("Optional transform to rotate. If null, uses sprayParticles.transform.")]
        public Transform particleAimTransform;

        [Header("Debug")]
        public bool verboseLogging = false;

        [Header("Runtime (read-only)")]
        [SerializeField] private bool isSprayToggledOn;

        private PlayerInputActions input;
        private readonly Dictionary<MoldPatch, float> cleanseTimers = new();
        private readonly HashSet<MoldPatch> validThisFrame = new HashSet<MoldPatch>();
        private readonly List<MoldPatch> toRemoveBuffer = new List<MoldPatch>();

        private void Awake() {
            input = new PlayerInputActions();
        }

        private void OnEnable() {
            input.Player.Enable();
            input.Player.Spray.started += OnSprayPressed;
        }

        private void OnDisable() {
            input.Player.Spray.started -= OnSprayPressed;
            input.Player.Disable();

            isSprayToggledOn = false;
            cleanseTimers.Clear();
            StopSprayEffects();
        }

        private void Start() {
            if (aimCamera == null) {
                aimCamera = Camera.main;
            }

            if (sprayLoopSource != null) {
                sprayLoopSource.loop = true;
                sprayLoopSource.playOnAwake = false;
            }
        }

        private void Update() {
            if (aimCamera == null || MoldManager.Instance == null) {
                cleanseTimers.Clear();
                StopSprayEffects();
                return;
            }

            if (!isSprayToggledOn) {
                cleanseTimers.Clear();
                StopSprayEffects();
                return;
            }

            StartSprayEffects();

            validThisFrame.Clear();
            toRemoveBuffer.Clear();

            if (Mouse.current == null) {
                return;
            }

            Ray screenRay = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            Vector3 rayOrigin = screenRay.origin;
            Vector3 rayDirection = screenRay.direction.normalized;

            Vector3 origin = sprayOrigin != null ? sprayOrigin.position : rayOrigin;
            Vector3 end = origin + rayDirection * range;

            UpdateParticleAim(origin, rayDirection);

            var allPatches = MoldManager.Instance.AllPatches;

            for (int i = 0; i < allPatches.Count; i++) {
                MoldPatch patch = allPatches[i];
                if (patch == null) continue;

                Vector3 patchPos = patch.transform.position;
                float dist = DistancePointToSegment(patchPos, origin, end);

                if (dist > radius) continue;

                MoldManager.Instance.NotifyPatchSprayed(patch);

                if (!MoldManager.Instance.CanPatchBeCleansed(patch)) {
                    continue;
                }

                validThisFrame.Add(patch);

                if (!cleanseTimers.ContainsKey(patch)) {
                    cleanseTimers[patch] = 0f;

                    if (verboseLogging) {
                        Debug.Log($"Spray started affecting: {patch.name}", this);
                    }
                }

                cleanseTimers[patch] += Time.deltaTime;

                float required = MoldManager.Instance.GetCleanseDuration(patch);
                if (cleanseTimers[patch] >= required) {
                    MoldManager.Instance.CleansePatch(patch);

                    if (verboseLogging) {
                        Debug.Log($"Spray cleansed: {patch.name}", this);
                    }

                    cleanseTimers.Remove(patch);
                }
            }

            foreach (var kvp in cleanseTimers) {
                if (!validThisFrame.Contains(kvp.Key)) {
                    toRemoveBuffer.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemoveBuffer.Count; i++) {
                MoldPatch patch = toRemoveBuffer[i];
                cleanseTimers.Remove(patch);

                if (verboseLogging) {
                    Debug.Log($"Spray stopped affecting: {patch.name}", this);
                }
            }
        }

        private void UpdateParticleAim(Vector3 origin, Vector3 direction) {
            if (!orientParticlesToMouse) return;

            Transform target = particleAimTransform != null
                ? particleAimTransform
                : (sprayParticles != null ? sprayParticles.transform : null);

            if (target == null) return;

            target.position = origin;
            target.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private void OnSprayPressed(InputAction.CallbackContext ctx) {
            isSprayToggledOn = !isSprayToggledOn;

            if (verboseLogging) {
                Debug.Log($"Spray toggled {(isSprayToggledOn ? "ON" : "OFF")}", this);
            }

            if (!isSprayToggledOn) {
                cleanseTimers.Clear();
                StopSprayEffects();
            }
            else {
                StartSprayEffects();
            }
        }

        private float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b) {
            Vector3 ab = b - a;
            float abLenSq = ab.sqrMagnitude;

            if (abLenSq <= 0.0001f) {
                return Vector3.Distance(point, a);
            }

            float t = Vector3.Dot(point - a, ab) / abLenSq;
            t = Mathf.Clamp01(t);

            Vector3 closest = a + ab * t;
            return Vector3.Distance(point, closest);
        }

        private void StartSprayEffects() {
            if (sprayParticles != null && !sprayParticles.isPlaying) {
                sprayParticles.Play();
            }

            if (sprayLoopSource != null && !sprayLoopSource.isPlaying) {
                sprayLoopSource.Play();
            }
        }

        private void StopSprayEffects() {
            if (sprayParticles != null && sprayParticles.isPlaying) {
                sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (sprayLoopSource != null && sprayLoopSource.isPlaying) {
                sprayLoopSource.Stop();
            }
        }
    }
}