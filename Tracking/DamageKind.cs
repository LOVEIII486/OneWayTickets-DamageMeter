namespace OWT.DamageMeter.Tracking
{
    /// <summary>
    /// The damage channel a hit belongs to. Direct means the weapon's own hit;
    /// every other value is a proc or a support-enabled secondary hit.
    /// </summary>
    public enum DamageKind
    {
        Direct = 0,
        Explosion = 1,
        Lightning = 2,
        Dart = 3,
        Ignite = 4,
        Assassinate = 5,
        MuzzleBlast = 6,
        ArrowChasing = 7,
        Blast = 8,
        OtherDerived = 9,
        Hail = 10,
    }
}
