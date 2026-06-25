namespace RP.Spectre.Missions
{
    /// <summary>
    /// Spectre's story spine (build brief S12): the ordered run of beats from the opening shakedown, through
    /// the graveyard fleet battles, to the descent into the <i>Tantalus</i> and its core. The arc deliberately
    /// pivots at the descent — open, fast fleet combat gives way to the claustrophobic interior. Content as
    /// data, built on the generic <see cref="Campaign"/>.
    /// </summary>
    public static class SpectreCampaign
    {
        /// <summary>Builds the campaign, optionally resuming at a saved <paramref name="progress"/> index.</summary>
        public static Campaign Build(int progress = 0) => new(
            new[]
            {
                new MissionBrief
                {
                    Name = "Shakedown",
                    Briefing = "Prototype trials at the edge of the graveyard. Learn the Spectre's teeth on a lone Severance picket.",
                },
                new MissionBrief
                {
                    Name = "The Graveyard",
                    Briefing = "A Coalition wing sweeps the debris field. Hold formation and clear the Severance screen.",
                },
                new MissionBrief
                {
                    Name = "Severance Ambush",
                    Briefing = "Reinforcements were a trap. Survive the swarm until the Warden frigates arrive.",
                },
                new MissionBrief
                {
                    Name = "Into the Tantalus",
                    Briefing = "The dreadnought's hulk tumbles dark and silent. Match its spin and enter the breach.",
                    IsDescent = true,
                },
                new MissionBrief
                {
                    Name = "The Core",
                    Briefing = "Whatever killed the Tantalus is still aboard. Reach the reactor core and end it.",
                    IsDescent = true,
                },
            },
            progress);
    }
}
