using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace OWT.DamageMeter.UI
{
    /// <summary>
    /// Loads the pre-baked UI sprites embedded in the plugin DLL. The PNGs are
    /// generated offline by tools/prepare_ui_assets.py, so no textures are
    /// procedurally drawn or sliced at runtime.
    /// </summary>
    internal static class UiAssets
    {
        private static bool _loaded;

        public static Sprite Rounded;
        public static Sprite Border;
        public static Sprite Shadow;
        public static Sprite Circle;
        public static Sprite Glow;
        public static Sprite Ball;
        public static Sprite Ring;
        public static Sprite ResizeGrip;

        public static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;

            Assembly assembly = typeof(UiAssets).Assembly;
            Rounded = Load(assembly, "rounded", new Vector4(18f, 18f, 18f, 18f));
            Border = Load(assembly, "border", new Vector4(18f, 18f, 18f, 18f));
            Shadow = Load(assembly, "shadow", new Vector4(34f, 34f, 34f, 34f));
            Circle = Load(assembly, "circle", Vector4.zero);
            Glow = Load(assembly, "glow", Vector4.zero);
            Ball = Load(assembly, "ball", Vector4.zero);
            Ring = Load(assembly, "ring", Vector4.zero);
            ResizeGrip = Load(assembly, "resize_grip", Vector4.zero);

            int loaded = 0;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] != null)
                {
                    loaded++;
                }
            }
            DamageMeterPlugin.Log.LogInfo("Embedded UI sprites loaded: " + loaded + "/" + All.Length);
        }

        private static readonly Sprite[] All =
        {
            null, null, null, null, null, null, null, null
        };

        private static Sprite Load(Assembly assembly, string name, Vector4 border)
        {
            string suffix = ".Assets." + name + ".png";
            string resource = null;
            string[] names = assembly.GetManifestResourceNames();
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    resource = names[i];
                    break;
                }
            }

            if (resource == null)
            {
                DamageMeterPlugin.Log.LogWarning("Embedded UI sprite missing: " + name);
                return null;
            }

            try
            {
                using (Stream stream = assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        return null;
                    }
                    using (MemoryStream memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!texture.LoadImage(memory.ToArray()))
                        {
                            UnityEngine.Object.Destroy(texture);
                            DamageMeterPlugin.Log.LogWarning("Failed to decode UI sprite: " + name);
                            return null;
                        }
                        texture.name = "OWT_DamageMeter_" + name;
                        texture.filterMode = FilterMode.Bilinear;
                        texture.wrapMode = TextureWrapMode.Clamp;

                        Sprite sprite = Sprite.Create(texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f), 100f, 0,
                            SpriteMeshType.FullRect, border);
                        sprite.name = name;
                        Remember(sprite);
                        return sprite;
                    }
                }
            }
            catch (Exception ex)
            {
                DamageMeterPlugin.Log.LogWarning("Failed to load embedded UI sprite " + name + ": " + ex.Message);
                return null;
            }
        }

        private static void Remember(Sprite sprite)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] == null)
                {
                    All[i] = sprite;
                    return;
                }
            }
        }
    }
}
