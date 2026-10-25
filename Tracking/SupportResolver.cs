using System.Collections.Generic;
using World;
using World.Devices;
using World.Devices.Device_AI;
using World.Helpers;
using World.Projectiles;

namespace OWT.DamageMeter.Tracking
{
    /// <summary>
    /// Finds the mounted support device that supplied an arrow. The quiver replaces
    /// the shooter's projectile id, so the projectile itself carries enough evidence
    /// to link the explosion back to the quiver.
    /// </summary>
    internal static class SupportResolver
    {
        public static Device ResolveProjectileSupport(Projectile projectile)
        {
            if (projectile == null
                || projectile.faction != WorldEnum.Faction.player
                || projectile.desc == null)
            {
                return null;
            }

            uint projectileId = projectile.desc.projectile_id;
            if (projectileId == 0u)
            {
                return null;
            }

            Device emitter = ResolveEmitter(projectile.emitter);
            Device fallback = null;

            try
            {
                List<Device> devices = Device_Slot_Helper.GetAllDevice();
                for (int i = 0; i < devices.Count; i++)
                {
                    Device device = devices[i];
                    DScript_Special_Quiver quiver = device as DScript_Special_Quiver;
                    if (quiver == null
                        || !quiver.should_replace_projectile
                        || quiver.projectile_id != projectileId)
                    {
                        continue;
                    }

                    if (emitter != null
                        && emitter.desc != null
                        && quiver.effect_tag != 0u
                        && emitter.desc.device_tag != null
                        && !emitter.desc.device_tag.Contains(quiver.effect_tag))
                    {
                        continue;
                    }

                    if (quiver.is_activate)
                    {
                        return device;
                    }

                    if (fallback == null)
                    {
                        fallback = device;
                    }
                }
            }
            catch
            {
            }

            return fallback;
        }

        public static bool IsSupportDevice(Device device)
        {
            return device is DScript_Special_Quiver;
        }

        private static Device ResolveEmitter(ITarget source)
        {
            for (int depth = 0; source != null && depth < 8; depth++)
            {
                Device device = source as Device;
                if (device != null)
                {
                    try
                    {
                        return ((ITarget)device).Faction == WorldEnum.Faction.player ? device : null;
                    }
                    catch
                    {
                        return device;
                    }
                }

                Projectile projectile = source as Projectile;
                if (projectile != null)
                {
                    source = projectile.emitter;
                    continue;
                }

                Boomerang boomerang = source as Boomerang;
                if (boomerang != null)
                {
                    return boomerang.boomerang_device;
                }

                return null;
            }

            return null;
        }
    }
}
