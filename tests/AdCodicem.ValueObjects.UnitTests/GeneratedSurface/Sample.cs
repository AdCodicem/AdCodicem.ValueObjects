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
/// (docs/adr/0006-coverage-is-a-signal-not-a-goal.md). Each CI family of <c>.github/ci-families.json</c> declares
/// and implements its checks in a part of its own, <c>Sample.&lt;Family&gt;.cs</c>, which only its tests call.
/// </remarks>
public abstract partial class Sample
{
    private static readonly JsonSerializerOptions NumbersFromStrings =
        new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    /// <summary>The numeric types a value object may wrap, which the type converter of a numeric one converts.</summary>
    protected static readonly Type[] Numbers =
    [
        typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(Int128), typeof(UInt128), typeof(decimal), typeof(double), typeof(float),
    ];

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
public partial class Sample<TSelf, TValue> : Sample
    where TSelf : struct, IValueObject<TSelf, TValue>
{
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

    /// <summary>Gets the text the smaller instance is parsed from.</summary>
    private string Text { get; }

    /// <summary>Gets the text the smaller instance is formatted as, which is <see cref="Text"/> but for a hook.</summary>
    private string Written { get; }

    public override bool IsReal => typeof(TValue) == typeof(decimal) || typeof(TValue) == typeof(double) || typeof(TValue) == typeof(float);

    public override string ToString() => typeof(TSelf).Name;

    protected static TDelegate Method<TDelegate>(string name, params Type[] parameters)
        where TDelegate : Delegate
        => typeof(TSelf).GetMethod(name, BindingFlags.Public | BindingFlags.Static, parameters)!.CreateDelegate<TDelegate>();

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
}
