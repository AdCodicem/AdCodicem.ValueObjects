using System.ComponentModel;
using System.Globalization;
using AdCodicem.ValueObjects.Benchmarks.Domain;
using AdCodicem.ValueObjects.Metadata;
using BenchmarkDotNet.Order;

namespace AdCodicem.ValueObjects.Benchmarks;

/// <summary>
/// The three routes from text to a value object, and what each costs.
/// </summary>
/// <remarks>
/// The typed route is what domain code uses. The descriptor route is what model binding, FluentValidation and
/// Dapper use, because they only know a <see cref="Type"/> at run time. The TypeDescriptor route is how the
/// framework this one replaces did it, and is here to size the gap.
/// </remarks>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.Declared)]
public class CreationBenchmarks
{
    private const string Formatted = "FR76 3000 6000 0112 3456 7890 189";
    private const string Normalized = "FR7630006000011234567890189";

    private readonly ValueObjectDescriptor _descriptor = Resolve(typeof(Iban));
    private readonly ValueObjectDescriptor _closedSet = Resolve(typeof(CountryCode));
    private readonly TypeConverter _typeConverter = TypeDescriptor.GetConverter(typeof(Iban));

    /// <summary>Doing the work by hand, with no value object at all: the floor.</summary>
    /// <returns>The normalized text, or null when rejected.</returns>
    [Benchmark(Baseline = true, Description = "By hand, no value object")]
    public string? ByHand()
    {
        var normalized = Normalization.Strip(Formatted);

        return Normalization.HasValidCheckDigits(normalized) ? normalized : null;
    }

    /// <summary>The typed factory domain code calls.</summary>
    /// <returns>The value object.</returns>
    [Benchmark(Description = "Iban.Create")]
    public Iban Typed() => Iban.Create(Formatted);

    /// <summary>The typed non throwing factory.</summary>
    /// <returns>Whether the text was accepted.</returns>
    [Benchmark(Description = "Iban.TryCreate")]
    public bool TypedTry() => Iban.TryCreate(Formatted, out _);

    /// <summary>The typed parser, taking a span so no string need exist first.</summary>
    /// <returns>Whether the text was accepted.</returns>
    [Benchmark(Description = "Iban.TryParse(span)")]
    public bool TypedParse() => Iban.TryParse(Formatted.AsSpan(), CultureInfo.InvariantCulture, out _);

    /// <summary>The boxed route the integration packages take when they only know a type at run time.</summary>
    /// <returns>Whether the text was accepted.</returns>
    [Benchmark(Description = "Descriptor.TryParse (boxed)")]
    public bool Descriptor()
        => _descriptor.TryParse(Formatted, CultureInfo.InvariantCulture, out _, out _);

    /// <summary>How the previous framework converted text to a value object.</summary>
    /// <returns>The value object, boxed.</returns>
    [Benchmark(Description = "TypeDescriptor.ConvertFrom")]
    public object? ViaTypeConverter() => _typeConverter.ConvertFromInvariantString(Formatted);

    /// <summary>Creating from an already trusted value, as the EF Core read path does.</summary>
    /// <returns>The value object.</returns>
    [Benchmark(Description = "CreateUnchecked (EF read path)")]
    public Iban Unchecked() => Iban.CreateUnchecked(Normalized);

    /// <summary>Boxing a member of a closed set, which is pre-boxed and therefore shared.</summary>
    /// <returns>The shared box.</returns>
    [Benchmark(Description = "Closed set, boxed (shared)")]
    public object ClosedSetBoxed() => _closedSet.Create("FR");

    /// <summary>The same conversion on an open set, where every box is a fresh allocation.</summary>
    /// <returns>A freshly boxed value object.</returns>
    [Benchmark(Description = "Open set, boxed (allocates)")]
    public object OpenSetBoxed() => _descriptor.Create("FR7630006000011234567890189");

    private static ValueObjectDescriptor Resolve(Type type)
    {
        ValueObjectRegistry.TryGet(type, out var descriptor);

        return descriptor!;
    }
}
