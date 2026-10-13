using System.Globalization;
using System.Text.Json;
using AdCodicem.ValueObjects.Shared;
using HotChocolate;
using HotChocolate.Configuration;
using HotChocolate.Features;
using HotChocolate.Language;
using HotChocolate.Text.Json;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors.Configurations;

namespace AdCodicem.ValueObjects.HotChocolate;

/// <summary>
/// A value object as a GraphQL scalar of its own, whose literal is its underlying value's.
/// </summary>
/// <typeparam name="TSelf">The value object type.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
/// <remarks>
/// <para>
/// A value object over a <see cref="bool"/> is serialized as a Boolean, one over an integer of 64 bits at most as an Int,
/// whatever its width, and one over a real as a Float, read from an Int literal too. Every other one, over text, a
/// character, a <see cref="Guid"/>, a 128-bit integer, a date or a time, or a value object written by hand over another
/// type, is serialized as a String, read through <c>TSelf.TryParse</c> in the invariant culture, its normalizer included.
/// A number is read exactly as the underlying type, then goes through <c>TSelf.TryCreate</c>.
/// </para>
/// <para>
/// A value the type refuses is answered with a <see cref="LeafCoercionException"/> whose GraphQL error carries the
/// message of the violated rule and, as its <c>extensions.code</c>, the rule's code; the exception carries the code in
/// its <see cref="Exception.Data"/> under <see cref="ValueObjectErrors.ErrorCodeKey"/>, and the error's
/// <c>Exception</c> is a <see cref="ValueObjectException"/> with the type and the code, never the value. A literal or a
/// JSON value of another kind, or a number the underlying type cannot hold, is refused with
/// <see cref="ValueObjectErrorCodes.NotParsable"/>. GraphQL input always comes from a client, so a scalar always
/// validates.
/// </para>
/// <para>
/// A value is written as its underlying value, never through a formatting hook of the value object. An uninitialized
/// instance, equal to the default, whose value the type refuses is never written, nor is a real that is not finite.
/// </para>
/// <para>
/// The description is the type's, from its schema. The pattern it declares, or the one the wire form of a duration, a
/// time of day or a <see cref="DateTime"/> follows, is the scalar's <see cref="ScalarType.Pattern"/>, which HotChocolate
/// publishes as <c>@serializeAs</c> under <c>ApplySerializeAsToScalars</c>. GraphQL has no place for a length, a bound
/// or a known value.
/// </para>
/// </remarks>
public sealed class ValueObjectScalarType<TSelf, TValue> : ScalarType
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// A name valid until the schema's naming conventions name the scalar.
    /// </summary>
    private const string Unnamed = "ValueObject";

    private static readonly LiteralCodec<TValue>? Codec = LiteralCodec<TValue>.Instance;

    private static readonly ValueText<TValue>? Text = ValueText<TValue>.Instance;

    private readonly string? _name;

    /// <summary>
    /// Initializes the scalar.
    /// </summary>
    /// <param name="name">
    /// Its name, or <see langword="null"/> to take the one the schema's options give, or else the schema's naming
    /// conventions.
    /// </param>
    internal ValueObjectScalarType(string? name)
        : base(name ?? Unnamed, BindingBehavior.Implicit)
    {
        _name = name;
        _ = Instantiations;

        var schema = TSelf.Schema;
        if (SerializationType == ScalarSerializationType.String)
        {
            Pattern = schema.Pattern ?? ValueObjectSchemaKeywords.WirePattern(typeof(TValue), asText: false);
        }
    }

    /// <inheritdoc />
    public override Type RuntimeType => typeof(TSelf);

    /// <inheritdoc />
    public override ScalarSerializationType SerializationType => Codec?.Kind ?? ScalarSerializationType.String;

    /// <summary>
    /// Gets the collections of the value object HotChocolate builds by reflection for a list argument, named here so that
    /// the AOT compiler generates their code for every value object a scalar is closed over.
    /// </summary>
    internal static object[] Instantiations { get; } = [new List<TSelf>(), new List<TSelf?>(), Array.Empty<TSelf>(), Array.Empty<TSelf?>()];

    /// <inheritdoc />
    public override object CoerceInputLiteral(IValueNode valueLiteral)
    {
        ArgumentNullException.ThrowIfNull(valueLiteral);

        if (Codec is null)
        {
            return valueLiteral is StringValueNode text
                ? Parse(text.Value, literal: true)
                : throw WrongKind(KindOf(valueLiteral), literal: true);
        }

        return Codec.TryRead(valueLiteral, out var value, out var read)
            ? Create(value, read, literal: true)
            : throw WrongKind(KindOf(valueLiteral), literal: true);
    }

    /// <inheritdoc />
    public override object CoerceInputValue(JsonElement inputValue, IFeatureProvider context)
    {
        if (Codec is null)
        {
            return inputValue.ValueKind == JsonValueKind.String
                ? Parse(inputValue.GetString()!, literal: false)
                : throw WrongKind(KindOf(inputValue), literal: false);
        }

        return Codec.TryRead(inputValue, out var value, out var read)
            ? Create(value, read, literal: false)
            : throw WrongKind(KindOf(inputValue), literal: false);
    }

    /// <inheritdoc />
    public override void CoerceOutputValue(object runtimeValue, ResultElement resultValue)
    {
        var value = Unbox(runtimeValue);
        if (Codec is not null)
        {
            Codec.Write(Writable(Codec, value), resultValue);
        }
        else if (Text is not null)
        {
            Text.Write(value.Value, resultValue);
        }
        else
        {
            resultValue.SetStringValue(value.ToString(null, CultureInfo.InvariantCulture));
        }
    }

    /// <inheritdoc />
    public override IValueNode ValueToLiteral(object runtimeValue)
    {
        var value = Unbox(runtimeValue);
        if (Codec is not null)
        {
            return Codec.ToLiteral(Writable(Codec, value));
        }

        return new StringValueNode(Text is not null ? Text.Format(value.Value) : value.ToString(null, CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    protected override ScalarTypeConfiguration CreateConfiguration(ITypeDiscoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = context.DescriptorContext.Features.Get<ValueObjectScalarOptions>();
        Name = _name
               ?? options?.NameOf(typeof(TSelf))
               ?? context.DescriptorContext.Naming.GetTypeName(typeof(TSelf), TypeKind.Scalar);
        Description = TSelf.Schema.Description;
        SpecifiedBy = options?.SpecificationOf(typeof(TSelf));

        return base.CreateConfiguration(context);
    }

    /// <summary>
    /// Reads the value a result or a literal is written from, refusing an uninitialized instance its type refuses.
    /// </summary>
    private TSelf Unbox(object runtimeValue)
    {
        if (runtimeValue is not TSelf value)
        {
            throw new LeafCoercionException($"{Name} cannot write {Indefinite(runtimeValue.GetType().Name)}.", this);
        }

        if (value.IsDefault)
        {
            var current = value.Value;
            if (TSelf.Validate(in current) is { IsValid: false } refusal)
            {
                throw Refused(refusal, literal: false);
            }
        }

        return value;
    }

    /// <summary>
    /// Reads the underlying value of an instance to write as a number, refusing a real that is not finite.
    /// </summary>
    private TValue Writable(LiteralCodec<TValue> codec, TSelf value)
        => codec.Writable(value.Value) is { IsValid: false } refusal ? throw Refused(refusal, literal: false) : value.Value;

    /// <summary>
    /// Names the kind of a literal as GraphQL does, <c>Int</c> for an <c>IntValue</c>.
    /// </summary>
    private static string KindOf(IValueNode literal) => literal.Kind.ToString().Replace("Value", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// Names the kind of a JSON value as GraphQL names the kind of value it stands for, <c>Boolean</c> for <c>true</c>.
    /// </summary>
    private static string KindOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False => "Boolean",
        JsonValueKind.Array => "List",
        var kind => kind.ToString(),
    };

    private TSelf Parse(string text, bool literal)
        => TSelf.TryParse(text, CultureInfo.InvariantCulture, out var result, out var validation)
            ? result
            : throw Refused(validation, literal);

    private TSelf Create(TValue value, ValidationResult read, bool literal)
    {
        if (!read.IsValid)
        {
            throw Refused(read, literal);
        }

        return TSelf.TryCreate(value, out var result, out var validation) ? result : throw Refused(validation, literal);
    }

    private LeafCoercionException WrongKind(string kind, bool literal)
        => Refused(
            ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, $"{Name} cannot read {Indefinite(kind)}."),
            literal);

    /// <summary>
    /// Writes a noun after its indefinite article, <c>an Int</c>, <c>a String</c>.
    /// </summary>
    private static string Indefinite(string noun) => ("AEIOU".Contains(noun[0], StringComparison.Ordinal) ? "an " : "a ") + noun;

    /// <summary>
    /// Builds the refusal of a value: a GraphQL error with the rule's message and code, the exception a filter reads, and
    /// the code in the exception's data.
    /// </summary>
    /// <param name="validation">The verdict of the type's rules.</param>
    /// <param name="literal">Whether the value refused is a literal of the document, rather than JSON or a result.</param>
    private LeafCoercionException Refused(ValidationResult validation, bool literal)
    {
        // A value object written by hand may refuse without saying why.
        var code = validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable;
        var message = validation.ErrorMessage ?? $"The value is not a valid {Name}.";
        var error = ErrorBuilder.New()
            .SetMessage(message)
            .SetCode(code)
            .SetException(new ValueObjectScalarRefusal<TSelf, TValue>(message, code, Name, literal))
            .Build();

        var exception = new LeafCoercionException(error, this);
        exception.Data[ValueObjectErrors.ErrorCodeKey] = code;
        return exception;
    }
}
