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
/// Calling both is fine, given the same strictness: whichever call runs last sets the converter of the identifiers,
/// so a context reading through <c>ConfigureValueObjects(strict: true)</c> passes <c>strict: true</c> here too, or its
/// identifiers are read without validation. Either converter refuses to write an identifier that never went through
/// <c>New</c> or <c>Create</c>: a required one throws a <see cref="ValueObjectException"/>, and an optional one,
/// <c>TId?</c>, is stored as <c>NULL</c>.
/// </para>
/// <para>
/// Each converter and comparer is closed over its identifier through the type arguments the identifier's
/// <see cref="ValueObjectDescriptor"/> hands back to a visitor
/// (<see cref="ValueObjectDescriptor.Accept{TResult}(IValueObjectVisitor{TResult})"/>), never with
/// <see cref="Type.MakeGenericType(Type[])"/>. An identifier registered by hand with <see cref="EntityIdRegistry"/>
/// alone is described by reflection the first time the convention meets it, as
/// <see cref="ValueObjectRegistry.TryResolve"/> describes a value object nothing registered, and stays registered with
/// <see cref="ValueObjectRegistry"/> from then on, as a generated identifier always is: <c>ConfigureValueObjects</c> maps
/// it in a model built afterwards. Entity Framework Core builds no model under native AOT, where it reads a compiled
/// model instead, so none of this runs there.
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
        => builder.ConfigureEntityIds(collation: null, strict: false, assemblies);

    /// <summary>
    /// Maps every registered entity identifier to a fixed-width column, optionally validating what is read.
    /// </summary>
    /// <param name="builder">Model configuration builder, from <c>DbContext.ConfigureConventions</c>.</param>
    /// <param name="strict">
    /// When <see langword="true"/>, identifiers read from the database are normalized and validated again, as
    /// <c>ConfigureValueObjects(strict: true)</c> does for the other value objects.
    /// </param>
    /// <param name="assemblies">Assemblies declaring the identifiers.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static ModelConfigurationBuilder ConfigureEntityIds(
        this ModelConfigurationBuilder builder,
        bool strict,
        params Assembly[] assemblies)
        => builder.ConfigureEntityIds(collation: null, strict, assemblies);

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
        => builder.ConfigureEntityIds(collation, strict: false, assemblies);

    /// <summary>
    /// Maps every registered entity identifier to a fixed-width column with an explicit collation, optionally
    /// validating what is read.
    /// </summary>
    /// <param name="builder">Model configuration builder, from <c>DbContext.ConfigureConventions</c>.</param>
    /// <param name="collation">
    /// Collation for the identifier columns, or <see langword="null"/> to leave the database default in place.
    /// <see cref="IdCollations"/> names the binary one per provider.
    /// </param>
    /// <param name="strict">
    /// When <see langword="true"/>, identifiers read from the database are normalized and validated again, as
    /// <c>ConfigureValueObjects(strict: true)</c> does for the other value objects. A value the identifier refuses
    /// fails the query with a <see cref="ValueObjectException"/>.
    /// </param>
    /// <param name="assemblies">Assemblies declaring the identifiers.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static ModelConfigurationBuilder ConfigureEntityIds(
        this ModelConfigurationBuilder builder,
        string? collation,
        bool strict,
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
            // The value object registry describes an identifier, and its descriptor hands the identifier's type back to
            // a visitor that names each converter and comparer. A generated identifier is registered there as well as
            // here; one registered here alone, by hand, is described by reflection, which only the JIT runs, as only
            // the JIT builds a model. Either way the identifier is a value object, so the lookup succeeds.
            _ = ValueObjectRegistry.TryResolve(descriptor.ValueObjectType, out var valueObject);
            Size(valueObject!.Accept(new IdentifierConversion(builder, strict)), descriptor.Length, collation);
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
    public static PropertyBuilder<TId> HasEntityIdConversion<TId>(this PropertyBuilder<TId> builder, string? collation = null)
        where TId : struct, IEntityId<TId>
        => HasEntityIdConversion(builder, collation, strict: false);

    /// <summary>
    /// Maps a single property holding an entity identifier, validating what is read from the database or not.
    /// </summary>
    /// <typeparam name="TId">Identifier type.</typeparam>
    /// <param name="builder">Property builder.</param>
    /// <param name="strict">When <see langword="true"/>, identifiers read from the database are validated again.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>Use this for a property that needs to depart from the convention; otherwise prefer the convention.</remarks>
    public static PropertyBuilder<TId> HasEntityIdConversion<TId>(this PropertyBuilder<TId> builder, bool strict)
        where TId : struct, IEntityId<TId>
        => HasEntityIdConversion(builder, collation: null, strict);

    /// <summary>
    /// Maps a single property holding an entity identifier, with a collation, validating what is read from the
    /// database or not.
    /// </summary>
    /// <typeparam name="TId">Identifier type.</typeparam>
    /// <param name="builder">Property builder.</param>
    /// <param name="collation">Collation for the column, or <see langword="null"/> for the database default.</param>
    /// <param name="strict">When <see langword="true"/>, identifiers read from the database are validated again.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Use this for a property that needs to depart from the convention; otherwise prefer the convention. Pass the same
    /// <paramref name="strict"/> as to the convention, which a property configured here otherwise departs from.
    /// </remarks>
    public static PropertyBuilder<TId> HasEntityIdConversion<TId>(this PropertyBuilder<TId> builder, string? collation, bool strict)
        where TId : struct, IEntityId<TId>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasConversion(
            strict ? new StrictValueObjectConverter<TId, string>() : new ValueObjectConverter<TId, string>(),
            new ValueObjectComparer<TId>());
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

    /// <summary>
    /// Maps a value object over <see cref="string"/>, receiving it under that constraint.
    /// </summary>
    /// <typeparam name="TValue">Underlying value type, <see cref="string"/> for an identifier.</typeparam>
    /// <remarks>
    /// A descriptor hands the underlying type back as a type parameter, which C# cannot prove to be
    /// <see cref="string"/>, and the converter of an optional identifier stores text, which C# names only for a value
    /// object over <see cref="string"/>. A class implementing <c>IMapping&lt;string&gt;</c> receives the value object
    /// with that constraint.
    /// </remarks>
    private interface IMapping<TValue>
    {
        /// <summary>
        /// Maps the value object.
        /// </summary>
        /// <typeparam name="TSelf">Value object type.</typeparam>
        /// <returns>The configuration of its properties.</returns>
        PropertiesConfigurationBuilder Map<TSelf>()
            where TSelf : struct, IValueObject<TSelf, TValue>;
    }

    /// <summary>
    /// Sets the converter and the comparer of an identifier, and of its nullable type, closed over the type arguments
    /// its descriptor hands back.
    /// </summary>
    /// <param name="builder">Model configuration builder.</param>
    /// <param name="strict">Whether identifiers read from the database are validated again.</param>
    private sealed class IdentifierConversion(ModelConfigurationBuilder builder, bool strict)
        : IValueObjectVisitor<PropertiesConfigurationBuilder>, IMapping<string>
    {
        /// <summary>
        /// Hands the identifier on to <see cref="Map{TSelf}"/>, under the constraint its converters take.
        /// </summary>
        /// <typeparam name="TSelf">Identifier type.</typeparam>
        /// <typeparam name="TValue">Underlying value type, <see cref="string"/> for an identifier.</typeparam>
        /// <returns>The configuration of the identifier's properties, which the caller sizes.</returns>
        /// <remarks>
        /// An identifier is a value object over <see cref="string"/>, as <see cref="IEntityId{TSelf}"/> declares, so
        /// the cast to the mapping this class implements over <see cref="string"/> always succeeds.
        /// </remarks>
        public PropertiesConfigurationBuilder Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => ((IMapping<TValue>)(object)this).Map<TSelf>();

        /// <summary>
        /// Sets the converter and the comparer of the identifier, and of its nullable type.
        /// </summary>
        /// <typeparam name="TSelf">Identifier type.</typeparam>
        /// <returns>The configuration of the identifier's properties, which the caller sizes.</returns>
        public PropertiesConfigurationBuilder Map<TSelf>()
            where TSelf : struct, IValueObject<TSelf, string>
        {
            var properties = builder.Properties<TSelf>();
            properties.HaveConversion(
                strict
                    ? typeof(StrictValueObjectConverter<TSelf, string>)
                    : typeof(ValueObjectConverter<TSelf, string>),
                typeof(ValueObjectComparer<TSelf>));

            // An optional identifier stores one that never went through New or Create as NULL, where a required one
            // throws; it takes the column of the identifier from the configuration the caller completes, and a
            // comparer of the nullable type, which a compiled model can write.
            builder.Properties<TSelf?>().HaveConversion(
                strict
                    ? typeof(StrictNullableValueObjectConverter<TSelf>)
                    : typeof(NullableValueObjectConverter<TSelf>),
                typeof(NullableValueObjectComparer<TSelf>));

            return properties;
        }
    }
}
