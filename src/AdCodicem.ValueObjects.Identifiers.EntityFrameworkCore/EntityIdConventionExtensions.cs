using System.Reflection;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;

/// <summary>
/// Maps entity identifiers onto the narrowest column that can hold them.
/// </summary>
/// <remarks>
/// <para>
/// An identifier is a value object, so <c>ConfigureValueObjects</c> already maps it. What this adds is
/// everything that follows from the identifier being fixed-width and ASCII: <c>char(n)</c> rather than
/// <c>varchar(n)</c>, non-Unicode so a SQL Server column is not silently doubled to <c>nchar</c>, and
/// optionally a binary collation.
/// </para>
/// <para>
/// It also applies the conversion itself, so a model holding nothing but identifiers needs this call alone.
/// Calling both is fine: the second call configures the same properties the same way.
/// </para>
/// <para>
/// What it deliberately does not do is decide the physical layout of your tables. On SQL Server a primary key
/// is clustered by default, which makes the table itself order by the key; declaring it
/// <c>IsClustered(false)</c> confines index churn to the ~30-byte index instead of the whole row, and with the
/// monotonic time bucket at the head of the body the fill factor can go back up towards 95. Those are choices
/// about your schema, not about the identifier type, and they need the provider-specific packages.
/// </para>
/// </remarks>
public static class EntityIdConventionExtensions
{
    /// <summary>
    /// Maps every registered entity identifier to a fixed-width column.
    /// </summary>
    /// <param name="builder">Model configuration builder, from <c>DbContext.ConfigureConventions</c>.</param>
    /// <param name="assemblies">
    /// Assemblies declaring the identifiers. When none is given, everything already registered is mapped, which
    /// is enough as soon as the entity assembly has been loaded.
    /// </param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static ModelConfigurationBuilder ConfigureEntityIds(
        this ModelConfigurationBuilder builder,
        params Assembly[] assemblies)
        => builder.ConfigureEntityIds(collation: null, assemblies);

    /// <summary>
    /// Maps every registered entity identifier to a fixed-width column with an explicit collation.
    /// </summary>
    /// <param name="builder">Model configuration builder, from <c>DbContext.ConfigureConventions</c>.</param>
    /// <param name="collation">
    /// Collation for the identifier columns, or <see langword="null"/> to leave the database default in place.
    /// <see cref="IdCollations"/> names the binary one per provider.
    /// </param>
    /// <param name="assemblies">Assemblies declaring the identifiers.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <example>
    /// <code>
    /// protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    ///     => builder.ConfigureEntityIds(IdCollations.PostgreSql, typeof(AccountId).Assembly);
    /// </code>
    /// </example>
    public static ModelConfigurationBuilder ConfigureEntityIds(
        this ModelConfigurationBuilder builder,
        string? collation,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
#pragma warning disable IL2026 // Model building is reflection-based already; EF Core is not trim compatible.
            ValueObjectRegistry.EnsureAssemblyRegistered(assembly);
#pragma warning restore IL2026
        }

        foreach (var descriptor in EntityIdRegistry.GetRegistered())
        {
            var properties = builder.Properties(descriptor.ValueObjectType);

            properties.HaveConversion(
                typeof(ValueObjectConverter<,>).MakeGenericType(descriptor.ValueObjectType, typeof(string)),
                typeof(ValueObjectComparer<>).MakeGenericType(descriptor.ValueObjectType));

            Size(properties, descriptor.Length, collation);
        }

        return builder;
    }

    /// <summary>
    /// Maps a single property holding an entity identifier.
    /// </summary>
    /// <typeparam name="TId">Identifier type.</typeparam>
    /// <param name="builder">Property builder.</param>
    /// <param name="collation">Collation for the column, or <see langword="null"/> for the database default.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>Use this for a property that needs to depart from the convention; otherwise prefer the convention.</remarks>
    public static PropertyBuilder<TId> HasEntityIdConversion<TId>(
        this PropertyBuilder<TId> builder,
        string? collation = null)
        where TId : struct, IEntityId<TId>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasConversion(new ValueObjectConverter<TId, string>(), new ValueObjectComparer<TId>());
        builder.HasMaxLength(TId.Length);
        builder.IsFixedLength();
        builder.IsUnicode(false);

        if (collation is not null)
        {
            builder.UseCollation(collation);
        }

        return builder;
    }

    /// <summary>
    /// Applies everything that follows from an identifier being fixed-width and ASCII.
    /// </summary>
    /// <param name="properties">Properties of one identifier type.</param>
    /// <param name="length">Exact length of the identifier.</param>
    /// <param name="collation">Collation, or <see langword="null"/> for the database default.</param>
    private static void Size(PropertiesConfigurationBuilder properties, int length, string? collation)
    {
        properties.HaveMaxLength(length);

        // Both bounds are the same, so the column is CHAR rather than VARCHAR: no length prefix per row, and
        // nothing to fingerprint from a column whose width never varies.
        properties.AreFixedLength();

        // Crockford Base32 and a lowercase prefix are ASCII throughout. Left alone, SQL Server would map this
        // to NCHAR and pay two bytes a character for an alphabet of 32 symbols.
        properties.AreUnicode(false);

        if (collation is not null)
        {
            properties.UseCollation(collation);
        }
    }
}
