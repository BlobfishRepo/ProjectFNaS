using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FNaS/Stroke Glyph Library")]
public class StrokeGlyphLibrary : ScriptableObject {
    [Header("Source")]
    public TextAsset jsonFile;

    private Dictionary<char, RuntimeGlyph> glyphMap;
    private bool isLoaded;

    [Serializable]
    public class GlyphCollection {
        public List<Glyph> glyphs;
    }

    [Serializable]
    public class Glyph {
        public string character;
        public float advance = 1f;
        public List<Stroke> strokes;
    }

    [Serializable]
    public class Stroke {
        public List<Vector2Wrapper> points;
    }

    [Serializable]
    public class Vector2Wrapper {
        public float x;
        public float y;

        public Vector2 ToVector2() => new Vector2(x, y);
    }

    public void Preload() {
        EnsureLoaded();
    }

    public bool TryGetGlyph(char c, out RuntimeGlyph glyph) {
        EnsureLoaded();

        if (glyphMap != null && glyphMap.TryGetValue(c, out glyph)) {
            return true;
        }

        glyph = null;
        return false;
    }

    private void EnsureLoaded() {
        if (isLoaded && glyphMap != null) {
            return;
        }

        Load();
    }

    private void Load() {
        glyphMap = new Dictionary<char, RuntimeGlyph>();

        if (jsonFile == null) {
            Debug.LogError("StrokeGlyphLibrary: No JSON file assigned.", this);
            isLoaded = true;
            return;
        }

        GlyphCollection collection = null;

        try {
            collection = JsonUtility.FromJson<GlyphCollection>(jsonFile.text);
        }
        catch (Exception ex) {
            Debug.LogError($"StrokeGlyphLibrary: Failed to parse glyph JSON. Exception: {ex}", this);
            isLoaded = true;
            return;
        }

        if (collection == null || collection.glyphs == null) {
            Debug.LogError("StrokeGlyphLibrary: Parsed JSON was null or missing glyphs list.", this);
            isLoaded = true;
            return;
        }

        foreach (var g in collection.glyphs) {
            if (g == null || string.IsNullOrEmpty(g.character)) {
                continue;
            }

            char c = g.character[0];
            glyphMap[c] = Convert(g);
        }

        isLoaded = true;
    }

    private RuntimeGlyph Convert(Glyph g) {
        var runtime = new RuntimeGlyph {
            advance = g != null ? g.advance : 1f,
            strokes = new List<RuntimeStroke>()
        };

        if (g == null || g.strokes == null) {
            return runtime;
        }

        foreach (var s in g.strokes) {
            if (s == null || s.points == null) {
                continue;
            }

            var rs = new RuntimeStroke {
                points = new List<RuntimePoint>()
            };

            foreach (var p in s.points) {
                if (p == null) {
                    continue;
                }

                rs.points.Add(new RuntimePoint {
                    position = p.ToVector2()
                });
            }

            runtime.strokes.Add(rs);
        }

        return runtime;
    }

    public class RuntimeGlyph {
        public float advance;
        public List<RuntimeStroke> strokes;
    }

    public class RuntimeStroke {
        public List<RuntimePoint> points;
    }

    public class RuntimePoint {
        public Vector2 position;
    }
}