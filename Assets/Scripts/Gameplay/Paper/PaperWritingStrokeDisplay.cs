using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using FNaS.Gameplay;
using FNaS.Settings;

namespace FNaS.Systems {
    public class PaperWritingStrokeDisplay : MonoBehaviour, IRuntimeSettingsConsumer {
        [Serializable]
        private class DrawSegment {
            public Vector2 pageA;
            public Vector2 pageB;
            public Vector2 pixelA;
            public Vector2 pixelB;
            public float localStartLength;
            public float localEndLength;
            public float length;
        }

        [Serializable]
        private class BuiltLine {
            public readonly List<DrawSegment> segments = new();
            public float lineLength;
            public float globalStartLength;
            public float globalEndLength;
        }

        private enum TokenType {
            Word,
            Newline
        }

        private enum WritingSoundMode {
            Normal = 0,
            Fun = 1
        }

        private struct TextToken {
            public TokenType type;
            public string text;
        }

        [Header("Input")]
        [TextArea(4, 20)]
        public string textToWrite =
@"DEAR DIARY
SOMETHING IS
WRONG IN THIS
APARTMENT";

        public StrokeGlyphLibrary glyphLibrary;
        public PaperWinProgress paperProgress;

        [Header("Optional View Visibility")]
        public ViewController viewController;
        public View paperView;
        public string paperViewNameFallback = "Paper";
        public bool onlyRenderOverlayOnPaperView = true;

        [Header("Layout")]
        public Vector2 pageOrigin = new(-0.44f, -0.45f);
        public Vector2 pageSize = new(0.88f, 0.9f);
        public float lineHeight = 0.09f;
        public float lineSpacingMultiplier = 1.4f;
        public float characterSpacing = 0.05f;
        public float wordSpacing = 0.18f;
        public float glyphScale = 1f;

        [Header("Safety")]
        [Min(1)] public int maxCharacters = 1000;

        [Header("Surface")]
        public float localSurfaceOffset = 0.003f;

        [Header("Ink Rendering")]
        [Min(64)] public int textureWidth = 512;
        [Min(64)] public int textureHeight = 512;
        public Color inkColor = Color.black;
        public float lineWidth = 0.01f;
        public Transform overlayRoot;
        public string overlayShaderName = "Unlit/Transparent";

        [Header("Ink Performance")]
        [Min(1f)] public float inkApplyRate = 20f;
        [Min(0.25f)] public float brushSpacingPixels = 1.5f;

        [Header("Pen Audio")]
        public Transform penTip;
        public AudioSource writingAudioSource;
        public AudioClip writingLoopClip;
        public AudioClip funWritingLoopClip;
        [Range(0f, 1f)] public float writingVolume = 0.8f;
        public float minPitch = 0.85f;
        public float maxPitch = 1.35f;
        public float speedForMaxPitch = 0.8f;
        public float audioSmoothing = 10f;

        [Header("Behavior")]
        public bool rebuildOnStart = true;
        public bool uppercaseInput = true;
        public bool verboseLogging = false;

        [Header("Gizmos")]
        public bool drawPageBoundsGizmo = true;
        public Color pageBoundsColor = Color.cyan;

        [Header("Runtime (read-only)")]
        [SerializeField] private int builtStrokeCount;
        [SerializeField] private int builtSegmentCount;
        [SerializeField] private float totalLength;
        [SerializeField] private float currentRevealLength;
        [SerializeField] private string resolvedTextPreview;
        [SerializeField] private int inkRadiusPixels = 1;
        [SerializeField] private int brushOffsetCount;
        [SerializeField] private int revealCursorLineIndex;
        [SerializeField] private int revealCursorSegmentIndex;
        [SerializeField] private float currentPenSpeed;
        [SerializeField] private float smoothedPenSpeed;
        [SerializeField] private WritingSoundMode activeWritingSoundMode = WritingSoundMode.Normal;
        [SerializeField] private bool forcePitch;
        [SerializeField] private float forcedPitch = 1f;
        [SerializeField] private bool resumeWritingAudioFromLastPosition;

        private readonly List<BuiltLine> builtLines = new();
        private readonly List<Vector2Int> brushOffsets = new();

