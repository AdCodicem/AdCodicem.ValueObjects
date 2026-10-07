using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.UnitTests.Logging;
using AdCodicem.ValueObjects.UnitTests.Persistence;
using AdCodicem.ValueObjects.UnitTests.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

/// <summary>
/// Two instances of one value object of the domain, the smaller first, and text the type must refuse: enough to
/// put every member the generator emits through its paces without knowing the type at compile time.
/// </summary>
/// <remarks>
/// The members that no interface declares — the operators, the two-argument <c>Parse</c> and <c>TryParse</c> — are
/// reached through their public signatures by reflection, once, rather than through thirty-two hand-written call
/// sites. They are public members of generated code, which is what the audit of emitted code wants to run
/// (docs/adr/0006-coverage-is-a-signal-not-a-goal.md).
/// </remarks>
public abstract class Sample
{
    private static readonly JsonSerializerOptions NumbersFromStrings =
        new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    /// <summary>The numeric types a value object may wrap, which the type converter of a numeric one converts.</summary>
    protected static readonly Type[] Numbers =
    [
        typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(Int128), typeof(UInt128), typeof(decimal), typeof(double), typeof(float),
    ];

    public abstract void ConvertsThroughItsTypeConverter();

    public abstract void ConvertsNumbersThroughItsTypeConverter();

    public abstract void RoundTripsAsADictionaryKey();

    public abstract void ParsesThroughEveryOverload();

    public abstract void ComparesThroughEveryOperator();

    public abstract void ExposesItsValueThroughEveryAccessor();

    public abstract void FormatsWithoutAProvider();

    public abstract void RefusesJsonOfTheWrongShape();

    public abstract void RefusesAGroupSeparatorInTheInvariantCulture();

    public abstract void OffersNoConstructorTakingTheValueAlone();

    public abstract void HidesIsDefaultFromReflection();

    public abstract void WritesNoJsonItsTypeRejects();

    public abstract void CarriesTheCodeOfEveryJsonRefusal();

    public abstract void HandsItsTypeArgumentsToAVisitor();

    public abstract void DetailsEachKnownValueInItsSchema();

    /// <summary>Maps a minimal API endpoint binding the value object from the query string, under its name.</summary>
    /// <param name="routes">Where the endpoint is mapped.</param>
    public abstract void MapMinimalApiEndpoint(IEndpointRouteBuilder routes);

    /// <summary>
    /// Sends the refused text to that endpoint, under <c>/samples</c>, and checks that the problem details carry the rule
    /// that refused it.
    /// </summary>
    /// <param name="client">A client of the application that maps it.</param>
    /// <returns>The check.</returns>
    public abstract Task AnswersARefusedMinimalApiValueWithItsRuleAsync(HttpClient client);

    /// <summary>
    /// Reads a body holding the refused text, as the token Newtonsoft.Json reads it from, through the formatter
    /// <c>AddValueObjectsNewtonsoftJson()</c> puts in MVC, and checks that it records the code the converter gives the
    /// refusal under the key of its error.
    /// </summary>
    /// <returns>The check.</returns>
    public abstract Task RecordsTheCodeOfARefusedNewtonsoftBodyAsync();

    /// <summary>
    /// Logs the value object through Serilog, with and without <c>@</c>, under the policy and under the option of
    /// <c>Destructure.ValueObjects()</c>, and checks that it is logged as the value it carries.
    /// </summary>
    public abstract void LogsAsItsUnderlyingValueThroughSerilog();

    /// <summary>
    /// Stores the value object through MongoDB.Driver, with the serializer <c>ValueObjectBson.Register</c> hands it, and
    /// checks that it writes and reads back what the bare value it carries writes and reads.
    /// </summary>
    public abstract void RoundTripsThroughBsonAsItsUnderlyingValue();

    /// <summary>Gets a value indicating whether the value object declares <c>Arithmetic = true</c>.</summary>
    public virtual bool IsNumeric => false;

