using System.Reflection;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Maps value objects onto their underlying database types.
/// </summary>
public static class ValueObjectConventionExtensions
{
    /// <summary>
    /// Maps every value object declared in the given assemblies to its underlying column type.
    /// </summary>
    /// <param name="builder">Model configuration builder, from <c>DbContext.ConfigureConventions</c>.</param>
    /// <param name="assemblies">
    /// Assemblies declaring the value objects. When none is given, everything already registered is mapped,
    /// which is enough as soon as the entity assembly has been loaded.
    /// </param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// A value object declaring a maximum length also sizes its column, so an IBAN lands in <c>varchar(34)</c>
    /// rather than an unbounded column — the rule is stated once, on the type, and the schema follows.
    /// </para>
    /// <para>
    /// A value object over <see cref="Int128"/> or <see cref="UInt128"/> is left alone: Entity Framework Core maps
    /// neither type, on any provider, so the column type, and the conversion to it, are the application's to choose
    /// with a converter of its own.
    /// </para>
    /// <para>
    /// A generic value object, <c>Code&lt;T&gt;</c> or <c>Outer&lt;T&gt;.Code</c>, is mapped too, whatever its
    /// constructions: each property of one gets the converter, the comparer and the length closed over its own.
    /// </para>
    /// <para>
    /// Writing refuses a value the value object rejects, which only an instance equal to <c>default(TSelf)</c> can hold,
    /// rather than store it for a read to trust: a property of the value object's type throws a
    /// <see cref="ValueObjectException"/>, and a property of its nullable type, <c>TSelf?</c>, stores a <c>NULL</c>.
    /// </para>
    /// <para>
    /// A compiled model, which <c>dotnet ef dbcontext optimize</c> generates, holds the same mapping. A property of
    /// <c>TSelf?</c> is compared by <see cref="NullableValueObjectComparer{TSelf}"/>, which such a model can write.
    /// </para>
    /// <para>
    /// This runs once, while the model is built. Nothing here happens per query or per row.
    /// </para>
    /// </remarks>
    public static ModelConfigurationBuilder ConfigureValueObjects(
        this ModelConfigurationBuilder builder,
        params Assembly[] assemblies)
        => builder.ConfigureValueObjects(strict: false, assemblies);

    /// <summary>
    /// Maps every value object declared in the given assemblies, optionally re-validating on read.
    /// </summary>
    /// <param name="builder">Model configuration builder, from <c>DbContext.ConfigureConventions</c>.</param>
    /// <param name="strict">
    /// When <see langword="true"/>, values read from the database are normalized and validated again. Use it for
    /// tables another system also writes to; it costs a validation per materialized value.
    /// </param>
    /// <param name="assemblies">Assemblies declaring the value objects.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static ModelConfigurationBuilder ConfigureValueObjects(
        this ModelConfigurationBuilder builder,
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

        foreach (var descriptor in ValueObjectRegistry.GetRegistered())
        {
            if (Is128Bit(descriptor.ValueType))
            {
                continue;
            }

            Apply(builder, descriptor, strict);
        }

        // A generic value object is configured by its definition, which makes each construction a scalar property, and
        // the convention closes the converter over the construction of each property it meets.
        var definitions = ValueObjectRegistry.GetRegisteredGenericDefinitions()
            .Where(static definition => !Is128Bit(ValueTypeOf(definition)))
            .ToHashSet();
        if (definitions.Count > 0)
        {
            foreach (var definition in definitions)
            {
                builder.Properties(definition);
            }

            builder.Conventions.Add(_ => new GenericValueObjectConvention(definitions, strict));
        }

        return builder;
    }

    /// <summary>
    /// Maps a single property to the underlying value of its value object.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="builder">Property builder.</param>
    /// <param name="strict">When <see langword="true"/>, values read from the database are validated again.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Use this for a property that needs to depart from the convention; otherwise prefer the convention. It cannot
    /// map a value object over <see cref="Int128"/> or <see cref="UInt128"/>, which Entity Framework Core maps to no
    /// column: such a property needs a converter of the application's own. Writing refuses a value the value object
    /// rejects with a <see cref="ValueObjectException"/>, as the convention does.
    /// </remarks>
    public static PropertyBuilder<TSelf> HasValueObjectConversion<TSelf, TValue>(
        this PropertyBuilder<TSelf> builder,
        bool strict = false)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasConversion(
            strict
                ? new StrictValueObjectConverter<TSelf, TValue>()
                : new ValueObjectConverter<TSelf, TValue>(),
            new ValueObjectComparer<TSelf>());

        // A construction of a generic value object is never registered as such: the registry describes it the first
        // time it is asked, which happens here, so that the column does not depend on what else asked first.
#pragma warning disable IL2026, IL3050 // Model building is reflection-based already; EF Core is not trim compatible.
        var described = typeof(TSelf).IsConstructedGenericType
            ? ValueObjectRegistry.TryResolve(typeof(TSelf), out var descriptor)
            : ValueObjectRegistry.TryGet(typeof(TSelf), out descriptor);
#pragma warning restore IL2026, IL3050
        if (described && descriptor!.Schema.MaxLength is { } maxLength)
        {
            builder.HasMaxLength(maxLength);
        }

        return builder;
    }

    private static bool Is128Bit(Type? valueType) => valueType == typeof(Int128) || valueType == typeof(UInt128);

    /// <summary>
    /// Reads the underlying type of a generic value object off its definition, which the registry describes no
    /// construction of.
    /// </summary>
    private static Type? ValueTypeOf(Type definition)
        => definition.GetInterfaces()
            .FirstOrDefault(static candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IValueObject<,>))
            ?.GetGenericArguments()[1];

    private static void Apply(ModelConfigurationBuilder builder, ValueObjectDescriptor descriptor, bool strict)
    {
        var converterType = ConverterTypes.Required(descriptor.ValueObjectType, descriptor.ValueType, strict);

        var comparerType = typeof(ValueObjectComparer<>).MakeGenericType(descriptor.ValueObjectType);

        var properties = builder.Properties(descriptor.ValueObjectType);
        properties.HaveConversion(converterType, comparerType);

        if (descriptor.Schema.MaxLength is { } maxLength)
        {
            properties.HaveMaxLength(maxLength);
        }

        // A TSelf? property takes the configuration of TSelf, the length included, then its own: a converter storing a
        // value the value object rejects as NULL, where the column of a TSelf throws (a value object written by hand
        // over a reference type other than string keeps its own), and a comparer of the nullable type, which a compiled
        // model can write, where it cannot write the wrapping EF Core would otherwise give ValueObjectComparer.
        builder.Properties(typeof(Nullable<>).MakeGenericType(descriptor.ValueObjectType))
            .HaveConversion(
                ConverterTypes.Optional(descriptor.ValueObjectType, descriptor.ValueType, strict) ?? converterType,
                typeof(NullableValueObjectComparer<>).MakeGenericType(descriptor.ValueObjectType));
    }
}
