using ProtoBuf;
using ProtoBuf.Meta;
using ProtoBuf.Serializers;

namespace AdCodicem.ValueObjects.ProtobufNet;

/// <summary>
/// Writes a value object as protobuf-net writes its underlying type at <see cref="CompatibilityLevel.Level200"/>, and
/// reads it back through its rules.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// The strict profile at protobuf-net's default level, <see cref="ValueObjectProtoProfiles.StrictLevel200"/>: the same as
/// <see cref="ValueObjectProtoSerializer{TSelf, TValue, TProfile}"/> closed over it, under a shorter name for
/// <c>[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer&lt;Sku, string&gt;))]</c>.
/// </remarks>
public sealed class ValueObjectProtoSerializer<TSelf, TValue> : ISerializer<TSelf>, ISerializer<TSelf?>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    private static readonly ISerializer<TValue>? Inbuilt = ValueObjectProtoWire<TSelf, TValue>.Inbuilt(CompatibilityLevel.Level200);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">protobuf-net has no inbuilt serializer for <typeparamref name="TValue"/>.</exception>
    public SerializerFeatures Features => ValueObjectProtoWire<TSelf, TValue>.Require(Inbuilt, CompatibilityLevel.Level200).Features;

    /// <inheritdoc />
    SerializerFeatures ISerializer<TSelf?>.Features => Features;

    /// <inheritdoc />
    /// <exception cref="ProtoException">
    /// The value read is one the inbuilt serializer of the underlying type cannot read, or one the value object refuses.
    /// </exception>
    public TSelf Read(ref ProtoReader.State state, TSelf value)
        => ValueObjectProtoWire<TSelf, TValue>.Read(
            ValueObjectProtoWire<TSelf, TValue>.Require(Inbuilt, CompatibilityLevel.Level200), trusted: false, ref state);

    /// <inheritdoc />
    TSelf? ISerializer<TSelf?>.Read(ref ProtoReader.State state, TSelf? value) => Read(ref state, default);

    /// <inheritdoc />
    /// <exception cref="ProtoException"><paramref name="value"/> equals <c>default(TSelf)</c> and holds a value the value object rejects.</exception>
    public void Write(ref ProtoWriter.State state, TSelf value)
        => ValueObjectProtoWire<TSelf, TValue>.Write(
            ValueObjectProtoWire<TSelf, TValue>.Require(Inbuilt, CompatibilityLevel.Level200), ref state, value);

    /// <inheritdoc />
    void ISerializer<TSelf?>.Write(ref ProtoWriter.State state, TSelf? value)
        => Write(ref state, value ?? throw new ArgumentNullException(nameof(value)));
}

/// <summary>
/// Writes a value object as protobuf-net writes its underlying type at the level <typeparamref name="TProfile"/> names,
/// and reads it back through its rules unless the profile trusts the bytes.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <typeparam name="TProfile">The trust and the compatibility level, one of <see cref="ValueObjectProtoProfiles"/>.</typeparam>
/// <remarks>
/// <para>
/// The value goes through <c>TypeModel.GetInbuiltSerializer&lt;TValue&gt;</c> at the profile's level, so a value object
/// writes what protobuf-net writes for the primitive it replaces at that level, but a valid zero, which it writes where
/// protobuf-net leaves out the primitive's, and a formatting hook of the value object never reaches the wire. protobuf-net calls a custom serializer whatever <c>[CompatibilityLevel]</c> a member, a
/// type or an assembly declares, and whatever <c>DataFormat</c> a member declares: the profile alone decides, and a
/// <c>ZigZag</c>, <c>FixedSize</c> or <c>WellKnown</c> member of a value object is written and read as
/// <c>DataFormat.Default</c> writes the underlying type, so such a member keeps its primitive type or its <c>.proto</c>
/// changes. A root value object, the message itself, is written at the profile's level too, where protobuf-net writes a
/// root primitive at <see cref="CompatibilityLevel.Level200"/> whatever the model's level.
/// </para>
/// <para>
/// A strict read goes through <c>TryCreate</c>, which normalizes the value, and refuses one the value object rejects with
/// a <see cref="ProtoException"/> naming the type and the rule, never the value, with the rule's code in
/// <see cref="Exception.Data"/> under <see cref="ValueObjectErrors.ErrorCodeKey"/>. A trusted read goes through
/// <c>CreateUnchecked</c>. Either way, a value the inbuilt serializer cannot read, of another wire type or beyond the range
/// of <typeparamref name="TValue"/>, is refused with <see cref="ValueObjectErrorCodes.NotParsable"/>. A field missing from
/// the message is never read: the member keeps the default instance, which no rule checks.
/// </para>
/// <para>
/// A write refuses, with a <see cref="ProtoException"/> carrying the code the same way, an uninitialized instance whose
/// value the value object rejects. Any other instance went through <c>Create</c>, or was read by a trusted serializer.
/// </para>
/// </remarks>
public sealed class ValueObjectProtoSerializer<TSelf, TValue, TProfile> : ISerializer<TSelf>, ISerializer<TSelf?>
    where TSelf : struct, IValueObject<TSelf, TValue>
    where TProfile : IValueObjectProtoProfile
{
    private static readonly ISerializer<TValue>? Inbuilt = ValueObjectProtoWire<TSelf, TValue>.Inbuilt(TProfile.CompatibilityLevel);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">protobuf-net has no inbuilt serializer for <typeparamref name="TValue"/>.</exception>
    public SerializerFeatures Features => ValueObjectProtoWire<TSelf, TValue>.Require(Inbuilt, TProfile.CompatibilityLevel).Features;

    /// <inheritdoc />
    SerializerFeatures ISerializer<TSelf?>.Features => Features;

    /// <inheritdoc />
    /// <exception cref="ProtoException">
    /// The value read is one the inbuilt serializer of the underlying type cannot read, or, unless the profile is trusted,
    /// one the value object refuses.
    /// </exception>
    public TSelf Read(ref ProtoReader.State state, TSelf value)
        => ValueObjectProtoWire<TSelf, TValue>.Read(
            ValueObjectProtoWire<TSelf, TValue>.Require(Inbuilt, TProfile.CompatibilityLevel), TProfile.Trusted, ref state);

    /// <inheritdoc />
    TSelf? ISerializer<TSelf?>.Read(ref ProtoReader.State state, TSelf? value) => Read(ref state, default);

    /// <inheritdoc />
    /// <exception cref="ProtoException"><paramref name="value"/> equals <c>default(TSelf)</c> and holds a value the value object rejects.</exception>
    public void Write(ref ProtoWriter.State state, TSelf value)
        => ValueObjectProtoWire<TSelf, TValue>.Write(
            ValueObjectProtoWire<TSelf, TValue>.Require(Inbuilt, TProfile.CompatibilityLevel), ref state, value);

    /// <inheritdoc />
    void ISerializer<TSelf?>.Write(ref ProtoWriter.State state, TSelf? value)
        => Write(ref state, value ?? throw new ArgumentNullException(nameof(value)));
}