    /// <summary>
    /// Gets a value indicating whether the underlying type is a <c>decimal</c>, a <c>double</c> or a <c>float</c>, whose
    /// own number styles read a group separator.
    /// </summary>
    public abstract bool IsReal { get; }

    public virtual void ComputesThroughEveryArithmeticMember()
        => throw new InvalidOperationException($"{this} declares no arithmetic.");

    /// <param name="first">One instance.</param>
    /// <param name="second">Another instance, larger or smaller.</param>
    /// <param name="refused">Text the type refuses.</param>
    /// <param name="parsed">
    /// The text the smaller instance is parsed from, when its formatting hook writes text that does not parse back;
    /// <see langword="null"/> when what it writes is what it reads.
    /// </param>
    public static Sample Of<TSelf, TValue>(TSelf first, TSelf second, string refused, string? parsed = null)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => new Sample<TSelf, TValue>(first, second, refused, parsed);

    public static Sample Numeric<TSelf, TValue>(TSelf first, TSelf second, string refused)
        where TSelf : struct, INumericValueObject<TSelf, TValue>
        where TValue : struct, System.Numerics.INumber<TValue>
        => new NumericSample<TSelf, TValue>(first, second, refused);

    protected static JsonSerializerOptions LenientNumbers => NumbersFromStrings;
}

