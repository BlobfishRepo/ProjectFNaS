using System;
using TMPro;
using UnityEngine;

namespace FNaS.UI.Settings {
    public class TextInputSettingRow : MonoBehaviour {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private TMP_Text characterCountText;

        [Header("Limits")]
        [SerializeField] private int characterLimit = 1000;

        private Action<string> onValueChanged;

        public void Setup(string label, string currentValue, Action<string> callback) {
            if (labelText != null)
                labelText.text = label;

            onValueChanged = callback;

            if (input != null) {
                input.onValueChanged.RemoveAllListeners();

                input.lineType = TMP_InputField.LineType.MultiLineNewline;
                input.characterLimit = characterLimit;

                string safeValue = ClampText(currentValue ?? "");
                input.SetTextWithoutNotify(safeValue);

                UpdateCharacterCount(safeValue);

                input.onValueChanged.AddListener(HandleValueChanged);
            }
        }

        private void HandleValueChanged(string value) {
            string normalized = NormalizeText(value);
            normalized = ClampText(normalized);

            if (input != null && input.text != normalized) {
                input.SetTextWithoutNotify(normalized);
            }

            UpdateCharacterCount(normalized);
            onValueChanged?.Invoke(normalized);
        }

        private string NormalizeText(string value) {
            if (string.IsNullOrEmpty(value)) return "";

            return value
                .Replace("\\n", "\n")
                .Replace("\\t", " ");
        }

        private string ClampText(string value) {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Length <= characterLimit) return value;
            return value[..characterLimit];
        }

        private void UpdateCharacterCount(string value) {
            if (characterCountText == null) return;

            int count = string.IsNullOrEmpty(value) ? 0 : value.Length;
            characterCountText.text = $"{count}/{characterLimit}";

            bool invalidLength = (count > 0 && count < 500) || count >= characterLimit;
            characterCountText.color = invalidLength ? Color.red : Color.white;
        }
    }
}