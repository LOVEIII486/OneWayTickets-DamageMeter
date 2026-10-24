using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using OWT.DamageMeter.Tracking;
using UnityEngine;
using World;
using World.Devices;
using World.Devices.Device_AI;
using World.Enemys;
using World.Helpers;
using World.Projectiles;
using World.Relic.Relics;
using World.VFXs.Damage_PopUps;

namespace OWT.DamageMeter.Patches
{
    internal static class DamagePatches
    {
        private static int _applied;
        private static int _attempted;

        public static void Apply(Harmony harmony, ManualLogSource log)
        {
            _applied = 0;
            _attempted = 0;

            // Enemy.hurt unconditionally calls Damage_PopUp.create_cell with the final
            // Dmg_Data and the harm source. Hooking it avoids explicit interface surgery
            // and reads the post-mitigation damage exactly as the game shows it.
            _attempted++;
            if (PatchPopupCapture(harmony, log))
            {
                _applied++;
            }
            else
            {
                _attempted++;
                log.LogWarning("Falling back to Enemy.ITarget.hurt for damage capture.");
                if (PatchEnemyHurtFallback(harmony, log))
                {
                    _applied++;
                }
            }

            // Projectile provenance.
            Patch(harmony, log, "Projectile.Init",
                AccessTools.Method(typeof(Projectile), "Init"),
                null, Method(nameof(ProjectileInitPostfix)));

            Patch(harmony, log, "Projectile.HitEnemy",
                AccessTools.Method(typeof(Projectile), "HitEnemy"),
                Method(nameof(ProjectileHitPrefix)), Method(nameof(PopPostfix)));

            Patch(harmony, log, "Projectile.projectile_explode",
                AccessTools.Method(typeof(Projectile), "projectile_explode"),
                Method(nameof(ProjectileExplodePrefix)), Method(nameof(PopPostfix)));

            Patch(harmony, log, "DartProjectile.projectile_explode",
                AccessTools.Method(typeof(DartProjectile), "projectile_explode"),
                Method(nameof(DartExplodePrefix)), Method(nameof(PopPostfix)));

            // Lightning is a delayed object: tag it when it spawns, restore the tag while it ticks.
            Patch(harmony, log, "Thunder_Helper.Thunder.Init",
                AccessTools.Method(typeof(Thunder_Helper.Thunder), "Init"),
                null, Method(nameof(ThunderInitPostfix)));

            Patch(harmony, log, "Thunder_Helper.Thunder.make_dmg",
                AccessTools.Method(typeof(Thunder_Helper.Thunder), "make_dmg"),
                Method(nameof(ThunderMakeDmgPrefix)), Method(nameof(PopPostfix)));

            // Proc triggers. These run synchronously from a weapon hit but the game discards
            // the source before dealing the derived damage.
            Patch(harmony, log, "Relic_Thunder_Modify_Variable.thunder_melee",
                AccessTools.Method(typeof(Relic_Thunder_Modify_Variable), "thunder_melee"),
                Method(nameof(RelicMeleeScopePrefix)), Method(nameof(PopPostfix)));

            Patch(harmony, log, "Relic_Blast_Modify_Variable.blast_hit(source,target)",
                AccessTools.Method(typeof(Relic_Blast_Modify_Variable), "blast_hit",
                    new[] { typeof(ITarget), typeof(ITarget) }),
                Method(nameof(RelicMeleeScopePrefix)), Method(nameof(PopPostfix)));

            Patch(harmony, log, "Relic_Dart_Modify_Variable.dart_melee",
                AccessTools.Method(typeof(Relic_Dart_Modify_Variable), "dart_melee"),
                Method(nameof(RelicMeleeScopePrefix)), Method(nameof(PopPostfix)));

            Patch(harmony, log, "Relic_Melee_Ignite.melee_ignite_event",
                AccessTools.Method(typeof(Relic_Melee_Ignite), "melee_ignite_event"),
                Method(nameof(RelicMeleeScopePrefix)), Method(nameof(PopPostfix)));

            Patch(harmony, log, "Relic_Melee_Arrow_Chasing.melee_arrow_chasing_event",
                AccessTools.Method(typeof(Relic_Melee_Arrow_Chasing), "melee_arrow_chasing_event"),
                Method(nameof(RelicMeleeScopePrefix)), Method(nameof(PopPostfix)));

            Patch(harmony, log, "Relic_Melee_Assassinate.melee_assassinate_event",
                AccessTools.Method(typeof(Relic_Melee_Assassinate), "melee_assassinate_event"),
                Method(nameof(AssassinateScopePrefix)), Method(nameof(PopPostfix)));

            Patch(harmony, log, "Relic_Muzzle_Blast.knock_off",
                AccessTools.Method(typeof(Relic_Muzzle_Blast), "knock_off"),
                Method(nameof(MuzzleBlastScopePrefix)), Method(nameof(PopPostfix)));

            log.LogInfo(string.Format("Damage attribution hooks: {0}/{1} applied.", _applied, _attempted));
        }

