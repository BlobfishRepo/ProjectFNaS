using System;
using UnityEngine;
using FNaS.Settings;

namespace FNaS.Systems {
    public class GlobalAIScheduler : MonoBehaviour {
        public static GlobalAIScheduler Instance { get; private set; }

        [Header("Global Opportunity Clock")]
        [Tooltip("Base global AI movement opportunity interval in seconds.")]
        [Min(0.01f)] public float baseIntervalSeconds = 5f;

        [Header("Runtime (read-only)")]
        [SerializeField] private int currentTick;
        [SerializeField] private float timer;

        public event Action<int> OnOpportunityTick;

        public int CurrentTick => currentTick;
        public float BaseIntervalSeconds => baseIntervalSeconds;

        private void Awake() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("Duplicate GlobalAIScheduler found. Destroying extra instance.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            var settings = RuntimeGameSettings.Instance;
            if (settings != null) {
                baseIntervalSeconds = Mathf.Max(0.01f, settings.GetFloat("globalAI.baseIntervalSeconds"));
            }
        }

        private void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
        }

        private void Update() {
            timer += Time.deltaTime;

            while (timer >= baseIntervalSeconds) {
                timer -= baseIntervalSeconds;
                currentTick++;
                OnOpportunityTick?.Invoke(currentTick);
            }
        }

        public int SecondsToTickStride(float intervalSeconds) {
            if (baseIntervalSeconds <= 0f) return 1;

            int stride = Mathf.RoundToInt(intervalSeconds / baseIntervalSeconds);
            return Mathf.Max(1, stride);
        }

        public bool IsTickDue(int tick, float intervalSeconds) {
            int stride = SecondsToTickStride(intervalSeconds);
            return tick % stride == 0;
        }

        public int GetNextDueTick(int fromTick, float intervalSeconds) {
            int stride = SecondsToTickStride(intervalSeconds);
            if (stride <= 1) return Mathf.Max(1, fromTick);

            int remainder = fromTick % stride;
            if (remainder == 0) return Mathf.Max(1, fromTick);

            return fromTick + (stride - remainder);
        }

        public int GetNextGlobalTick() {
            return currentTick + 1;
        }

        public float SecondsUntilNextTick() {
            return Mathf.Max(0f, baseIntervalSeconds - timer);
        }
    }
}