        private GameObject inkQuad;
        private Material inkMaterial;
        private Renderer inkRenderer;
        private Texture2D inkTexture;
        private Color32[] inkPixels;
        private Color32 inkColor32;
        private bool inkDirty;

        private float lastRevealLength;
        private float nextInkApplyTime;
        private Vector3 lastPenAudioPosition;
        private bool hasLastPenAudioPosition;

        private void Start() {
            SetupWritingAudio();

            if (rebuildOnStart) {
                Rebuild();
            }
            else {
                EnsureInkQuad();
                UpdateOverlayVisibility();
            }
        }

        private void OnDisable() {
            StopWritingAudio(resetPlaybackPosition: true);
        }

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) return;

            glyphScale = settings.GetFloat("paper.glyphScale");
            textToWrite = PaperTextPresets.ResolveText(settings.GetInt("paper.textPreset"));
            activeWritingSoundMode = (WritingSoundMode)Mathf.Clamp(settings.GetInt("fun.paperWritingSoundMode"), 0, 1);
            forcePitch = settings.GetBool("fun.paperWritingForcePitch");
            forcedPitch = settings.GetFloat("fun.paperWritingForcedPitch");
            resumeWritingAudioFromLastPosition = activeWritingSoundMode == WritingSoundMode.Fun;

