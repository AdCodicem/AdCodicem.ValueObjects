using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orleans.Serialization;
using Orleans.Serialization.Serializers;

namespace AdCodicem.ValueObjects.Orleans;

/// <summary>
/// Wires value objects into an Orleans serializer, the one every silo and every client builds.
/// </summary>
public static class ValueObjectSerializerBuilderExtensions
{
    /// <summary>
    /// Serializes every value object the registry resolves, strict unless trusted.
    /// </summary>
    /// <param name="builder">The serializer builder of a silo or a client.</param>
    /// <param name="trusted">
    /// Whether value objects are read without validation, for a cluster whose silos and clients are all the
    /// application's own; <see langword="false"/>, the default, reads them through their rules.
    /// </param>
    /// <returns><paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Orleans asks for a value object's codec the first time it meets the type, and keeps it. A value object the registry
    /// holds is closed through its descriptor's visitor; one it does not hold, a construction of a generic value object or
    /// a value object written by hand, is described by reflection where the runtime supports dynamic code, and refused
    /// with a <see cref="NotSupportedException"/> naming <see cref="AddValueObject{TSelf, TValue}"/> elsewhere.
    /// </para>
    /// <para>
    /// It also allows every value object through Orleans' type filter, since a grain method's result travels inside a
    /// response whose type names it, and carries a <see cref="ValueObjectException"/> a grain lets escape back to the
    /// caller with its code, never the value it refused. Called twice, the last call answers first: its trust applies.
    /// </para>
    /// </remarks>
    public static ISerializerBuilder AddValueObjects(this ISerializerBuilder builder, bool trusted = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // First, so that the last call answers before an earlier one, as a typed registration made after it does.
        builder.Services.Insert(0, ServiceDescriptor.Singleton<ISpecializableCodec>(_ => new ValueObjectCodecSelector(trusted)));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ITypeFilter, ValueObjectTypeFilter>());
        return AddExceptionCodec(builder);
    }

    /// <summary>
    /// Serializes one value object, a construction of a generic one or one written by hand included, without the registry.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="builder">The serializer builder of a silo or a client.</param>
    /// <param name="trusted">
    /// Whether the value object is read without validation; <see langword="false"/>, the default, reads it through its
    /// rules.
    /// </param>
    /// <returns><paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// It allows the value object through Orleans' type filter, and carries a <see cref="ValueObjectException"/> back to
    /// the caller as <see cref="AddValueObjects"/> does. Made after <see cref="AddValueObjects"/>, it answers for its type
    /// first, with its own trust.
    /// </remarks>
    public static ISerializerBuilder AddValueObject<TSelf, TValue>(this ISerializerBuilder builder, bool trusted = false)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Insert(0, ServiceDescriptor.Singleton<ISpecializableCodec>(_ => new ValueObjectCodecRegistration<TSelf, TValue>(trusted)));
        builder.Configure(static options => options.AddAllowedType(typeof(TSelf)));
        return AddExceptionCodec(builder);
    }

    /// <summary>
    /// Adds the codec carrying a <see cref="ValueObjectException"/> across a call, once whatever the calls: the type
    /// manifest keeps its field codecs in a set, and allows the type each one serves.
    /// </summary>
    /// <param name="builder">The serializer builder.</param>
    /// <returns><paramref name="builder"/>.</returns>
    private static ISerializerBuilder AddExceptionCodec(ISerializerBuilder builder)
        => builder.Configure(static options => options.AddFieldCodec(typeof(ValueObjectExceptionCodec)));
}
