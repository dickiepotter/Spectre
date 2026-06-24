namespace RP.Spectre.Combat
{
    /// <summary>
    /// A shield facing. Capital ships have six directional facets that regenerate independently, so a
    /// flanker can hammer one weakened side rather than spreading damage across the whole shield (build
    /// brief S8.1). Fighters use the single <see cref="Omni"/> facing.
    /// </summary>
    public enum Facet
    {
        Fore,
        Aft,
        Port,
        Starboard,
        Dorsal,
        Ventral,
        Omni,
    }
}
