using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using OWT.DamageMeter.Patches;
using OWT.DamageMeter.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using World;

namespace OWT.DamageMeter
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class DamageMeterPlugin : BaseUnityPlugin
    {
        public const string Guid = "owt.damagemeter";
        public const string Name = "OWT Damage Meter";
        public const string Version = "0.5.2";

        internal const float DefaultPanelX = -24f;
        internal const float DefaultPanelY = -24f;
        internal const float DefaultBallX = -24f;
        internal const float DefaultBallY = 24f;
        internal const float DefaultPanelScale = 1f;
        internal const float DefaultPanelWidthScale = 1f;

        internal static ManualLogSource Log;
        internal static DamageMeterPlugin Instance;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> ToggleKeyName;
        internal static Key ToggleKey = Key.F10;
        internal static ConfigEntry<int> ConfigVersion;
        internal static ConfigEntry<float> PanelX;
        internal static ConfigEntry<float> PanelY;
        internal static ConfigEntry<float> BallX;
        internal static ConfigEntry<float> BallY;
        internal static ConfigEntry<float> PanelScale;
        internal static ConfigEntry<float> PanelWidthScale;
        internal static ConfigEntry<float> Opacity;
        internal static ConfigEntry<bool> ShowInSafeArea;
        internal static ConfigEntry<bool> HideInMenus;
        internal static ConfigEntry<bool> ShowZeroDamageDevices;
        internal static ConfigEntry<bool> ShowRemovedDevices;
        internal static ConfigEntry<bool> ShowOtherSources;
        internal static ConfigEntry<bool> CountDerivativeDamage;
        internal static ConfigEntry<bool> UseCompactNumbers;
        internal static ConfigEntry<bool> VerboseLogging;

        private static Harmony _harmony;
        private static bool _worldInitPatched;
        private static bool _worldFiniPatched;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BindConfig();
            Log.LogInfo(Name + " v" + Version + " loaded.");

            _harmony = new Harmony(Guid);
            DamagePatches.Apply(_harmony, Log);
            PatchWorldLifecycle();
        }

        private void BindConfig()
        {
            ConfigVersion = Config.Bind("0. Internal", "ConfigVersion", 0,
                "内部配置版本，请勿修改。");
            Enabled = Config.Bind("1. General", "Enabled", true,
                "总开关。关闭后隐藏统计面板和悬浮球，也不再记录伤害。");
            ToggleKeyName = Config.Bind("1. General", "ToggleKey", "F10",
                "显示 / 隐藏的快捷键。使用 Unity Input System 的键名，例如 F9、F10、F11、Tab、Backquote。");
            PanelX = Config.Bind("2. Layout", "PanelX", DefaultPanelX,
                "面板距屏幕右侧的像素距离（1920x1080 参考分辨率）。拖动面板后自动保存。");
            PanelY = Config.Bind("2. Layout", "PanelY", DefaultPanelY,
                "面板距屏幕顶部的像素距离（1920x1080 参考分辨率）。拖动面板后自动保存。");
            BallX = Config.Bind("2. Layout", "BallX", DefaultBallX,
                "悬浮球距屏幕右侧的像素距离。拖动悬浮球后自动保存。");
            BallY = Config.Bind("2. Layout", "BallY", DefaultBallY,
                "悬浮球距屏幕底部的像素距离。拖动悬浮球后自动保存。");

            PanelScale = Config.Bind("2. Layout", "PanelScale", DefaultPanelScale,
                new ConfigDescription("面板高度和整体 UI 缩放。拖动右下角把手上下移动也会改这一项。",
                    new AcceptableValueRange<float>(0.7f, 1.8f)));
            PanelWidthScale = Config.Bind("2. Layout", "PanelWidthScale", DefaultPanelWidthScale,
                new ConfigDescription("面板宽度缩放。拖动右下角把手左右移动也会改这一项。",
                    new AcceptableValueRange<float>(0.7f, 1.8f)));
            Opacity = Config.Bind("2. Layout", "Opacity", 0.96f,
                new ConfigDescription("面板不透明度。", new AcceptableValueRange<float>(0.4f, 1f)));
            ShowInSafeArea = Config.Bind("3. Display", "ShowInSafeArea", false,
                "在安全区里也显示面板。默认关闭。");
            HideInMenus = Config.Bind("3. Display", "HideInMenus", true,
                "打开游戏菜单、暂停菜单或结算面板时自动隐藏统计面板和悬浮球。");
            ShowZeroDamageDevices = Config.Bind("3. Display", "ShowZeroDamageDevices", true,
                "显示当前已装备但还没有造成伤害的装置。车轮槽位只有造成伤害后才显示。");
            ShowRemovedDevices = Config.Bind("3. Display", "ShowRemovedDevices", true,
                "旅途途中被拆下或换掉的装置，如果打过伤害，继续留在列表里并标记“已卸下”。");
            ShowOtherSources = Config.Bind("3. Display", "ShowOtherSources", true,
                "显示“其他来源”行，用来容纳环境伤害、车厢碾压、角色伤害以及无法归属的衍生伤害。");
            CountDerivativeDamage = Config.Bind("3. Display", "CountDerivativeDamage", true,
                "把闪电、爆炸、飞镖、点燃等衍生伤害计入触发它的武器。关闭后这些伤害会进入“其他来源”。");
            UseCompactNumbers = Config.Bind("3. Display", "UseCompactNumbers", true,
                "大数字使用万 / 亿（英文为 K / M / B）缩写。关闭后显示完整数字。");
            VerboseLogging = Config.Bind("4. Diagnostics", "VerboseLogging", false,
                "在日志里记录首次出现的伤害来源和未能归属的来源类型，用于排查统计偏差。");

            // v0.2.0: F8 -> F10. v0.4.0: add PanelWidthScale.
            if (ConfigVersion.Value < 3)
            {
                if (ConfigVersion.Value < 2
                    && string.Equals(ToggleKeyName.Value, "F8", StringComparison.OrdinalIgnoreCase))
                {
                    ToggleKeyName.Value = "F10";
                }
                ConfigVersion.Value = 3;
                Config.Save();
                Log.LogInfo("Migrated config to v3 (F10, PanelWidthScale).");
            }

            ToggleKey = ParseKey(ToggleKeyName.Value);
            Log.LogInfo("Toggle key: " + ToggleKey);
        }

        private static void PatchWorldLifecycle()
        {
            try
            {
                MethodInfo init = AccessTools.Method(typeof(WorldSceneRoot), "on_init");
                MethodInfo initPostfix = typeof(DamageMeterPlugin).GetMethod(
                    nameof(OnWorldInit), BindingFlags.Static | BindingFlags.NonPublic);
                if (init != null && initPostfix != null)
                {
                    _harmony.Patch(init, postfix: new HarmonyMethod(initPostfix));
                    _worldInitPatched = true;
                    Log.LogInfo("Patched WorldSceneRoot.on_init.");
                }
                else
                {
                    Log.LogError("Could not patch WorldSceneRoot.on_init; the HUD will not be created.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to patch WorldSceneRoot.on_init:\n" + ex);
            }

            try
            {
                MethodInfo fini = AccessTools.Method(typeof(WorldSceneRoot), "on_fini");
                MethodInfo finiPostfix = typeof(DamageMeterPlugin).GetMethod(
                    nameof(OnWorldFini), BindingFlags.Static | BindingFlags.NonPublic);
                if (fini != null && finiPostfix != null)
                {
                    _harmony.Patch(fini, postfix: new HarmonyMethod(finiPostfix));
                    _worldFiniPatched = true;
                    Log.LogInfo("Patched WorldSceneRoot.on_fini.");
                }
                else
                {
                    Log.LogWarning("Could not patch WorldSceneRoot.on_fini; the HUD will clean itself up on scene unload.");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to patch WorldSceneRoot.on_fini:\n" + ex);
            }
        }

        private static void OnWorldInit()
        {
            try
            {
                MeterHost.Ensure();
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to create the damage meter HUD:\n" + ex);
            }
        }

        private static void OnWorldFini()
        {
            try
            {
                MeterHost.Shutdown();
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to destroy the damage meter HUD:\n" + ex);
            }
        }

        private static Key ParseKey(string value)
        {
            Key parsed;
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out parsed) && parsed != Key.None)
            {
                return parsed;
            }

            Log.LogWarning("Invalid ToggleKey '" + value + "', falling back to F10.");
            return Key.F10;
        }

        internal static bool WorldPatchesReady
        {
            get { return _worldInitPatched || _worldFiniPatched; }
        }
    }
}
