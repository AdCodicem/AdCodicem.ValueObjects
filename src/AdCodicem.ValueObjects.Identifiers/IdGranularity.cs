namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// The width of the time bucket that heads the body of an entity identifier.
/// </summary>
/// <remarks>
/// <para>
/// The bucket exists to give the index a monotonic head, so that inserts land at the right edge of the B-tree
/// instead of scattering across it. It leaks the creation time of the identifier at exactly this granularity
/// and nothing finer; the random part keeps its full 80 bits either way, so enumeration is unaffected.
/// </para>
/// <para>
/// Choose it from the insert rate of the table, not from taste: aim for a bucket holding roughly 10⁴–10⁵ rows.
/// Wider and writes scatter again, narrower and the identifier leaks more precisely than it needs to.
/// </para>
/// </remarks>
public enum IdGranularity
{
    /// <summary>One bucket per minute. Six characters, usable until the year 4062.</summary>
    Minute,

    /// <summary>One bucket per hour. Four characters, usable until the year 2139. The default.</summary>
    Hour,

    /// <summary>One bucket per day. Three characters, usable until the year 2109.</summary>
    Day,
}
