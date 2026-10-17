using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.Grains;

/// <summary>A stock keeping unit: three letters, a dash and three digits, upper-cased.</summary>
[ValueObject<string>(MaxLength = 8)]
public readonly partial struct GrainSku : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    [GeneratedRegex(@"^[A-Z]{3}-\d{3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}

/// <summary>A quantity ordered, from 1 to 100: its default is refused.</summary>
[ValueObject<int>]
public readonly partial struct GrainQuantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    public static int Minimum => 1;

    public static int Maximum => 100;
}

/// <summary>An order's identifier.</summary>
[ValueObject<Guid>]
public readonly partial struct GrainOrderId;

/// <summary>An amount, zero or more: its default is accepted.</summary>
[ValueObject<decimal>]
public readonly partial struct GrainAmount : IValueObjectMinimum<decimal>
{
    public static decimal Minimum => 0m;
}

/// <summary>The day an order is due.</summary>
[ValueObject<DateOnly>]
public readonly partial struct GrainDueDate;

/// <summary>Whether an order is gift-wrapped.</summary>
[ValueObject<bool>]
public readonly partial struct GrainGiftWrap;

/// <summary>A parcel's weight.</summary>
[ValueObject<double>]
public readonly partial struct GrainWeight;

/// <summary>A code Orleans names by its alias wherever it writes the type.</summary>
[ValueObject<string>(MaxLength = 3)]
[Alias("fixtures.grains.aliased-code")]
public readonly partial struct AliasedCode;

/// <summary>Holds one value of any type, so that a test compares a value object's bytes with its underlying value's.</summary>
/// <typeparam name="T">The type of the value.</typeparam>
[GenerateSerializer]
public sealed class GrainHolder<T>
{
    [Id(0)]
    public T Value { get; set; } = default!;
}

/// <summary>Holds nothing: what a peer whose holder lacks the member writes.</summary>
[GenerateSerializer]
public sealed class GrainNothing;

/// <summary>Holds a value in a member typed <see cref="object"/>, which Orleans writes with its type.</summary>
[GenerateSerializer]
public sealed class GrainBox
{
    [Id(0)]
    public object? Value { get; set; }
}

/// <summary>An order of value objects, the twin of <see cref="PlainGrainOrder"/> member for member.</summary>
[GenerateSerializer]
public sealed class GrainOrder
{
    [Id(0)]
    public GrainOrderId Id { get; set; }

    [Id(1)]
    public GrainSku Sku { get; set; }

    [Id(2)]
    public GrainQuantity Quantity { get; set; }

    [Id(3)]
    public GrainQuantity? Discount { get; set; }

    [Id(4)]
    public List<GrainSku> Skus { get; set; } = [];

    [Id(5)]
    public Dictionary<GrainSku, GrainQuantity> Stock { get; set; } = [];

    [Id(6)]
    public GrainAmount Amount { get; set; }

    [Id(7)]
    public GrainDueDate Due { get; set; }

    [Id(8)]
    public GrainGiftWrap Gift { get; set; }

    [Id(9)]
    public GrainWeight Weight { get; set; }

    [Id(10)]
    public GrainOrderId[] Related { get; set; } = [];

    [Id(11)]
    public GrainSku? Optional { get; set; }
}

/// <summary>The order of <see cref="GrainOrder"/> over the primitives its value objects carry.</summary>
[GenerateSerializer]
public sealed class PlainGrainOrder
{
    [Id(0)]
    public Guid Id { get; set; }

    [Id(1)]
    public string? Sku { get; set; }

    [Id(2)]
    public int Quantity { get; set; }

    [Id(3)]
    public int? Discount { get; set; }

    [Id(4)]
    public List<string> Skus { get; set; } = [];

    [Id(5)]
    public Dictionary<string, int> Stock { get; set; } = [];

    [Id(6)]
    public decimal Amount { get; set; }

    [Id(7)]
    public DateOnly Due { get; set; }

    [Id(8)]
    public bool Gift { get; set; }

    [Id(9)]
    public double Weight { get; set; }

    [Id(10)]
    public Guid[] Related { get; set; } = [];

    [Id(11)]
    public string? Optional { get; set; }
}

/// <summary>A refusal of the application's own, derived from the library's, carrying a value it must not send.</summary>
/// <param name="message">The message.</param>
public sealed class GrainRefusal(string message)
    : ValueObjectException(message, typeof(GrainQuantity), "fixture.custom", attemptedValue: "secret");

/// <summary>A grain taking and returning value objects, and refusing some.</summary>
public interface IValueObjectGrain : IGrainWithGuidKey
{
    /// <summary>Returns what it is sent, whatever its type.</summary>
    Task<T> Echo<T>(T value);

    /// <summary>Returns what it is sent in an argument typed <see cref="object"/>.</summary>
    Task<object?> EchoObject(object? value);

    /// <summary>Adds a quantity to the grain's total, which it returns.</summary>
    Task<GrainQuantity> Add(GrainSku sku, GrainQuantity quantity);

    /// <summary>Returns the nullable quantity it is sent.</summary>
    Task<GrainQuantity?> Maybe(GrainQuantity? quantity);

    /// <summary>Describes the quantity it received: its value, whether it is the default, and the silo.</summary>
    Task<string> Accept(GrainQuantity quantity);

    /// <summary>Returns a quantity of 1000, created unchecked, which its rules refuse.</summary>
    Task<GrainQuantity> Forged();

    /// <summary>Creates a quantity from a value, throwing the <see cref="ValueObjectException"/> of a refused one.</summary>
    Task<string> CreateInside(int value);

    /// <summary>Throws a <see cref="GrainRefusal"/>.</summary>
    Task<string> ThrowSubclass();

    /// <summary>Throws a <see cref="ValueObjectException"/> for a <see cref="GrainQuantity"/>.</summary>
    Task<string> ThrowForQuantity();

    /// <summary>Names the silo the grain runs on.</summary>
    Task<string> Where();

    /// <summary>Sends a quantity to another grain's <see cref="Accept"/>.</summary>
    Task<string> Relay(Guid other, GrainQuantity quantity);

    /// <summary>Sends a quantity of 500, created unchecked, to another grain's <see cref="Accept"/>.</summary>
    Task<string> RelayForged(Guid other);

    /// <summary>Sends the default quantity, which its rules refuse, to another grain's <see cref="Accept"/>.</summary>
    Task<string> RelayDefault(Guid other);
}

/// <summary>The grain of <see cref="IValueObjectGrain"/>.</summary>
public sealed class ValueObjectGrain : Grain, IValueObjectGrain
{
    private int _total;

    public Task<T> Echo<T>(T value) => Task.FromResult(value);

    public Task<object?> EchoObject(object? value) => Task.FromResult(value);

    public Task<GrainQuantity> Add(GrainSku sku, GrainQuantity quantity)
    {
        _total += quantity.Value;
        return Task.FromResult(GrainQuantity.Create(_total));
    }

    public Task<GrainQuantity?> Maybe(GrainQuantity? quantity) => Task.FromResult(quantity);

    public Task<string> Accept(GrainQuantity quantity)
        => Task.FromResult($"accepted {quantity.Value} (IsDefault={((IValueObject<GrainQuantity, int>)quantity).IsDefault}) on {RuntimeIdentity}");

    public Task<GrainQuantity> Forged() => Task.FromResult(Unchecked.Of<GrainQuantity, int>(1000));

    public Task<string> CreateInside(int value) => Task.FromResult($"created {GrainQuantity.Create(value)}");

    public Task<string> ThrowSubclass() => throw new GrainRefusal("'GrainQuantity' refused it, the application's way.");

    public Task<string> ThrowForQuantity()
        => throw new ValueObjectException("'GrainQuantity' rejected the supplied value: test.", typeof(GrainQuantity), "value_object.test", "secret");

    public Task<string> Where() => Task.FromResult(RuntimeIdentity);

    public async Task<string> Relay(Guid other, GrainQuantity quantity)
        => $"{RuntimeIdentity} -> {await GrainFactory.GetGrain<IValueObjectGrain>(other).Accept(quantity)}";

    public async Task<string> RelayForged(Guid other)
        => $"{RuntimeIdentity} -> {await GrainFactory.GetGrain<IValueObjectGrain>(other).Accept(Unchecked.Of<GrainQuantity, int>(500))}";

#pragma warning disable VO0010 // the default instance, which its rules refuse, on purpose
    public async Task<string> RelayDefault(Guid other)
        => $"{RuntimeIdentity} -> {await GrainFactory.GetGrain<IValueObjectGrain>(other).Accept(default)}";
#pragma warning restore VO0010
}

/// <summary>Creates a value object its rules refuse, as a forged message or a looser version would hold one.</summary>
public static class Unchecked
{
    /// <summary>Creates the value object without validating its value.</summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The value object.</returns>
    public static TSelf Of<TSelf, TValue>(TValue value)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => TSelf.CreateUnchecked(value);
}
