using UnityEngine;
using FNaS.MasterNodes;
using FNaS.Settings;

namespace FNaS.Gameplay {
    public class PlayerEntity : MonoBehaviour {
        [Header("References")]
        [SerializeField] private PlayerInputController inputController;
        [SerializeField] private ViewController viewController;

        [Header("Movement Drivers")]
        [SerializeField] private PlayerWaypointController nodeMovement;
        [SerializeField] private PlayerRoamMovement roamMovement;

        [Header("Optional Rig Roots")]
        [SerializeField] private GameObject nodeRigRoot;
        [SerializeField] private GameObject roamRigRoot;

        [Header("Runtime")]
        [SerializeField] private PlayerMovementBase activeMovement;

        public PlayerInputController InputController => inputController;
        public PlayerMovementBase ActiveMovement => activeMovement;

        public MasterNode CurrentMasterNode => activeMovement != null ? activeMovement.CurrentMasterNode : null;
        public bool IsMoving => activeMovement != null && activeMovement.IsMoving;

        public Transform RigTransform => activeMovement != null ? activeMovement.RigTransform : transform;
        public Transform ViewTransform => activeMovement != null ? activeMovement.ViewTransform : transform;

        public PlayerWaypointController NodeMovement => nodeMovement;
        public PlayerRoamMovement RoamMovement => roamMovement;

        private void Awake() {
            if (inputController == null) inputController = GetComponent<PlayerInputController>();
            if (viewController == null) viewController = GetComponent<ViewController>();
            if (nodeMovement == null) nodeMovement = GetComponent<PlayerWaypointController>();
            if (roamMovement == null) roamMovement = GetComponent<PlayerRoamMovement>();

            activeMovement = ResolveMovementFromSettings();

            ConfigureMode();

            if (activeMovement == null) {
                Debug.LogError("PlayerEntity: No movement driver found.", this);
                enabled = false;
                return;
            }

            if (inputController == null) {
                Debug.LogError("PlayerEntity: No PlayerInputController found.", this);
                enabled = false;
                return;
            }

            activeMovement.Initialize(this, inputController);
        }

        private PlayerMovementBase ResolveMovementFromSettings() {
            var settings = GameSettingsManager.Instance;
            PlayerMovementMode mode = settings != null
                ? settings.PlayerMovementMode
                : PlayerMovementMode.NodeBased;

            return mode switch {
                PlayerMovementMode.FreeRoam => roamMovement != null ? roamMovement : nodeMovement,
                _ => nodeMovement != null ? nodeMovement : roamMovement
            };
        }

        private void ConfigureMode() {
            bool usingNode = activeMovement == nodeMovement;
            bool usingRoam = activeMovement == roamMovement;

            if (nodeMovement != null) nodeMovement.enabled = usingNode;
            if (roamMovement != null) roamMovement.enabled = usingRoam;

            if (viewController != null) viewController.enabled = usingNode;

            if (nodeRigRoot != null) nodeRigRoot.SetActive(usingNode);
            if (roamRigRoot != null) roamRigRoot.SetActive(usingRoam);
        }
    }
}