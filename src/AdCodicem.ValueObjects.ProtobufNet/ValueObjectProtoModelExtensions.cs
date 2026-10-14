using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using ProtoBuf;
using ProtoBuf.Meta;

namespace AdCodicem.ValueObjects.ProtobufNet;

/// <summary>
/// Registers the serializers of value objects on a protobuf-net <see cref="RuntimeTypeModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// protobuf-net adds a member's type to a model only when the type carries a contract attribute, so a value object it was
/// not told about fails the first contract that holds one: "No serializer defined for type". These methods tell it, at
/// start-up, before the model serializes anything: <c>RuntimeTypeModel.Default.AddValueObjects(typeof(Sku).Assembly)</c>
/// serves <c>Serializer.Serialize</c> and protobuf-net.Grpc's default binder.
/// </para>
/// <para>
/// Each value object gets the serializer of <see cref="ValueObjectProtoSerializer{TSelf, TValue, TProfile}"/> at the
/// model's <see cref="RuntimeTypeModel.DefaultCompatibilityLevel"/>, which protobuf-net lets nobody change once a type is
/// added, and is named in the <c>.proto</c> export as the scalar protobuf-net names its underlying type at that level,
/// <c>string</c> or <c>int32</c>, with the <c>import</c> a <c>.bcl.Guid</c> or a <c>.google.protobuf.Timestamp</c> needs.
/// A value object the model already serializes some way keeps it: one given a serializer type on the model, or by
/// <c>[ProtoContract(Serializer = …)]</c> on the type, which is named in the export only when the serializer is one of
/// this package's over a profile it ships and the attribute gives no <c>Name</c>.
/// </para>
/// <para>
/// These methods serve protobuf-net's runtime model, which emits code at run time: under native AOT, a value object names
/// its serializer with <c>[ProtoContract(Serializer = …)]</c>, and the application serializes through protobuf-net's
/// compile-time model, a <c>[ProtoModel]</c>.
/// </para>
/// </remarks>
public static class ValueObjectProtoModelExtensions
{
    /// <summary>Why the registration needs the JIT, which native AOT reports where it is called.</summary>
    private const string RuntimeModel =
        "protobuf-net's runtime model emits code at run time; under native AOT, name the serializer on the value object with "
        + "[ProtoContract(Serializer = ...)] and serialize through a [ProtoModel].";

    /// <summary>
    /// Registers, strict, a serializer for every value object the registry holds, once the generated registration of the
    /// assemblies given has run.
    /// </summary>
    /// <param name="model">The model, <see cref="RuntimeTypeModel.Default"/> or the application's own.</param>
    /// <param name="assemblies">Assemblies declaring value objects, whose generated registration runs first.</param>
    /// <returns><paramref name="model"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="assemblies"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
    [RequiresDynamicCode(RuntimeModel)]
    public static RuntimeTypeModel AddValueObjects(this RuntimeTypeModel model, params Assembly[] assemblies)
        => model.AddValueObjects(trusted: false, assemblies);

    /// <summary>
    /// Registers, trusted or strict, a serializer for every value object the registry holds, once the generated
    /// registration of the assemblies given has run.
    /// </summary>
    /// <param name="model">The model, <see cref="RuntimeTypeModel.Default"/> or the application's own.</param>
    /// <param name="trusted">
    /// Whether reads skip validation, for bytes the application alone wrote, a cache or a store among them;
    /// <see langword="false"/> reads through the value objects' rules.
    /// </param>
    /// <param name="assemblies">Assemblies declaring value objects, whose generated registration runs first.</param>
    /// <returns><paramref name="model"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="assemblies"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A construction of a generic value object and a value object written by hand are in the registry only once something
    /// registered them: <see cref="AddValueObject{TSelf, TValue}(RuntimeTypeModel, bool)"/> adds each. A value object over
    /// <see cref="Int128"/>, <see cref="UInt128"/> or <see cref="DateTimeOffset"/>, which protobuf-net has no inbuilt
    /// serializer for, is left out, and protobuf-net's own error names it when a contract holds one.
    /// </remarks>
    [RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
    [RequiresDynamicCode(RuntimeModel)]
    public static RuntimeTypeModel AddValueObjects(this RuntimeTypeModel model, bool trusted, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            ValueObjectRegistry.EnsureAssemblyRegistered(assembly);
        }

        var registration = new Registration(model, trusted, required: false);
        foreach (var descriptor in ValueObjectRegistry.GetRegistered())
        {
            descriptor.Accept(registration);
        }

        return model;
    }