        private static MethodInfo Method(string name)
        {
            return typeof(DamagePatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        }

        private static void Patch(Harmony harmony, ManualLogSource log, string label,
            MethodInfo target, MethodInfo prefix, MethodInfo postfix)
        {
            _attempted++;
            if (target == null)
            {
                log.LogWarning("Hook not found: " + label);
                return;
            }

            try
            {
                harmony.Patch(target,
                    prefix: prefix != null ? new HarmonyMethod(prefix) : null,
                    postfix: postfix != null ? new HarmonyMethod(postfix) : null);
                _applied++;
            }
            catch (Exception ex)
            {
                log.LogWarning("Hook failed: " + label + "\n" + ex.Message);
            }
        }

        // ---------------------------------------------------------------- enemy damage

        private static bool PatchPopupCapture(Harmony harmony, ManualLogSource log)
        {
            try
            {
                MethodInfo target = AccessTools.Method(typeof(Damage_PopUp), "create_cell");
                if (target == null)
                {
                    log.LogError("Could not locate Damage_PopUp.create_cell; damage tracking is disabled.");
                    return false;
                }
                harmony.Patch(target, postfix: new HarmonyMethod(Method(nameof(PopupPostfix))));
                log.LogInfo("Patched Damage_PopUp.create_cell.");
                return true;
            }
            catch (Exception ex)
            {
                log.LogError("Failed to patch Damage_PopUp.create_cell:\n" + ex);
                return false;
            }
        }

        private static bool PatchEnemyHurtFallback(Harmony harmony, ManualLogSource log)
        {
            try
            {
                InterfaceMapping map = typeof(Enemy).GetInterfaceMap(typeof(ITarget));
                MethodInfo target = null;
                for (int i = 0; i < map.InterfaceMethods.Length; i++)
                {
                    if (map.InterfaceMethods[i].Name == "hurt")
                    {
                        target = map.TargetMethods[i];
                        break;
                    }
                }

                if (target == null)
                {
                    log.LogError("Could not locate Enemy's ITarget.hurt implementation either.");
                    return false;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(Method(nameof(EnemyHurtPostfix))));
                log.LogInfo("Patched Enemy.ITarget.hurt (fallback).");
                return true;
            }
            catch (Exception ex)
            {
                log.LogError("Failed to patch Enemy.ITarget.hurt fallback:\n" + ex);
                return false;
            }
        }

        private static void EnemyHurtPostfix(ITarget harm_source, Attack_Data attack_data, ref Dmg_Data dmg_data)
        {
            DamageTracker tracker = DamageTracker.Current;
            if (tracker != null)
            {
                tracker.RecordDamage(harm_source, attack_data, dmg_data);
            }
        }

        private static void PopupPostfix(object[] prms)
        {
            try
            {
                if (prms == null || prms.Length < 3 || !(prms[0] is Dmg_Data))
                {
                    return;
                }

                DamageTracker tracker = DamageTracker.Current;
                if (tracker == null)
                {
                    return;
                }

                Dmg_Data damage = (Dmg_Data)prms[0];
                ITarget source = prms[2] as ITarget;
                tracker.RecordDamage(source, default(Attack_Data), damage);
            }
            catch
            {
                // Never let diagnostics break the damage popup.
            }
        }

        // ---------------------------------------------------------------- projectiles

        private static void ProjectileInitPostfix(Projectile __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                Device origin = DamageOrigin.HasScope ? DamageOrigin.Current : null;
                bool derived = DamageOrigin.HasScope && DamageOrigin.CurrentDerived;
                if (__instance.faction == WorldEnum.Faction.player && origin != null)
                {
                    SourceResolver.TagProjectile(__instance, origin, derived);
                }
                else
                {
                    // Pooled projectiles may still carry a previous tag.
                    SourceResolver.TagProjectile(__instance, null, false);
                }
            }
            catch
            {
            }
        }

