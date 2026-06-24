namespace RP.Spectre.Ships
{
    using RP.Game.Physics;
    using RP.Spectre.Combat;

    /// <summary>Which side a ship fights for.</summary>
    public enum Faction
    {
        /// <summary>The friendly Earth Coalition (build brief S10.1).</summary>
        Coalition,

        /// <summary>The hostile Severance breakaway faction (S10.2).</summary>
        Severance,
    }

    /// <summary>
    /// A ship in a battle: its physical body plus the combat state (shield, hull, one weapon and its
    /// heat/capacitor) and which faction it belongs to. This bundles the engine's physics with the game's
    /// combat rules into the single thing the AI and the battle loop push around.
    /// </summary>
    public sealed class Combatant
    {
        public RigidBody Body { get; }
        public Shield Shield { get; }
        public Hull Hull { get; }
        public Weapon Weapon { get; }
        public Capacitor Capacitor { get; }
        public HeatSink Heat { get; }
        public Faction Faction { get; }

        /// <summary>Hull-sphere radius (metres) for targeting/collision.</summary>
        public double Radius { get; }

        public Combatant(Faction faction, RigidBody body, Shield shield, Hull hull, Weapon weapon,
            Capacitor capacitor, HeatSink heat, double radius)
        {
            Faction = faction;
            Body = body;
            Shield = shield;
            Hull = hull;
            Weapon = weapon;
            Capacitor = capacitor;
            Heat = heat;
            Radius = radius;
        }

        /// <summary>True while the hull holds.</summary>
        public bool Alive => !Hull.IsDestroyed;
    }
}
