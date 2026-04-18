using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FNaS.Gameplay;
using FNaS.Settings;

namespace FNaS.Systems {
    public class PostItHintController : MonoBehaviour {
        [Serializable]
        public class OverlayImageBinding {
            public string id;
            public Sprite image;
        }

        [Header("Data")]
        public TextAsset notesJson;

        [Header("Scene Bindings")]
        public List<PostItNoteHotspot> hotspots = new();
        public List<OverlayImageBinding> overlayImageBindings = new();

        [Header("References")]
        public ViewController viewController;
        public PaperWinProgress paperWinProgress;
        public GameplayPauseManager pauseManager;

        [Header("Monitor View")]
        public View monitorView;
        public string monitorViewNameFallback = "Monitor";

        [Header("Overlay UI")]
        public CanvasGroup overlayGroup;
        public TMP_Text bodyText;
        public Image contentImage;
        public GameObject contentImageRoot;

        [Header("World Click")]
        public Camera clickCamera;
        public LayerMask postItMask = ~0;
        public float maxClickDistance = 20f;

        [Header("Input")]
        public float navThreshold = 0.5f;

        [Header("Setting Key")]
        public string disableNotesSettingKey = "player.disablePostItNotes";

        [Header("Audio")]
        public AudioSource uiAudioSource;
        public AudioClip openClip;
        public AudioClip closeClip;

        [Header("Debug")]
        public bool verboseLogging = false;

        private PlayerInputActions input;

        private readonly Dictionary<string, PostItNoteRecord> notesById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PostItNoteHotspot> hotspotsById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> overlayImagesById = new(StringComparer.Ordinal);
        private readonly List<string> visibleNoteIds = new();
        private readonly HashSet<string> hiddenNoteIds = new(StringComparer.Ordinal);

        private bool noteOpen;
        private int currentVisibleSlot = -1;
        private int openedOnFrame = -999;
        private bool leftHeld;
        private bool rightHeld;

        private void Awake() {
            input = new PlayerInputActions();

            if (viewController == null) {
                viewController = FindFirstObjectByType<ViewController>();
            }

            if (paperWinProgress == null) {
                paperWinProgress = FindFirstObjectByType<PaperWinProgress>();
            }

            if (pauseManager == null) {
                pauseManager = FindFirstObjectByType<GameplayPauseManager>();
            }

            LoadNoteDatabase();
            BuildBindings();

            noteOpen = false;
            currentVisibleSlot = -1;
            SetOverlayVisible(false);

            if (bodyText != null) {
                bodyText.text = string.Empty;
            }
        }

        private void Start() {
            RefreshHotspotPresentation();
            SetOverlayVisible(false);
        }

        private void OnEnable() {
            input.Player.Enable();
            input.Player.Interact.started += OnInteractPressed;
        }

        private void OnDisable() {
            input.Player.Interact.started -= OnInteractPressed;
            input.Player.Disable();

            ForceCloseOverlay();
        }

        private void Update() {
            RefreshHotspotPresentation();

            if (!noteOpen) {
                if (overlayGroup != null && overlayGroup.alpha > 0f) {
                    SetOverlayVisible(false);
                }

                HandleWorldClickOpen();
                return;
            }

            HandleNavigationInput();
            HandleLeftClickClose();
        }

        private void LoadNoteDatabase() {
            notesById.Clear();

            string json = notesJson != null ? notesJson.text : string.Empty;
            PostItNoteDatabase db = PostItNoteDatabase.FromJson(json);

            if (db.notes == null) return;

            for (int i = 0; i < db.notes.Count; i++) {
                PostItNoteRecord note = db.notes[i];
                if (note == null || string.IsNullOrWhiteSpace(note.id)) continue;

                notesById[note.id] = note;
            }

            if (verboseLogging) {
                Debug.Log($"PostItHintController loaded {notesById.Count} notes from JSON.", this);
            }
        }

        private void BuildBindings() {
            hotspotsById.Clear();
            overlayImagesById.Clear();

            if (hotspots != null) {
                for (int i = 0; i < hotspots.Count; i++) {
                    PostItNoteHotspot hotspot = hotspots[i];
                    if (hotspot == null || string.IsNullOrWhiteSpace(hotspot.noteId)) continue;

                    hotspotsById[hotspot.noteId] = hotspot;
                }
            }

            if (overlayImageBindings != null) {
                for (int i = 0; i < overlayImageBindings.Count; i++) {
                    OverlayImageBinding binding = overlayImageBindings[i];
                    if (binding == null || string.IsNullOrWhiteSpace(binding.id)) continue;

                    overlayImagesById[binding.id] = binding.image;
                }
            }
        }

