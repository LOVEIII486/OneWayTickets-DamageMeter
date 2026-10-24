using System.Runtime.CompilerServices;
using UnityEngine;
using World;
using World.Caravans;
using World.Devices;
using World.Devices.DeviceViews;
using World.Devices.Device_AI;
using World.Helpers;
using World.Projectiles;

namespace OWT.DamageMeter.Tracking
{
    internal static class SourceResolver
    {
        private sealed class Tag
        {
            public Device Device;
            public bool Derived;
        }

        private static readonly ConditionalWeakTable<Projectile, Tag> ProjectileTags =
            new ConditionalWeakTable<Projectile, Tag>();

        private static readonly ConditionalWeakTable<Thunder_Helper.Thunder, Tag> ThunderTags =
            new ConditionalWeakTable<Thunder_Helper.Thunder, Tag>();

        public static void TagProjectile(Projectile projectile, Device device, bool derived)
        {
            if (projectile == null)
            {
                return;
            }

            ProjectileTags.Remove(projectile);
            if (device != null)
            {
                ProjectileTags.Add(projectile, new Tag { Device = device, Derived = derived });
            }
        }

        public static bool TryGetProjectileTag(Projectile projectile, out Device device, out bool derived)
        {
            device = null;
            derived = false;
            Tag tag;
            if (projectile != null && ProjectileTags.TryGetValue(projectile, out tag) && tag != null)
            {
                device = tag.Device;
                derived = tag.Derived;
                return device != null;
            }
            return false;
        }

        public static void TagThunder(Thunder_Helper.Thunder thunder, Device device)
        {
            if (thunder == null)
            {
                return;
            }

            ThunderTags.Remove(thunder);
            if (device != null)
            {
                // Lightning is always a proc, never a direct hit.
                ThunderTags.Add(thunder, new Tag { Device = device, Derived = true });
            }
        }

        public static bool TryGetThunderTag(Thunder_Helper.Thunder thunder, out Device device)
        {
            device = null;
            Tag tag;
            if (thunder != null && ThunderTags.TryGetValue(thunder, out tag) && tag != null)
            {
                device = tag.Device;
                return device != null;
            }
            return false;
        }

        public static Device Resolve(ITarget source, out bool viaTag, out bool tagDerived)
        {
            viaTag = false;
            tagDerived = false;

            ITarget cursor = source;
            for (int depth = 0; cursor != null && depth < 8; depth++)
            {
                Projectile projectile = cursor as Projectile;
                if (projectile != null)
                {
                    Device tagged;
                    bool derived;
                    if (TryGetProjectileTag(projectile, out tagged, out derived))
                    {
                        viaTag = true;
                        tagDerived = derived;
                        return tagged;
                    }
                    cursor = projectile.emitter;
                    continue;
                }

                Device device = cursor as Device;
                if (device != null)
                {
                    return IsPlayerDevice(device) ? device : null;
                }

                Boomerang boomerang = cursor as Boomerang;
                if (boomerang != null)
                {
                    Device owner = boomerang.boomerang_device;
                    return owner != null && IsPlayerDevice(owner) ? owner : null;
                }

                return null;
            }

            return null;
        }

        /// <summary>
        /// Resolve the weapon responsible for a hit. Direct harm_source wins; otherwise the
        /// ambient proc context is used. enemySource means the hit came from the opposite
        /// faction and should not be counted as player output.
        /// </summary>
        public static Device ResolveForHit(ITarget harmSource, out bool derived, out bool enemySource)
        {
            derived = false;
            enemySource = false;

            if (harmSource != null)
            {
                if (IsOppositeFaction(harmSource))
                {
                    enemySource = true;
                    return null;
                }

                bool viaTag;
                bool tagDerived;
                Device direct = Resolve(harmSource, out viaTag, out tagDerived);
                if (direct != null)
                {
                    derived = (viaTag && tagDerived)
                        || (DamageOrigin.HasScope
                            && DamageOrigin.CurrentDerived
                            && ReferenceEquals(DamageOrigin.Current, direct));
                    return direct;
                }
            }

            if (DamageOrigin.HasScope)
            {
                Device ambient = DamageOrigin.Current;
                if (ambient != null)
                {
                    derived = true;
                    return ambient;
                }
            }

            return null;
        }

        public static Device DeviceFromTransform(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            DeviceView view = transform.GetComponentInParent<DeviceView>();
            Device device = view != null ? view.owner : null;
            return device != null && IsPlayerDevice(device) ? device : null;
        }

        public static bool IsPlayerDevice(Device device)
        {
            if (device == null)
            {
                return false;
            }

            try
            {
                return ((ITarget)device).Faction == WorldEnum.Faction.player;
            }
            catch
            {
                return true;
            }
        }

        public static bool IsOppositeFaction(ITarget target)
        {
            if (target == null)
            {
                return false;
            }

            try
            {
                return target.Faction == WorldEnum.Faction.opposite;
            }
            catch
            {
                return false;
            }
        }

        public static string DescribeUnresolved(ITarget source)
        {
            if (source == null)
            {
                return "环境 / 未归属";
            }
            if (source is Projectile)
            {
                return "未追踪投射物";
            }
            if (source is Caravan)
            {
                return "车厢 / 碾压";
            }
            if (source is Device)
            {
                return "敌方装置";
            }

            string name = source.GetType().Name;
            if (name.IndexOf("Character", System.StringComparison.Ordinal) >= 0
                || name.IndexOf("Adherent", System.StringComparison.Ordinal) >= 0)
            {
                return "角色";
            }
            return name;
        }
    }
}
