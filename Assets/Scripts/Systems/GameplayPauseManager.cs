using System;
using System.Collections.Generic;
using UnityEngine;
using FNaS.Gameplay;

namespace FNaS.Systems {
    public class GameplayPauseManager : MonoBehaviour {
        public static GameplayPauseManager Instance { get; private set; }

        [Header("Pause Targets")]
        public PlayerWaypointController playerMovement;

        [Tooltip("Behaviours to disable while a post-it note is open.")]
        public MonoBehaviour[] disableDuringPause;

        [Header("Debug")]
        public bool verboseLogging = false;

        private readonly Dictionary<MonoBehaviour, bool> previousEnabledStates = new();
        private int pauseDepth;

        public bool IsPaused => pauseDepth > 0;
        public static bool IsPausedGlobal => Instance != null && Instance.IsPaused;

        public event Action<bool> OnPauseChanged;

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (playerMovement == null) {
                playerMovement = FindFirstObjectByType<PlayerWaypointController>();
            }
        }

        private void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
        }

        public void PushPause() {
            pauseDepth++;

            if (pauseDepth == 1) {
                ApplyPauseState(true);
            }

            if (verboseLogging) {
                Debug.Log($"GameplayPauseManager.PushPause -> depth={pauseDepth}", this);
            }
        }

        public void PopPause() {
            if (pauseDepth <= 0) {
                pauseDepth = 0;
                return;
            }

            pauseDepth--;

            if (pauseDepth == 0) {
                ApplyPauseState(false);
            }

            if (verboseLogging) {
                Debug.Log($"GameplayPauseManager.PopPause -> depth={pauseDepth}", this);
            }
        }

        private void ApplyPauseState(bool paused) {
            if (paused) {
                if (playerMovement != null) {
                    playerMovement.PauseActiveMovement();
                }

                previousEnabledStates.Clear();

                if (disableDuringPause != null) {
                    for (int i = 0; i < disableDuringPause.Length; i++) {
                        MonoBehaviour behaviour = disableDuringPause[i];
                        if (behaviour == null) continue;
                        if (previousEnabledStates.ContainsKey(behaviour)) continue;

                        previousEnabledStates[behaviour] = behaviour.enabled;
                        behaviour.enabled = false;
                    }
                }
            }
            else {
                if (disableDuringPause != null) {
                    foreach (var kvp in previousEnabledStates) {
                        if (kvp.Key != null) {
                            kvp.Key.enabled = kvp.Value;
                        }
                    }
                }

                previousEnabledStates.Clear();

                if (playerMovement != null) {
                    playerMovement.ResumeActiveMovement();
                }
            }

            OnPauseChanged?.Invoke(paused);

            if (verboseLogging) {
                Debug.Log($"GameplayPauseManager.ApplyPauseState paused={paused}", this);
            }
        }
    }
}