        private void HandleWorldClickOpen() {
            if (!AreNotesEnabled()) return;
            if (!IsMonitorViewActive()) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            Camera cam = clickCamera != null ? clickCamera : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (!Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, postItMask, QueryTriggerInteraction.Ignore)) {
                return;
            }

            PostItNoteHotspot hotspot = hit.collider.GetComponentInParent<PostItNoteHotspot>();
            if (hotspot == null || string.IsNullOrWhiteSpace(hotspot.noteId)) return;

            RebuildVisibleNoteList();

            for (int i = 0; i < visibleNoteIds.Count; i++) {
                string id = visibleNoteIds[i];
                if (!string.Equals(id, hotspot.noteId, StringComparison.Ordinal)) continue;

                MarkClickedThisSession(id);
                currentVisibleSlot = i;
                OpenVisibleSlot();
                return;
            }
        }

        private void HandleNavigationInput() {
            if (input == null) return;

            Vector2 move = input.Player.Move.ReadValue<Vector2>();

            if (move.x <= -navThreshold) {
                if (!leftHeld) {
                    leftHeld = true;
                    Cycle(-1);
                }
            }
            else if (move.x > -0.25f) {
                leftHeld = false;
            }

            if (move.x >= navThreshold) {
                if (!rightHeld) {
                    rightHeld = true;
                    Cycle(+1);
                }
            }
            else if (move.x < 0.25f) {
                rightHeld = false;
            }
        }

        private void HandleLeftClickClose() {
            if (Time.frameCount == openedOnFrame) return;
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            CloseOverlay();
        }

        private void OnInteractPressed(InputAction.CallbackContext ctx) {
            if (!noteOpen) return;
            HideCurrentNoteForThisSession();
        }

        private void OpenVisibleSlot() {
            RebuildVisibleNoteList();

            if (visibleNoteIds.Count == 0) {
                CloseOverlay();
                return;
            }

            currentVisibleSlot = Mathf.Clamp(currentVisibleSlot, 0, visibleNoteIds.Count - 1);
            string id = visibleNoteIds[currentVisibleSlot];

            if (!notesById.TryGetValue(id, out PostItNoteRecord note)) {
                CloseOverlay();
                return;
            }

            if (pauseManager != null && !pauseManager.IsPaused) {
                pauseManager.PushPause();
            }

            PopulateOverlay(note);
            noteOpen = true;
            openedOnFrame = Time.frameCount;
            SetOverlayVisible(true);
            PlayClip(openClip);

            RefreshHotspotPresentation();

            if (verboseLogging) {
                Debug.Log($"Opened post-it note '{id}'", this);
            }
        }

        private void CloseOverlay() {
            if (!noteOpen) return;

            noteOpen = false;
            currentVisibleSlot = -1;
            SetOverlayVisible(false);
            PlayClip(closeClip);

            if (pauseManager != null && pauseManager.IsPaused) {
                pauseManager.PopPause();
            }

            RefreshHotspotPresentation();
        }

        private void ForceCloseOverlay() {
            bool wasOpen = noteOpen;

            noteOpen = false;
            currentVisibleSlot = -1;
            SetOverlayVisible(false);

            if (wasOpen && pauseManager != null && pauseManager.IsPaused) {
                pauseManager.PopPause();
            }
        }

        private void Cycle(int delta) {
            RebuildVisibleNoteList();
            if (visibleNoteIds.Count <= 1) return;

            currentVisibleSlot += delta;

            while (currentVisibleSlot < 0) currentVisibleSlot += visibleNoteIds.Count;
            while (currentVisibleSlot >= visibleNoteIds.Count) currentVisibleSlot -= visibleNoteIds.Count;

            string id = visibleNoteIds[currentVisibleSlot];
            if (!notesById.TryGetValue(id, out PostItNoteRecord note)) return;

            MarkClickedThisSession(id);
            PopulateOverlay(note);
            RefreshHotspotPresentation();
            PlayClip(openClip);
        }

        private void HideCurrentNoteForThisSession() {
            RebuildVisibleNoteList();

            if (currentVisibleSlot < 0 || currentVisibleSlot >= visibleNoteIds.Count) {
                CloseOverlay();
                return;
            }

            string id = visibleNoteIds[currentVisibleSlot];
            MarkHiddenThisSession(id);
            PlayClip(closeClip);

            int replacementSlot = currentVisibleSlot;
            RebuildVisibleNoteList();

            if (visibleNoteIds.Count == 0) {
                CloseOverlay();
                return;
            }

            currentVisibleSlot = Mathf.Clamp(replacementSlot, 0, visibleNoteIds.Count - 1);

            string replacementId = visibleNoteIds[currentVisibleSlot];
            if (!notesById.TryGetValue(replacementId, out PostItNoteRecord replacement)) {
                CloseOverlay();
                return;
            }

            MarkClickedThisSession(replacementId);
            PopulateOverlay(replacement);
            RefreshHotspotPresentation();
            PlayClip(openClip);
        }

