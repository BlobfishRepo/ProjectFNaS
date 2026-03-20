using UnityEngine;
using FNaS.Gameplay;
using FNaS.Entities.Stalker;
using FNaS.Systems;

namespace FNaS.Settings {
    public class GameSettingsApplier : MonoBehaviour {
        [Header("Scene References")]
        [SerializeField] private PlayerWaypointController playerWaypointController;
        [SerializeField] private DoorInteractor doorInteractor;
        [SerializeField] private StalkerEntity stalkerEntity;
        [SerializeField] private FlashlightTool flashlightTool;

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
                //stalkerEntity.opportunityIntervalTicks = Mathf.Max(1, Mathf.RoundToInt(settings.opportunityInterval / 5f));

                stalkerEntity.freezeIfSeenOnCamera = settings.freezeIfSeenOnCamera;
                stalkerEntity.freezeIfSeenInPerson = settings.freezeIfSeenInPerson;
                stalkerEntity.allowShareNodeWithPlayer = settings.allowShareNodeWithPlayer;
            }

            if (flashlightTool != null)
                flashlightTool.ApplyMaxBatterySeconds(settings.maxBatterySeconds);
        }
    }
}