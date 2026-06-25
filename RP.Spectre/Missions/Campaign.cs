namespace RP.Spectre.Missions
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// One beat of the campaign as data (build brief S12): its name, the pre-mission briefing text, and a flag
    /// for the descent into the <i>Tantalus</i> (the act break where the game turns from open fleet combat to
    /// the dread-soaked interior). The live <see cref="Mission"/> — objectives, wards, the spawned battle — is
    /// built when the beat is entered; this is just the spine entry.
    /// </summary>
    public sealed class MissionBrief
    {
        public string Name { get; init; } = "";
        public string Briefing { get; init; } = "";

        /// <summary>True for the beat that takes the player inside the wreck.</summary>
        public bool IsDescent { get; init; }
    }

    /// <summary>
    /// The campaign spine: an ordered list of <see cref="MissionBrief"/>s and a cursor through them (build
    /// brief S12/S21.2). It advances only when a mission is actually won, so a loss replays the same beat, and
    /// its <see cref="Progress"/> is exactly the integer the save schema persists — so resuming drops the
    /// player back on the right mission. Pure progression bookkeeping; it owns no battle state.
    /// </summary>
    public sealed class Campaign
    {
        private readonly List<MissionBrief> _missions;

        /// <param name="missions">The ordered beats.</param>
        /// <param name="progress">Resume cursor (e.g. <c>SpectreSaveData.MissionProgress</c>); clamped to range.</param>
        public Campaign(IEnumerable<MissionBrief> missions, int progress = 0)
        {
            _missions = missions.ToList();
            Progress = progress < 0 ? 0 : progress > _missions.Count ? _missions.Count : progress;
        }

        public IReadOnlyList<MissionBrief> Missions => _missions;

        /// <summary>Index of the current beat; equals <see cref="System.Collections.Generic.List{T}.Count"/> when finished.</summary>
        public int Progress { get; private set; }

        /// <summary>True once every beat has been won.</summary>
        public bool IsComplete => Progress >= _missions.Count;

        /// <summary>The beat now being played, or null if the campaign is finished.</summary>
        public MissionBrief? Current => IsComplete ? null : _missions[Progress];

        /// <summary>
        /// Records a mission outcome. A <see cref="MissionState.Succeeded"/> advances to the next beat; a loss
        /// or an unfinished mission leaves the cursor where it is (you replay it).
        /// </summary>
        public void Record(MissionState outcome)
        {
            if (!IsComplete && outcome == MissionState.Succeeded) Progress++;
        }
    }
}
