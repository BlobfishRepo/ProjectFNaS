using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FNaS.UI {
    public class HintPanelController : MonoBehaviour {
        [Serializable]
        public class HintEntry {
            public string id;
            public string buttonLabel;

            [TextArea(4, 20)]
            public string body;

            public Sprite image;
        }

        [Header("Hints")]
        [SerializeField] private List<HintEntry> hints = new();

        [Header("Hint Buttons")]
        [SerializeField] private Transform buttonRoot;
        [SerializeField] private Button hintButtonPrefab;

        [Header("Content")]
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Image hintImage;

        private readonly List<Button> spawnedButtons = new();

        private void OnEnable() {
            RebuildButtons();

            if (hints.Count > 0) {
                ShowHint(0);
            }
            else {
                ClearContent();
            }
        }

        private void OnDisable() {
            ClearButtons();
        }

        private void RebuildButtons() {
            ClearButtons();

            if (buttonRoot == null || hintButtonPrefab == null) return;

            for (int i = 0; i < hints.Count; i++) {
                int index = i;
                HintEntry hint = hints[i];

                Button button = Instantiate(hintButtonPrefab, buttonRoot);
                spawnedButtons.Add(button);

                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) {
                    label.text = string.IsNullOrWhiteSpace(hint.buttonLabel)
                        ? hint.id
                        : hint.buttonLabel;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ShowHint(index));
            }
        }

        private void ClearButtons() {
            for (int i = 0; i < spawnedButtons.Count; i++) {
                if (spawnedButtons[i] != null) {
                    Destroy(spawnedButtons[i].gameObject);
                }
            }

            spawnedButtons.Clear();
        }

        private void ShowHint(int index) {
            if (index < 0 || index >= hints.Count) return;

            HintEntry hint = hints[index];

            if (bodyText != null) {
                bodyText.text = hint.body ?? string.Empty;
            }

            if (hintImage != null) {
                bool hasImage = hint.image != null;
                hintImage.sprite = hint.image;
                hintImage.enabled = hasImage;
            }
        }

        private void ClearContent() {
            if (bodyText != null) {
                bodyText.text = string.Empty;
            }

            if (hintImage != null) {
                hintImage.sprite = null;
                hintImage.enabled = false;
            }
        }
    }
}