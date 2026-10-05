using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Metadata;
using Xunit;
using Xunit.Sdk;

namespace AdCodicem.ValueObjects.Testing;

/// <summary>
/// The behavioural contract every value object is expected to honour, as a ready-made xUnit test class.
/// </summary>
/// <typeparam name="TSelf">Value object under test.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// Derive from it, supply a handful of accepted and rejected values, and the properties that are easy to get
/// subtly wrong are all checked: that normalization settles, that equality and ordering agree, that a value
/// survives a round-trip through text and through JSON, that a rejected value is rejected the same way by
/// every entry point, that the example and the known values the type declares are values it accepts, and that the
/// details of its known values name each of them in its place.
/// </para>
/// <para>
/// <code>
/// public sealed class IbanContract : ValueObjectContract&lt;Iban, string&gt;
/// {
///     protected override IEnumerable&lt;string&gt; AcceptedValues =&gt; ["FR7630006000011234567890189"];
///     protected override IEnumerable&lt;string&gt; RejectedValues =&gt; ["", "not-an-iban"];
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class ValueObjectContract<TSelf, TValue>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Gets values the type must accept. Supply at least two distinct ones so ordering can be checked.
    /// </summary>
    protected abstract IEnumerable<TValue> AcceptedValues { get; }

    /// <summary>
    /// Gets values the type must reject.
    /// </summary>
    protected abstract IEnumerable<TValue> RejectedValues { get; }

    /// <summary>
    /// Gets a value indicating whether the JSON representation is expected to be a bare scalar.
    /// </summary>
    /// <remarks>Always true for the value objects this framework generates; overridable for exotic hand-written ones.</remarks>
    protected virtual bool SerializesAsScalar => true;

    [Fact]
    public void Every_accepted_value_produces_an_initialized_instance()
    {
        Assert.NotEmpty(AcceptedValues);

        foreach (var value in AcceptedValues)
        {
            Assert.True(TSelf.TryCreate(value, out var created, out var validation), $"'{value}' should be accepted.");
            Assert.True(validation.IsValid);

            // IsDefault means "equal to default(TSelf)", nothing more. For a value object over a numeric type
            // that accepts zero, a legitimate zero is indistinguishable from an uninitialized instance — which
            // is precisely why the VO0010 analyzer exists rather than a run-time guard.
            var isDefaultValue = EqualityComparer<TValue>.Default.Equals(created.Value, default!);
            Assert.Equal(isDefaultValue, created.IsDefault);
        }
    }

    [Fact]
    public void Every_rejected_value_is_reported_the_same_way_by_every_entry_point()
    {
        foreach (var value in RejectedValues)
        {
            Assert.False(TSelf.TryCreate(value, out _, out var validation), $"'{value}' should be rejected.");
            Assert.False(validation.IsValid);
            Assert.False(string.IsNullOrWhiteSpace(validation.ErrorCode), "A rejection must carry a stable code.");
            Assert.False(string.IsNullOrWhiteSpace(validation.ErrorMessage), "A rejection must carry a message.");

            var thrown = Assert.Throws<ValueObjectException>(() => TSelf.Create(value));
            Assert.Equal(validation.ErrorCode, thrown.ErrorCode);
        }
    }

    [Fact]
    public void Normalization_settles_after_one_pass()
    {
        foreach (var value in AcceptedValues.Concat(RejectedValues))
        {
            var once = TSelf.Normalize(value);
            var twice = TSelf.Normalize(once);

            Assert.Equal(once, twice);
        }
    }

    [Fact]
    public void Creating_from_an_already_created_value_changes_nothing()
    {
        foreach (var created in Accepted())
        {
            Assert.Equal(created, TSelf.Create(created.Value));
        }
    }

    [Fact]
    public void Equality_is_reflexive_symmetric_and_agrees_with_the_hash_code()
    {
        foreach (var created in Accepted())
        {
            var twin = TSelf.Create(created.Value);

            Assert.Equal(created, twin);
            Assert.Equal(twin, created);
            Assert.Equal(created.GetHashCode(), twin.GetHashCode());
            Assert.False(created.Equals(null));
        }
    }

    [Fact]
    public void Ordering_agrees_with_equality()
    {
        var values = Accepted().ToList();

        foreach (var left in values)
        {
            foreach (var right in values)
            {
                var comparison = left.CompareTo(right);

                Assert.Equal(left.Equals(right), comparison == 0);
                Assert.Equal(Math.Sign(comparison), -Math.Sign(right.CompareTo(left)));
            }
        }
    }

    [Fact]
    public void Text_survives_a_round_trip()
    {
        foreach (var created in Accepted())
        {
            var text = created.ToString(null, CultureInfo.InvariantCulture);

            Assert.True(
                TSelf.TryParse(text, CultureInfo.InvariantCulture, out var parsed, out var validation),
                $"'{text}' came from ToString but Parse rejected it: {validation.ErrorMessage}");

            Assert.Equal(created, parsed);
        }
    }

    [Fact]
    public void Formatting_into_a_span_matches_formatting_into_a_string()
    {
        foreach (var created in Accepted())
        {
            var expected = created.ToString(null, CultureInfo.InvariantCulture);

            var buffer = new char[expected.Length];
            Assert.True(created.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture));
            Assert.Equal(expected, new string(buffer, 0, written));

            if (expected.Length > 0)
            {
                var tooSmall = new char[expected.Length - 1];
                Assert.False(
                    created.TryFormat(tooSmall, out _, default, CultureInfo.InvariantCulture),
                    "A buffer that cannot hold the value must be reported, not truncated.");
            }
        }
    }

    [Fact]
    public void Json_carries_the_bare_underlying_value()
    {
        foreach (var created in Accepted())
        {
            var json = JsonSerializer.Serialize(created);

            if (SerializesAsScalar)
            {
                Assert.DoesNotContain("{", json, StringComparison.Ordinal);
            }

            Assert.Equal(created, JsonSerializer.Deserialize<TSelf>(json));
        }
    }

    [Fact]
    public void Json_rejects_a_value_the_type_would_reject()
    {
        // NaN and the infinities are values a bounded floating-point type rejects, and plain JSON cannot write them.
        var options = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

        foreach (var value in RejectedValues)
        {
            var json = JsonSerializer.Serialize(value, options);

            Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<TSelf>(json, options));
        }
    }

    [Fact]
    public void The_type_is_discoverable_at_run_time()
    {
        EnsureRegistered();

        Assert.True(
            ValueObjectRegistry.TryGet(typeof(TSelf), out var descriptor),
            $"'{typeof(TSelf).Name}' did not register itself. Is the generator running on its assembly?");

        Assert.Equal(typeof(TValue), descriptor.ValueType);

        foreach (var created in Accepted())
        {
            var boxed = descriptor.Create(created.Value);
            Assert.Equal(created.Value, descriptor.GetValue(boxed));
        }
    }

    [Fact]
    public void Declared_length_limits_hold_for_every_accepted_value()
    {
        // The limits are read from the registry's schema. Without it there is nothing to check, which is a skip to
        // report rather than a pass to claim.
        EnsureRegistered();
        if (!ValueObjectRegistry.TryGet(typeof(TSelf), out var descriptor))
        {
            Assert.Skip(
                $"'{typeof(TSelf).Name}' did not register itself, so it has no declared length limits to check. "
                + "The_type_is_discoverable_at_run_time says why.");
        }

        foreach (var created in Accepted())
        {
            if (created.Value is not string text)
            {
                return;
            }

            if (descriptor.Schema.MinLength is { } minimum)
            {
                Assert.True(text.Length >= minimum, $"'{text}' is shorter than the declared minimum length.");
            }

            if (descriptor.Schema.MaxLength is { } maximum)
            {
                Assert.True(text.Length <= maximum, $"'{text}' is longer than the declared maximum length.");
            }
        }
    }

    /// <summary>
    /// The example the type declares is the one the OpenAPI document publishes, which generated clients, mock servers
    /// and readers take at its word: the type must accept it, read as the document's transformer reads it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A generated value object declares its example through <see cref="IValueObjectExample{TSelf}"/>, as an instance
    /// that went through its rules, whose underlying value the schema holds: created again here, it must still be
    /// accepted. The generator refuses at compile time an example its rules refuse when it can evaluate them
    /// (<c>VO0031</c>). A schema written by hand may hold text instead, which is parsed as the transformer parses it.
    /// </para>
    /// <para>
    /// The check reads <c>TSelf.Schema</c>, as the declaration checks below do, so it runs on a value object written by
    /// hand whether anything registered it or not.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_declared_example_is_accepted()
    {
        Initialize();

        var example = TSelf.Schema.Example;
        if (example is null)
        {
            Assert.Skip($"'{typeof(TSelf).Name}' declares no example.");
        }

        ValidationResult validation;
        var accepted = example is TValue value
            ? TSelf.TryCreate(value, out _, out validation)
            : TSelf.TryParse(Convert.ToString(example, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, out _, out validation);

        Assert.True(
            accepted,
            $"The example '{example}' declared on '{typeof(TSelf).Name}' is refused ({validation.ErrorCode}): {validation.ErrorMessage}");
    }

    /// <summary>
    /// A known value is built as the type initializes, and a refused one makes the type fail to initialize: every check
    /// of the type then fails with a <see cref="TypeInitializationException"/>, and this one says which value broke
    /// which rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generator refuses at compile time a known value its rules refuse when it can evaluate them (<c>VO0031</c>).
    /// </para>
    /// <para>
    /// A value object that is not generic is initialized by the registration of its module, which the runtime runs as
    /// soon as anything of the module is used, before this contract can even be created: the runner then reports the
    /// <see cref="TypeInitializationException"/> of the module for every check, and its innermost exception names the
    /// type and the rule. A construction of a generic value object, and a value object written by hand, initialize
    /// when first used, which this check does.
    /// </para>
    /// <para>
    /// The check reads <c>TSelf.Schema</c>, so it runs on a value object written by hand whether anything registered it
    /// or not. Known values left at the default of their array are none, and one that is not of the underlying type, as
    /// a schema written by hand may hold, is reported as such.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_declared_known_value_is_accepted()
    {
        Initialize();

        var knownValues = TSelf.Schema.KnownValues.AsSpan();
        if (knownValues.IsEmpty)
        {
            Assert.Skip($"'{typeof(TSelf).Name}' declares no known value.");
        }

        foreach (var known in knownValues)
        {
            Assert.True(
                known is TValue,
                $"The known value {DescribeTyped(known)} declared on '{typeof(TSelf).Name}' is not of its underlying type, "
                + $"{typeof(TValue).Name}.");

            Assert.True(
                TSelf.TryCreate((TValue)known, out _, out var validation),
                $"The known value '{known}' declared on '{typeof(TSelf).Name}' is refused ({validation.ErrorCode}): {validation.ErrorMessage}");
        }
    }

    /// <summary>
    /// The details of the known values hold the names the OpenAPI document publishes beside the <c>enum</c> of a closed
    /// set, which a client generated from it names the members of its enumeration after: they list the known values one
    /// for one, in the same order, each under a name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generator writes them so. A schema written by hand is where they drift apart, or are left out, and the
    /// OpenAPI integration then publishes no name at all, rather than one beside the wrong value. The values are
    /// compared as that integration compares them, with <see cref="object.Equals(object, object)"/>, so a value boxed as
    /// another type than its counterpart is out of step.
    /// </para>
    /// <para>
    /// The check reads <c>TSelf.Schema</c>, the schema the type declares, which generic code reads and the registry
    /// describes the type with unless it was registered with another, so it runs on a value object written by hand
    /// whether anything registered it or not. A type that declares no known value reports the check skipped; one that
    /// declares known values and details none of them fails it.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_known_value_details_line_up_with_the_known_values()
    {
        Initialize();

        var schema = TSelf.Schema;
        var knownValues = schema.KnownValues.AsSpan();
        var details = schema.KnownValueDetails.AsSpan();
        if (details.IsEmpty && knownValues.IsEmpty)
        {
            Assert.Skip($"'{typeof(TSelf).Name}' declares no known value.");
        }

        Assert.True(
            details.Length == knownValues.Length,
            $"'{typeof(TSelf).Name}' has {details.Length} known value details for {knownValues.Length} known values: "
            + "KnownValueDetails lists the values of KnownValues one for one, in the same order.");

        for (var index = 0; index < details.Length; index++)
        {
            var detail = details[index];
            Assert.True(
                detail is not null && !string.IsNullOrWhiteSpace(detail.Name),
                $"The known value detail at index {index} of '{typeof(TSelf).Name}' has no name, which the OpenAPI document "
                + "names the value after.");

            var known = knownValues[index];
            Assert.True(
                Equals(detail.Value, known),
                $"The known value detail '{detail.Name}' of '{typeof(TSelf).Name}' is of the value "
                + $"{Describe(detail.Value, known)}, where the known value at index {index} is {Describe(known, detail.Value)}: "
                + "KnownValueDetails lists the values of KnownValues one for one, in the same order.");
        }
    }

    private IEnumerable<TSelf> Accepted() => AcceptedValues.Select(TSelf.Create);

    /// <summary>
    /// Writes a value as a failure names it, with its type when the value it is compared with is of another, which two
    /// values written alike would otherwise hide.
    /// </summary>
    private static string Describe(object? value, object? other)
        => value is null ? "null"
            : other is not null && value.GetType() != other.GetType() ? $"'{value}' ({value.GetType().Name})"
            : $"'{value}'";

    /// <summary>
    /// Writes a value as a failure names it, with its type.
    /// </summary>
    private static string DescribeTyped(object? value) => value is null ? "null" : $"'{value}' ({value.GetType().Name})";

    /// <summary>
    /// Initializes the type, reporting one that cannot because a value it creates as it does, such as a known value, is
    /// refused.
    /// </summary>
    /// <remarks>
    /// The type is initialized directly rather than through the reflection the registry describes a construction of a
    /// generic value object with, so that its failure reaches the kit as it happened.
    /// </remarks>
    private static void Initialize()
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(typeof(TSelf).TypeHandle);
        }
        catch (TypeInitializationException exception) when (Refusal(exception) is { } refusal)
        {
            throw FailException.ForFailure(
                $"'{typeof(TSelf).Name.Split('`')[0]}' cannot initialize: a value it creates as it does, a known value most "
                + $"likely, is refused ({refusal.ErrorCode}): {refusal.Message}");
        }
    }

    /// <summary>
    /// Finds the rejection behind a failure to initialize, through the type initializers it went through.
    /// </summary>
    private static ValueObjectException? Refusal(TypeInitializationException exception)
    {
        Exception? current = exception;
        while (current is TypeInitializationException)
        {
            current = current.InnerException;
        }

        return current as ValueObjectException;
    }

    /// <summary>
    /// Runs the generated registration of the assembly declaring <typeparamref name="TSelf"/>, which a contract kept in
    /// a test assembly of its own may be the first to need: the registry only knows a type once that has run.
    /// </summary>
    /// <remarks>
    /// A construction of a generic value object is never registered as such: its registration registers its generic
    /// definition, and the registry describes each construction once asked for it, as an integration asks.
    /// </remarks>
    private static void EnsureRegistered()
    {
        var type = typeof(TSelf);
        ValueObjectRegistry.EnsureAssemblyRegistered(type.Assembly);

        if (type.IsConstructedGenericType)
        {
            ValueObjectRegistry.TryResolve(type, out _);
        }
    }
}
