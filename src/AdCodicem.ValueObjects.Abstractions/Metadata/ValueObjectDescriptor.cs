using System.Globalization;

namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// Attempts to build a value object from a boxed underlying value.
/// </summary>
/// <param name="value">Boxed candidate value.</param>
/// <param name="result">The boxed value object, or <see langword="null"/> when the value is rejected.</param>
/// <param name="validation">The outcome of the validation.</param>
/// <returns><see langword="true"/> when the value was accepted.</returns>
public delegate bool BoxedTryCreate(object? value, out object? result, out ValidationResult validation);

/// <summary>
/// Attempts to build a value object from its text representation.
/// </summary>
/// <param name="text">Text to parse.</param>
/// <param name="provider">Format provider used to parse the underlying value.</param>
/// <param name="result">The boxed value object, or <see langword="null"/> when the text is rejected.</param>
/// <param name="validation">The outcome of the parse and validation.</param>
/// <returns><see langword="true"/> when the text was accepted.</returns>
public delegate bool BoxedTryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out object? result, out ValidationResult validation);

/// <summary>
/// Runtime description of a value object type, used by the integrations that only know a <see cref="Type"/>.
/// </summary>
/// <remarks>
/// <para>
/// The delegates it exposes operate on boxed values, which is acceptable because every one of them is invoked
/// during start-up work — building the EF Core model, generating an OpenAPI document, registering a Dapper
/// type handler — never on a per-request path. Request paths go through the strongly typed generic APIs.
/// </para>
/// <para>
/// Descriptors are produced by <see cref="For{TSelf, TValue}"/>, which is fully generic and therefore free
/// of reflection: the source generator emits one call per value object in a module initializer.
/// </para>
/// </remarks>
public sealed class ValueObjectDescriptor
{
    private ValueObjectDescriptor(
        Type valueObjectType,
        Type valueType,
        ValueObjectSchema schema,
        Func<object?, object> create,
        Func<object?, object> createUnchecked,
        BoxedTryCreate tryCreate,
        BoxedTryParse tryParse,
        Func<object, object?> getValue,
        Func<object, string> format)
    {
        ValueObjectType = valueObjectType;
        ValueType = valueType;
        Schema = schema;
        Create = create;
        CreateUnchecked = createUnchecked;
        TryCreate = tryCreate;
        TryParse = tryParse;
        GetValue = getValue;
        Format = format;
    }

    /// <summary>
    /// Gets the value object type.
    /// </summary>
    public Type ValueObjectType { get; }

    /// <summary>
    /// Gets the underlying value type.
    /// </summary>
    public Type ValueType { get; }

    /// <summary>
    /// Gets the declarative constraints of the value object.
    /// </summary>
    public ValueObjectSchema Schema { get; }

    /// <summary>
    /// Gets the validating factory. Throws <see cref="ValueObjectException"/> on a rejected value.
    /// </summary>
    public Func<object?, object> Create { get; }

    /// <summary>
    /// Gets the trusted-source factory, which performs neither normalization nor validation.
    /// </summary>
    public Func<object?, object> CreateUnchecked { get; }

    /// <summary>
    /// Gets the non-throwing validating factory.
    /// </summary>
    public BoxedTryCreate TryCreate { get; }

    /// <summary>
    /// Gets the non-throwing text parser.
    /// </summary>
    public BoxedTryParse TryParse { get; }

    /// <summary>
    /// Gets the accessor returning the boxed underlying value of a boxed value object.
    /// </summary>
    public Func<object, object?> GetValue { get; }

    /// <summary>
    /// Gets the invariant text representation of a boxed value object.
    /// </summary>
    public Func<object, string> Format { get; }

    /// <summary>
    /// Builds a descriptor for a value object, resolving every operation statically.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="schema">Declarative constraints of the value object.</param>
    /// <returns>The descriptor.</returns>
    public static ValueObjectDescriptor For<TSelf, TValue>(ValueObjectSchema schema)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(schema);

        return new ValueObjectDescriptor(
            typeof(TSelf),
            typeof(TValue),
            schema,
            create: static value => TSelf.Create(Unbox<TValue>(value)),
            createUnchecked: static value => TSelf.CreateUnchecked(Unbox<TValue>(value)),
            tryCreate: static (object? value, out object? result, out ValidationResult validation) =>
            {
                TValue typed;
                switch (value)
                {
                    case null:
                        typed = default!;
                        break;
                    case TValue cast:
                        typed = cast;
                        break;
                    default:
                        result = null;
                        validation = ValidationResult.Failure(
                            ValueObjectErrorCodes.NotParsable,
                            $"Expected a value of type {typeof(TValue).Name}.");
                        return false;
                }

                if (TSelf.TryCreate(typed, out var created, out validation))
                {
                    result = created;
                    return true;
                }

                result = null;
                return false;
            },
            tryParse: static (ReadOnlySpan<char> text, IFormatProvider? provider, out object? result, out ValidationResult validation) =>
            {
                // The four argument overload carries the rule that actually rejected the text, which is the
                // whole point of declaring rules on the value object: a wrong IBAN check digit must surface as
                // 'invalid_format' with its message, not flattened into a generic 'not_parsable'.
                if (TSelf.TryParse(text, provider ?? CultureInfo.InvariantCulture, out var parsed, out validation))
                {
                    result = parsed;
                    return true;
                }

                result = null;

                // A rejection always carries a code. Only guard the case of a value object whose hand written
                // TryParse returns false without one, so callers can rely on ErrorCode being present.
                if (validation.IsValid)
                {
                    validation = ValidationResult.Failure(
                        ValueObjectErrorCodes.NotParsable,
                        $"The text could not be parsed as {typeof(TSelf).Name}.");
                }

                return false;
            },
            getValue: static valueObject => ((TSelf)valueObject).Value,
            format: static valueObject => ((TSelf)valueObject).ToString(null, CultureInfo.InvariantCulture));
    }

    private static TValue Unbox<TValue>(object? value)
        => value is null ? default! : (TValue)value;
}