        private static void ProjectileHitPrefix(Projectile __instance)
        {
            try
            {
                if (__instance == null || __instance.faction != WorldEnum.Faction.player)
                {
                    DamageOrigin.Push(null, true);
                    return;
                }

                bool viaTag;
                bool tagDerived;
                Device origin = SourceResolver.Resolve(__instance, out viaTag, out tagDerived);
                DamageOrigin.Push(origin, viaTag && tagDerived);
            }
            catch
            {
                DamageOrigin.Push(null, true);
            }
        }

        private static void ProjectileExplodePrefix(Projectile __instance)
        {
            PushProjectileScope(__instance);
        }

        private static void DartExplodePrefix(DartProjectile __instance)
        {
            PushProjectileScope(__instance);
        }

        private static void PushProjectileScope(Projectile projectile)
        {
            try
            {
                if (projectile == null || projectile.faction != WorldEnum.Faction.player)
                {
                    DamageOrigin.Push(null, true);
                    return;
                }

                bool viaTag;
                bool tagDerived;
                Device origin = SourceResolver.Resolve(projectile, out viaTag, out tagDerived);
                DamageOrigin.Push(origin, viaTag && tagDerived);
            }
            catch
            {
                DamageOrigin.Push(null, true);
            }
        }

        private static void PopPostfix()
        {
            DamageOrigin.Pop();
        }

        // ---------------------------------------------------------------- thunder

        private static void ThunderInitPostfix(Thunder_Helper.Thunder __instance)
        {
            try
            {
                Device origin = DamageOrigin.HasScope ? DamageOrigin.Current : null;
                SourceResolver.TagThunder(__instance, origin);
            }
            catch
            {
            }
        }

        private static void ThunderMakeDmgPrefix(Thunder_Helper.Thunder __instance)
        {
            try
            {
                Device origin;
                if (!SourceResolver.TryGetThunderTag(__instance, out origin))
                {
                    origin = null;
                }
                DamageOrigin.Push(origin, true);
            }
            catch
            {
                DamageOrigin.Push(null, true);
            }
        }

        // ---------------------------------------------------------------- relic proc scopes

        private static void RelicMeleeScopePrefix(object[] __args)
        {
            PushSourceScope(__args, 0);
        }

        private static void AssassinateScopePrefix(object[] __args)
        {
            PushSourceScope(__args, 1);
        }

        private static void MuzzleBlastScopePrefix(object[] __args)
        {
            try
            {
                object raw = __args != null && __args.Length > 0 ? __args[0] : null;
                Transform transform = raw as Transform;
                Device origin = SourceResolver.DeviceFromTransform(transform);
                DamageOrigin.Push(origin, true);
            }
            catch
            {
                DamageOrigin.Push(null, true);
            }
        }

        private static void PushSourceScope(object[] args, int index)
        {
            try
            {
                ITarget source = args != null && args.Length > index ? args[index] as ITarget : null;
                bool viaTag;
                bool tagDerived;
                Device origin = SourceResolver.Resolve(source, out viaTag, out tagDerived);
                DamageOrigin.Push(origin, true);
            }
            catch
            {
                DamageOrigin.Push(null, true);
            }
        }
    }
}
