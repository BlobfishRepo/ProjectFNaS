using TMPro;
using UnityEngine;

namespace FNaS.UI.Settings {
    public class SettingsSectionHeader : MonoBehaviour {
        [SerializeField] private TMP_Text titleText;

        public void Setup(string title) {
            if (titleText != null)
                titleText.text = title;
        }
    }
}