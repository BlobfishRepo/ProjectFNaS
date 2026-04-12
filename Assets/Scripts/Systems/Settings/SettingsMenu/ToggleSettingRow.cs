using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FNaS.UI.Settings {
    public class ToggleSettingRow : MonoBehaviour {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Toggle toggle;

        private Action<bool> onValueChanged;

        public void Setup(string label, bool currentValue, Action<bool> callback) {
            if (labelText != null)
                labelText.text = label;

            onValueChanged = callback;

            if (toggle != null) {
                toggle.onValueChanged.RemoveAllListeners();
                toggle.SetIsOnWithoutNotify(currentValue);
                toggle.onValueChanged.AddListener(HandleValueChanged);
            }
        }

        private void HandleValueChanged(bool value) {
            onValueChanged?.Invoke(value);
        }
    }
}