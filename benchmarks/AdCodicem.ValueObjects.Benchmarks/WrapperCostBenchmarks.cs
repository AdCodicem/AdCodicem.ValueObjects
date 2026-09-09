using AdCodicem.ValueObjects.Benchmarks.Domain;
using BenchmarkDotNet.Order;

namespace AdCodicem.ValueObjects.Benchmarks;

/// <summary>
/// What a value object wrapper costs over the bare underlying value, and what the type kind costs.
/// </summary>
/// <remarks>
/// The comparison that matters for a string backed value object is not "value object versus nothing", it is
/// "struct versus class". <see cref="StructWrapper"/> and <see cref="ClassWrapper"/> are byte for byte the same
/// code apart from that one keyword, so the difference between their rows is the decision itself.
/// </remarks>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.Declared)]
public class WrapperCostBenchmarks
{
    private const string Text = "FR7630006000011234567890189";

    private StructWrapper[] _structs = [];
    private ClassWrapper[] _classes = [];
    private string[] _strings = [];

    /// <summary>Gets or sets the number of instances held at once.</summary>
    [Params(1_000, 100_000)]
    public int Count { get; set; }

    /// <summary>Builds the collections the read benchmarks walk.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _strings = new string[Count];
        _structs = new StructWrapper[Count];
        _classes = new ClassWrapper[Count];

        for (var index = 0; index < Count; index++)
        {
            var text = string.Create(null, $"FR76300060000112345678{index:D5}");
            _strings[index] = text;
            _structs[index] = new StructWrapper(text);
            _classes[index] = new ClassWrapper(text);
        }
    }

    /// <summary>The baseline: the underlying value, unwrapped.</summary>
    /// <returns>The array, so nothing is elided.</returns>
    [Benchmark(Baseline = true, Description = "Hold N raw strings")]
    public string[] Hold_Raw()
    {
        var items = new string[Count];
        for (var index = 0; index < Count; index++)
        {
            items[index] = _strings[index];
        }

        return items;
    }

    /// <summary>One allocation for the array; the wrappers live inside it.</summary>
    /// <returns>The array, so nothing is elided.</returns>
    [Benchmark(Description = "Hold N struct wrappers")]
    public StructWrapper[] Hold_Struct()
    {
        var items = new StructWrapper[Count];
        for (var index = 0; index < Count; index++)
        {
            items[index] = new StructWrapper(_strings[index]);
        }

        return items;
    }

    /// <summary>One allocation for the array, plus one per element.</summary>
    /// <returns>The array, so nothing is elided.</returns>
    [Benchmark(Description = "Hold N class wrappers")]
    public ClassWrapper[] Hold_Class()
    {
        var items = new ClassWrapper[Count];
        for (var index = 0; index < Count; index++)
        {
            items[index] = new ClassWrapper(_strings[index]);
        }

        return items;
    }

    /// <summary>Reading through the wrapper, which for a struct is a field access on a local.</summary>
    /// <returns>A checksum, so nothing is elided.</returns>
    [Benchmark(Description = "Read N struct wrappers")]
    public int Read_Struct()
    {
        var total = 0;
        foreach (var item in _structs)
        {
            total += item.Value.Length;
        }

        return total;
    }

    /// <summary>Reading through the wrapper, which for a class is a pointer dereference.</summary>
    /// <returns>A checksum, so nothing is elided.</returns>
    [Benchmark(Description = "Read N class wrappers")]
    public int Read_Class()
    {
        var total = 0;
        foreach (var item in _classes)
        {
            total += item.Value.Length;
        }

        return total;
    }

    /// <summary>Where a struct gives its advantage back: crossing a non generic boundary boxes it.</summary>
    /// <returns>A checksum, so nothing is elided.</returns>
    [Benchmark(Description = "Box N struct wrappers")]
    public int Box_Struct()
    {
        var total = 0;
        foreach (var item in _structs)
        {
            total += Identity(item) is StructWrapper unboxed ? unboxed.Value.Length : 0;
        }

        return total;
    }

    /// <summary>A class crossing the same boundary costs nothing, because it is already a reference.</summary>
    /// <returns>A checksum, so nothing is elided.</returns>
    [Benchmark(Description = "Box N class wrappers")]
    public int Box_Class()
    {
        var total = 0;
        foreach (var item in _classes)
        {
            total += Identity(item) is ClassWrapper unboxed ? unboxed.Value.Length : 0;
        }

        return total;
    }

    private static object Identity(object value) => value;
}
