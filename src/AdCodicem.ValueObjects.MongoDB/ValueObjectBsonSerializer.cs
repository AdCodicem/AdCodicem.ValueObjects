using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace AdCodicem.ValueObjects.MongoDB;

/// <summary>
/// Stores a value object as the bare value the serializer of its underlying type writes, and reads it back through the
/// value object's rules.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// The value goes through the serializer of <typeparamref name="TValue"/> the serializer is built over, which
/// <see cref="ValueObjectBson"/> takes from the registry, so a value object writes exactly what the primitive it replaces
/// writes, under the application's conventions for <see cref="Guid"/>, <see cref="DateTime"/> or <see cref="decimal"/>:
/// documents and indexes written before the type replaced the primitive stay readable. A serialization option on a
/// member, <c>[BsonRepresentation(BsonType.String)]</c>, <c>[BsonGuidRepresentation]</c> or <c>[BsonDateTimeOptions]</c>
/// among them, reaches that serializer as it would on the primitive, through <see cref="IChildSerializerConfigurable"/>,
/// on a <c>TSelf?</c> member and on the elements of a collection too.
/// </para>
/// <para>
/// A read is strict unless the serializer is trusted: the value read goes through <c>TryCreate</c>, which normalizes it,
/// and one the value object refuses throws a <see cref="FormatException"/> naming the type and the rule, never the value,
/// with the rule's code in <see cref="Exception.Data"/> under <see cref="ValueObjectErrors.ErrorCodeKey"/>, where
/// <see cref="ValueObjectErrors.TryGetCode"/> finds it through the exception the driver wraps it in. A trusted serializer
/// reads through <c>CreateUnchecked</c>, for a collection the application alone writes. Either way, a BSON
/// <c>null</c> is refused with <see cref="ValueObjectErrorCodes.Required"/>, since a value object cannot hold one: a
/// member that can be missing is a <c>TSelf?</c>, which the driver reads as <see langword="null"/> without asking this
/// serializer, and a value the serializer of <typeparamref name="TValue"/> cannot read, a BSON value of another type,
/// one out of its range, text it does not parse, with <see cref="ValueObjectErrorCodes.NotParsable"/>.
/// </para>
/// <para>
/// A write refuses, with a <see cref="BsonSerializationException"/> carrying the rule's code the same way, an
/// uninitialized instance, equal to the default, whose value the value object rejects: validation tells a valid zero
/// from a refused one. Any other instance went through <c>Create</c>, or was read by a trusted serializer, and is
/// written as it is, without being validated again. The driver serializes a query constant, in LINQ or
/// <c>Builders</c>, and the value of an update through the same serializer, so a default instance the type rejects is
/// refused there too. An update through <c>Value</c>, <c>Set(x =&gt; x.Page.Value, 0)</c>, hands its value to the
/// serializer of <typeparamref name="TValue"/> alone, as a filter through <c>Value</c> does, and writes it unchecked.
/// </para>
/// <para>
/// LINQ translates <c>x.Iban.Value</c> as it translates <c>x.Iban</c>: <see cref="TryGetMemberSerializationInfo"/>
/// answers <c>Value</c> with the field itself and the serializer of <typeparamref name="TValue"/>, so that a method of the
/// underlying type, <c>StartsWith</c>, <c>Length</c>, <c>ToLower</c>, an arithmetic operator, translates as it would on
/// the primitive.
/// </para>
/// </remarks>
public sealed class ValueObjectBsonSerializer<TSelf, TValue> :
    SerializerBase<TSelf>,
    IBsonDocumentSerializer,
    IChildSerializerConfigurable,
    IHasRepresentationSerializer
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Whether the serializer of the underlying value is the driver's <see cref="GuidSerializer"/> with no
    /// representation, which throws on the first <see cref="Guid"/> it writes.
    /// </summary>
    private readonly bool _guidRepresentationMissing;

    /// <summary>
    /// Initializes the serializer over the serializer of the underlying value.
    /// </summary>
    /// <param name="valueSerializer">Serializer of <typeparamref name="TValue"/>, which writes and reads the value.</param>
    /// <param name="trusted">
    /// Whether a read skips validation, for a collection the application alone writes; <see langword="false"/>, the
    /// default, reads through the value object's rules.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="valueSerializer"/> is <see langword="null"/>.</exception>
    public ValueObjectBsonSerializer(IBsonSerializer<TValue> valueSerializer, bool trusted = false)
    {
        ArgumentNullException.ThrowIfNull(valueSerializer);

        ValueSerializer = valueSerializer;
        Trusted = trusted;
        _guidRepresentationMissing = ValueObjectBson.LacksGuidRepresentation(valueSerializer);
    }

    /// <summary>
    /// Gets the BSON type the serializer of the underlying value writes, or <see cref="BsonType.Undefined"/> when that
    /// serializer does not say.
    /// </summary>
    /// <remarks>
    /// LINQ reads it to translate a dictionary keyed by the value object, <c>x.Totals[currency]</c> and
    /// <c>ContainsKey</c>, which it accepts only for a key written as text.
    /// </remarks>
    public BsonType Representation => ValueSerializer is IHasRepresentationSerializer represented
        ? represented.Representation
        : BsonType.Undefined;

    /// <summary>
    /// Gets the serializer of the underlying value.
    /// </summary>
    internal IBsonSerializer<TValue> ValueSerializer { get; }

    /// <summary>
    /// Gets whether a read skips validation.
    /// </summary>
    internal bool Trusted { get; }

    /// <inheritdoc />
    IBsonSerializer IChildSerializerConfigurable.ChildSerializer => ValueSerializer;

    /// <inheritdoc />
    /// <exception cref="BsonSerializationException">
    /// <paramref name="value"/> equals <c>default(TSelf)</c> and holds a value the value object rejects, or the value
    /// object is over <see cref="Guid"/> and the serializer of <see cref="Guid"/> has no representation.
    /// </exception>
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TSelf value)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (value.IsDefault)
        {
            var current = value.Value;
            var validation = TSelf.Validate(in current);
            if (!validation.IsValid)
            {
                throw Refusal(
                    new BsonSerializationException(
                        $"The value to write is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}"),
                    validation.ErrorCode);
            }
        }

        if (_guidRepresentationMissing)
        {
            throw GuidRepresentationMissing();
        }

        ValueSerializer.Serialize(context, value.Value);
    }

    /// <inheritdoc />
    /// <exception cref="FormatException">
    /// The value read is a BSON <c>null</c>, which a value object cannot hold, a value the serializer of the underlying
    /// value cannot read, or, unless the serializer is trusted, a value the value object refuses.
    /// </exception>
    /// <exception cref="BsonSerializationException">
    /// The value object is over <see cref="Guid"/>, the value read is binary, and the serializer of <see cref="Guid"/>
    /// has no representation to read it with.
    /// </exception>
    public override TSelf Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        ArgumentNullException.ThrowIfNull(context);

        var read = context.Reader.GetCurrentBsonType();
        if (read == BsonType.Null)
        {
            context.Reader.ReadNull();
            throw Refusal(
                new FormatException(
                    $"A BSON null cannot be read as {typeof(TSelf).Name}; declare the member as a nullable "
                    + $"{typeof(TSelf).Name}? instead."),
                ValueObjectErrorCodes.Required);
        }

        if (_guidRepresentationMissing && read == BsonType.Binary)
        {
            throw GuidRepresentationMissing();
        }

        TValue value;
        try
        {
            value = ValueSerializer.Deserialize(context);
        }
        catch (Exception exception) when (exception
            is FormatException
            or OverflowException
            or TruncationException
            or ArgumentOutOfRangeException)
        {
            // Not kept as the inner exception: the driver's own message may quote the value, "The input string 'abc'
            // was not in a correct format.", or "The value 8640000000000000 for the BsonDateTime
            // MillisecondsSinceEpoch is outside the range...", and no exception carries a value it refused. The
            // serializers of the dates and times throw ArgumentOutOfRangeException for a value beyond the .NET type's
            // range, and a misconfigured serializer an ArgumentException or a BsonSerializationException, which propagate.
            throw Refusal(
                new FormatException(
                    $"The BSON {read} read cannot be read as {typeof(TSelf).Name}, a value object over "
                    + $"{typeof(TValue).Name}."),
                ValueObjectErrorCodes.NotParsable);
        }

        if (Trusted)
        {
            return TSelf.CreateUnchecked(value);
        }

        return TSelf.TryCreate(value, out var result, out var validation)
            ? result
            : throw Refusal(
                new FormatException($"The value read is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}"),
                validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// Answers <c>Value</c> with the field itself, so that LINQ translates <c>x.Iban.Value</c> as it does <c>x.Iban</c>.
    /// </summary>
    /// <param name="memberName">The member a query names.</param>
    /// <param name="serializationInfo">
    /// For <c>Value</c>, an empty element path, which leaves the query on the value object's own field, with the
    /// serializer of the underlying value; <see langword="null"/> otherwise.
    /// </param>
    /// <returns><see langword="true"/> for <c>Value</c>, the one member stored.</returns>
    public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
    {
        if (memberName == nameof(IValueObject<TValue>.Value))
        {
            serializationInfo = BsonSerializationInfo.CreateWithPath([], ValueSerializer, typeof(TValue));
            return true;
        }

        serializationInfo = null!;
        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Equal for the same trust over an equal serializer of the underlying value, which is how
    /// <see cref="BsonSerializer.TryRegisterSerializer(Type, IBsonSerializer)"/> tells a second registration of the same
    /// serializer, which it ignores, from a different one, which it refuses.
    /// </remarks>
    public override bool Equals(object? obj)
        => obj is ValueObjectBsonSerializer<TSelf, TValue> other
           && Trusted == other.Trusted
           && ValueSerializer.Equals(other.ValueSerializer);

    /// <inheritdoc />
    /// <remarks>
    /// Read off the trust alone: the driver's serializers, the serializer of the underlying value among them, all hash
    /// to the same value.
    /// </remarks>
    public override int GetHashCode() => Trusted.GetHashCode();

    /// <inheritdoc />
    /// <exception cref="InvalidCastException">
    /// <paramref name="childSerializer"/> does not serialize <typeparamref name="TValue"/>.
    /// </exception>
    IBsonSerializer IChildSerializerConfigurable.WithChildSerializer(IBsonSerializer childSerializer)
        => new ValueObjectBsonSerializer<TSelf, TValue>((IBsonSerializer<TValue>)childSerializer, Trusted);

    /// <summary>
    /// Reports a value object over <see cref="Guid"/> whose <see cref="Guid"/> serializer has no representation.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private static BsonSerializationException GuidRepresentationMissing()
        => new($"{typeof(TSelf).Name} is a value object over Guid, and the serializer of Guid it goes through has no "
               + "representation (GuidRepresentation.Unspecified). Call BsonSerializer.RegisterSerializer(new "
               + "GuidSerializer(GuidRepresentation.Standard)) at start-up, before anything is serialized, or give the "
               + "member a representation with [BsonGuidRepresentation].");

    /// <summary>
    /// Stores the code of the rule in an exception, where <see cref="ValueObjectErrors.TryGetCode"/> reads it.
    /// </summary>
    /// <typeparam name="TException">The exception type.</typeparam>
    /// <param name="exception">The exception, whose message names the value object and the rule, never the value.</param>
    /// <param name="code">The code of the rule.</param>
    /// <returns>The exception to throw.</returns>
    private static TException Refusal<TException>(TException exception, string code)
        where TException : Exception
    {
        exception.Data[ValueObjectErrors.ErrorCodeKey] = code;
        return exception;
    }
}
