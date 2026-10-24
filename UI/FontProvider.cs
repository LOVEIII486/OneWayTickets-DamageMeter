using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace OWT.DamageMeter.UI
{
    internal static class FontProvider
    {
        private static TMP_FontAsset _font;
        private static TMP_FontAsset _osFallback;
        private static bool _warned;
        private static float _nextRetry;

        public static TMP_FontAsset Font
        {
            get
            {
                if (_font == null && Time.unscaledTime >= _nextRetry)
                {
                    _font = Resolve();
                    if (_font == null)
                    {
                        _nextRetry = Time.unscaledTime + 1f;
                    }
                }
                return _font;
            }
        }

        public static bool Ready
        {
            get { return Font != null; }
        }

        private static TMP_FontAsset Resolve()
        {
            TMP_FontAsset font = ResolveFromScene();
            TMP_FontAsset os = CreateFromOsFont();
            if (font == null)
            {
                font = os;
            }
            else if (os != null)
            {
                AddFallback(font, os);
            }

            if (font != null)
            {
                try
                {
                    DamageMeterPlugin.Log.LogInfo("Using TMP font: " + font.name
                        + (os != null ? " (OS fallback added)" : string.Empty));
                }
                catch
                {
                }
            }
            else if (!_warned)
            {
                _warned = true;
                try
                {
                    DamageMeterPlugin.Log.LogWarning("No TMP font asset found yet; will retry.");
                }
                catch
                {
                }
            }
            return font;
        }

        private static void AddFallback(TMP_FontAsset primary, TMP_FontAsset fallback)
        {
            try
            {
                if (primary.fallbackFontAssetTable == null)
                {
                    primary.fallbackFontAssetTable = new List<TMP_FontAsset>();
                }
                if (!primary.fallbackFontAssetTable.Contains(fallback))
                {
                    primary.fallbackFontAssetTable.Add(fallback);
                }
            }
            catch
            {
            }
        }

        private static TMP_FontAsset ResolveFromScene()
        {
            try
            {
                // Prefer the font that is visibly rendering Chinese text in the game UI.
                TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
                TMP_FontAsset best = null;
                int bestCjk = 0;
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text text = texts[i];
                    if (text == null || text.font == null)
                    {
                        continue;
                    }

                    int cjk = CountCjk(text.text);
                    if (cjk > bestCjk)
                    {
                        bestCjk = cjk;
                        best = text.font;
                    }
                }
                if (best != null)
                {
                    return PreferCjk(best);
                }
            }
            catch
            {
            }

            try
            {
                if (TMP_Settings.defaultFontAsset != null)
                {
                    return TMP_Settings.defaultFontAsset;
                }
            }
            catch
            {
            }

            try
            {
                TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                TMP_FontAsset fallback = null;
                for (int i = 0; i < fonts.Length; i++)
                {
                    TMP_FontAsset font = fonts[i];
                    if (font == null)
                    {
                        continue;
                    }

                    string name = font.name ?? string.Empty;
                    if (name.IndexOf("CN", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("SC", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Hei", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Noto", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return font;
                    }
                    if (fallback == null)
                    {
                        fallback = font;
                    }
                }
                if (fallback != null)
                {
                    return fallback;
                }
            }
            catch
            {
            }

            return CreateFromOsFont();
        }

        private static TMP_FontAsset CreateFromOsFont()
        {
            if (_osFallback != null)
            {
                return _osFallback;
            }
            try
            {
                string[] candidates =
                {
                    "Microsoft YaHei UI",
                    "Microsoft YaHei",
                    "SimHei",
                    "Noto Sans CJK SC",
                    "Arial Unicode MS"
                };
                Font osFont = UnityEngine.Font.CreateDynamicFontFromOSFont(candidates, 48);
                if (osFont == null)
                {
                    return null;
                }
                _osFallback = TMP_FontAsset.CreateFontAsset(osFont);
                return _osFallback;
            }
            catch
            {
                return null;
            }
        }

        private static int CountCjk(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c >= '㐀' && c <= '鿿') || (c >= '豈' && c <= '﫿'))
                {
                    count++;
                }
            }
            return count;
        }

        private static TMP_FontAsset PreferCjk(TMP_FontAsset font)
        {
            try
            {
                if (HasCjk(font))
                {
                    return font;
                }
                if (font.fallbackFontAssetTable != null)
                {
                    for (int i = 0; i < font.fallbackFontAssetTable.Count; i++)
                    {
                        TMP_FontAsset fallback = font.fallbackFontAssetTable[i];
                        if (fallback != null && HasCjk(fallback))
                        {
                            return fallback;
                        }
                    }
                }
            }
            catch
            {
            }
            return font;
        }

        private static bool HasCjk(TMP_FontAsset font)
        {
            if (font == null)
            {
                return false;
            }
            try
            {
                return font.HasCharacter('伤', true, true);
            }
            catch
            {
                return false;
            }
        }

        public static void Reset()
        {
            _font = null;
            _warned = false;
            _nextRetry = 0f;
        }
    }
}
