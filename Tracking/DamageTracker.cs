using System;
using System.Collections.Generic;
using AutoCodes;
using Commons;
using OWT.DamageMeter.UI;
using UnityEngine;
using World;
using World.Devices;
using World.Devices.Device_AI;

namespace OWT.DamageMeter.Tracking
{
    public sealed class EffectStat
    {
        public DamageKind Kind;
        public Device Support;
        public double Damage;
    }

    public sealed class AssistStat
    {
        public Device Weapon;
        public double Damage;
    }

    public sealed class WeaponStat
    {
        public Device Device;
        public uint DeviceId;
        public int Rank;
        public string Name;
        public string SlotKey;
        public string IconKey;
        public bool MetaResolved;
        public int MetaAttempts;
        public float NextMetaRetry;
        public bool Mounted;
        public bool LoggedFirstHit;

        public double TotalDamage;
        public double DirectDamage;
        public double DerivedDamage;
        public double AssistDamage;
        public double MountedSeconds;
        public int Hits;
        public int CritHits;

        public readonly Dictionary<string, double> DamageTypes = new Dictionary<string, double>(4);
        public readonly List<EffectStat> Effects = new List<EffectStat>(4);
        public readonly List<AssistStat> Assisted = new List<AssistStat>(2);

        public bool HasDamage
        {
            get { return TotalDamage > 0.0; }
        }

        public bool HasContribution
        {
            get { return TotalDamage > 0.0 || AssistDamage > 0.0; }
        }

        public bool IsSupport
        {
            get
            {
                return Device is DScript_Special_Quiver
                    || (AssistDamage > 0.0 && TotalDamage <= 0.0);
            }
        }

        public bool IsWheelSlot
        {
            get { return !string.IsNullOrEmpty(SlotKey) && SlotKey.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0; }
        }

        public bool Destroyed
        {
            get { return Device != null && !Device.is_validate; }
        }

        public double Dps
        {
            get { return TotalDamage / Math.Max(1.0, MountedSeconds); }
        }

        public double AssistDps
        {
            get { return AssistDamage / Math.Max(1.0, MountedSeconds); }
        }

        public void Add(int damage, bool derived, string damageType,
            DamageKind kind, Device support)
        {
            TotalDamage += damage;
            if (derived)
            {
                DerivedDamage += damage;
                AddEffect(kind, support, damage);
            }
            else
            {
                DirectDamage += damage;
            }
            Hits++;

            string type = string.IsNullOrEmpty(damageType) ? "normal" : damageType;
            double old;
            DamageTypes.TryGetValue(type, out old);
            DamageTypes[type] = old + damage;
        }

        public void AddAssist(Device weapon, int damage, DamageKind kind)
        {
            AssistDamage += damage;
            AddEffect(kind, null, damage);

            for (int i = 0; i < Assisted.Count; i++)
            {
                if (ReferenceEquals(Assisted[i].Weapon, weapon))
                {
                    Assisted[i].Damage += damage;
                    return;
                }
            }
            Assisted.Add(new AssistStat { Weapon = weapon, Damage = damage });
        }