            RefreshWritingAudioConfig();
        }

        private void Update() {
            UpdateOverlayVisibility();
            UpdateReveal();
            UploadInkIfNeeded();
            UpdateWritingAudio();
        }

        [ContextMenu("Rebuild Writing Geometry")]
        public void Rebuild() {
            ResetBuildState();
            EnsureInkQuad();
            RefreshInkSettings();
            ClearInkTexture();

            if (glyphLibrary == null) {
                Debug.LogWarning("PaperWritingStrokeDisplay: No glyph library assigned.", this);
                UpdateOverlayVisibility();
                return;
            }

            resolvedTextPreview = ResolveWorkingText();
            BuildTextGeometry(Tokenize(resolvedTextPreview));
            builtSegmentCount = CountSegments();
            UpdateOverlayVisibility();

            if (verboseLogging) {
                Debug.Log(
                    $"Built {builtStrokeCount} strokes / {builtSegmentCount} segments. Total length = {totalLength:F3}. " +
                    $"LineHeight={lineHeight:F3}, GlyphScale={glyphScale:F3}, BrushRadius={inkRadiusPixels}, BrushOffsets={brushOffsetCount}",
                    this
                );
            }
        }

        private void ResetBuildState() {
            builtLines.Clear();
            totalLength = 0f;
            currentRevealLength = 0f;
            lastRevealLength = 0f;
            builtStrokeCount = 0;
            builtSegmentCount = 0;
            revealCursorLineIndex = 0;
            revealCursorSegmentIndex = 0;
            nextInkApplyTime = 0f;
        }

        private string ResolveWorkingText() {
            string text = textToWrite ?? string.Empty;
            if (uppercaseInput) text = text.ToUpperInvariant();

            if (text.Length > maxCharacters) {
                text = text[..maxCharacters];
                if (verboseLogging) {
                    Debug.LogWarning($"PaperWritingStrokeDisplay: Text truncated to {maxCharacters} characters.", this);
                }
            }

            return text;
        }

        private int CountSegments() {
            int count = 0;
            for (int i = 0; i < builtLines.Count; i++) {
                count += builtLines[i].segments.Count;
            }
            return count;
        }

        private void UpdateReveal() {
            float progress01 = paperProgress != null ? paperProgress.GetProgress01() : 0f;
            currentRevealLength = totalLength * Mathf.Clamp01(progress01);

            if (currentRevealLength < lastRevealLength - 0.0001f) {
                ClearInkTexture();
                revealCursorLineIndex = 0;
                revealCursorSegmentIndex = 0;
                DrawRangeAcrossLines(0f, currentRevealLength);
            }
            else if (currentRevealLength > lastRevealLength + 0.0001f) {
                DrawRangeAcrossLines(lastRevealLength, currentRevealLength);
            }

            lastRevealLength = currentRevealLength;
        }

        private void UploadInkIfNeeded() {
            if (!inkDirty) return;

            bool writingActive = paperProgress != null && paperProgress.IsWritingActive();
            if (Time.time >= nextInkApplyTime || !writingActive) {
                UploadInkTexture();
            }
        }

        private void SetupWritingAudio() {
            if (writingAudioSource == null) return;

            writingAudioSource.playOnAwake = false;
            writingAudioSource.loop = true;
            writingAudioSource.volume = writingVolume;
            writingAudioSource.pitch = 1f;
            RefreshWritingAudioConfig();
        }

        private void RefreshWritingAudioConfig() {
            if (writingAudioSource == null) return;

            AudioClip targetClip = GetActiveWritingClip();
            bool clipChanged = writingAudioSource.clip != targetClip;

            if (clipChanged && writingAudioSource.isPlaying) {
                writingAudioSource.Stop();
            }

            if (clipChanged) {
                writingAudioSource.time = 0f;
            }

            writingAudioSource.clip = targetClip;
            writingAudioSource.loop = true;
            writingAudioSource.volume = writingVolume;
            writingAudioSource.pitch = forcePitch ? Mathf.Clamp(forcedPitch, 0.01f, 3f) : 1f;
        }

        private AudioClip GetActiveWritingClip() {
            return activeWritingSoundMode == WritingSoundMode.Fun && funWritingLoopClip != null
                ? funWritingLoopClip
                : writingLoopClip;
        }

        private void StopWritingAudio(bool resetPlaybackPosition) {
            if (writingAudioSource == null) return;

            if (resumeWritingAudioFromLastPosition && !resetPlaybackPosition) {
                if (writingAudioSource.isPlaying) {
                    writingAudioSource.Pause();
                }
            }
            else {
                if (writingAudioSource.isPlaying) {
                    writingAudioSource.Stop();
                }

                if (writingAudioSource.clip != null) {
                    writingAudioSource.time = 0f;
                }
            }

            hasLastPenAudioPosition = false;
            currentPenSpeed = 0f;
        }

        private void UpdateWritingAudio() {
            if (writingAudioSource == null) return;

            AudioClip activeClip = GetActiveWritingClip();
            if (activeClip == null) return;

            bool shouldPlay = paperProgress != null && paperProgress.IsWritingActive() && penTip != null;
            if (!shouldPlay) {
                StopWritingAudio(resetPlaybackPosition: false);
                smoothedPenSpeed = Mathf.Lerp(smoothedPenSpeed, 0f, Time.deltaTime * audioSmoothing);
                ApplyAudioPitchAndVolume(forcePitch ? forcedPitch : 1f, forcePitch ? writingVolume : writingVolume);
                if (!forcePitch) {
                    writingAudioSource.pitch = 1f;
                    writingAudioSource.volume = writingVolume;
                }
                return;
            }

            if (writingAudioSource.clip != activeClip) {
                bool wasPlaying = writingAudioSource.isPlaying;
                writingAudioSource.Stop();
                writingAudioSource.clip = activeClip;
                writingAudioSource.time = 0f;
                if (wasPlaying) writingAudioSource.Play();
            }

            writingAudioSource.loop = true;
            writingAudioSource.volume = writingVolume;

            if (!writingAudioSource.isPlaying) {
                if (resumeWritingAudioFromLastPosition && writingAudioSource.time > 0f) {
                    writingAudioSource.UnPause();
                }
                else {
                    writingAudioSource.Play();
                }
            }

            UpdatePenSpeedForAudio();

            if (forcePitch) {
                writingAudioSource.pitch = Mathf.Clamp(forcedPitch, 0.01f, 3f);
                writingAudioSource.volume = writingVolume;
                return;
            }

            float t = Mathf.Clamp01(smoothedPenSpeed / Mathf.Max(0.001f, speedForMaxPitch));
            writingAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, t);
            writingAudioSource.volume = Mathf.Lerp(writingVolume * 0.75f, writingVolume, t);
        }

        private void UpdatePenSpeedForAudio() {
            Vector3 currentPos = penTip.position;

            if (!hasLastPenAudioPosition) {
                lastPenAudioPosition = currentPos;
                hasLastPenAudioPosition = true;
                currentPenSpeed = 0f;
            }
            else {
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                currentPenSpeed = Vector3.Distance(lastPenAudioPosition, currentPos) / dt;
                currentPenSpeed *= paperProgress != null ? paperProgress.GetCurrentWritingSpeedMultiplier() : 1f;
                currentPenSpeed = Mathf.Min(currentPenSpeed, speedForMaxPitch * 1.5f);
                lastPenAudioPosition = currentPos;
            }

            smoothedPenSpeed = Mathf.Lerp(smoothedPenSpeed, currentPenSpeed, Time.deltaTime * audioSmoothing);
        }

        private void ApplyAudioPitchAndVolume(float pitch, float volume) {
            if (writingAudioSource == null) return;
            writingAudioSource.pitch = Mathf.Clamp(pitch, 0.01f, 3f);
            writingAudioSource.volume = volume;
        }

        private void RefreshInkSettings() {
            inkColor32 = inkColor;

            float pixelsPerUnitX = textureWidth / Mathf.Max(0.0001f, pageSize.x);
            float pixelsPerUnitY = textureHeight / Mathf.Max(0.0001f, pageSize.y);
            float pixelsPerUnit = (pixelsPerUnitX + pixelsPerUnitY) * 0.5f;

            inkRadiusPixels = Mathf.Max(1, Mathf.RoundToInt(lineWidth * 0.5f * pixelsPerUnit));
            RebuildBrushOffsets();
        }

        private void RebuildBrushOffsets() {
            brushOffsets.Clear();
            int r2 = inkRadiusPixels * inkRadiusPixels;

            for (int y = -inkRadiusPixels; y <= inkRadiusPixels; y++) {
                for (int x = -inkRadiusPixels; x <= inkRadiusPixels; x++) {
                    if (x * x + y * y <= r2) {
                        brushOffsets.Add(new Vector2Int(x, y));
                    }
                }
            }

            brushOffsetCount = brushOffsets.Count;
        }

        private List<TextToken> Tokenize(string text) {
            List<TextToken> tokens = new();
            if (string.IsNullOrEmpty(text)) return tokens;

            StringBuilder sb = new();

            void FlushWord() {
                if (sb.Length == 0) return;
                tokens.Add(new TextToken { type = TokenType.Word, text = sb.ToString() });
                sb.Clear();
            }

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (c == '\r') continue;

                if (c == '\n') {
                    FlushWord();
                    tokens.Add(new TextToken { type = TokenType.Newline, text = "\n" });
                    continue;
                }

                if (char.IsWhiteSpace(c)) {
                    FlushWord();
                    continue;
                }

                sb.Append(c);
            }

            FlushWord();
            return tokens;
        }

        private void BuildTextGeometry(List<TextToken> tokens) {
            float unitsPerGlyphHeight = lineHeight * glyphScale;
            float lineAdvance = lineHeight * lineSpacingMultiplier;
            float z = pageOrigin.y;
            float x = pageOrigin.x + lineHeight;
            BuiltLine currentLine = new();

            void FinalizeLine() {
                if (currentLine.segments.Count == 0 && currentLine.lineLength <= 0f) return;

                currentLine.globalStartLength = totalLength;
                currentLine.globalEndLength = totalLength + currentLine.lineLength;
                totalLength = currentLine.globalEndLength;
                builtLines.Add(currentLine);
                currentLine = new BuiltLine();
            }

            void AdvanceLine() {
                FinalizeLine();
                z = pageOrigin.y;
                x += lineAdvance;
            }

            for (int i = 0; i < tokens.Count; i++) {
                TextToken token = tokens[i];
                if (token.type == TokenType.Newline) {
                    AdvanceLine();
                    continue;
                }

                float wordWidth = MeasureWordWidth(token.text, unitsPerGlyphHeight);
                bool atLineStart = Mathf.Abs(z - pageOrigin.y) < 0.0001f;

                if (!atLineStart && z + wordWidth > pageOrigin.y + pageSize.y) {
                    AdvanceLine();
                }

                if (x > pageOrigin.x + pageSize.x) {
                    if (verboseLogging) {
                        Debug.LogWarning("PaperWritingStrokeDisplay: Text overflowed page bounds.", this);
                    }
                    break;
                }

                for (int charIndex = 0; charIndex < token.text.Length; charIndex++) {
                    char c = token.text[charIndex];

                    if (!glyphLibrary.TryGetGlyph(c, out StrokeGlyphLibrary.RuntimeGlyph glyph)) {
                        z += 0.5f * unitsPerGlyphHeight;
                        if (charIndex < token.text.Length - 1) {
                            z += characterSpacing * unitsPerGlyphHeight;
                        }
                        continue;
                    }

                    for (int s = 0; s < glyph.strokes.Count; s++) {
                        var stroke = glyph.strokes[s];
                        if (stroke == null || stroke.points == null || stroke.points.Count < 2) continue;

                        Vector2[] pagePoints = new Vector2[stroke.points.Count];
                        for (int p = 0; p < stroke.points.Count; p++) {
                            Vector2 gp = stroke.points[p].position;
                            pagePoints[p] = new Vector2(
                                x - gp.y * unitsPerGlyphHeight,
                                z + gp.x * unitsPerGlyphHeight
                            );
                        }

                        AddBuiltStroke(currentLine, pagePoints);
                    }

                    z += glyph.advance * unitsPerGlyphHeight;
                    if (charIndex < token.text.Length - 1) {
                        z += characterSpacing * unitsPerGlyphHeight;
                    }
                }

                bool hasNextWord = i + 1 < tokens.Count && tokens[i + 1].type == TokenType.Word;
                if (hasNextWord) {
                    z += wordSpacing * unitsPerGlyphHeight;
                }
            }

            FinalizeLine();
        }

        private float MeasureWordWidth(string word, float unitsPerGlyphHeight) {
            if (string.IsNullOrEmpty(word)) return 0f;

            float width = 0f;
            for (int i = 0; i < word.Length; i++) {
                if (glyphLibrary.TryGetGlyph(word[i], out StrokeGlyphLibrary.RuntimeGlyph glyph)) {
                    width += glyph.advance * unitsPerGlyphHeight;
                }
                else {
                    width += 0.5f * unitsPerGlyphHeight;
                }

                if (i < word.Length - 1) {
                    width += characterSpacing * unitsPerGlyphHeight;
                }
            }

            return width;
        }

        private void AddBuiltStroke(BuiltLine line, Vector2[] pagePoints) {
            if (line == null || pagePoints == null || pagePoints.Length < 2) return;

            bool addedAnySegment = false;
            for (int i = 1; i < pagePoints.Length; i++) {
                Vector2 a = pagePoints[i - 1];
                Vector2 b = pagePoints[i];
                float len = Vector2.Distance(a, b);
                if (len <= 0.000001f) continue;

                line.segments.Add(new DrawSegment {
                    pageA = a,
                    pageB = b,
                    pixelA = PageToPixelFloat(a),
                    pixelB = PageToPixelFloat(b),
                    localStartLength = line.lineLength,
                    localEndLength = line.lineLength + len,
                    length = len
                });

                line.lineLength += len;
                addedAnySegment = true;
            }

            if (addedAnySegment) builtStrokeCount++;
        }

        private void EnsureInkQuad() {
            if (inkQuad == null) {
                inkQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                inkQuad.name = "InkOverlay";
                inkQuad.transform.SetParent(overlayRoot != null ? overlayRoot : transform, false);

                Collider col = inkQuad.GetComponent<Collider>();
                if (col != null) {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
            }

            inkQuad.transform.localPosition = new Vector3(
                pageOrigin.x + pageSize.x * 0.5f,
                localSurfaceOffset,
                pageOrigin.y + pageSize.y * 0.5f
            );
            inkQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            inkQuad.transform.localScale = new Vector3(pageSize.x, pageSize.y, 1f);

            if (inkTexture == null || inkTexture.width != textureWidth || inkTexture.height != textureHeight) {
                inkTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false, true) {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                inkPixels = new Color32[textureWidth * textureHeight];
            }

            if (inkMaterial == null) {
                Shader shader = Shader.Find(overlayShaderName) ?? Shader.Find("Sprites/Default");
                inkMaterial = new Material(shader) { name = "InkOverlay_Mat" };
            }

            inkMaterial.mainTexture = inkTexture;
            inkRenderer = inkQuad.GetComponent<Renderer>();
            if (inkRenderer != null) {
                inkRenderer.sharedMaterial = inkMaterial;
            }
        }

        private void ClearInkTexture() {
            EnsureInkQuad();
            Array.Fill(inkPixels, new Color32(0, 0, 0, 0));
            inkTexture.SetPixels32(inkPixels);
            inkTexture.Apply(false, false);
            inkDirty = false;
        }

        private void UploadInkTexture() {
            if (inkTexture == null || inkPixels == null) return;
            inkTexture.SetPixels32(inkPixels);
            inkTexture.Apply(false, false);
            inkDirty = false;
            nextInkApplyTime = Time.time + (1f / Mathf.Max(1f, inkApplyRate));
        }

        private void DrawRangeAcrossLines(float fromLength, float toLength) {
            if (builtLines.Count == 0 || toLength <= fromLength) return;

            while (revealCursorLineIndex < builtLines.Count) {
                BuiltLine line = builtLines[revealCursorLineIndex];

                if (line.globalEndLength <= fromLength) {
                    revealCursorLineIndex++;
                    revealCursorSegmentIndex = 0;
                    continue;
                }

                if (line.globalStartLength >= toLength) break;

                float localFrom = Mathf.Max(0f, fromLength - line.globalStartLength);
                float localTo = Mathf.Min(line.lineLength, toLength - line.globalStartLength);

                if (localTo > localFrom) {
                    DrawRangeInCurrentLine(line, localFrom, localTo);
                }

                if (localTo >= line.lineLength - 0.0001f) {
                    revealCursorLineIndex++;
                    revealCursorSegmentIndex = 0;
                    continue;
                }

                break;
            }
        }

        private void DrawRangeInCurrentLine(BuiltLine line, float fromLength, float toLength) {
            if (line == null || line.segments.Count == 0 || toLength <= fromLength) return;

            while (revealCursorSegmentIndex < line.segments.Count &&
                   line.segments[revealCursorSegmentIndex].localEndLength <= fromLength) {
                revealCursorSegmentIndex++;
            }

            int i = revealCursorSegmentIndex;
            while (i < line.segments.Count) {
                DrawSegment seg = line.segments[i];
                if (seg.localStartLength >= toLength) break;

                float segFrom = Mathf.Max(fromLength, seg.localStartLength);
                float segTo = Mathf.Min(toLength, seg.localEndLength);
                if (segTo > segFrom) {
                    DrawSegmentRange(seg, segFrom, segTo);
                }

                i++;
            }

            while (revealCursorSegmentIndex < line.segments.Count &&
                   line.segments[revealCursorSegmentIndex].localEndLength <= toLength) {
                revealCursorSegmentIndex++;
            }
        }

        private void DrawSegmentRange(DrawSegment seg, float fromLocal, float toLocal) {
            if (seg.length <= 0.000001f || toLocal <= fromLocal) return;

            float t0 = Mathf.InverseLerp(seg.localStartLength, seg.localEndLength, fromLocal);
            float t1 = Mathf.InverseLerp(seg.localStartLength, seg.localEndLength, toLocal);
            Vector2 a = Vector2.Lerp(seg.pixelA, seg.pixelB, t0);
            Vector2 b = Vector2.Lerp(seg.pixelA, seg.pixelB, t1);

            DrawThickLine(
                Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y),
                Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.y)
            );
        }

        private Vector2 PageToPixelFloat(Vector2 pagePoint) {
            float u = Mathf.Clamp01(Mathf.InverseLerp(pageOrigin.x, pageOrigin.x + pageSize.x, pagePoint.x));
            float v = Mathf.Clamp01(Mathf.InverseLerp(pageOrigin.y, pageOrigin.y + pageSize.y, pagePoint.y));
            return new Vector2(u * (textureWidth - 1), v * (textureHeight - 1));
        }

        private void DrawThickLine(int x0, int y0, int x1, int y1) {
            float dx = x1 - x0;
            float dy = y1 - y0;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist <= 0.0001f) {
                StampBrush(x0, y0);
                return;
            }

            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(0.25f, brushSpacingPixels)));
            for (int i = 0; i <= steps; i++) {
                float t = i / (float)steps;
                StampBrush(
                    Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)),
                    Mathf.RoundToInt(Mathf.Lerp(y0, y1, t))
                );
            }
        }

        private void StampBrush(int cx, int cy) {
            for (int i = 0; i < brushOffsets.Count; i++) {
                Vector2Int o = brushOffsets[i];
                int x = cx + o.x;
                int y = cy + o.y;
                if ((uint)x >= textureWidth || (uint)y >= textureHeight) continue;
                inkPixels[y * textureWidth + x] = inkColor32;
            }

            inkDirty = true;
        }

        private void UpdateOverlayVisibility() {
            if (!onlyRenderOverlayOnPaperView || inkRenderer == null) return;
            inkRenderer.enabled = IsCurrentlyOnPaperView();
        }

        private bool IsCurrentlyOnPaperView() {
            if (viewController == null) return true;

            View currentView = viewController.CurrentView;
            if (currentView == null) return false;
            if (paperView != null) return currentView == paperView;

            return !string.IsNullOrEmpty(paperViewNameFallback) &&
                   string.Equals(currentView.gameObject.name, paperViewNameFallback, StringComparison.OrdinalIgnoreCase);
        }

        private int GetLineIndexAtGlobalLength(float globalLength) {
            if (builtLines.Count == 0) return -1;

            globalLength = Mathf.Clamp(globalLength, 0f, totalLength);
            for (int i = 0; i < builtLines.Count; i++) {
                if (globalLength <= builtLines[i].globalEndLength) {
                    return i;
                }
            }

            return builtLines.Count - 1;
        }

        private Vector2 GetPagePointAtGlobalLength(float length) {
            if (builtLines.Count == 0) return Vector2.zero;

            length = Mathf.Clamp(length, 0f, totalLength);
            int lineIndex = GetLineIndexAtGlobalLength(length);
            if (lineIndex < 0) return Vector2.zero;

            BuiltLine line = builtLines[lineIndex];
            float localLength = Mathf.Clamp(length - line.globalStartLength, 0f, line.lineLength);

            for (int i = 0; i < line.segments.Count; i++) {
                DrawSegment seg = line.segments[i];
                if (localLength <= seg.localEndLength) {
                    float t = Mathf.InverseLerp(seg.localStartLength, seg.localEndLength, localLength);
                    return Vector2.Lerp(seg.pageA, seg.pageB, t);
                }
            }

            return line.segments.Count > 0 ? line.segments[^1].pageB : Vector2.zero;
        }

        public Vector3 GetWorldPointAtCurrentRevealLengthOrStart() {
            if (builtLines.Count == 0) {
                return transform.TransformPoint(new Vector3(pageOrigin.x, localSurfaceOffset, pageOrigin.y));
            }

            if (currentRevealLength <= 0.0001f && builtLines[0].segments.Count > 0) {
                Vector2 start = builtLines[0].segments[0].pageA;
                return transform.TransformPoint(new Vector3(start.x, localSurfaceOffset, start.y));
            }

            Vector2 pagePos = GetPagePointAtGlobalLength(currentRevealLength);
            return transform.TransformPoint(new Vector3(pagePos.x, localSurfaceOffset, pagePos.y));
        }

        public int GetCurrentLineIndex() {
            return GetLineIndexAtGlobalLength(currentRevealLength);
        }

        private void OnDrawGizmosSelected() {
            if (!drawPageBoundsGizmo) return;

            Gizmos.color = pageBoundsColor;
            Vector3 bl = transform.TransformPoint(new Vector3(pageOrigin.x, localSurfaceOffset, pageOrigin.y));
            Vector3 br = transform.TransformPoint(new Vector3(pageOrigin.x + pageSize.x, localSurfaceOffset, pageOrigin.y));
            Vector3 tl = transform.TransformPoint(new Vector3(pageOrigin.x, localSurfaceOffset, pageOrigin.y + pageSize.y));
            Vector3 tr = transform.TransformPoint(new Vector3(pageOrigin.x + pageSize.x, localSurfaceOffset, pageOrigin.y + pageSize.y));

            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
    }
}