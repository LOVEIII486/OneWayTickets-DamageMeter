using System;
using System.Globalization;
using OWT.DamageMeter.Tracking;
using UnityEngine;

namespace OWT.DamageMeter.UI
{
    internal static class Theme
    {
        public const float PanelWidth = 420f;
        public const float Padding = 14f;
        public const float HeaderHeight = 60f;
        public const float RowHeight = 62f;
        public const float RowGap = 6f;
        public const float FooterHeight = 26f;
        public const float RowsTop = HeaderHeight + 8f;
        public const float ContentWidth = PanelWidth - Padding * 2f;
        public const float MinContentWidth = 320f;
        public const float BarHeight = 4f;

        public static readonly Color PanelBg = Hex("0C1017", 0.965f);
        public static readonly Color Border = Hex("FFFFFF", 0.085f);
        public static readonly Color Shadow = Hex("000000", 0.55f);
        public static readonly Color Accent = Hex("E9B45C", 1f);
        public static readonly Color Text = Hex("E8EEF8", 1f);
        public static readonly Color Muted = Hex("8B97A8", 1f);
        public static readonly Color Faint = Hex("5F6B7C", 1f);
        public static readonly Color BarBg = Hex("FFFFFF", 0.075f);
        public static readonly Color Danger = Hex("F7768E", 1f);
        public static readonly Color OtherColor = Hex("7C8798", 1f);
        public static readonly Color DerivedColor = Hex("E9B45C", 1f);
        public static readonly Color ButtonHover = Hex("FFFFFF", 0.08f);

        /// <summary>
        /// Device ranks 0-4 map to the game's ui_device_nametag_1..5 sprites,
        /// whose colours are grey, green, blue, purple and gold. Wheel ranks
        /// (99+) use the wooden ui_device_nametag_wheel_bag nameplate.
        /// </summary>
        public static Color QualityColor(int rank)
        {
            switch (rank)
            {
                case 0: return Hex("B8C2CF", 1f);
                case 1: return Hex("9ECE6A", 1f);
                case 2: return Hex("7AA2F7", 1f);
                case 3: return Hex("C792EA", 1f);
                case 4: return Hex("F2B84B", 1f);
            }
            return rank >= 90 ? Hex("C7A87A", 1f) : Hex("F2B84B", 1f);
        }

