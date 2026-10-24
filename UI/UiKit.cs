using System;
using System.Collections.Generic;
using Addrs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OWT.DamageMeter.UI
{
    internal static class UiKit
    {
        private const float RadiusBorderPixels = 18f;

        private static Sprite _rounded;
        private static Sprite _border;
        private static Sprite _shadow;
        private static Sprite _circle;
        private static Sprite _glow;

        private static readonly Dictionary<string, Sprite> IconCache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> IconTried = new HashSet<string>();

        public static Sprite Rounded
        {
            get
            {
                UiAssets.EnsureLoaded();
                if (UiAssets.Rounded != null)
                {
                    return UiAssets.Rounded;
                }
                if (_rounded == null)
                {
                    _rounded = CreateRounded(64, 16f, false, 0f);
                }
                return _rounded;
            }
        }

        public static Sprite BorderSprite
        {
            get
            {
                UiAssets.EnsureLoaded();
                if (UiAssets.Border != null)
                {
                    return UiAssets.Border;
                }
                if (_border == null)
                {
                    _border = CreateRounded(64, 16f, true, 1.15f);
                }
                return _border;
            }
        }

        public static Sprite ShadowSprite
        {
            get
            {
                UiAssets.EnsureLoaded();
                if (UiAssets.Shadow != null)
                {
                    return UiAssets.Shadow;
                }
                if (_shadow == null)
                {
                    _shadow = CreateShadow(96, 20f);
                }
                return _shadow;
            }
        }

        public static Sprite Circle
        {
            get
            {
                UiAssets.EnsureLoaded();
                if (UiAssets.Circle != null)
                {
                    return UiAssets.Circle;
                }
                if (_circle == null)
                {
                    _circle = CreateCircle(32);
                }
                return _circle;
            }
        }

        public static Sprite Glow
        {
            get
            {
                UiAssets.EnsureLoaded();
                if (UiAssets.Glow != null)
                {
                    return UiAssets.Glow;
                }
                if (_glow == null)
                {
                    _glow = CreateGlow(64);
                }
                return _glow;
            }
        }

        public static Sprite Ball
        {
            get
            {
                UiAssets.EnsureLoaded();
                return UiAssets.Ball;
            }
        }

        public static Sprite Ring
        {
            get
            {
                UiAssets.EnsureLoaded();
                return UiAssets.Ring;
            }
        }

        public static Sprite ResizeGrip
        {
            get
            {
                UiAssets.EnsureLoaded();
                return UiAssets.ResizeGrip;
            }
        }

        // ---------------------------------------------------------------- sprites

        private static Sprite CreateRounded(int size, float radius, bool stroke, float strokeWidth)
        {
            Texture2D texture = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f - 1.5f;
            float innerHalf = half - strokeWidth;
            float innerRadius = Mathf.Max(0f, radius - strokeWidth);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = SampleRounded(x, y, size, half, radius);
                    if (stroke)
                    {
                        float inner = SampleRounded(x, y, size, innerHalf, innerRadius);
                        alpha = Mathf.Clamp01(alpha - inner);
                    }
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            float border = Mathf.Min(radius + 2f, size * 0.5f - 1f);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static float SampleRounded(int px, int py, int size, float half, float radius)
        {
            const int samples = 3;
            int inside = 0;
            for (int sy = 0; sy < samples; sy++)
            {
                for (int sx = 0; sx < samples; sx++)
                {
                    float x = px + (sx + 0.5f) / samples - size * 0.5f;
                    float y = py + (sy + 0.5f) / samples - size * 0.5f;
                    if (RoundedSdf(x, y, half, half, radius) <= 0f)
                    {
                        inside++;
                    }
                }
            }
            return inside / (float)(samples * samples);
        }

        private static float RoundedSdf(float x, float y, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(x) - halfW + radius;
            float qy = Mathf.Abs(y) - halfH + radius;
            float outsideX = Mathf.Max(qx, 0f);
            float outsideY = Mathf.Max(qy, 0f);
            float outside = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);
            return Mathf.Min(Mathf.Max(qx, qy), 0f) + outside - radius;
        }

        private static Sprite CreateShadow(int size, float radius)
        {
            int inset = 18;
            float half = size * 0.5f - inset;
            float[] alpha = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f - size * 0.5f;
                    float fy = y + 0.5f - size * 0.5f;
                    alpha[y * size + x] = RoundedSdf(fx, fy, half, half, radius) <= 0f ? 1f : 0f;
                }
            }
            BoxBlur(alpha, size, 5);
            BoxBlur(alpha, size, 5);

            Texture2D texture = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < alpha.Length; i++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[i]) * 220f);
                pixels[i] = new Color32(0, 0, 0, a);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            float border = Mathf.Min(radius + 14f, size * 0.5f - 1f);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static void BoxBlur(float[] data, int size, int radius)
        {
            float[] temp = new float[data.Length];
            float norm = 1f / (radius * 2 + 1);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int xx = Mathf.Clamp(x + k, 0, size - 1);
                        sum += data[y * size + xx];
                    }
                    temp[y * size + x] = sum * norm;
                }
            }

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int yy = Mathf.Clamp(y + k, 0, size - 1);
                        sum += temp[yy * size + x];
                    }
                    data[y * size + x] = sum * norm;
                }
            }
        }

        private static Sprite CreateCircle(int size)
        {
            Texture2D texture = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - d + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateGlow(int size)
        {
            Texture2D texture = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    float alpha = Mathf.Clamp01(1f - d);
                    alpha = alpha * alpha * 0.9f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "OWT_DamageMeter_UI";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        // ---------------------------------------------------------------- widgets

        public static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image RoundedImage(Transform parent, string name, Color color, float radius, bool raycast)
        {
            Image image = Image(parent, name, Rounded, color, raycast);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Mathf.Clamp(radius / RadiusBorderPixels, 0.04f, 2f);
            return image;
        }

        public static Image BorderImage(Transform parent, string name, Color color, float radius, bool raycast)
        {
            Image image = Image(parent, name, BorderSprite, color, raycast);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Mathf.Clamp(radius / RadiusBorderPixels, 0.04f, 2f);
            return image;
        }

        public static Image ShadowImage(Transform parent, string name)
        {
            Image image = Image(parent, name, ShadowSprite, Theme.Shadow, false);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            return image;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            image.type = sprite != null ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Simple;
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string value, float size,
            Color color, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = FontProvider.Font;
            if (text.font != null && text.font.material != null)
            {
                text.fontSharedMaterial = text.font.material;
            }
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.richText = false;
            text.margin = Vector4.zero;
            return text;
        }

        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void PlaceRight(RectTransform rect, float right, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        // ---------------------------------------------------------------- icons

        public static Sprite LoadDeviceIcon(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            Sprite cached;
            if (IconCache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }
            if (IconTried.Contains(key))
            {
                return null;
            }

            IconTried.Add(key);
            try
            {
                Sprite sprite;
                if (Addressable_Utility.try_load_asset<Sprite>(key, out sprite) && sprite != null)
                {
                    IconCache[key] = sprite;
                    return sprite;
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public static void ResetIconCache()
        {
            IconCache.Clear();
            IconTried.Clear();
        }
    }
}
