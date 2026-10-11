using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Translates what a LINQ query reads of a value object.
/// </summary>
public static class ValueObjectDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Translates <c>.Value</c> of a value object, in a LINQ query, to its column, typed as the underlying value.
    /// </summary>
    /// <param name="optionsBuilder">Options builder of the context.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Entity Framework Core translates a comparison between value objects, <c>x.Iban == iban</c>, but nothing a query
    /// reads of the value one holds: <c>x.Iban.Value.StartsWith("FR")</c> or <c>x.Quantity.Value &gt; 2</c> fail, as
    /// "could not be translated". Once this is called, <c>.Value</c> reads the column itself, under the type mapping of
    /// the underlying type for the column's own store type: <c>WHERE [o].[Iban] LIKE N'FR%'</c>, which the index on the
    /// column serves, and a parameter compared with it is declared as the column is, <c>nvarchar(34)</c>. An optional
    /// value object, a property inside a JSON column, an element of a primitive collection and a construction of a
    /// generic value object are translated too.
    /// </para>
    /// <para>
    /// Only a property this package maps is translated: through <c>ConfigureValueObjects</c>,
    /// <c>HasValueObjectConversion</c>, <c>ConfigureEntityIds</c> or <c>HasEntityIdConversion</c>, whose converters
    /// store the bare value, including where Entity Framework Core composes one with its own conversion of the
    /// underlying type, a <see cref="ushort"/> to an <c>int</c> column, say. A property mapped through a converter of the
    /// application's own, as a value object over <see cref="Int128"/> must be, keeps Entity Framework Core's refusal: what
    /// its column holds may not be the value. So does this package's converter composed with a conversion of the
    /// application's own, except over a type the provider converts itself, a <see cref="ushort"/> or a
    /// <see cref="Uri"/>, where it cannot be told from Entity Framework Core's composition.
    /// </para>
    /// <para>
    /// A column of a table is read as it stands. Anything else, the element of a collection, a column an optional
    /// navigation joins in, a <c>COALESCE</c>, is converted to the underlying type by a <c>CAST</c> to its own store type,
    /// which Entity Framework Core 11 leaves out of the SQL.
    /// </para>
    /// <para>
    /// <c>.Value</c> reads what the column holds, in a projection too: a strict context,
    /// <c>ConfigureValueObjects(strict: true)</c>, validates a value object it materializes, not a <c>.Value</c> a query
    /// projects. Project the value object to have it validated.
    /// </para>
    /// <para>
    /// Nothing but <c>.Value</c> is translated: <c>ToString()</c>, which follows the formatting hooks, and
    /// <c>EF.Property</c> still fail, and so does <c>.Value</c> of an element of a collection passed as a parameter, which
    /// has no type mapping yet when it is translated; a conversion operator stays the cast Entity Framework Core writes,
    /// to the unbounded type.
    /// </para>
    /// <para>
    /// The translation runs once per query shape, which Entity Framework Core caches: nothing more runs per execution.
    /// A query precompiled by <c>dotnet ef dbcontext optimize --precompile-queries</c> carries the SQL it wrote, so the
    /// options the design-time context is built with call this too, or the precompilation fails on that query. The
    /// translator works on a relational provider; with any other, this does nothing.
    /// </para>
    /// <para>
    /// Entity Framework Core adds the translator to the service provider it builds for the context, never to one the
    /// application hands it through <c>UseInternalServiceProvider</c>: a context started on such a provider throws an
    /// <see cref="InvalidOperationException"/> as it starts, unless the services of the extensions of its options were
    /// added to that provider, through <c>IDbContextOptionsExtension.ApplyServices</c>.
    /// </para>
    /// </remarks>
    public static DbContextOptionsBuilder UseValueObjectQueries(this DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        // The extension holds no option, so a second call replaces it with one just like it.
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new ValueObjectQueryOptionsExtension());

        return optionsBuilder;
    }

    /// <summary>
    /// Translates <c>.Value</c> of a value object, in a LINQ query, to its column, typed as the underlying value.
    /// </summary>
    /// <typeparam name="TContext">Type of the context.</typeparam>
    /// <param name="optionsBuilder">Options builder of the context.</param>
    /// <returns>The same builder, so calls can be chained and its options keep the type of the context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The same as <see cref="UseValueObjectQueries(DbContextOptionsBuilder)"/>, for a builder of the options of one
    /// context type.
    /// </remarks>
    public static DbContextOptionsBuilder<TContext> UseValueObjectQueries<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseValueObjectQueries((DbContextOptionsBuilder)optionsBuilder);
}
