namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// The databases <see cref="ValueObjectCheckConstraintConvention"/> knows how to write a CHECK constraint for, each
/// storing and comparing values in its own way.
/// </summary>
internal enum CheckEngine
{
    /// <summary>SQL Server, through <c>Microsoft.EntityFrameworkCore.SqlServer</c>.</summary>
    SqlServer,

    /// <summary>PostgreSQL, through <c>Npgsql.EntityFrameworkCore.PostgreSQL</c>.</summary>
    PostgreSql,

    /// <summary>SQLite, through <c>Microsoft.EntityFrameworkCore.Sqlite</c>.</summary>
    Sqlite,
}
