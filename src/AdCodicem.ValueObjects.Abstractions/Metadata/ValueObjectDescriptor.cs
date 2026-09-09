using System.Collections.Frozen;
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

        // A closed value set has a handful of instances that never change, so box each of them once here and
        // hand the same box to every caller instead of allocating a new one per conversion. Lookups happen on
        // the normalized value, and a miss simply falls through to creating one: the cache is never load
        // bearing, only an allocation the boxed paths do not have to make.
        var boxedKnownValues = BuildBoxedCache<TSelf, TValue>(schema);

        return new ValueObjectDescriptor(
            typeof(TSelf),
            typeof(TValue),
            schema,
            create: value =>
            {
                var typed = TSelf.Normalize(Unbox<TValue>(value));

                return boxedKnownValues is not null && typed is not null && TryGetCached(boxedKnownValues, typed, out var cached)
                    ? cached
                    : TSelf.Create(typed);
            },
            createUnchecked: value =>
            {
                var typed = Unbox<TValue>(value);

                return boxedKnownValues is not null && typed is not null && TryGetCached(boxedKnownValues, typed, out var cached)
                    ? cached
                    : TSelf.CreateUnchecked(typed);
            },
            tryCreate: (object? value, out object? result, out ValidationResult validation) =>
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
                    result = boxedKnownValues is not null && created.Value is not null && TryGetCached(boxedKnownValues, created.Value, out var cached)
                        ? cached
                        : created;
                    return true;
                }

                result = null;
                return false;
            },
            tryParse: (ReadOnlySpan<char> text, IFormatProvider? provider, out object? result, out ValidationResult validation) =>
            {
                // The four argument overload carries the rule that actually rejected the text, which is the
                // whole point of declaring rules on the value object: a wrong IBAN check digit must surface as
                // 'invalid_format' with its message, not flattened into a generic 'not_parsable'.
                if (TSelf.TryParse(text, provider ?? CultureInfo.InvariantCulture, out var parsed, out validation))
                {
                    result = boxedKnownValues is not null && parsed.Value is not null && TryGetCached(boxedKnownValues, parsed.Value, out var cached)
                        ? cached
                        : parsed;
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

    /// <summary>
    /// Boxes every member of a closed value set once, so the boxed paths can hand out shared instances.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="schema">Declarative constraints of the value object.</param>
    /// <returns>The cache, or <see langword="null"/> when the type is not a closed set.</returns>
    private static FrozenDictionary<object, object>? BuildBoxedCache<TSelf, TValue>(ValueObjectSchema schema)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        // Only reference-typed underlying values qualify. Keying on a boxed value type would mean boxing the
        // key on every lookup, which costs exactly the allocation the cache exists to avoid.
        if (!schema.IsClosedValueSet || schema.KnownValues.IsDefaultOrEmpty || typeof(TValue).IsValueType)
        {
            return null;
        }

        var entries = new Dictionary<object, object>(schema.KnownValues.Length);
        foreach (var known in schema.KnownValues)
        {
            if (known is TValue typed)
            {
                // The schema publishes normalized values, so these keys already match what a lookup will hold.
                entries[known] = TSelf.CreateUnchecked(typed);
            }
        }

        return entries.Count == 0 ? null : entries.ToFrozenDictionary();
    }

    /// <summary>
    /// Looks a normalized value up in the boxed cache, if there is one.
    /// </summary>
    /// <remarks>
    /// Callers must test <paramref name="cache"/> before evaluating the key, so that a value-typed underlying
    /// value is never boxed just to miss. The cache is keyed with the default comparer, so a value object
    /// declaring a case-insensitive comparison may miss; a miss costs one allocation and nothing else, and
    /// correctness never depends on hitting.
    /// </remarks>
    /// <param name="cache">Cache built by <see cref="BuildBoxedCache{TSelf, TValue}"/>.</param>
    /// <param name="key">Normalized value, already boxed because the cache only holds reference types.</param>
    /// <param name="cached">The shared box, when one exists.</param>
    /// <returns><see langword="true"/> when a shared box was found.</returns>
    private static bool TryGetCached(
        FrozenDictionary<object, object> cache,
        object key,
        [NotNullWhen(true)] out object? cached)
        => cache.TryGetValue(key, out cached);
}
