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
            Apply(builder, descriptor, strict);
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
    /// <remarks>Use this for a property that needs to depart from the convention; otherwise prefer the convention.</remarks>
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

        if (ValueObjectRegistry.TryGet(typeof(TSelf), out var descriptor) && descriptor.Schema.MaxLength is { } maxLength)
        {
            builder.HasMaxLength(maxLength);
        }

        return builder;
    }

    private static void Apply(ModelConfigurationBuilder builder, ValueObjectDescriptor descriptor, bool strict)
    {
        var converterType = (strict ? typeof(StrictValueObjectConverter<,>) : typeof(ValueObjectConverter<,>))
            .MakeGenericType(descriptor.ValueObjectType, descriptor.ValueType);

        var comparerType = typeof(ValueObjectComparer<>).MakeGenericType(descriptor.ValueObjectType);

        var properties = builder.Properties(descriptor.ValueObjectType);
        properties.HaveConversion(converterType, comparerType);

        if (descriptor.Schema.MaxLength is { } maxLength)
        {
            properties.HaveMaxLength(maxLength);
        }
    }
}
