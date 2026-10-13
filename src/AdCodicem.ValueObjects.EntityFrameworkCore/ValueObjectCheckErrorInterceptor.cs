using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Adds the code of the rule a CHECK constraint of <c>AddValueObjectCheckConstraints</c> refused to the provider's error,
/// as <see cref="ValueObjectDbContextOptionsBuilderExtensions.UseValueObjectCheckErrors(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder)"/>
/// asks.
/// </summary>
/// <remarks>
/// <para>
/// Entity Framework Core hands a command interceptor the error a provider throws while it executes a command, whatever
/// sent it: <c>SaveChanges</c>, <c>ExecuteUpdate</c>, raw SQL or a migration. It hands it none of the errors a provider
/// throws while it reads the results of a batch, for a statement after the first that returns rows, such as one reading
/// back a generated key or SQL Server's <c>MERGE</c>: <c>SaveChanges</c> wraps that one in a <c>DbUpdateException</c>,
/// which only a save interceptor sees. So the interceptor is both. Nor does it hand any interceptor an error a provider
/// throws while the rows of a query are read, which SqlClient does for a raw SQL query writing rows it reads back
/// through <c>OUTPUT</c>: that error carries no code, and <see cref="ValueObjectCheckConstraints.TryGetCode"/> reads it.
/// </para>
/// <para>
/// It changes nothing but the data of the provider's error, which it neither replaces nor wraps, and leaves alone an error
/// whose data takes no new entry, read-only or of a fixed size. It holds no state, so one instance serves every context.
/// </para>
/// </remarks>
internal sealed class ValueObjectCheckErrorInterceptor : IDbCommandInterceptor, ISaveChangesInterceptor
{
    private ValueObjectCheckErrorInterceptor()
    {
    }

    /// <summary>Gets the interceptor every context's options are given.</summary>
    public static ValueObjectCheckErrorInterceptor Instance { get; } = new();

    /// <inheritdoc />
    public void CommandFailed(DbCommand command, CommandErrorEventData eventData) => Mark(eventData.Exception);

    /// <inheritdoc />
    public Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Mark(eventData.Exception);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void SaveChangesFailed(DbContextErrorEventData eventData) => Mark(eventData.Exception);

    /// <inheritdoc />
    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Mark(eventData.Exception);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stores the code of the violated rule in the provider's error, found in the exception or in one it wraps.
    /// </summary>
    private static void Mark(Exception exception)
    {
        // A dictionary that takes no new entry would throw here, and its exception would replace the provider's.
        if (ValueObjectCheckConstraints.TryFind(exception, out var refusal, out var code)
            && refusal.Data is { IsReadOnly: false, IsFixedSize: false } data)
        {
            data[ValueObjectErrors.ErrorCodeKey] = code;
        }
    }
}