        private void PopulateOverlay(PostItNoteRecord note) {
            if (bodyText != null) {
                bodyText.text = note.body ?? string.Empty;
            }

            overlayImagesById.TryGetValue(note.id, out Sprite image);
            bool hasImage = contentImage != null && image != null;

            if (contentImage != null) {
                contentImage.sprite = image;
                contentImage.enabled = hasImage;
            }

            if (contentImageRoot != null) {
                contentImageRoot.SetActive(hasImage);
            }
        }

        private void RefreshHotspotPresentation() {
            bool notesEnabled = AreNotesEnabled();
            bool monitorActive = IsMonitorViewActive();

            foreach (var kvp in hotspotsById) {
                string id = kvp.Key;
                PostItNoteHotspot hotspot = kvp.Value;
                if (hotspot == null) continue;

                bool visible = notesEnabled && monitorActive && IsNoteEligibleAndNotHidden(id);
                bool glow = visible && !WasClickedThisSession(id);

                hotspot.SetPresentation(visible, glow);
            }

            if (noteOpen && (!notesEnabled || !monitorActive)) {
                CloseOverlay();
            }
        }

        private void RebuildVisibleNoteList() {
            visibleNoteIds.Clear();

            if (!AreNotesEnabled()) return;

            foreach (var kvp in notesById) {
                if (IsNoteEligibleAndNotHidden(kvp.Key)) {
                    visibleNoteIds.Add(kvp.Key);
                }
            }

            visibleNoteIds.Sort(StringComparer.Ordinal);
        }

        private bool IsNoteEligibleAndNotHidden(string id) {
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (IsHiddenThisSession(id)) return false;
            if (!notesById.TryGetValue(id, out PostItNoteRecord note)) return false;

            NightSessionManager session = NightSessionManager.Instance;
            if (session == null) return false;
            if (session.PlayMode == NightPlayMode.None) return false;

            switch (session.PlayMode) {
                case NightPlayMode.Campaign:
                    if (note.campaignNights == null || note.campaignNights.Count == 0) return false;
                    return note.campaignNights.Contains(session.CurrentCampaignNight);

                case NightPlayMode.Presentation:
                    if (note.presentationUnlockPercent < 0) return false;

                    float progress01 = paperWinProgress != null ? Mathf.Clamp01(paperWinProgress.GetProgress01()) : 0f;
                    int percent = Mathf.FloorToInt(progress01 * 100f);
                    int stage =
                        percent >= 80 ? 80 :
                        percent >= 60 ? 60 :
                        percent >= 40 ? 40 :
                        percent >= 20 ? 20 : 0;

                    return stage >= note.presentationUnlockPercent;

                case NightPlayMode.CustomNight:
                default:
                    return false;
            }
        }

        private bool IsMonitorViewActive() {
            if (viewController == null) return false;

            View current = viewController.CurrentView;
            if (current == null) return false;

            if (monitorView != null) {
                return current == monitorView;
            }

            return !string.IsNullOrEmpty(monitorViewNameFallback) &&
                   string.Equals(current.gameObject.name, monitorViewNameFallback, StringComparison.OrdinalIgnoreCase);
        }

        private bool AreNotesEnabled() {
            RuntimeGameSettings settings = RuntimeGameSettings.Instance;
            return settings == null || !settings.GetBool(disableNotesSettingKey);
        }

        private bool WasClickedThisSession(string noteId) {
            NightSessionManager session = NightSessionManager.Instance;
            return session != null && session.WasPostItClicked(noteId);
        }

        private void MarkClickedThisSession(string noteId) {
            NightSessionManager session = NightSessionManager.Instance;
            session?.MarkPostItClicked(noteId);
        }

        private bool IsHiddenThisSession(string noteId) {
            return !string.IsNullOrWhiteSpace(noteId) && hiddenNoteIds.Contains(noteId);
        }

        private void MarkHiddenThisSession(string noteId) {
            if (string.IsNullOrWhiteSpace(noteId)) return;
            hiddenNoteIds.Add(noteId);
        }

        private void SetOverlayVisible(bool visible) {
            if (overlayGroup == null) return;

            overlayGroup.alpha = visible ? 1f : 0f;
            overlayGroup.blocksRaycasts = visible;
            overlayGroup.interactable = visible;
        }

        private void PlayClip(AudioClip clip) {
            if (uiAudioSource != null && clip != null) {
                uiAudioSource.PlayOneShot(clip);
            }
        }
    }
}