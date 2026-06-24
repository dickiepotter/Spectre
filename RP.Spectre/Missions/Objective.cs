namespace RP.Spectre.Missions
{
    using RP.Spectre.Ships;

    /// <summary>Where an objective is in its lifecycle.</summary>
    public enum ObjectiveStatus
    {
        /// <summary>Still being worked on.</summary>
        Active,

        /// <summary>Satisfied — this objective is done.</summary>
        Complete,

        /// <summary>Lost — this objective can no longer be satisfied (e.g. a timed survival was cut short).</summary>
        Failed,
    }

    /// <summary>
    /// One thing a mission asks of the player (build brief S12). An objective watches the live battle each
    /// step and resolves itself to <see cref="ObjectiveStatus.Complete"/> or <see cref="ObjectiveStatus.Failed"/>.
    /// Missions are nothing more than a bag of these plus the rule "all complete = win" (see <see cref="Mission"/>),
    /// so new mission types are new <see cref="IObjective"/> implementations, not new mission machinery.
    /// </summary>
    public interface IObjective
    {
        /// <summary>Human-readable objective text, for the HUD/log.</summary>
        string Description { get; }

        /// <summary>Current lifecycle state.</summary>
        ObjectiveStatus Status { get; }

        /// <summary>Re-evaluates this objective against the battle after it has advanced by <paramref name="dt"/>.</summary>
        void Update(BattleSimulation battle, double dt);
    }

    /// <summary>
    /// "Destroy all enemy forces." Completes the moment no ship of the target faction is left alive. This is
    /// the spine of every clear-the-field engagement (build brief S12.1).
    /// </summary>
    public sealed class EliminateFactionObjective : IObjective
    {
        private readonly Faction _enemy;

        public EliminateFactionObjective(Faction enemy)
        {
            _enemy = enemy;
        }

        public string Description => $"Destroy all {_enemy} forces";

        public ObjectiveStatus Status { get; private set; } = ObjectiveStatus.Active;

        public void Update(BattleSimulation battle, double dt)
        {
            if (Status != ObjectiveStatus.Active) return;
            if (battle.AliveCount(_enemy) == 0) Status = ObjectiveStatus.Complete;
        }
    }

    /// <summary>
    /// "Hold the line for <i>N</i> seconds" — keep a particular ship alive for a fixed duration (build brief
    /// S12.2). Completes when the clock runs out; fails the instant the subject is destroyed before then. Use
    /// it for the player ("survive the ambush") or an ally ("keep the freighter intact until extraction").
    /// </summary>
    public sealed class KeepAliveObjective : IObjective
    {
        private readonly Combatant _subject;
        private readonly double _duration;
        private double _elapsed;

        public KeepAliveObjective(Combatant subject, double duration, string description)
        {
            _subject = subject;
            _duration = duration;
            Description = description;
        }

        public string Description { get; }

        public ObjectiveStatus Status { get; private set; } = ObjectiveStatus.Active;

        public void Update(BattleSimulation battle, double dt)
        {
            if (Status != ObjectiveStatus.Active) return;

            if (!_subject.Alive)
            {
                Status = ObjectiveStatus.Failed;
                return;
            }

            _elapsed += dt;
            if (_elapsed >= _duration) Status = ObjectiveStatus.Complete;
        }
    }
}
