using System;
using Foundations;
using OWT.DamageMeter.Tracking;
using UnityEngine;
using UnityEngine.InputSystem;
using World;
using World.Devices;

namespace OWT.DamageMeter.UI
{
    public sealed class MeterHost : MonoBehaviour
    {
        private static MeterHost _instance;

        private DamageTracker _tracker;
        private MeterPanel _panel;
        private bool _userVisible = true;
        private bool _errorLogged;

        public static void Ensure()
        {
            if (_instance != null)
            {
                UnityEngine.Object.Destroy(_instance.gameObject);
                _instance = null;
            }

            GameObject go = new GameObject("[OWT Damage Meter]");
            _instance = go.AddComponent<MeterHost>();
        }

        public static void Shutdown()
        {
            if (_instance != null)
            {
                UnityEngine.Object.Destroy(_instance.gameObject);
                _instance = null;
            }
            DamageOrigin.Reset();
        }

        private void Awake()
        {
            DamageOrigin.Reset();
            _tracker = new DamageTracker();
            DamageTracker.Current = _tracker;
            _panel = new MeterPanel();
            _panel.BallClicked = OnBallClicked;
            _panel.CollapseRequested = OnCollapseRequested;
            DamageMeterPlugin.Log.LogInfo("Damage meter host created.");
        }

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception ex)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    DamageMeterPlugin.Log.LogError("Damage meter HUD update failed:\n" + ex);
                }
            }
        }

        private void Tick()
        {
            bool enabled = DamageMeterPlugin.Enabled == null || DamageMeterPlugin.Enabled.Value;
            if (enabled)
            {
                HandleHotkey();
            }

            if (!enabled)
            {
                _panel.SetShown(false);
                _panel.SetBallShown(false);
                if (_panel.Built)
                {
                    _panel.UpdateUI(_tracker, Time.unscaledDeltaTime);
                }
                return;
            }

            WorldContext context = Singleton<WorldContext>.instance;
            bool inSafeArea = context != null && (context.in_safe_area || context.in_safe_area2);
            bool inMenu = IsMenuOpen();
            bool active = !inSafeArea && !inMenu && Time.deltaTime > 0f;

            if (active)
            {
                _tracker.AdvanceSession(Time.deltaTime);
            }

            DeviceMgr manager = GetDeviceMgr();
            _tracker.RefreshRoster(manager, active ? Time.deltaTime : 0f);

            bool showInSafeArea = DamageMeterPlugin.ShowInSafeArea != null && DamageMeterPlugin.ShowInSafeArea.Value;
            bool external = (!inSafeArea || showInSafeArea) && !inMenu;
            _panel.SetShown(_userVisible && external);
            _panel.SetBallShown(!_userVisible && external);

            if (!_panel.EnsureBuilt())
            {
                return;
            }
            _panel.UpdateUI(_tracker, Time.unscaledDeltaTime);
        }

        private void OnBallClicked()
        {
            _userVisible = true;
        }

        private void OnCollapseRequested()
        {
            _userVisible = false;
        }

        private void HandleHotkey()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Key key = DamageMeterPlugin.ToggleKey;
            if (key == Key.None)
            {
                return;
            }

            var control = keyboard[key];
            if (control != null && control.wasPressedThisFrame)
            {
                _userVisible = !_userVisible;
            }
        }

        private static DeviceMgr GetDeviceMgr()
        {
            try
            {
                Mission mission = Singleton<Mission>.instance;
                if (mission == null)
                {
                    return null;
                }

                DeviceMgr manager;
                return mission.try_get_mgr("DeviceMgr", out manager) ? manager : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsMenuOpen()
        {
            bool hide = DamageMeterPlugin.HideInMenus == null || DamageMeterPlugin.HideInMenus.Value;
            if (!hide)
            {
                return false;
            }

            if (Time.timeScale <= 0.0001f)
            {
                return true;
            }

            try
            {
                WorldSceneRoot root = MonoBehaviourSingleton<WorldSceneRoot>.instance;
                if (root == null)
                {
                    return false;
                }
                if (root.game_menu != null && root.game_menu.gameObject.activeInHierarchy)
                {
                    return true;
                }
                if (root.safearea_game_menu != null && root.safearea_game_menu.gameObject.activeInHierarchy)
                {
                    return true;
                }
                if (root.settlement_panel != null && root.settlement_panel.gameObject.activeInHierarchy)
                {
                    return true;
                }
                if (root.console_IPF != null && root.console_IPF.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private void OnDestroy()
        {
            if (_panel != null)
            {
                _panel.Destroy();
                _panel = null;
            }
            if (DamageTracker.Current == _tracker)
            {
                DamageTracker.Current = null;
            }
            if (_instance == this)
            {
                _instance = null;
            }
            DamageOrigin.Reset();
        }
    }
}
