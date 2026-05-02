using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FNaS.UI {
    public class InfoPopupOverlay : MonoBehaviour {
        [Header("References")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Button clickToCloseButton;

        private void Awake() {
            if (root == null) {
                root = gameObject;
            }

            if (clickToCloseButton != null) {
                clickToCloseButton.onClick.RemoveListener(Hide);
                clickToCloseButton.onClick.AddListener(Hide);
            }

            Hide();
        }

        public void Show(string message) {
            if (infoText != null) {
                infoText.text = message;
            }

            if (root != null) {
                root.SetActive(true);
            }
        }

        public void Hide() {
            if (root != null) {
                root.SetActive(false);
            }
        }
    }
}