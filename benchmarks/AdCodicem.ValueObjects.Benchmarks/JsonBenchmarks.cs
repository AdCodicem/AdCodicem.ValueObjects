using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Benchmarks.Domain;
using AdCodicem.ValueObjects.Json;
using BenchmarkDotNet.Order;

namespace AdCodicem.ValueObjects.Benchmarks;

/// <summary>
/// What typing a payload with value objects costs against typing it with primitives.
/// </summary>
/// <remarks>
/// The value objects must serialize to exactly the same JSON as the primitives, so the raw rows are both the
/// baseline and the correctness statement: same bytes, and the question is only what the wrapper costs.
/// </remarks>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.Declared)]
public class JsonBenchmarks
{
    private static readonly JsonSerializerOptions Reflection = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions Raw = new(JsonSerializerDefaults.Web);

    private TypedPayload _typed;
    private RawPayload _raw = null!;
    private string _json = string.Empty;

    /// <summary>Builds the payloads and warms the metadata caches.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _typed = new TypedPayload
        {
            Id = CustomerId.Create(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff")),
            Iban = Iban.Create("FR7630006000011234567890189"),
            Balance = Amount.Create(1250.50m),
        };

        _raw = new RawPayload
        {
            Id = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"),
            Iban = "FR7630006000011234567890189",
            Balance = 1250.50m,
        };

        _json = JsonSerializer.Serialize(_typed, Reflection);

        // The two shapes must be indistinguishable on the wire, otherwise the comparison is meaningless.
        var rawJson = JsonSerializer.Serialize(_raw, Raw);
        if (!string.Equals(_json, rawJson, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Shapes differ: '{_json}' against '{rawJson}'.");
        }
    }

    /// <summary>The baseline: primitives.</summary>
    /// <returns>The JSON.</returns>
    [Benchmark(Baseline = true, Description = "Serialize raw primitives")]
    public string Serialize_Raw() => JsonSerializer.Serialize(_raw, Raw);

    /// <summary>Value objects through the converter the generator emitted onto the type.</summary>
    /// <returns>The JSON.</returns>
    [Benchmark(Description = "Serialize value objects")]
    public string Serialize_Typed() => JsonSerializer.Serialize(_typed, Reflection);

    /// <summary>Value objects through a source generated contract, the trimming friendly route.</summary>
    /// <returns>The JSON.</returns>
    [Benchmark(Description = "Serialize value objects, source generated")]
    public string Serialize_SourceGenerated()
        => JsonSerializer.Serialize(_typed, BenchmarkJsonContext.Default.TypedPayload);

    /// <summary>The baseline: primitives.</summary>
    /// <returns>The payload.</returns>
    [Benchmark(Description = "Deserialize raw primitives")]
    public RawPayload Deserialize_Raw() => JsonSerializer.Deserialize<RawPayload>(_json, Raw)!;

    /// <summary>Value objects, each of which normalizes and validates on the way in.</summary>
    /// <returns>The payload.</returns>
    [Benchmark(Description = "Deserialize value objects")]
    public TypedPayload Deserialize_Typed() => JsonSerializer.Deserialize<TypedPayload>(_json, Reflection)!;

    /// <summary>Value objects through a source generated contract.</summary>
    /// <returns>The payload.</returns>
    [Benchmark(Description = "Deserialize value objects, source generated")]
    public TypedPayload Deserialize_SourceGenerated()
        => JsonSerializer.Deserialize(_json, BenchmarkJsonContext.Default.TypedPayload)!;
}

/// <summary>A payload typed with value objects.</summary>
public struct TypedPayload
{
    /// <summary>Gets or sets the customer identifier.</summary>
    public CustomerId Id { get; set; }

    /// <summary>Gets or sets the account number.</summary>
    public Iban Iban { get; set; }

    /// <summary>Gets or sets the balance.</summary>
    public Amount Balance { get; set; }
}

/// <summary>The same payload typed with primitives.</summary>
public sealed class RawPayload
{
    /// <summary>Gets or sets the customer identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the account number.</summary>
    public string Iban { get; set; } = string.Empty;

    /// <summary>Gets or sets the balance.</summary>
    public decimal Balance { get; set; }
}

/// <summary>
/// The source generated contract, which needs the factory named explicitly.
/// </summary>
/// <remarks>
/// The System.Text.Json generator cannot see the <c>[JsonConverter]</c> our generator writes onto the value
/// object, because source generators never observe each other's output. Naming the factory here is what closes
/// that gap, and is exactly why the .Json package exists.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(TypedPayload))]
public partial class BenchmarkJsonContext : JsonSerializerContext;