    /// <summary>
    /// Registers the serializer of one value object, a construction of a generic one or one written by hand included,
    /// without reflection.
    /// </summary>
    /// <typeparam name="TSelf">Value object type, <c>Reference&lt;PurchaseOrder&gt;</c> for instance.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="model">The model, <see cref="RuntimeTypeModel.Default"/> or the application's own.</param>
    /// <param name="trusted">
    /// Whether reads skip validation, for bytes the application alone wrote; <see langword="false"/>, the default, reads
    /// through the value object's rules.
    /// </param>
    /// <returns><paramref name="model"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">protobuf-net has no inbuilt serializer for <typeparamref name="TValue"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The model already serializes <typeparamref name="TSelf"/> through another serializer type: one of the other trust,
    /// one of the application's own, or the one <c>[ProtoContract(Serializer = …)]</c> names on the type.
    /// </exception>
    [RequiresDynamicCode(RuntimeModel)]
    public static RuntimeTypeModel AddValueObject<TSelf, TValue>(this RuntimeTypeModel model, bool trusted = false)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(model);

        new Registration(model, trusted, required: true).Visit<TSelf, TValue>();
        return model;
    }

    /// <summary>
    /// Registers a value object, closed over the type arguments the descriptor hands back.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="trusted">Whether reads skip validation.</param>
    /// <param name="required">
    /// Whether a value object that cannot be registered throws, as one asked for by name does, rather than being left out.
    /// </param>
    private sealed class Registration(RuntimeTypeModel model, bool trusted, bool required) : IValueObjectVisitor<bool>
    {
        /// <inheritdoc />
        public bool Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            var level = Level(model.DefaultCompatibilityLevel);
            if (ValueObjectProtoWire<TSelf, TValue>.Inbuilt(level) is null)
            {
                return required ? throw ProtoRefusals.Unsupported(typeof(TSelf), typeof(TValue), level) : false;
            }

            var serializerType = SerializerType<TSelf, TValue>(trusted, level);
            var type = typeof(TSelf);

            // [ProtoContract(Serializer = …)] on the type is the author's choice: protobuf-net applies it when the type is
            // added with its default behaviour, which the attribute route relies on with no registration at all.
            var metaType = Find(model, type);
            if (metaType is null && type.GetCustomAttribute<ProtoContractAttribute>()?.Serializer is not null)
            {
                metaType = model.Add(type, applyDefaultBehaviour: true);
            }

            if (metaType?.SerializerType is { } existing)
            {
                // The attribute route on a runtime model: the type keeps its serializer, and is named in the export as the
                // scalar of the level a serializer of this package fixes.
                if (string.IsNullOrEmpty(metaType.Name) && LevelOf<TSelf, TValue>(existing) is { } named)
                {
                    Name(metaType, typeof(TValue), named);
                }

                if (existing == serializerType || !required)
                {
                    return existing == serializerType;
                }

                throw new InvalidOperationException(
                    $"The model already serializes '{TypeNames.Of(type)}' through '{TypeNames.Of(existing)}', which "
                    + $"AddValueObject<{TypeNames.Of(type)}, {TypeNames.Of(typeof(TValue))}>(trusted: "
                    + $"{(trusted ? "true" : "false")}) would replace with '{TypeNames.Of(serializerType)}'.");
            }

            // A type the model built as a message, an empty one for a bare [ProtoContract] on the value object, becomes the
            // value object it is, as long as nothing has been serialized through it yet.
            metaType ??= model.Add(type, applyDefaultBehaviour: false);
            metaType.SerializerType = serializerType;
            Name(metaType, typeof(TValue), level);
            return true;
        }

        /// <summary>Names a value object in the export as the scalar its underlying type is written as, with its import.</summary>
        private static void Name(MetaType metaType, Type valueType, CompatibilityLevel level)
        {
            if (ProtoScalars.Of(valueType, level) is var (name, import))
            {
                metaType.Name = name;
                metaType.Origin = import;
            }
        }

        /// <summary>Reads the level a serializer this package ships fixes, or <see langword="null"/> for any other.</summary>
        private static CompatibilityLevel? LevelOf<TSelf, TValue>(Type serializerType)
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            if (serializerType == typeof(ValueObjectProtoSerializer<TSelf, TValue>))
            {
                return CompatibilityLevel.Level200;
            }

            foreach (var level in (ReadOnlySpan<CompatibilityLevel>)[CompatibilityLevel.Level200, CompatibilityLevel.Level240, CompatibilityLevel.Level300])
            {
                if (serializerType == SerializerType<TSelf, TValue>(trusted: false, level)
                    || serializerType == SerializerType<TSelf, TValue>(trusted: true, level))
                {
                    return level;
                }
            }

            return null;
        }

        /// <summary>Finds the type in the model without adding it, as <c>IsDefined</c> would add one carrying a contract attribute.</summary>
        private static MetaType? Find(RuntimeTypeModel model, Type type)
            => model.GetTypes().Cast<MetaType>().FirstOrDefault(metaType => metaType.Type == type);

        /// <summary>Reads a model's level as one of the three a profile names.</summary>
        private static CompatibilityLevel Level(CompatibilityLevel level) => level switch
        {
            >= CompatibilityLevel.Level300 => CompatibilityLevel.Level300,
            >= CompatibilityLevel.Level240 => CompatibilityLevel.Level240,
            _ => CompatibilityLevel.Level200,
        };

        /// <summary>Names the closed serializer of a value object: <c>typeof</c> over the visitor's own type arguments.</summary>
        private static Type SerializerType<TSelf, TValue>(bool trusted, CompatibilityLevel level)
            where TSelf : struct, IValueObject<TSelf, TValue>
            => (trusted, level) switch
            {
                (false, CompatibilityLevel.Level300) => typeof(ValueObjectProtoSerializer<TSelf, TValue, ValueObjectProtoProfiles.StrictLevel300>),
                (false, CompatibilityLevel.Level240) => typeof(ValueObjectProtoSerializer<TSelf, TValue, ValueObjectProtoProfiles.StrictLevel240>),
                (false, _) => typeof(ValueObjectProtoSerializer<TSelf, TValue, ValueObjectProtoProfiles.StrictLevel200>),
                (true, CompatibilityLevel.Level300) => typeof(ValueObjectProtoSerializer<TSelf, TValue, ValueObjectProtoProfiles.TrustedLevel300>),
                (true, CompatibilityLevel.Level240) => typeof(ValueObjectProtoSerializer<TSelf, TValue, ValueObjectProtoProfiles.TrustedLevel240>),
                (true, _) => typeof(ValueObjectProtoSerializer<TSelf, TValue, ValueObjectProtoProfiles.TrustedLevel200>),
            };
    }
}

