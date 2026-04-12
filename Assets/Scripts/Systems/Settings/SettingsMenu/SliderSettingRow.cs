using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FNaS.UI.Settings {
    public class SliderSettingRow : MonoBehaviour {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_InputField valueInput;

        private Action<float> onValueChanged;
        private bool wholeNumbers;
        private bool suppressCallbacks;

        public void Setup(
            string label,
            float currentValue,
            float min,
            float max,
            bool useWholeNumbers,
            Action<float> callback
        ) {
            if (labelText != null)
                labelText.text = label;

            wholeNumbers = useWholeNumbers;
            onValueChanged = callback;

            if (slider != null) {
                slider.minValue = min;
                slider.maxValue = max;
                slider.wholeNumbers = useWholeNumbers;
                slider.onValueChanged.RemoveAllListeners();
                slider.SetValueWithoutNotify(currentValue);
                slider.onValueChanged.AddListener(HandleSliderChanged);
            }

            if (valueInput != null) {
                valueInput.onValueChanged.RemoveAllListeners();
                valueInput.onEndEdit.RemoveAllListeners();
                valueInput.SetTextWithoutNotify(FormatValue(currentValue));
                valueInput.onEndEdit.AddListener(HandleInputSubmitted);
            }
        }

        private void HandleSliderChanged(float value) {
            if (suppressCallbacks) return;

            suppressCallbacks = true;

            if (valueInput != null)
                valueInput.SetTextWithoutNotify(FormatValue(value));

            onValueChanged?.Invoke(NormalizeValue(value));

            suppressCallbacks = false;
        }

        private void HandleInputSubmitted(string text) {
            if (suppressCallbacks) return;

            if (!TryParseValue(text, out float parsed)) {
                if (slider != null)
                    valueInput.SetTextWithoutNotify(FormatValue(slider.value));
                return;
            }

            if (slider != null) {
                parsed = Mathf.Clamp(parsed, slider.minValue, slider.maxValue);
            }

            parsed = NormalizeValue(parsed);

            suppressCallbacks = true;

            if (slider != null)
                slider.SetValueWithoutNotify(parsed);

            if (valueInput != null)
                valueInput.SetTextWithoutNotify(FormatValue(parsed));

            onValueChanged?.Invoke(parsed);

            suppressCallbacks = false;
        }

        private float NormalizeValue(float value) {
            return wholeNumbers ? Mathf.Round(value) : value;
        }

        private bool TryParseValue(string text, out float value) {
            return float.TryParse(text, out value);
        }

        private string FormatValue(float value) {
            return wholeNumbers
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }
    }
}