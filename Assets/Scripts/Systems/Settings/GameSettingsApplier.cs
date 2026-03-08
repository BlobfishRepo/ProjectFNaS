using UnityEngine;
using FNaS.Gameplay;
using FNaS.Entities.Stalker;

namespace FNaS.Settings {
    public class GameSettingsApplier : MonoBehaviour {
        [Header("Scene References")]
        [SerializeField] private PlayerWaypointController playerWaypointController;
        [SerializeField] private DoorInteractor doorInteractor;
        [SerializeField] private StalkerEntity stalkerEntity;

        private void Start() {
            var settings = RuntimeGameSettings.Instance;
            if (settings == null) {
                Debug.LogWarning("GameSettingsApplier: No RuntimeGameSettings found.");
                return;
            }

            if (playerWaypointController != null)
                playerWaypointController.moveSpeed = settings.playerMoveSpeed;

            if (doorInteractor != null)
                doorInteractor.maxDistance = settings.doorMaxDistance;

            if (stalkerEntity != null) {
                stalkerEntity.ai = settings.stalkerAI;
                stalkerEntity.opportunityIntervalSeconds = settings.opportunityInterval;
            }
        }
    }
}