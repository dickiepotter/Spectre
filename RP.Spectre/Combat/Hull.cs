namespace RP.Spectre.Combat
{
    /// <summary>
    /// A ship's hull: fragile on purpose (build brief S8.2). Once the shield over a region is down, hull
    /// damage is fast — a few solid hits end a fighter. Capital ships add destructible components on top of
    /// this (Phase 6); for now it is a single HP pool.
    /// </summary>
    public sealed class Hull
    {
        /// <summary>Maximum hull HP.</summary>
        public double MaxHp { get; }

        /// <summary>Current hull HP.</summary>
        public double Hp { get; private set; }

        public Hull(double maxHp)
        {
            MaxHp = maxHp;
            Hp = maxHp;
        }

        /// <summary>True once the hull has been breached to nothing — the ship is destroyed.</summary>
        public bool IsDestroyed => Hp <= 0;

        /// <summary>Applies hull damage (clamped at zero).</summary>
        public void TakeDamage(double amount)
        {
            if (amount <= 0) return;
            Hp -= amount;
            if (Hp < 0) Hp = 0;
        }
    }
}