/// <inheritdoc />
public class Sample<TSelf, TValue> : Sample
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    private delegate bool TryParseText(string? text, out TSelf result);

    private delegate bool TryParseSpan(ReadOnlySpan<char> text, out TSelf result);

    public Sample(TSelf first, TSelf second, string refused, string? parsed = null)
    {
        (Small, Large) = first.CompareTo(second) <= 0 ? (first, second) : (second, first);
        Small.Should().NotBe(Large, "a sample needs two distinct values to compare");
        Refused = refused;
        Written = Small.ToString(null, CultureInfo.InvariantCulture);
        Text = parsed ?? Written;
    }

    protected TSelf Small { get; }

    protected TSelf Large { get; }

    protected string Refused { get; }

    private const string Unparsable = "not a value";

    /// <summary>Gets the text the smaller instance is parsed from.</summary>
    private string Text { get; }

    /// <summary>Gets the text the smaller instance is formatted as, which is <see cref="Text"/> but for a hook.</summary>
    private string Written { get; }

    public override bool IsReal => typeof(TValue) == typeof(decimal) || typeof(TValue) == typeof(double) || typeof(TValue) == typeof(float);

    public override string ToString() => typeof(TSelf).Name;

    public override void ConvertsThroughItsTypeConverter()
    {
        var converter = TypeDescriptor.GetConverter(typeof(TSelf));

        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
        converter.CanConvertFrom(typeof(TValue)).Should().BeTrue();
        converter.CanConvertFrom(typeof(Uri)).Should().BeFalse();
        converter.ConvertFromInvariantString(Text).Should().Be(Small);
        converter.ConvertFrom(null, null, Text).Should().Be(Small);
        converter.ConvertFrom(Small.Value!).Should().Be(Small);
        converter.Invoking(c => c.ConvertFromInvariantString(Refused)).Should().Throw<ValueObjectException>();
        converter.Invoking(c => c.ConvertFrom(new Uri("https://example.com"))).Should().Throw<NotSupportedException>();

        converter.CanConvertTo(typeof(string)).Should().BeTrue();
        converter.CanConvertTo(typeof(TValue)).Should().BeTrue();
        converter.CanConvertTo(typeof(Uri)).Should().BeFalse();
        converter.ConvertToInvariantString(Small).Should().Be(Written);
        converter.ConvertTo(null, null, Small, typeof(string)).Should().Be(Written);
        converter.ConvertTo(Small, typeof(TValue)).Should().Be(typeof(TValue) == typeof(string) ? Written : Small.Value);
        converter.ConvertTo("not a value object", typeof(string)).Should().Be("not a value object");
        converter.Invoking(c => c.ConvertTo(Small, typeof(Uri))).Should().Throw<NotSupportedException>();
    }

    public override void ConvertsNumbersThroughItsTypeConverter()
    {
        var converter = TypeDescriptor.GetConverter(typeof(TSelf));

        if (!NumberConversion.IsNumber(typeof(TValue)))
        {
            // Over anything but a number, the converter takes text and its own type, and no number.
            foreach (var type in Numbers)
            {
                converter.CanConvertFrom(type).Should().BeFalse("{0} is no {1}", type.Name, typeof(TValue).Name);
                converter.CanConvertTo(type).Should().BeFalse("{0} is no {1}", type.Name, typeof(TValue).Name);
            }

            converter.Invoking(c => c.ConvertFrom(1)).Should().Throw<NotSupportedException>();
            converter.Invoking(c => c.ConvertTo(Small, typeof(int))).Should().Throw<NotSupportedException>();
            return;
        }

        converter.CanConvertTo(null).Should().BeFalse("no type is no number");

        // Every arm the generator writes: each numeric type that holds the value whole converts both ways, and one that
        // cannot is refused as a conversion the converter does not perform. The conversions the generic converter makes
        // through the same bridges say which types hold it.
        foreach (var type in Numbers)
        {
            converter.CanConvertFrom(type).Should().BeTrue("a {0} is a number", type.Name);
            converter.CanConvertTo(type).Should().BeTrue("a {0} is a number", type.Name);

            if (NumberConversion.TryConvert(Small.Value, type, out var number))
            {
                converter.ConvertFrom(number).Should().Be(Small, "{0} is {1} as a {2}", number, Small, type.Name);
                converter.ConvertTo(Small, type).Should().Be(number, "{0} as a {1} is {2}", Small, type.Name, number);
            }
            else
            {
                converter.Invoking(c => c.ConvertTo(Small, type)).Should().Throw<NotSupportedException>();
            }
        }

        // A fraction is no integer, and a number beyond the range of a decimal or a float is neither: refused before any
        // rule runs, and named in no message. A double holds every one of them.
        foreach (var candidate in new object[] { 0.5, 1e300 })
        {
            if (NumberConversion.TryConvert(candidate, typeof(TValue), out _))
            {
                continue;
            }

            var refusal = converter.Invoking(c => c.ConvertFrom(candidate)).Should().Throw<ValueObjectException>().Which;
            refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
            refusal.Message.Should().StartWith($"'{typeof(TSelf).Name}' rejected the supplied number: The number is not a valid ");
            refusal.AttemptedValue.Should().Be(candidate);
        }
    }

    public override void RoundTripsAsADictionaryKey()
    {
        var json = JsonSerializer.Serialize(new Dictionary<TSelf, int> { [Small] = 1, [Large] = 2 });

        var read = JsonSerializer.Deserialize<Dictionary<TSelf, int>>(json)!;

        read.Should().HaveCount(2);
        read[Small].Should().Be(1);
        read[Large].Should().Be(2);

        var refused = JsonSerializer.Serialize(new Dictionary<string, int> { [Refused] = 1 });
        FluentActions.Invoking(() => JsonSerializer.Deserialize<Dictionary<TSelf, int>>(refused))
            .Should().Throw<JsonException>().WithMessage($"The dictionary key is not a valid {typeof(TSelf).Name}: ?*");
    }

    public override void ParsesThroughEveryOverload()
    {
        var parse = Method<Func<string, TSelf>>("Parse", typeof(string));
        var tryParseText = Method<TryParseText>("TryParse", typeof(string), typeof(TSelf).MakeByRefType());
        var tryParseSpan = Method<TryParseSpan>("TryParse", typeof(ReadOnlySpan<char>), typeof(TSelf).MakeByRefType());

        parse(Text).Should().Be(Small);
        ParseThroughIParsable<TSelf>(Text).Should().Be(Small);
        TSelf.Parse(Text.AsSpan(), null).Should().Be(Small);

        tryParseText(Text, out var fromText).Should().BeTrue();
        fromText.Should().Be(Small);
        tryParseSpan(Text, out var fromSpan).Should().BeTrue();
        fromSpan.Should().Be(Small);
        TryParseThroughIParsable<TSelf>(Text, out var withProvider).Should().BeTrue();
        withProvider.Should().Be(Small);
        TSelf.TryParse(Text.AsSpan(), null, out var spanWithProvider).Should().BeTrue();
        spanWithProvider.Should().Be(Small);

        tryParseText(null, out _).Should().BeFalse("null is no text");
        TryParseThroughIParsable<TSelf>(null, out _).Should().BeFalse("null is no text, whatever the provider");
        tryParseText(Refused, out _).Should().BeFalse("'{0}' is refused", Refused);
        tryParseSpan(Refused, out _).Should().BeFalse("'{0}' is refused as a span too", Refused);
        TSelf.TryParse(Refused, null, out _, out var refusal).Should().BeFalse("'{0}' is refused with its rule", Refused);
        FluentActions.Invoking(() => parse(Refused))
            .Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(refusal.ErrorCode);
        FluentActions.Invoking(() => ParseThroughIParsable<TSelf>(Refused)).Should().Throw<ValueObjectException>();

        if (typeof(TValue) != typeof(string))
        {
            // Text that is not even of the underlying type's shape, which no string value object can be handed.
            tryParseText(Unparsable, out _).Should().BeFalse();
            tryParseSpan(Unparsable, out _).Should().BeFalse();
            TryParseThroughIParsable<TSelf>(Unparsable, out _).Should().BeFalse();
            TSelf.TryParse(Unparsable, null, out _, out var validation).Should().BeFalse();
            validation.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        }
    }

    public override void ComparesThroughEveryOperator()
    {
        var equal = Operator("op_Equality");
        var notEqual = Operator("op_Inequality");
        var less = Operator("op_LessThan");
        var greater = Operator("op_GreaterThan");
        var lessOrEqual = Operator("op_LessThanOrEqual");
        var greaterOrEqual = Operator("op_GreaterThanOrEqual");
        var twin = TSelf.Create(Small.Value);

        equal(Small, twin).Should().BeTrue();
        equal(Small, Large).Should().BeFalse();
        notEqual(Small, Large).Should().BeTrue();
        notEqual(Small, twin).Should().BeFalse();
        less(Small, Large).Should().BeTrue();
        less(Large, Small).Should().BeFalse();
        greater(Large, Small).Should().BeTrue();
        greater(Small, Large).Should().BeFalse();
        lessOrEqual(Small, twin).Should().BeTrue();
        lessOrEqual(Large, Small).Should().BeFalse();
        greaterOrEqual(Small, twin).Should().BeTrue();
        greaterOrEqual(Small, Large).Should().BeFalse();

        Small.Equals((object)twin).Should().BeTrue();
        Small.Equals((object)Large).Should().BeFalse();
        Small.Equals((object)Written).Should().BeFalse();
        ((IComparable)Small).CompareTo(Large).Should().BeNegative();
    }

    public override void ExposesItsValueThroughEveryAccessor()
    {
        ((IValueObject)Small).GetBoxedValue().Should().Be(Small.Value);
        TSelf.CreateUnchecked(Small.Value).Should().Be(Small);
        TSelf.TryCreate(Small.Value, out var created).Should().BeTrue();
        created.Should().Be(Small);

        // The uninitialized instance is what a deserializer or an array hands out before any validation ran: it
        // must still answer for its value and its hash code rather than throw.
        var uninitialized = default(TSelf);
        uninitialized.IsDefault.Should().BeTrue();
        uninitialized.GetHashCode().Should().Be(default(TSelf).GetHashCode());
        ((object?)uninitialized.Value).Should().Be(typeof(TValue) == typeof(string) ? string.Empty : default(TValue));
    }

    public override void FormatsWithoutAProvider()
    {
        Small.ToString(null, null).Should().Be(Written);
        Small.ToString().Should().Be(Written, "the default format is the same whichever overload asks for it");
        $"{Small}".Should().Be(Written, "interpolation formats through TryFormat, with the default format");

        var buffer = new char[Written.Length];
        Small.TryFormat(buffer, out var written, default, null).Should().BeTrue();
        new string(buffer, 0, written).Should().Be(Written);
    }

    public override void RefusesJsonOfTheWrongShape()
    {
        var json = JsonSerializer.Serialize(Small);
        var wrongShape = json.StartsWith('"') ? "42" : "\"not a value\"";

        FluentActions.Invoking(() => JsonSerializer.Deserialize<TSelf>(wrongShape)).Should().Throw<JsonException>();
        FluentActions.Invoking(() => JsonSerializer.Deserialize<TSelf>(JsonSerializer.Serialize(Refused)))
            .Should().Throw<JsonException>();

        if (typeof(TValue) != typeof(string))
        {
            FluentActions.Invoking(() => JsonSerializer.Deserialize<TSelf>($"\"{Unparsable}\""))
                .Should().Throw<JsonException>();
        }

        if (!json.StartsWith('"') && json is not ("true" or "false"))
        {
            // A number written as a string is read when the options allow it, and refused when it is no number.
            JsonSerializer.Deserialize<TSelf>($"\"{json}\"", LenientNumbers).Should().Be(Small);
            FluentActions.Invoking(() => JsonSerializer.Deserialize<TSelf>("\"not a number\"", LenientNumbers))
                .Should().Throw<JsonException>();
        }
    }

    public override void RefusesAGroupSeparatorInTheInvariantCulture()
    {
        // A zero and a group separator ahead of the text: the type's own number styles read the same value from it.
        var grouped = Text.StartsWith('-') ? "-0," + Text[1..] : "0," + Text;
        var english = CultureInfo.GetCultureInfo("en-US");
        var converter = TypeDescriptor.GetConverter(typeof(TSelf));
        var parse = Method<Func<string, TSelf>>("Parse", typeof(string));

        IFormatProvider?[] invariant = [null, CultureInfo.InvariantCulture, new CultureInfo(string.Empty), NumberFormatInfo.InvariantInfo];
        foreach (var provider in invariant)
        {
            TSelf.TryParse(grouped, provider, out _, out var validation).Should().BeFalse("'{0}' holds a group separator", grouped);
            validation.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
            TSelf.TryParse(grouped.AsSpan(), provider, out _).Should().BeFalse();
            FluentActions.Invoking(() => TSelf.Parse(grouped.AsSpan(), provider))
                .Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        }

        TryParseThroughIParsable<TSelf>(grouped, out _).Should().BeFalse();
        FluentActions.Invoking(() => parse(grouped))
            .Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        converter.Invoking(c => c.ConvertFromInvariantString(grouped))
            .Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        var key = JsonSerializer.Serialize(new Dictionary<string, int> { [grouped] = 1 });
        FluentActions.Invoking(() => JsonSerializer.Deserialize<Dictionary<TSelf, int>>(key)).Should().Throw<JsonException>();

        // Any other culture says what a comma is, and keeps the type's own styles.
        TSelf.TryParse(grouped, english, out var read, out _).Should().BeTrue();
        read.Should().Be(Small);
        TSelf.Parse(grouped.AsSpan(), english).Should().Be(Small);
        converter.ConvertFrom(null, english, grouped).Should().Be(Small);
    }

    public override void OffersNoConstructorTakingTheValueAlone()
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // What a reflection mapper looks for, AutoMapper first among them: a constructor of any accessibility taking
        // the source value alone, which it calls with a value no rule has checked.
        typeof(TSelf).GetConstructor(Instance, [typeof(TValue)]).Should().BeNull();

        var constructor = typeof(TSelf).GetConstructors(Instance).Should().ContainSingle().Subject;
        constructor.IsPrivate.Should().BeTrue();
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(typeof(TValue), typeof(UncheckedTag));
        constructor.GetParameters().Should().NotContain(parameter => parameter.IsOptional, "a mapper fills an optional parameter from its default");
    }

    public override void HidesIsDefaultFromReflection()
    {
        // What a logger destructuring the value object, a schema generator or an exporter walks: the guard is for code.
        typeof(TSelf).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name)
            .Should().Contain("Value").And.NotContain(nameof(IValueObject<TSelf, TValue>.IsDefault));

        // Still there for generic code, which reaches it through the constraint without boxing. It tells an instance
        // equal to the default, which over a value type a constructed zero is too: Consent.Create(false) reads true.
        IsDefault(default(TSelf)).Should().BeTrue();
        IsDefault(Small).Should().Be(Small.Equals(default(TSelf)));
        IsDefault(Large).Should().Be(Large.Equals(default(TSelf)));
    }

    public override void HandsItsTypeArgumentsToAVisitor()
    {
        ValueObjectRegistry.TryGet(typeof(TSelf), out var descriptor).Should().BeTrue();

        descriptor!.Accept(TypeArgumentsVisitor.Instance).Should().Be((typeof(TSelf), typeof(TValue)));

        // The generated registration hands over the schema the type declares, which generic code reads through TSelf.
        descriptor.Accept(SchemaVisitor.Instance).Should().BeSameAs(descriptor.Schema);
        TSelf.Schema.Should().BeSameAs(descriptor.Schema);

        var adapter = descriptor.Accept(AdapterVisitor.Instance);
        adapter.Should().BeOfType<TypedAdapter<TSelf, TValue>>();
        adapter.MaxLength.Should().Be(descriptor.Schema.MaxLength);
        adapter.Parse(Text).Should().Be(Small);
    }

    public override void DetailsEachKnownValueInItsSchema()
    {
        var schema = TSelf.Schema;

        // The values the enum of the document lists, in its order, so that a name stands beside its own value.
        schema.KnownValueDetails.Select(detail => detail.Value).Should().Equal(schema.KnownValues);

        // Each named after the member that holds it, which the type marks [KnownValue].
        foreach (var detail in schema.KnownValueDetails)
        {
            var member = typeof(TSelf).GetMember(detail.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SingleOrDefault(candidate => candidate is FieldInfo or PropertyInfo);
            member.Should().NotBeNull($"{detail.Name} is a known value of {typeof(TSelf).Name}");
            member!.IsDefined(typeof(KnownValueAttribute)).Should().BeTrue();

            var known = member is FieldInfo field ? field.GetValue(null) : ((PropertyInfo)member).GetValue(null);
            ((TSelf)known!).Value.Should().Be(detail.Value);
        }
    }

    public override void MapMinimalApiEndpoint(IEndpointRouteBuilder routes)
        => routes.MapGet($"/{this}", static (TSelf value) => value.ToString());

    public override async Task AnswersARefusedMinimalApiValueWithItsRuleAsync(HttpClient client)
    {
        using var response = await client.GetAsync(
            $"/samples/{this}?value={Uri.EscapeDataString(Refused)}", TestContext.Current.CancellationToken);
        var problem = await MinimalApiProblemDetailsTests.ReadProblemAsync(response);

        var (message, code) = MinimalApiProblemDetailsTests.Refusal<TSelf, TValue>(Refused);
        problem.Messages.Should().ContainKey("value").WhoseValue.Should().Equal(message);
        problem.Codes.Should().Equal(new Dictionary<string, string> { ["value"] = code });
    }

    public override async Task RecordsTheCodeOfARefusedNewtonsoftBodyAsync()
    {
        var written = JsonSerializer.Serialize(Small);
        var isNumber = !written.StartsWith('"') && written is not ("true" or "false");
        var token = isNumber && IsJsonNumber(Refused) ? Refused : JsonSerializer.Serialize(Refused);
        var body = $$"""{"value":{{token}}}""";

        var refusal = FluentActions.Invoking(() => Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftHolder<TSelf>>(
                body, new Newtonsoft.Json.JsonSerializerSettings().AddValueObjects()))
            .Should().Throw<Newtonsoft.Json.JsonSerializationException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue("the converter refuses {0} with a code", body);

        (await NewtonsoftJsonInputFormatterTests.ReadCodesAsync(typeof(NewtonsoftHolder<TSelf>), body))
            .Should().Equal(new Dictionary<string, string> { ["value"] = code! });
    }

    public override void LogsAsItsUnderlyingValueThroughSerilog()
    {
        SerilogParity.Check<TSelf, TValue>(Small);
        SerilogParity.Check<TSelf, TValue>(Large);
    }

    public override void RoundTripsThroughBsonAsItsUnderlyingValue()
    {
        MongoDbParity.Check<TSelf, TValue>(Small);
        MongoDbParity.Check<TSelf, TValue>(Large);
    }

    public override void WritesNoJsonItsTypeRejects()
    {
        // What a deserializer, an array or a message initializer hands out before any rule ran.
        var uninitialized = default(TSelf);
        var value = uninitialized.Value;
        var validation = TSelf.Validate(in value);

        if (validation.IsValid)
        {
            // A type that accepts its zero writes it, as a value and as a key, and reads it back.
            var json = JsonSerializer.Serialize(uninitialized);
            json.Should().Be(JsonSerializer.Serialize(TSelf.CreateUnchecked(value)));
            ((object?)JsonSerializer.Deserialize<TSelf>(json).Value).Should().Be(value);
            JsonSerializer.Serialize(new Dictionary<TSelf, int> { [uninitialized] = 1 }).Should().NotBeNullOrEmpty();
            return;
        }

        var refusal = $"The value to write is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}";
        FluentActions.Invoking(() => JsonSerializer.Serialize(uninitialized))
            .Should().Throw<JsonException>().WithMessage(refusal);
        FluentActions.Invoking(() => JsonSerializer.Serialize<TSelf?>(uninitialized))
            .Should().Throw<JsonException>().WithMessage(refusal);
        FluentActions.Invoking(() => JsonSerializer.Serialize(new Dictionary<TSelf, int> { [uninitialized] = 1 }))
            .Should().Throw<JsonException>().WithMessage(refusal);
        JsonSerializer.Serialize<TSelf?>(null).Should().Be("null", "an optional value object holding nothing is no refused value");
        JsonSerializer.Serialize(Small).Should().NotBeNullOrEmpty("a created instance is written without a second look");
    }

    public override void CarriesTheCodeOfEveryJsonRefusal()
    {
        var json = JsonSerializer.Serialize(Small);
        var isText = json.StartsWith('"');
        var isNumber = !isText && json is not ("true" or "false");

        // A token of the wrong kind is not of the underlying type at all, and a null, which only a value object that
        // cannot be null is handed, says no value was supplied.
        ExpectRefusal(isText ? "42" : "\"not a value\"", ValueObjectErrorCodes.NotParsable);
        ExpectRefusal("{}", ValueObjectErrorCodes.NotParsable);
        ExpectRefusal("null", ValueObjectErrorCodes.Required);

        // A token of the right kind that the underlying type cannot hold. Where the reader reads it, the message is the
        // one System.Text.Json gives the reader's own refusal, as it was before the code was added. A double or a float
        // holds every JSON number, reading one beyond its range as an infinity, which the rules then judge.
        if (isNumber && typeof(TValue) != typeof(double) && typeof(TValue) != typeof(float))
        {
            ExpectRefusal("1e400", ValueObjectErrorCodes.NotParsable)
                .Message.Should().StartWith("The JSON value could not be converted to ").And.Contain(" Path: $.Value | LineNumber: ");
        }

        if (isNumber)
        {
            ExpectRefusal("\"not a number\"", ValueObjectErrorCodes.NotParsable, LenientNumbers)
                .Message.Should().Be($"The value could not be read as {typeof(TSelf).Name}.");
        }
        else if (typeof(TValue) != typeof(string) && typeof(TValue) != typeof(bool))
        {
            ExpectRefusal($"\"{Unparsable}\"", ValueObjectErrorCodes.NotParsable);
        }

        // A value or a key a rule rejects carries the code the parser reports for the same text.
        TSelf.TryParse(Refused, CultureInfo.InvariantCulture, out _, out var expected).Should().BeFalse();
        var refused = isText ? JsonSerializer.Serialize(Refused) : isNumber && IsJsonNumber(Refused) ? Refused : null;
        if (refused is not null)
        {
            var refusal = ExpectRefusal(refused, expected.ErrorCode!);
            if (expected.ErrorCode != ValueObjectErrorCodes.NotParsable)
            {
                refusal.Message.Should().StartWith($"The value is not a valid {typeof(TSelf).Name}: ");
            }
        }

        var key = JsonSerializer.Serialize(new Dictionary<string, int> { [Refused] = 1 });
        var keyRefusal = FluentActions.Invoking(() => JsonSerializer.Deserialize<Dictionary<TSelf, int>>(key))
            .Should().Throw<ValueObjectJsonException>().Which;
        keyRefusal.ErrorCode.Should().Be(expected.ErrorCode);
        keyRefusal.ValueObjectType.Should().Be<TSelf>();
        keyRefusal.Path.Should().StartWith("$");

        // A value to write that the type rejects, as a value and as a key.
        var uninitialized = default(TSelf);
        var value = uninitialized.Value;
        var validation = TSelf.Validate(in value);
        if (!validation.IsValid)
        {
            foreach (var write in new Action[]
            {
                () => JsonSerializer.Serialize(uninitialized),
                () => JsonSerializer.Serialize(new Dictionary<TSelf, int> { [uninitialized] = 1 }),
            })
            {
                var refusal = write.Should().Throw<ValueObjectJsonException>().Which;
                refusal.ErrorCode.Should().Be(validation.ErrorCode);
                refusal.ValueObjectType.Should().Be<TSelf>();
            }
        }
    }

    protected static TDelegate Method<TDelegate>(string name, params Type[] parameters)
        where TDelegate : Delegate
        => typeof(TSelf).GetMethod(name, BindingFlags.Public | BindingFlags.Static, parameters)!.CreateDelegate<TDelegate>();

    // Through IParsable<T> on purpose: on a type parameter that is also ISpanParsable<T>, C# removes the string
    // overloads of the base interface from overload resolution, and TSelf.Parse(text, null) binds to the span one.
    private static T ParseThroughIParsable<T>(string text)
        where T : IParsable<T>
        => T.Parse(text, null);

    private static bool TryParseThroughIParsable<T>(string? text, out T result)
        where T : IParsable<T>
        => T.TryParse(text, null, out result!);

    private static bool IsDefault(TSelf value) => value.IsDefault;

    /// <summary>
    /// Reads a value object from a JSON value inside an object, and asserts the refusal carries the type, the code,
    /// where <see cref="ValueObjectErrors.TryGetCode"/> reads it, and the path of the member.
    /// </summary>
    private static ValueObjectJsonException ExpectRefusal(string value, string code, JsonSerializerOptions? options = null)
    {
        var refusal = FluentActions.Invoking(() => JsonSerializer.Deserialize<Holder>($"{{\"Value\":{value}}}", options))
            .Should().Throw<ValueObjectJsonException>("{0} is refused", value).Which;

        refusal.ValueObjectType.Should().Be<TSelf>();
        refusal.ErrorCode.Should().Be(code, "{0} is refused with {1}", value, code);
        refusal.Path.Should().Be("$.Value");
        refusal.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(code);
        ValueObjectErrors.TryGetCode(refusal, out var read).Should().BeTrue();
        read.Should().Be(code);

        return refusal;
    }

    private static bool IsJsonNumber(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Number;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>An object holding the value object under test, so that a refusal has a member to name.</summary>
    /// <param name="Value">The value object.</param>
    private sealed record Holder(TSelf Value);

    private static Func<TSelf, TSelf, bool> Operator(string name)
        => Method<Func<TSelf, TSelf, bool>>(name, typeof(TSelf), typeof(TSelf));
}

/// <summary>An object holding a value object as a property Newtonsoft.Json sets, so that a refusal has a member to name.</summary>
/// <typeparam name="TSelf">The value object.</typeparam>
public sealed class NewtonsoftHolder<TSelf>
    where TSelf : struct
{
    /// <summary>Gets or sets the value object.</summary>
    public TSelf Value { get; set; }
}
