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
            public float globalStartLength;
            public float globalEndLength;
            public float length;
        }

        private enum TokenType {
            Word,
            Newline
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
        public Vector2 pageOrigin = new Vector2(-0.44f, -0.45f);
        public Vector2 pageSize = new Vector2(0.88f, 0.9f);

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
        [Tooltip("How often the ink texture is uploaded to the GPU while writing.")]
        [Min(1f)] public float inkApplyRate = 20f;

        [Tooltip("Distance in pixels between brush stamps along a line. Higher = faster, but more dotted if pushed too far.")]
        [Min(0.25f)] public float brushSpacingPixels = 1.5f;

        [Header("Pen")]
        public Transform penTip;
        public bool hidePenWhenNotWriting = true;

        [Header("Behavior")]
        public bool rebuildOnStart = true;
        public bool uppercaseInput = true;
        public bool verboseLogging = false;

        [Header("Gizmos")]
        public bool drawPageBoundsGizmo = true;
        public Color pageBoundsColor = Color.cyan;

        [Header("Writing Audio")]
        public AudioSource writingAudioSource;
        public AudioClip writingLoopClip;
        [Range(0f, 1f)] public float writingVolume = 0.8f;
        public float minPitch = 0.85f;
        public float maxPitch = 1.35f;
        [Tooltip("World-units-per-second that maps to max pitch.")]
        public float speedForMaxPitch = 0.8f;
        [Tooltip("How quickly pitch responds to speed changes.")]
        public float audioSmoothing = 10f;
        [Tooltip("If enabled, stopping writing pauses the loop so it resumes from the same playback position next time.")]
        public bool resumeWritingAudioFromLastPosition = true;

        [Header("Runtime (read-only)")]
        [SerializeField] private int builtStrokeCount;
        [SerializeField] private int builtSegmentCount;
        [SerializeField] private float totalLength;
        [SerializeField] private float currentRevealLength;
        [SerializeField] private string resolvedTextPreview;

        [Header("Runtime Perf (read-only)")]
        [SerializeField] private int inkRadiusPixels = 1;
        [SerializeField] private int brushOffsetCount = 0;
        [SerializeField] private int revealCursorSegmentIndex = 0;

        [Header("Runtime Audio (read-only)")]
        [SerializeField] private float currentPenSpeed;
        [SerializeField] private float smoothedPenSpeed;

        private readonly List<DrawSegment> drawSegments = new();
        private readonly List<Vector2Int> brushOffsets = new();

        private Vector3 penInitialPosition;
        private Quaternion penInitialRotation;
        private bool cachedPenInitialPose;

        private Vector3 lastPenAudioPosition;
        private bool hasLastPenAudioPosition;

        private GameObject inkQuad;
        private Material inkMaterial;
        private Renderer inkRenderer;
        private Texture2D inkTexture;
        private Color32[] inkPixels;
        private Color32 inkColor32;
        private bool inkDirty;

        private float lastRevealLength = 0f;
        private float nextInkApplyTime = 0f;

        private void Start() {
            CachePenInitialPose();
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
            resumeWritingAudioFromLastPosition = settings.GetBool("paper.resumeWritingAudioFromLastPosition");

            if (Application.isPlaying) {
                Rebuild();
            }
        }

        private void CachePenInitialPose() {
            if (penTip == null || cachedPenInitialPose) return;

            penInitialPosition = penTip.position;
            penInitialRotation = penTip.rotation;
            cachedPenInitialPose = true;
        }

        private void SetupWritingAudio() {
            if (writingAudioSource != null) {
                writingAudioSource.playOnAwake = false;
                writingAudioSource.loop = true;
                writingAudioSource.clip = writingLoopClip;
                writingAudioSource.volume = writingVolume;
                writingAudioSource.pitch = 1f;
            }
        }

        private void Update() {
            UpdateOverlayVisibility();

            float progress01 = paperProgress != null ? paperProgress.GetProgress01() : 0f;
            currentRevealLength = totalLength * Mathf.Clamp01(progress01);

            if (currentRevealLength < lastRevealLength - 0.0001f) {
                ClearInkTexture();
                revealCursorSegmentIndex = 0;
                DrawRange(0f, currentRevealLength);
            }
            else if (currentRevealLength > lastRevealLength + 0.0001f) {
                DrawRange(lastRevealLength, currentRevealLength);
            }

            lastRevealLength = currentRevealLength;

            bool writingActive = paperProgress != null && paperProgress.IsWritingActive();

            if (inkDirty && Time.time >= nextInkApplyTime) {
                UploadInkTexture();
            }
            else if (inkDirty && !writingActive) {
                UploadInkTexture();
            }

            UpdatePen(currentRevealLength);
            UpdateWritingAudio();
        }

        [ContextMenu("Rebuild Writing Geometry")]
        public void Rebuild() {
            drawSegments.Clear();
            totalLength = 0f;
            currentRevealLength = 0f;
            lastRevealLength = 0f;
            builtStrokeCount = 0;
            builtSegmentCount = 0;
            revealCursorSegmentIndex = 0;
            nextInkApplyTime = 0f;

            EnsureInkQuad();
            RefreshInkSettings();
            ClearInkTexture();

            if (glyphLibrary == null) {
                Debug.LogWarning("PaperWritingStrokeDisplay: No glyph library assigned.", this);
                UpdateOverlayVisibility();
                return;
            }

            string workingText = textToWrite ?? string.Empty;

            if (uppercaseInput) {
                workingText = workingText.ToUpperInvariant();
            }

            if (workingText.Length > maxCharacters) {
                workingText = workingText.Substring(0, maxCharacters);

                if (verboseLogging) {
                    Debug.LogWarning($"PaperWritingStrokeDisplay: Text truncated to {maxCharacters} characters.", this);
                }
            }

            resolvedTextPreview = workingText;

            List<TextToken> tokens = Tokenize(workingText);
            BuildTextGeometry(tokens);

            builtSegmentCount = drawSegments.Count;
            UpdateOverlayVisibility();

            if (verboseLogging) {
                Debug.Log(
                    $"Built {builtStrokeCount} strokes / {builtSegmentCount} segments. " +
                    $"Total length = {totalLength:F3}. " +
                    $"LineHeight={lineHeight:F3}, GlyphScale={glyphScale:F3}, " +
                    $"BrushRadius={inkRadiusPixels}, BrushOffsets={brushOffsetCount}",
                    this
                );
            }
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

                tokens.Add(new TextToken {
                    type = TokenType.Word,
                    text = sb.ToString()
                });

                sb.Clear();
            }

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];

                if (c == '\r') continue;

                if (c == '\n') {
                    FlushWord();
                    tokens.Add(new TextToken {
                        type = TokenType.Newline,
                        text = "\n"
                    });
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

            for (int i = 0; i < tokens.Count; i++) {
                TextToken token = tokens[i];

                if (token.type == TokenType.Newline) {
                    z = pageOrigin.y;
                    x += lineAdvance;
                    continue;
                }

                float wordWidth = MeasureWordWidth(token.text, unitsPerGlyphHeight);

                bool atLineStart = Mathf.Abs(z - pageOrigin.y) < 0.0001f;
                if (!atLineStart && z + wordWidth > pageOrigin.y + pageSize.y) {
                    z = pageOrigin.y;
                    x += lineAdvance;
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

                        Vector2[] pagePts = new Vector2[stroke.points.Count];

                        for (int p = 0; p < stroke.points.Count; p++) {
                            Vector2 gp = stroke.points[p].position;

                            float pz = z + gp.x * unitsPerGlyphHeight;
                            float px = x - gp.y * unitsPerGlyphHeight;

                            pagePts[p] = new Vector2(px, pz);
                        }

                        AddBuiltStroke(pagePts);
                    }

                    z += glyph.advance * unitsPerGlyphHeight;

                    if (charIndex < token.text.Length - 1) {
                        z += characterSpacing * unitsPerGlyphHeight;
                    }
                }

                bool hasNextWord =
                    i + 1 < tokens.Count &&
                    tokens[i + 1].type == TokenType.Word;

                if (hasNextWord) {
                    z += wordSpacing * unitsPerGlyphHeight;
                }
            }
        }

        private float MeasureWordWidth(string word, float unitsPerGlyphHeight) {
            if (string.IsNullOrEmpty(word)) return 0f;

            float width = 0f;

            for (int i = 0; i < word.Length; i++) {
                char c = word[i];

                if (glyphLibrary.TryGetGlyph(c, out StrokeGlyphLibrary.RuntimeGlyph glyph)) {
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

        private void AddBuiltStroke(Vector2[] pagePoints) {
            if (pagePoints == null || pagePoints.Length < 2) return;

            bool addedAnySegment = false;

            for (int i = 1; i < pagePoints.Length; i++) {
                Vector2 a = pagePoints[i - 1];
                Vector2 b = pagePoints[i];

                float len = Vector2.Distance(a, b);
                if (len <= 0.000001f) continue;

                drawSegments.Add(new DrawSegment {
                    pageA = a,
                    pageB = b,
                    pixelA = PageToPixelFloat(a),
                    pixelB = PageToPixelFloat(b),
                    globalStartLength = totalLength,
                    globalEndLength = totalLength + len,
                    length = len
                });

                totalLength += len;
                addedAnySegment = true;
            }

            if (addedAnySegment) {
                builtStrokeCount++;
            }
        }

        private void EnsureInkQuad() {
            if (inkQuad == null) {
                inkQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                inkQuad.name = "InkOverlay";
                inkQuad.transform.SetParent(overlayRoot != null ? overlayRoot : transform, false);

                Collider col = inkQuad.GetComponent<Collider>();
                if (col != null) {
                    if (Application.isPlaying) {
                        Destroy(col);
                    }
                    else {
                        DestroyImmediate(col);
                    }
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
                inkTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false, true);
                inkTexture.wrapMode = TextureWrapMode.Clamp;
                inkTexture.filterMode = FilterMode.Bilinear;
                inkPixels = new Color32[textureWidth * textureHeight];
            }

            if (inkMaterial == null) {
                Shader shader = Shader.Find(overlayShaderName);
                if (shader == null) {
                    shader = Shader.Find("Sprites/Default");
                }

                inkMaterial = new Material(shader);
                inkMaterial.name = "InkOverlay_Mat";
            }

            inkMaterial.mainTexture = inkTexture;

            inkRenderer = inkQuad.GetComponent<Renderer>();
            if (inkRenderer != null) {
                inkRenderer.sharedMaterial = inkMaterial;
            }
        }

        private void ClearInkTexture() {
            EnsureInkQuad();

            Color32 clear = new Color32(0, 0, 0, 0);
            Array.Fill(inkPixels, clear);

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

        private void DrawRange(float fromLength, float toLength) {
            if (drawSegments.Count == 0) return;
            if (toLength <= fromLength) return;

            if (fromLength <= 0f) {
                revealCursorSegmentIndex = 0;
            }

            while (revealCursorSegmentIndex < drawSegments.Count &&
                   drawSegments[revealCursorSegmentIndex].globalEndLength <= fromLength) {
                revealCursorSegmentIndex++;
            }

            int i = revealCursorSegmentIndex;

            while (i < drawSegments.Count) {
                DrawSegment seg = drawSegments[i];

                if (seg.globalStartLength >= toLength) {
                    break;
                }

                float segFrom = Mathf.Max(fromLength, seg.globalStartLength);
                float segTo = Mathf.Min(toLength, seg.globalEndLength);

                if (segTo > segFrom) {
                    DrawSegmentRange(seg, segFrom, segTo);
                }

                i++;
            }

            while (revealCursorSegmentIndex < drawSegments.Count &&
                   drawSegments[revealCursorSegmentIndex].globalEndLength <= toLength) {
                revealCursorSegmentIndex++;
            }
        }

        private void DrawSegmentRange(DrawSegment seg, float fromGlobal, float toGlobal) {
            if (seg.length <= 0.000001f || toGlobal <= fromGlobal) return;

            float t0 = Mathf.InverseLerp(seg.globalStartLength, seg.globalEndLength, fromGlobal);
            float t1 = Mathf.InverseLerp(seg.globalStartLength, seg.globalEndLength, toGlobal);

            Vector2 a = Vector2.Lerp(seg.pixelA, seg.pixelB, t0);
            Vector2 b = Vector2.Lerp(seg.pixelA, seg.pixelB, t1);

            DrawThickLine(
                Mathf.RoundToInt(a.x),
                Mathf.RoundToInt(a.y),
                Mathf.RoundToInt(b.x),
                Mathf.RoundToInt(b.y)
            );
        }

        private Vector2 PageToPixelFloat(Vector2 pagePoint) {
            float u = Mathf.InverseLerp(pageOrigin.x, pageOrigin.x + pageSize.x, pagePoint.x);
            float v = Mathf.InverseLerp(pageOrigin.y, pageOrigin.y + pageSize.y, pagePoint.y);

            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);

            return new Vector2(
                u * (textureWidth - 1),
                v * (textureHeight - 1)
            );
        }

        private void DrawThickLine(int x0, int y0, int x1, int y1) {
            float dx = x1 - x0;
            float dy = y1 - y0;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist <= 0.0001f) {
                StampBrush(x0, y0);
                return;
            }

            float spacing = Mathf.Max(0.25f, brushSpacingPixels);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / spacing));

            for (int i = 0; i <= steps; i++) {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                StampBrush(x, y);
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
            if (!onlyRenderOverlayOnPaperView) return;
            if (inkRenderer == null) return;

            inkRenderer.enabled = IsCurrentlyOnPaperView();
        }

        private bool IsCurrentlyOnPaperView() {
            if (viewController == null) return true;

            View currentView = viewController.CurrentView;
            if (currentView == null) return false;

            if (paperView != null) {
                return currentView == paperView;
            }

            if (!string.IsNullOrEmpty(paperViewNameFallback)) {
                return string.Equals(
                    currentView.gameObject.name,
                    paperViewNameFallback,
                    StringComparison.OrdinalIgnoreCase
                );
            }

            return false;
        }

        private void UpdatePen(float revealLength) {
            if (penTip == null) return;

            bool showPen = paperProgress != null &&
                           (paperProgress.IsWritingActive() || paperProgress.IsInPickupDelay());

            if (hidePenWhenNotWriting) {
                penTip.gameObject.SetActive(showPen);
            }

            if (!showPen) {
                RestorePenPoseIfNeeded();
                return;
            }

            if (revealLength <= 0f || totalLength <= 0f || drawSegments.Count == 0) {
                return;
            }

            Vector2 pagePos = GetPagePointAtGlobalLength(revealLength);
            Vector3 localPos = new Vector3(pagePos.x, localSurfaceOffset, pagePos.y);
            penTip.position = transform.TransformPoint(localPos);
        }

        private Vector2 GetPagePointAtGlobalLength(float length) {
            if (drawSegments.Count == 0) return Vector2.zero;

            length = Mathf.Clamp(length, 0f, totalLength);

            for (int i = 0; i < drawSegments.Count; i++) {
                DrawSegment seg = drawSegments[i];

                if (length <= seg.globalEndLength) {
                    float t = Mathf.InverseLerp(seg.globalStartLength, seg.globalEndLength, length);
                    return Vector2.Lerp(seg.pageA, seg.pageB, t);
                }
            }

            return drawSegments[drawSegments.Count - 1].pageB;
        }

        private void RestorePenPoseIfNeeded() {
            if (penTip == null || !cachedPenInitialPose) return;

            penTip.position = penInitialPosition;
            penTip.rotation = penInitialRotation;
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
            if (writingAudioSource == null || writingLoopClip == null) return;

            bool shouldPlay =
                paperProgress != null &&
                paperProgress.IsWritingActive() &&
                penTip != null;

            if (!shouldPlay) {
                if (writingAudioSource.isPlaying) {
                    writingAudioSource.Pause();
                }

                hasLastPenAudioPosition = false;
                currentPenSpeed = 0f;
                smoothedPenSpeed = Mathf.Lerp(smoothedPenSpeed, 0f, Time.deltaTime * audioSmoothing);
                return;
            }

            writingAudioSource.clip = writingLoopClip;
            writingAudioSource.loop = true;
            writingAudioSource.volume = writingVolume;

            if (!writingAudioSource.isPlaying) {
                writingAudioSource.clip = writingLoopClip;
                writingAudioSource.volume = writingVolume;

                if (writingAudioSource.time > 0f) {
                    writingAudioSource.UnPause();
                }
                else {
                    writingAudioSource.Play();
                }
            }

            Vector3 currentPos = penTip.position;

            if (!hasLastPenAudioPosition) {
                lastPenAudioPosition = currentPos;
                hasLastPenAudioPosition = true;
                currentPenSpeed = 0f;
            }
            else {
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                currentPenSpeed = Vector3.Distance(lastPenAudioPosition, currentPos) / dt;
                currentPenSpeed = Mathf.Min(currentPenSpeed, speedForMaxPitch * 1.5f);
                lastPenAudioPosition = currentPos;
            }

            smoothedPenSpeed = Mathf.Lerp(smoothedPenSpeed, currentPenSpeed, Time.deltaTime * audioSmoothing);

            float t = Mathf.Clamp01(smoothedPenSpeed / Mathf.Max(0.001f, speedForMaxPitch));
            writingAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, t);
            writingAudioSource.volume = Mathf.Lerp(writingVolume * 0.75f, writingVolume, t);
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