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
        }

        [Serializable]
        public class HintCollection {
            public List<HintEntry> hints = new();
        }

        [Serializable]
        public class HintImageEntry {
            public Sprite image;
            public float preferredHeight = 250f;
        }

        [Serializable]
        public class HintImageBinding {
            public string id;

            [Header("Main Image")]
            public Sprite mainImage;
            public float mainImagePreferredHeight = 550f;

            [Header("Secondary Images")]
            public List<HintImageEntry> secondaryImages = new();
        }

        [Header("Data")]
        [SerializeField] private TextAsset hintsJson;
        [SerializeField] private List<HintImageBinding> imageBindings = new();

        [Header("Buttons")]
        [SerializeField] private Transform buttonRoot;
        [SerializeField] private Button hintButtonPrefab;

        [Header("Content")]
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Transform mainImageRoot;
        [SerializeField] private Transform secondaryImageRoot;
        [SerializeField] private Image hintImagePrefab;

        [Header("Button Colors")]
        [SerializeField] private Color normalColor = new Color(0.75f, 0.75f, 0.75f);
        [SerializeField] private Color selectedColor = new Color(0.25f, 0.45f, 1f);

        private readonly List<HintEntry> hints = new();
        private readonly Dictionary<string, HintImageBinding> imagesById = new();

        private readonly List<Button> buttons = new();
        private readonly List<TMP_Text> buttonLabels = new();
        private readonly List<Image> spawnedImages = new();

        private int selectedIndex = -1;

        private void Awake() {
            LoadHints();
            BuildImageLookup();
        }

        private void OnEnable() {
            if (hints.Count == 0) {
                LoadHints();
            }

            if (imagesById.Count == 0) {
                BuildImageLookup();
            }

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
            ClearImages();
            selectedIndex = -1;
        }

        private void LoadHints() {
            hints.Clear();

            if (hintsJson == null || string.IsNullOrWhiteSpace(hintsJson.text)) {
                Debug.LogWarning("HintPanelController: hintsJson is missing or empty.", this);
                return;
            }

            try {
                HintCollection collection = JsonUtility.FromJson<HintCollection>(hintsJson.text);
                if (collection?.hints == null) return;

                for (int i = 0; i < collection.hints.Count; i++) {
                    HintEntry hint = collection.hints[i];
                    if (hint == null || string.IsNullOrWhiteSpace(hint.id)) continue;

                    hints.Add(hint);
                }
            }
            catch (Exception ex) {
                Debug.LogError($"HintPanelController: Failed to parse hints JSON.\n{ex}", this);
            }
        }

        private void BuildImageLookup() {
            imagesById.Clear();

            for (int i = 0; i < imageBindings.Count; i++) {
                HintImageBinding binding = imageBindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.id)) continue;

                imagesById[binding.id] = binding;
            }
        }

        private void RebuildButtons() {
            ClearButtons();

            if (buttonRoot == null || hintButtonPrefab == null) return;

            for (int i = 0; i < hints.Count; i++) {
                int index = i;
                HintEntry hint = hints[i];

                Button button = Instantiate(hintButtonPrefab, buttonRoot);
                buttons.Add(button);

                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                buttonLabels.Add(label);

                if (label != null) {
                    label.text = string.IsNullOrWhiteSpace(hint.buttonLabel)
                        ? hint.id
                        : hint.buttonLabel;

                    label.color = normalColor;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ShowHint(index));
            }
        }

        private void ClearButtons() {
            for (int i = 0; i < buttons.Count; i++) {
                if (buttons[i] != null) {
                    Destroy(buttons[i].gameObject);
                }
            }

            buttons.Clear();
            buttonLabels.Clear();
        }

        private void ShowHint(int index) {
            if (index < 0 || index >= hints.Count) return;

            selectedIndex = index;
            HintEntry hint = hints[index];

            if (bodyText != null) {
                bodyText.text = hint.body ?? string.Empty;
            }

            ClearImages();

            if (imagesById.TryGetValue(hint.id, out HintImageBinding binding) && binding != null) {

                // MAIN IMAGE (prefab, bottom-right)
                if (binding.mainImage != null &&
                    mainImageRoot != null &&
                    hintImagePrefab != null) {

                    Image img = Instantiate(hintImagePrefab, mainImageRoot);
                    img.sprite = binding.mainImage;
                    img.enabled = true;
                    img.preserveAspect = true;

                    LayoutElement layout = img.GetComponent<LayoutElement>();
                    if (layout == null) {
                        layout = img.gameObject.AddComponent<LayoutElement>();
                    }

                    layout.preferredHeight = Mathf.Max(1f, binding.mainImagePreferredHeight);
                    layout.flexibleHeight = 0f;

                    spawnedImages.Add(img);
                }

                // SECONDARY IMAGES (stacked)
                if (secondaryImageRoot != null &&
                    hintImagePrefab != null &&
                    binding.secondaryImages != null) {

                    for (int i = 0; i < binding.secondaryImages.Count; i++) {
                        HintImageEntry entry = binding.secondaryImages[i];
                        if (entry == null || entry.image == null) continue;

                        Image img = Instantiate(hintImagePrefab, secondaryImageRoot);
                        img.sprite = entry.image;
                        img.enabled = true;
                        img.preserveAspect = true;

                        LayoutElement layout = img.GetComponent<LayoutElement>();
                        if (layout == null) {
                            layout = img.gameObject.AddComponent<LayoutElement>();
                        }

                        layout.preferredHeight = Mathf.Max(1f, entry.preferredHeight);
                        layout.flexibleHeight = 0f;

                        spawnedImages.Add(img);
                    }
                }
            }

            RefreshButtonColors();
        }

        private void ClearImages() {
            for (int i = 0; i < spawnedImages.Count; i++) {
                if (spawnedImages[i] != null) {
                    Destroy(spawnedImages[i].gameObject);
                }
            }

            spawnedImages.Clear();
        }

        private void RefreshButtonColors() {
            for (int i = 0; i < buttonLabels.Count; i++) {
                if (buttonLabels[i] == null) continue;

                buttonLabels[i].color = i == selectedIndex
                    ? selectedColor
                    : normalColor;
            }
        }

        private void ClearContent() {
            if (bodyText != null) {
                bodyText.text = string.Empty;
            }

            ClearImages();
            RefreshButtonColors();
        }
    }
}