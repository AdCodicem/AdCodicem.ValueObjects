namespace AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;

/// <summary>
/// The binary collations worth naming, per provider.
/// </summary>
/// <remarks>
/// <para>
/// Applying one is a performance choice, not a correctness one — and that is only true because normalization
/// folds an identifier to a single canonical spelling before it is ever stored. Without that, a
/// case-insensitive collation would collapse two distinct identifiers into one, and a lookup could return the
/// wrong row. With it, the collation only decides how fast the comparison runs.
/// </para>
/// <para>
/// Binary comparison also matches what the application does in memory, where identifiers compare ordinally, so
/// a query and a sort in code agree on the order.
/// </para>
/// </remarks>
public static class IdCollations
{
    /// <summary>
    /// Byte-wise comparison on SQL Server.
    /// </summary>
    /// <remarks>Worth setting explicitly: the default collation of a SQL Server database is case insensitive.</remarks>
    public const string SqlServer = "Latin1_General_BIN2";

    /// <summary>
    /// Byte-wise comparison on PostgreSQL.
    /// </summary>
    /// <remarks>Also what makes a B-tree index usable by a <c>LIKE 'acc_%'</c> prefix scan.</remarks>
    public const string PostgreSql = "C";
}
