namespace RP.Spectre.Missions
{
    using System.Collections.Generic;
    using System.Linq;
    using RP.Spectre.Ships;

    /// <summary>Overall outcome of a mission.</summary>
    public enum MissionState
    {
        /// <summary>Still being played.</summary>
        InProgress,

        /// <summary>Every objective was satisfied.</summary>
        Succeeded,

        /// <summary>An objective was lost, or a ward (the player or a protected ally) was destroyed.</summary>
        Failed,
    }

    /// <summary>
    /// A mission is a small piece of rules layered over a live <see cref="BattleSimulation"/> (build brief
    /// S12). It owns a set of <see cref="IObjective"/>s that must all be completed to win, and a set of
    /// <i>wards</i> — ships whose loss ends the mission in failure (always the player, optionally an escort).
    /// Splitting "must complete" objectives from "must not die" wards lets one model cover clear-the-field,
    /// timed-survival and escort missions without per-type machinery: an escort is just an eliminate
    /// objective with the freighter added as a ward.
    /// </summary>
    public sealed class Mission
    {
        private readonly List<IObjective> _objectives;
        private readonly List<Combatant> _wards;

        /// <param name="name">Display name.</param>
        /// <param name="objectives">All must be <see cref="ObjectiveStatus.Complete"/> to win (at least one required).</param>
        /// <param name="wards">Ships whose destruction fails the mission — pass the player, plus any escort.</param>
        public Mission(string name, IEnumerable<IObjective> objectives, IEnumerable<Combatant> wards)
        {
            Name = name;
            _objectives = objectives.ToList();
            _wards = wards.ToList();
        }

        public string Name { get; }

        public IReadOnlyList<IObjective> Objectives => _objectives;

        public IReadOnlyList<Combatant> Wards => _wards;

        public MissionState State { get; private set; } = MissionState.InProgress;

        /// <summary>Advances the mission's rules after the battle has stepped by <paramref name="dt"/>.</summary>
        public void Update(BattleSimulation battle, double dt)
        {
            if (State != MissionState.InProgress) return;

            // A lost ward (the player or a protected ally) fails the mission outright.
            if (_wards.Any(w => !w.Alive))
            {
                State = MissionState.Failed;
                return;
            }

            foreach (IObjective objective in _objectives)
            {
                objective.Update(battle, dt);
            }

            if (_objectives.Any(o => o.Status == ObjectiveStatus.Failed))
            {
                State = MissionState.Failed;
                return;
            }

            // A mission with no objectives has no win condition, so it can only ever be lost.
            if (_objectives.Count > 0 && _objectives.All(o => o.Status == ObjectiveStatus.Complete))
            {
                State = MissionState.Succeeded;
            }
        }
    }
}
