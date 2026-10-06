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
    /// <para>
    /// Each converter and comparer is closed over its value object through the type arguments the descriptor hands back
    /// to a visitor (<see cref="ValueObjectDescriptor.Accept{TResult}(IValueObjectVisitor{TResult})"/>), never with
    /// <see cref="Type.MakeGenericType(Type[])"/> over a <see cref="Type"/> read off the descriptor. The converter of a
    /// <c>TSelf?</c> property, which C# cannot name without a constraint the visitor does not carry, is closed with it
    /// over those type arguments. Entity Framework Core builds no model under native AOT, where it reads a compiled
    /// model instead, so none of this runs there.
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
    /// <remarks>
    /// <paramref name="strict"/> is read once per context type, not per instance: Entity Framework Core builds the
    /// model the first time a context type is used and caches it for every later instance. A context that chooses the
    /// value from a constructor argument gets whichever model was built first. Give strict reads a context type of
    /// their own, or an <see cref="Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory"/> that puts the
    /// choice in the cache key.
    /// </remarks>
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
            // The length is the descriptor's: a registration made by hand may give a value object another schema than
            // the one its type declares.
            descriptor.Accept(new PropertiesConfiguration(builder, strict, descriptor.Schema.MaxLength));
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

        // The length the value object declares, read off the type rather than out of the registry, which would describe
        // a construction of a generic value object by reflection.
        if (TSelf.Schema.MaxLength is { } maxLength)
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

    /// <summary>
    /// Configures the properties of the value object a descriptor stands for, and those of its nullable type, closed
    /// over the type arguments the descriptor hands back.
    /// </summary>
    /// <param name="builder">Model configuration builder.</param>
    /// <param name="strict">Whether what is read is validated again.</param>
    /// <param name="maxLength">The length the descriptor declares, which sizes the column.</param>
    private sealed class PropertiesConfiguration(ModelConfigurationBuilder builder, bool strict, int? maxLength)
        : IValueObjectVisitor<bool>
    {
        /// <summary>
        /// Configures the properties, unless the value object is over a 128-bit integer, which Entity Framework Core
        /// maps to no column.
        /// </summary>
        /// <typeparam name="TSelf">Value object type.</typeparam>
        /// <typeparam name="TValue">Underlying value type.</typeparam>
        /// <returns><see langword="true"/> when the properties were configured.</returns>
        public bool Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            if (Is128Bit(typeof(TValue)))
            {
                return false;
            }

            var converter = ConverterTypes.Required<TSelf, TValue>(strict);
            var properties = builder.Properties<TSelf>();
            properties.HaveConversion(converter, typeof(ValueObjectComparer<TSelf>));

            if (maxLength is { } length)
            {
                properties.HaveMaxLength(length);
            }

            // A TSelf? property takes the configuration of TSelf, the length included, then its own: a converter
            // storing a value the value object rejects as NULL, where the column of a TSelf throws (a value object
            // written by hand over a reference type other than string keeps its own), and a comparer of the nullable
            // type, which a compiled model can write, where it cannot write the wrapping EF Core would otherwise give
            // ValueObjectComparer.
            builder.Properties<TSelf?>().HaveConversion(
                ConverterTypes.Optional<TSelf, TValue>(strict) ?? converter,
                typeof(NullableValueObjectComparer<TSelf>));

            return true;
        }
    }
}
