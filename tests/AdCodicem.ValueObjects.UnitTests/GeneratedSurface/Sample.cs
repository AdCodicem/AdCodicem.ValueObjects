using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    public abstract void ConvertsThroughItsTypeConverter();

    public abstract void RoundTripsAsADictionaryKey();

    public abstract void ParsesThroughEveryOverload();

    public abstract void ComparesThroughEveryOperator();

    public abstract void ExposesItsValueThroughEveryAccessor();

    public abstract void FormatsWithoutAProvider();

    public abstract void RefusesJsonOfTheWrongShape();

    public abstract void RefusesAGroupSeparatorInTheInvariantCulture();

    public abstract void OffersNoConstructorTakingTheValueAlone();

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

    private static Func<TSelf, TSelf, bool> Operator(string name)
        => Method<Func<TSelf, TSelf, bool>>(name, typeof(TSelf), typeof(TSelf));
}
