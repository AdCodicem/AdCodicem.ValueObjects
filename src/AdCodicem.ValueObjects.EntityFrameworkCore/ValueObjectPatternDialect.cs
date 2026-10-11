namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// The regular expressions of the database a pattern is written for as a CHECK constraint, which the model cannot tell.
/// </summary>
public enum ValueObjectPatternDialect
{
    /// <summary>
    /// No pattern is written.
    /// </summary>
    None,

    /// <summary>
    /// SQL Server 2025, which checks a pattern with <c>REGEXP_LIKE</c> in a database at compatibility level 170: an
    /// earlier server, or a database at an earlier level, refuses the constraint.
    /// </summary>
    SqlServer2025,

    /// <summary>
    /// PostgreSQL, which checks a pattern with its <c>~</c> operator, under the <c>C</c> collation.
    /// </summary>
    PostgreSql,
}