        private void AddEffect(DamageKind kind, Device support, int damage)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                EffectStat effect = Effects[i];
                if (effect.Kind == kind && ReferenceEquals(effect.Support, support))
                {
                    effect.Damage += damage;
                    return;
                }
            }
            Effects.Add(new EffectStat { Kind = kind, Support = support, Damage = damage });
        }

        public void ResetStats()
        {
            TotalDamage = 0.0;
            DirectDamage = 0.0;
            DerivedDamage = 0.0;
            AssistDamage = 0.0;
            MountedSeconds = 0.0;
            Hits = 0;
            CritHits = 0;
            LoggedFirstHit = false;
            DamageTypes.Clear();
            Effects.Clear();
            Assisted.Clear();
        }
    }

    public sealed class DamageTracker
    {
        private const float MaxDelta = 0.25f;

        public static DamageTracker Current;

        private readonly Dictionary<Device, WeaponStat> _stats = new Dictionary<Device, WeaponStat>();
        private readonly List<WeaponStat> _visible = new List<WeaponStat>();
        private readonly HashSet<Device> _mounted = new HashSet<Device>();
        private readonly HashSet<string> _loggedUnresolved = new HashSet<string>();
        private bool _errorLogged;
        private static bool _loggedRanks;

        public double SessionSeconds { get; private set; }
        public double OtherDamage { get; private set; }
        public int OtherHits { get; private set; }

        public IEnumerable<WeaponStat> AllStats
        {
            get { return _stats.Values; }
        }

        public int WeaponStatCount
        {
            get { return _stats.Count; }
        }

        public void AdvanceSession(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                SessionSeconds += Math.Min(deltaTime, MaxDelta);
            }
        }

        public void RefreshRoster(DeviceMgr manager, float activeDelta)
        {
            foreach (WeaponStat stat in _stats.Values)
            {
                stat.Mounted = false;
            }
            _mounted.Clear();

            if (manager == null || manager.slots_device == null)
            {
                return;
            }

            float dt = Math.Max(0f, Math.Min(activeDelta, MaxDelta));
            foreach (DeviceSlot slot in manager.slots_device)
            {
                Device device = slot.slot_device;
                if (device == null)
                {
                    continue;
                }

                _mounted.Add(device);
                WeaponStat stat = GetOrCreate(device);
                stat.SlotKey = slot._name;
                stat.Mounted = true;
                if (!stat.MetaResolved && Time.unscaledTime >= stat.NextMetaRetry)
                {
                    ResolveMeta(stat);
                }
                if (dt > 0f)
                {
                    stat.MountedSeconds += dt;
                }
            }
        }

        private WeaponStat GetOrCreate(Device device)
        {
            WeaponStat stat;
            if (!_stats.TryGetValue(device, out stat))
            {
                stat = new WeaponStat { Device = device };
                _stats.Add(device, stat);
                ResolveMeta(stat);
            }
            return stat;
        }

        private static void ResolveMeta(WeaponStat stat)
        {
            Device device = stat.Device;
            if (device == null || device.desc == null)
            {
                return;
            }

            stat.DeviceId = device.desc.id;
            stat.Rank = device.desc.rank;
            stat.IconKey = device.desc.icon_photo;

            string raw = device.desc.name;
            bool keyLike = IsKeyLike(raw);
            string localized = string.IsNullOrEmpty(raw) ? null : LocalizeName(raw);
            if (!string.IsNullOrEmpty(localized) && !(keyLike && string.Equals(localized, raw, StringComparison.Ordinal)))
            {
                stat.Name = localized;
                stat.MetaResolved = true;
            }
            else if (!keyLike)
            {
                stat.Name = string.IsNullOrEmpty(localized) ? Strings.UnknownDevice : localized;
                stat.MetaResolved = true;
            }
            else
            {
                // The l10n table may not be ready yet; retry a few times before
                // accepting the raw key as the display name.
                stat.MetaAttempts++;
                stat.NextMetaRetry = Time.unscaledTime + 0.5f;
                if (stat.MetaAttempts >= 10)
                {
                    stat.Name = raw;
                    stat.MetaResolved = true;
                }
            }

            if (!stat.MetaResolved)
            {
                return;
            }

            DamageMeterPlugin.Log.LogInfo(string.Format(
                "Device {0}: name='{1}' rank={2} ({3}) icon='{4}'",
                stat.DeviceId, stat.Name, stat.Rank, Theme.QualityName(stat.Rank), stat.IconKey));

            if (!_loggedRanks)
            {
                _loggedRanks = true;
                try
                {
                    foreach (KeyValuePair<string, device_rank> pair in device_ranks.records)
                    {
                        DamageMeterPlugin.Log.LogInfo("Device rank " + pair.Key + " -> "
                            + pair.Value.img_name_bar);
                    }
                    DamageMeterPlugin.Log.LogInfo(
                        "Device rank colours: 0=grey, 1=green, 2=blue, 3=purple, 4=gold, 99+=wheel brown.");
                }
                catch
                {
                }
            }

            if (DamageMeterPlugin.VerboseLogging != null && DamageMeterPlugin.VerboseLogging.Value)
            {
                DamageMeterPlugin.Log.LogInfo(string.Format(
                    "Device meta id={0} type={1} raw='{2}' name='{3}' icon='{4}'",
                    stat.DeviceId,
                    device.GetType().Name,
                    raw,
                    stat.Name,
                    stat.IconKey));
            }
        }

        private static string LocalizeName(string raw)
        {
            string value = Localization_Utility.get_localization(raw);
            if (!string.IsNullOrEmpty(value) && !string.Equals(value, raw, StringComparison.Ordinal))
            {
                return value;
            }

            try
            {
                table_l10n record;
                if (table_l10ns.TryGetValue(raw, out record) && record != null)
                {
                    string[] candidates =
                    {
                        record.CN, record.EN, record.CHT, record.JA, record.KO, record.ES, record.RU
                    };
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(candidates[i]))
                        {
                            return candidates[i];
                        }
                    }
                }
            }
            catch
            {
            }

            return value;
        }

        private static bool IsKeyLike(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }
            if (raw.IndexOf('_') >= 0)
            {
                return true;
            }
            return raw.Length >= 12
                && string.Equals(raw, raw.ToUpperInvariant(), StringComparison.Ordinal)
                && raw.IndexOfAny(new[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L',
                    'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' }) >= 0;
        }

        public void RecordDamage(ITarget harmSource, Attack_Data attack, Dmg_Data damage)
        {
            try
            {
                if (DamageMeterPlugin.Enabled == null || !DamageMeterPlugin.Enabled.Value)
                {
                    return;
                }
                if (damage.dmg <= 0 || damage.is_heal || damage.is_miss || damage.is_ignore)
                {
                    return;
                }

                SourceResolver.HitSource hit = SourceResolver.ResolveForHit(harmSource);
                if (hit.EnemySource)
                {
                    return;
                }

                if (hit.Device == null)
                {
                    AddOther(damage.dmg);
                    if (DamageMeterPlugin.VerboseLogging != null && DamageMeterPlugin.VerboseLogging.Value)
                    {
                        LogUnresolved(harmSource, attack, damage);
                    }
                    return;
                }

                WeaponStat stat = GetOrCreate(hit.Device);
                if (hit.Derived && DamageMeterPlugin.CountDerivativeDamage != null
                    && !DamageMeterPlugin.CountDerivativeDamage.Value)
                {
                    AddOther(damage.dmg);
                    return;
                }

                stat.Add(damage.dmg, hit.Derived, damage.diy_dmg_str, hit.Kind, hit.Support);
                if (hit.Derived && hit.Support != null && !ReferenceEquals(hit.Support, hit.Device))
                {
                    WeaponStat supportStat = GetOrCreate(hit.Support);
                    bool firstAssist = supportStat.AssistDamage <= 0.0;
                    supportStat.AddAssist(hit.Device, damage.dmg, hit.Kind);
                    if (firstAssist
                        && DamageMeterPlugin.VerboseLogging != null
                        && DamageMeterPlugin.VerboseLogging.Value)
                    {
                        DamageMeterPlugin.Log.LogInfo(string.Format(
                            "Support {0} assisted {1} with {2} ({3} damage).",
                            GetDeviceName(hit.Support),
                            GetDeviceName(hit.Device),
                            hit.Kind,
                            damage.dmg));
                    }
                }
                if (damage.is_critical)
                {
                    stat.CritHits++;
                }

                if (DamageMeterPlugin.VerboseLogging != null
                    && DamageMeterPlugin.VerboseLogging.Value
                    && !stat.LoggedFirstHit)
                {
                    stat.LoggedFirstHit = true;
                    DamageMeterPlugin.Log.LogInfo(string.Format(
                        "First hit for {0}: {1} damage ({2}), kind={3}, type={4}, source={5}",
                        stat.Name ?? hit.Device.GetType().Name,
                        damage.dmg,
                        hit.Derived ? "derived" : "direct",
                        hit.Kind,
                        string.IsNullOrEmpty(damage.diy_dmg_str) ? "normal" : damage.diy_dmg_str,
                        harmSource == null ? "null" : harmSource.GetType().Name));
                }
            }
            catch (Exception ex)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    DamageMeterPlugin.Log.LogError("Damage tracking failed:\n" + ex);
                }
            }
        }

        public string GetDeviceDescription(Device device)
        {
            if (device == null || device.desc == null)
            {
                return null;
            }

            string raw = device.desc.alt_info;
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            string localized = LocalizeName(raw);
            if (string.IsNullOrEmpty(localized) || IsKeyLike(localized))
            {
                return null;
            }

            return StripTags(localized);
        }

        private static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string source = text.Replace("<br>", " ").Replace("<br/>", " ").Replace("<br />", " ");
            System.Text.StringBuilder builder = new System.Text.StringBuilder(source.Length);
            bool inTag = false;
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>')
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    builder.Append(c);
                }
            }
            return builder.ToString().Trim();
        }

        public string GetDeviceName(Device device)
        {
            if (device == null)
            {
                return Strings.UnknownDevice;
            }

            WeaponStat stat;
            if (_stats.TryGetValue(device, out stat) && stat.MetaResolved)
            {
                return string.IsNullOrEmpty(stat.Name) ? Strings.UnknownDevice : stat.Name;
            }

            if (device.desc != null)
            {
                string raw = device.desc.name;
                string localized = LocalizeName(raw);
                if (!string.IsNullOrEmpty(localized) && !IsKeyLike(localized))
                {
                    return localized;
                }
                if (!string.IsNullOrEmpty(raw) && !IsKeyLike(raw))
                {
                    return raw;
                }
            }

            return device.GetType().Name;
        }

        private void AddOther(int damage)
        {
            OtherDamage += damage;
            OtherHits++;
        }

        private void LogUnresolved(ITarget source, Attack_Data attack, Dmg_Data damage)
        {
            string label = SourceResolver.DescribeUnresolved(source);
            if (_loggedUnresolved.Add(label))
            {
                DamageMeterPlugin.Log.LogInfo(string.Format(
                    "Unresolved damage source '{0}' ({1} damage, type={2}); counted as other.",
                    label,
                    damage.dmg,
                    string.IsNullOrEmpty(damage.diy_dmg_str) ? "normal" : damage.diy_dmg_str));
            }
        }

        /// <summary>
        /// Rebuilds the sorted display list. Mounted weapons are always candidates; removed
        /// weapons stay only when they dealt damage and the user asked to keep them.
        /// </summary>
        public List<WeaponStat> BuildVisibleRows()
        {
            _visible.Clear();
            bool showZero = DamageMeterPlugin.ShowZeroDamageDevices == null
                || DamageMeterPlugin.ShowZeroDamageDevices.Value;
            bool showRemoved = DamageMeterPlugin.ShowRemovedDevices == null
                || DamageMeterPlugin.ShowRemovedDevices.Value;

            foreach (WeaponStat stat in _stats.Values)
            {
                if (stat.HasContribution)
                {
                    if (stat.Mounted || showRemoved)
                    {
                        _visible.Add(stat);
                    }
                }
                else if (stat.Mounted && showZero && !stat.IsWheelSlot)
                {
                    _visible.Add(stat);
                }
            }

            _visible.Sort(CompareStats);
            return _visible;
        }

        private static int CompareStats(WeaponStat a, WeaponStat b)
        {
            int bySupport = a.IsSupport.CompareTo(b.IsSupport);
            if (bySupport != 0)
            {
                return bySupport;
            }

            if (a.IsSupport && b.IsSupport)
            {
                int byAssist = b.AssistDamage.CompareTo(a.AssistDamage);
                if (byAssist != 0)
                {
                    return byAssist;
                }
            }

            int byDamage = b.TotalDamage.CompareTo(a.TotalDamage);
            if (byDamage != 0)
            {
                return byDamage;
            }

            int byMounted = b.Mounted.CompareTo(a.Mounted);
            if (byMounted != 0)
            {
                return byMounted;
            }

            return SlotRank(a.SlotKey).CompareTo(SlotRank(b.SlotKey));
        }

        private static int SlotRank(string slot)
        {
            switch (slot)
            {
                case "slot_top": return 0;
                case "slot_front_top": return 1;
                case "slot_front": return 2;
                case "slot_back": return 3;
                case "slot_back_top": return 4;
                case "device_slot_wheel": return 5;
                default: return 9;
            }
        }

        public static double SumDamage(List<WeaponStat> rows)
        {
            double total = 0.0;
            for (int i = 0; i < rows.Count; i++)
            {
                total += rows[i].TotalDamage;
            }
            return total;
        }

        public double SumDps(List<WeaponStat> rows)
        {
            return SumDamage(rows) / Math.Max(1.0, SessionSeconds);
        }

        public double OtherDps
        {
            get { return OtherDamage / Math.Max(1.0, SessionSeconds); }
        }

        public bool ShowOther
        {
            get
            {
                return OtherDamage > 0.0
                    && (DamageMeterPlugin.ShowOtherSources == null || DamageMeterPlugin.ShowOtherSources.Value);
            }
        }

        public void ResetStats()
        {
            SessionSeconds = 0.0;
            OtherDamage = 0.0;
            OtherHits = 0;
            _loggedUnresolved.Clear();
            foreach (WeaponStat stat in _stats.Values)
            {
                stat.ResetStats();
            }
        }
    }
}