/// <summary>
/// What protobuf-net's <c>.proto</c> export names each underlying type at each level, under <see cref="DataFormat.Default"/>,
/// and the file that defines it, which a value object's <c>MetaType</c> takes so that the export writes the scalar and its
/// <c>import</c> where it would write the primitive's.
/// </summary>
internal static class ProtoScalars
{
    private const string Bcl = "protobuf-net/bcl.proto";

    /// <summary>Names the scalar of an underlying type.</summary>
    /// <param name="valueType">The underlying type.</param>
    /// <param name="level">The level, one of the three a profile names.</param>
    /// <returns>The name and the import, or <see langword="null"/> for a type protobuf-net names otherwise.</returns>
    public static (string Name, string? Import)? Of(Type valueType, CompatibilityLevel level) => Type.GetTypeCode(valueType) switch
    {
        TypeCode.String => ("string", null),
        TypeCode.Boolean => ("bool", null),
        TypeCode.Char or TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 => ("uint32", null),
        TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 => ("int32", null),
        TypeCode.Int64 => ("int64", null),
        TypeCode.UInt64 => ("uint64", null),
        TypeCode.Single => ("float", null),
        TypeCode.Double => ("double", null),
        TypeCode.Decimal => level >= CompatibilityLevel.Level300 ? ("string", null) : (".bcl.Decimal", Bcl),
        TypeCode.DateTime => level >= CompatibilityLevel.Level240
            ? (".google.protobuf.Timestamp", "google/protobuf/timestamp.proto")
            : (".bcl.DateTime", Bcl),
        _ when valueType == typeof(Guid) => level >= CompatibilityLevel.Level300 ? ("string", null) : (".bcl.Guid", Bcl),
        _ when valueType == typeof(TimeSpan) => level >= CompatibilityLevel.Level240
            ? (".google.protobuf.Duration", "google/protobuf/duration.proto")
            : (".bcl.TimeSpan", Bcl),
        _ when valueType == typeof(DateOnly) => ("int32", null),
        _ when valueType == typeof(TimeOnly) => ("int64", null),
        _ when valueType == typeof(Uri) => ("string", null),
        _ => null,
    };
}
