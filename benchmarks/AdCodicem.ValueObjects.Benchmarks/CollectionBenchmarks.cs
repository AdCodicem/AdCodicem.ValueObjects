using AdCodicem.ValueObjects.Benchmarks.Domain;
using BenchmarkDotNet.Order;

namespace AdCodicem.ValueObjects.Benchmarks;

/// <summary>
/// Value objects used as dictionary keys and as sort keys, which is where a bad equality or comparison shows up.
/// </summary>
/// <remarks>
/// A value object that boxes on every lookup, or that falls back to the reflection based
/// <see cref="ValueType.Equals(object)"/>, is fine in a unit test and ruinous in a loop. These rows are the
/// check that neither happens.
/// </remarks>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.Declared)]
public class CollectionBenchmarks
{
    private const int Count = 10_000;

    private readonly Dictionary<string, int> _byString = new(StringComparer.Ordinal);
    private readonly Dictionary<Iban, int> _byValueObject = [];
    private readonly Dictionary<StructWrapper, int> _byStruct = [];
    private readonly Dictionary<ClassWrapper, int> _byClass = [];

    private string[] _keys = [];
    private Iban[] _valueObjects = [];
    private StructWrapper[] _structs = [];
    private ClassWrapper[] _classes = [];

    /// <summary>Fills every collection with the same keys.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _keys = new string[Count];
        _valueObjects = new Iban[Count];
        _structs = new StructWrapper[Count];
        _classes = new ClassWrapper[Count];

        for (var index = 0; index < Count; index++)
        {
            // Vary the account part and recompute the check digits, so every key is a genuinely valid IBAN.
            var text = MakeIban(index);
            _keys[index] = text;
            _valueObjects[index] = Iban.Create(text);
            _structs[index] = new StructWrapper(text);
            _classes[index] = new ClassWrapper(text);

            _byString[text] = index;
            _byValueObject[_valueObjects[index]] = index;
            _byStruct[_structs[index]] = index;
            _byClass[_classes[index]] = index;
        }
    }

    /// <summary>The baseline: a dictionary keyed by the underlying value.</summary>
    /// <returns>A checksum, so nothing is elided.</returns>
    [Benchmark(Baseline = true, Description = "Lookup by raw string")]
    public int Lookup_Raw()
    {
        var total = 0;
        foreach (var key in _keys)
        {
            total += _byString[key];
        }

        return total;
    }

    /// <summary>A dictionary keyed by the generated value object.</summary>
    /// <returns>A checksum, so nothing is elided.</returns>
    [Benchmark(Description = "Lookup by value object")]
    public int Lookup_ValueObject()
    {
        var total = 0;
        foreach (var key in _valueObjects)
        {
            total += _byValueObject[key];
        }

        return total;
    }

    /// <summary>A dictionary keyed by the hand written struct.</summary>
    /// <returns>A checksum, so nothing is elided.</returns>
    [Benchmark(Description = "Lookup by struct wrapper")]
    public int Lookup_Struct()
    {
        var total = 0;
        foreach (var key in _structs)
        {
            total += _byStruct[key];
        }

        return total;
    }

    /// <summary>A dictionary keyed by the hand written class.</summary>
    /// <returns>A checksum, so nothing is elided.</returns>
    [Benchmark(Description = "Lookup by class wrapper")]
    public int Lookup_Class()
    {
        var total = 0;
        foreach (var key in _classes)
        {
            total += _byClass[key];
        }

        return total;
    }

    /// <summary>Sorting the underlying values.</summary>
    /// <returns>The sorted array, so nothing is elided.</returns>
    [Benchmark(Description = "Sort raw strings")]
    public string[] Sort_Raw()
    {
        var items = _keys[..];
        Array.Sort(items, StringComparer.Ordinal);

        return items;
    }

    /// <summary>Sorting value objects, which must not box on every comparison.</summary>
    /// <returns>The sorted array, so nothing is elided.</returns>
    [Benchmark(Description = "Sort value objects")]
    public Iban[] Sort_ValueObject()
    {
        var items = _valueObjects[..];
        Array.Sort(items);

        return items;
    }

    /// <summary>Sorting the hand written classes, which pay a dereference per comparison.</summary>
    /// <returns>The sorted array, so nothing is elided.</returns>
    [Benchmark(Description = "Sort class wrappers")]
    public ClassWrapper[] Sort_Class()
    {
        var items = _classes[..];
        Array.Sort(items);

        return items;
    }

    private static string MakeIban(int index)
    {
        // Build the account part first, then solve for the two check digits that make MOD-97 come out at 1.
        var account = string.Create(null, $"3000600001{index:D13}");
        for (var candidate = 2; candidate <= 98; candidate++)
        {
            var attempt = string.Create(null, $"FR{candidate:D2}{account}");
            if (Normalization.HasValidCheckDigits(attempt))
            {
                return attempt;
            }
        }

        throw new InvalidOperationException($"No check digits solve index {index}.");
    }
}
