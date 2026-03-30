using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FNaS.UI.Settings {
    public class DropdownSettingRow : MonoBehaviour {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Dropdown dropdown;

        private Action<int> onValueChanged;

        public void Setup(string label, int currentValue, IReadOnlyList<string> options, Action<int> callback) {
            if (labelText != null)
                labelText.text = label;

            onValueChanged = callback;

            if (dropdown != null) {
                dropdown.onValueChanged.RemoveAllListeners();
                dropdown.ClearOptions();
                dropdown.AddOptions(new List<string>(options));
                dropdown.SetValueWithoutNotify(currentValue);
                dropdown.onValueChanged.AddListener(HandleValueChanged);
            }
        }

        private void HandleValueChanged(int value) {
            onValueChanged?.Invoke(value);
        }
    }
}