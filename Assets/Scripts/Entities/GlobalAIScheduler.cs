using System;
using UnityEngine;

namespace FNaS.Systems {
    public class GlobalAIScheduler : MonoBehaviour {
        public static GlobalAIScheduler Instance { get; private set; }

        [Header("Global Opportunity Clock")]
        [Tooltip("Base global AI movement opportunity interval in seconds.")]
        [Min(0.01f)] public float baseIntervalSeconds = 5f;

        [Header("Runtime (read-only)")]
        [SerializeField] private int currentTick;
        [SerializeField] private float timer;

        /// <summary>
        /// Fired every time a global AI tick happens.
        /// int = currentTick (starts at 1 on first pulse)
        /// </summary>
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

        /// <summary>
        /// Converts a desired interval in seconds into a whole-number tick stride.
        /// Example: 10 seconds with 5-second base interval => 2 ticks.
        /// Values are rounded to the nearest multiple of baseIntervalSeconds, minimum 1 tick.
        /// </summary>
        public int SecondsToTickStride(float intervalSeconds) {
            if (baseIntervalSeconds <= 0f) return 1;

            int stride = Mathf.RoundToInt(intervalSeconds / baseIntervalSeconds);
            return Mathf.Max(1, stride);
        }

        /// <summary>
        /// Returns true if the given tick should trigger logic for the requested interval.
        /// Example: With base=5 and interval=10, returns true on ticks 2,4,6,...
        /// </summary>
        public bool IsTickDue(int tick, float intervalSeconds) {
            int stride = SecondsToTickStride(intervalSeconds);
            return tick % stride == 0;
        }

        /// <summary>
        /// Returns the next tick >= currentTick that matches the requested interval.
        /// Useful for snapping stun/skip/reappear logic back onto the global grid.
        /// </summary>
        public int GetNextDueTick(int fromTick, float intervalSeconds) {
            int stride = SecondsToTickStride(intervalSeconds);
            if (stride <= 1) return Mathf.Max(1, fromTick);

            int remainder = fromTick % stride;
            if (remainder == 0) return Mathf.Max(1, fromTick);

            return fromTick + (stride - remainder);
        }

        /// <summary>
        /// Returns the next global tick number, regardless of interval.
        /// </summary>
        public int GetNextGlobalTick() {
            return currentTick + 1;
        }

        /// <summary>
        /// Returns approximately how many seconds remain until the next global pulse.
        /// </summary>
        public float SecondsUntilNextTick() {
            return Mathf.Max(0f, baseIntervalSeconds - timer);
        }
    }
}