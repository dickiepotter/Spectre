namespace RP.Spectre.Ships
{
    using RP.Game.Physics;
    using RP.Math;
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

        /// <summary>Directional shields: each of the six facets absorbs hits from its side independently, so
        /// concentrating fire on one quadrant drops that facet (and exposes the hull there) far faster than
        /// spreading damage around the hull (build brief S8.1).</summary>
        public FacetShields Shields { get; }

        public Hull Hull { get; }
        public Weapon Weapon { get; }
        public Capacitor Capacitor { get; }
        public HeatSink Heat { get; }
        public Faction Faction { get; }

        /// <summary>Hull-sphere radius (metres) for targeting/collision.</summary>
        public double Radius { get; }

        /// <summary>Display name for the HUD readout (e.g. "WASP INTERCEPTOR").</summary>
        public string Name { get; }

        /// <summary>The player's own hull, flown externally: the battle treats it as a target (it can be shot
        /// and regenerates its shields) but never steers or fires it, and it is not drawn as a contact.</summary>
        public bool IsPlayer { get; set; }

        public Combatant(Faction faction, RigidBody body, FacetShields shields, Hull hull, Weapon weapon,
            Capacitor capacitor, HeatSink heat, double radius, string name = "")
        {
            Faction = faction;
            Body = body;
            Shields = shields;
            Hull = hull;
            Weapon = weapon;
            Capacitor = capacitor;
            Heat = heat;
            Radius = radius;
            Name = name;
        }

        /// <summary>True while the hull holds.</summary>
        public bool Alive => !Hull.IsDestroyed;

        /// <summary>Total shield strength summed across all facets.</summary>
        public double ShieldCurrent
        {
            get
            {
                double sum = 0;
                foreach (Facet f in AllFacets) sum += Shields[f].Current;
                return sum;
            }
        }

        /// <summary>Total shield capacity summed across all facets.</summary>
        public double ShieldCapacity
        {
            get
            {
                double sum = 0;
                foreach (Facet f in AllFacets) sum += Shields[f].Capacity;
                return sum;
            }
        }

        /// <summary>Overall shield condition in 0..1 (mean over facets).</summary>
        public double ShieldFraction => ShieldCapacity <= 0 ? 0 : ShieldCurrent / ShieldCapacity;

        /// <summary>A single facet's charge in 0..1, for the HUD damage panel.</summary>
        public double FacetFraction(Facet f)
        {
            Shield s = Shields[f];
            return s.Capacity <= 0 ? 0 : s.Current / s.Capacity;
        }

        /// <summary>Hull integrity in 0..1.</summary>
        public double HullFraction => Hull.MaxHp <= 0 ? 0 : Hull.Hp / Hull.MaxHp;

        /// <summary>The facet shield that faces an attacker at <paramref name="worldSource"/> — the side a hit
        /// from there lands on, in this ship's current orientation.</summary>
        public Shield ShieldForHitFrom(Vector3d worldSource)
        {
            Vector3d worldDir = worldSource - Body.Position;        // ship centre -> attacker
            Vector3d local = Body.Orientation.Conjugate().Rotate(worldDir);
            return Shields.ForLocalHit(new Vector3((float)local.X, (float)local.Y, (float)local.Z));
        }

        /// <summary>The six facets, for iterating shield state (e.g. the HUD damage panel).</summary>
        public static readonly Facet[] AllFacets =
        {
            Facet.Fore, Facet.Aft, Facet.Port, Facet.Starboard, Facet.Dorsal, Facet.Ventral,
        };
    }
}
