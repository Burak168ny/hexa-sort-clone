using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HexaSort.Editor
{
    /// <summary>
    /// Bakes the few UI sprites the project needs (rounded rect, circle, gear, hexagon) from signed
    /// distance functions into PNGs, so the repository ships without external art.
    /// </summary>
    public static class SpriteBaker
    {
        public enum Shape { RoundedRect, Circle, Gear, Hexagon, Hammer, Move, Shuffle, Glow, Rays }

        private const string Folder = "Assets/_Project/Art/UI";

        public static Sprite GetOrBake(Shape shape)
        {
            string path = $"{Folder}/{shape}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(Folder);
            int size = shape == Shape.RoundedRect ? 96 : 128;
            var texture = shape == Shape.Glow || shape == Shape.Rays ? RenderAlpha(shape, 128) : Render(SdfFor(shape), size);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = shape == Shape.Glow;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100;
            if (shape == Shape.RoundedRect) importer.spriteBorder = new Vector4(40, 40, 40, 40);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Signed distance in normalized units (p in [-1, 1]), negative inside.</summary>
        private static Func<Vector2, float> SdfFor(Shape shape)
        {
            switch (shape)
            {
                case Shape.Circle:
                    return p => p.magnitude - 0.92f;
                case Shape.RoundedRect:
                    return p =>
                    {
                        // Corner radius ~ 40px of 96px so the sprite 9-slices cleanly.
                        const float r = 0.8f;
                        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(0.98f - r, 0.98f - r);
                        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
                    };
                case Shape.Gear:
                    return p =>
                    {
                        float angle = Mathf.Atan2(p.y, p.x);
                        // Eight rounded teeth around a ring with a hole.
                        float teeth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Cos(angle * 8f) * 2.5f + 0.5f));
                        float outer = p.magnitude - (0.7f + 0.2f * teeth);
                        float hole = 0.3f - p.magnitude;
                        return Mathf.Max(outer, hole);
                    };
                case Shape.Hexagon:
                    return p =>
                    {
                        // Flat-top hexagon, same orientation as the board.
                        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
                        float edge = Mathf.Max(q.y, q.x * 0.866f + q.y * 0.5f);
                        return edge - 0.8f;
                    };
                case Shape.Hammer:
                    return p =>
                    {
                        Vector2 q = Rotate(p, -35f);
                        float head = Box(q - new Vector2(0f, 0.38f), new Vector2(0.52f, 0.2f), 0.06f);
                        float handle = Box(q - new Vector2(0f, -0.22f), new Vector2(0.11f, 0.52f), 0.08f);
                        return Mathf.Min(head, handle);
                    };
                case Shape.Move:
                    return p =>
                    {
                        float shaft = Box(p - new Vector2(-0.22f, 0f), new Vector2(0.46f, 0.13f), 0.05f);
                        float head = Triangle(p, new Vector2(0.82f, 0f), new Vector2(0.18f, 0.48f), new Vector2(0.18f, -0.48f));
                        return Mathf.Min(shaft, head);
                    };
                case Shape.Shuffle:
                    return p =>
                    {
                        // Refresh arrow: a ring with a gap at the top right, arrowhead closing the gap.
                        float ring = Mathf.Abs(p.magnitude - 0.55f) - 0.12f;
                        float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                        float gap = Mathf.DeltaAngle(angle, 60f);
                        if (Mathf.Abs(gap) < 35f) ring = Mathf.Max(ring, 0.05f);
                        float head = Triangle(p, new Vector2(0.1f, 0.88f), new Vector2(0.08f, 0.22f), new Vector2(0.52f, 0.62f));
                        return Mathf.Min(ring, head);
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape));
            }
        }

        private static Vector2 Rotate(Vector2 p, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
        }

        /// <summary>Rounded box SDF centred at the origin.</summary>
        private static float Box(Vector2 p, Vector2 half, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + Vector2.one * radius;
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        /// <summary>Approximate triangle SDF (max of edge half-planes), exact enough for anti-aliased icons.</summary>
        private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float winding = Mathf.Sign((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
            float Edge(Vector2 from, Vector2 to)
            {
                Vector2 e = (to - from).normalized;
                Vector2 outward = new Vector2(e.y, -e.x) * winding;
                return Vector2.Dot(p - from, outward);
            }
            return Mathf.Max(Edge(a, b), Mathf.Max(Edge(b, c), Edge(c, a)));
        }

        /// <summary>Soft shapes defined by alpha directly: a radial glow and light rays.</summary>
        private static Texture2D RenderAlpha(Shape shape, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float r = p.magnitude;
                float a;
                if (shape == Shape.Glow)
                {
                    a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
                }
                else
                {
                    float beam = Mathf.Clamp01((Mathf.Cos(Mathf.Atan2(p.y, p.x) * 12f) + 0.2f) / 0.45f);
                    beam = beam * beam * (3f - 2f * beam);
                    a = beam * Mathf.Pow(Mathf.Clamp01(1f - r), 1.2f);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D Render(Func<Vector2, float> sdf, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float pixel = 2f / size; // one pixel in normalized units, for anti-aliasing
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float alpha = Mathf.Clamp01(0.5f - sdf(p) / pixel);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