        public static string QualityName(int rank)
        {
            switch (rank)
            {
                case 0: return "common";
                case 1: return "uncommon";
                case 2: return "rare";
                case 3: return "epic";
                case 4: return "legendary";
            }
            return rank >= 90 ? "wheel" : "special";
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static Color Hex(string hex, float alpha)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString("#" + hex, out color))
            {
                color = Color.white;
            }
            color.a = alpha;
            return color;
        }
    }

    internal static class Strings
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static bool? _chinese;

        public static bool Chinese
        {
            get
            {
                if (!_chinese.HasValue)
                {
                    string language = PlayerPrefs.GetString("language", "CN");
                    _chinese = language.StartsWith("CN", StringComparison.OrdinalIgnoreCase)
                        || language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                        || language.StartsWith("CHS", StringComparison.OrdinalIgnoreCase);
                }
                return _chinese.Value;
            }
        }

        public static string Title
        {
            get { return Chinese ? "伤害统计" : "DAMAGE METER"; }
        }

        public static string Reset
        {
            get { return Chinese ? "重置" : "Reset"; }
        }

        public static string Collapse
        {
            get { return Chinese ? "收起" : "Hide"; }
        }

        public static string Other
        {
            get { return Chinese ? "其他来源" : "Other sources"; }
        }

        public static string OtherHint
        {
            get { return Chinese ? "环境 / 车厢 / 未归属" : "environment / vehicle / untracked"; }
        }

        public static string Removed
        {
            get { return Chinese ? "已卸下" : "removed"; }
        }

        public static string Broken
        {
            get { return Chinese ? "损毁" : "broken"; }
        }

        public static string Waiting
        {
            get { return Chinese ? "等待装置数据…" : "Waiting for devices..."; }
        }

        public static string NoDamage
        {
            get { return Chinese ? "暂无伤害数据" : "No damage yet"; }
        }

        public static string UnknownDevice
        {
            get { return Chinese ? "未知装置" : "Unknown device"; }
        }

        public static string DerivedShort
        {
            get { return Chinese ? "衍" : "P"; }
        }

        public static string Support
        {
            get { return Chinese ? "辅助" : "ASSIST"; }
        }

        public static string DirectDamage
        {
            get { return Chinese ? "直接伤害" : "Direct damage"; }
        }

        public static string DerivedDamage
        {
            get { return Chinese ? "衍生伤害" : "Derived damage"; }
        }

        public static string AssistDamage
        {
            get { return Chinese ? "协助伤害" : "Assist damage"; }
        }

        public static string AffectedDevices
        {
            get { return Chinese ? "影响装置" : "Affected devices"; }
        }

        public static string NoDerived
        {
            get { return Chinese ? "无衍生伤害" : "No derived damage"; }
        }

        public static string EffectPrefix
        {
            get { return Chinese ? "效果： " : "Effect: "; }
        }

        public static string SupportNote
        {
            get { return Chinese ? "已计入受影响装置，不重复统计" : "Included in affected device, not double counted"; }
        }

        public static string EffectName(DamageKind kind)
        {
            if (Chinese)
            {
                switch (kind)
                {
                    case DamageKind.Explosion: return "爆炸";
                    case DamageKind.Lightning: return "闪电";
                    case DamageKind.Dart: return "飞镖";
                    case DamageKind.Ignite: return "点燃";
                    case DamageKind.Assassinate: return "处决";
                    case DamageKind.MuzzleBlast: return "枪口爆风";
                    case DamageKind.ArrowChasing: return "追击箭";
                    case DamageKind.Blast: return "冲击波";
                    case DamageKind.Hail: return "冰雹";
                    case DamageKind.OtherDerived: return "其他衍生";
                    default: return "直接";
                }
            }

            switch (kind)
            {
                case DamageKind.Explosion: return "Explosion";
                case DamageKind.Lightning: return "Lightning";
                case DamageKind.Dart: return "Dart";
                case DamageKind.Ignite: return "Ignite";
                case DamageKind.Assassinate: return "Assassinate";
                case DamageKind.MuzzleBlast: return "Muzzle blast";
                case DamageKind.ArrowChasing: return "Chasing arrow";
                case DamageKind.Blast: return "Blast";
                case DamageKind.Hail: return "Hail";
                case DamageKind.OtherDerived: return "Other derived";
                default: return "Direct";
            }
        }

        public static string Slot(string key)
        {
            if (Chinese)
            {
                switch (key)
                {
                    case "slot_top": return "车顶";
                    case "slot_front_top": return "车顶前";
                    case "slot_front": return "车前";
                    case "slot_back": return "车后";
                    case "slot_back_top": return "车顶后";
                    case "device_slot_wheel": return "车轮";
                    default: return "其他";
                }
            }

            switch (key)
            {
                case "slot_top": return "TOP";
                case "slot_front_top": return "TOP-F";
                case "slot_front": return "FRONT";
                case "slot_back": return "BACK";
                case "slot_back_top": return "TOP-B";
                case "device_slot_wheel": return "WHEEL";
                default: return "OTHER";
            }
        }

        public static string HeaderLine(string time, string total, string dps)
        {
            if (Chinese)
            {
                return string.Format(Inv, "本次旅途 {0}  ·  武器伤害 {1}  ·  平均 {2}/s", time, total, dps);
            }
            return string.Format(Inv, "Run {0}  ·  Weapon damage {1}  ·  Avg {2}/s", time, total, dps);
        }

        public static string FooterHint(string keyName)
        {
            if (Chinese)
            {
                return keyName + " 显示/隐藏  ·  拖动面板移动  ·  点击行看明细  ·  右下角缩放";
            }
            return keyName + " toggle  ·  drag panel  ·  click row for details  ·  resize corner";
        }

        public static string BallLabel
        {
            get { return "DPS"; }
        }

        public static string Time(double seconds)
        {
            if (seconds < 0.0)
            {
                seconds = 0.0;
            }
            TimeSpan span = TimeSpan.FromSeconds(seconds);
            if (span.TotalHours >= 1.0)
            {
                return string.Format(Inv, "{0}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds);
            }
            return string.Format(Inv, "{0:00}:{1:00}", span.Minutes, span.Seconds);
        }

        public static string Number(double value, bool compact)
        {
            double abs = Math.Abs(value);
            if (compact)
            {
                if (Chinese)
                {
                    if (abs >= 100000000.0)
                    {
                        return (value / 100000000.0).ToString("0.##", Inv) + "亿";
                    }
                    if (abs >= 1000000.0)
                    {
                        return (value / 10000.0).ToString("0.#", Inv) + "万";
                    }
                    if (abs >= 100000.0)
                    {
                        return (value / 10000.0).ToString("0.0", Inv) + "万";
                    }
                }
                else
                {
                    if (abs >= 1000000000.0)
                    {
                        return (value / 1000000000.0).ToString("0.##", Inv) + "B";
                    }
                    if (abs >= 1000000.0)
                    {
                        return (value / 1000000.0).ToString("0.##", Inv) + "M";
                    }
                    if (abs >= 100000.0)
                    {
                        return (value / 1000.0).ToString("0.#", Inv) + "K";
                    }
                }
            }

            return value.ToString("N0", Inv);
        }

        public static string Number1(double value, bool compact)
        {
            if (compact && Math.Abs(value) >= 100000.0)
            {
                return Number(value, true);
            }
            return value.ToString("N1", Inv);
        }

        public static string Percent(double fraction)
        {
            return (fraction * 100.0).ToString("0.0", Inv) + "%";
        }
    }
}
