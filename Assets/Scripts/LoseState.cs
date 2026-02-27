using UnityEngine;

namespace FNaS.Systems {
    public class LoseState : MonoBehaviour {
        [Header("Runtime")]
        public bool hasLost;
        public string reason;

        public void TriggerLose(string why) {
            if (hasLost) return;
            hasLost = true;
            reason = why;
            Debug.Log($"LOSE TRIGGERED: {why}");
        }

        public void ResetLose() {
            hasLost = false;
            reason = "";
        }
    }
}