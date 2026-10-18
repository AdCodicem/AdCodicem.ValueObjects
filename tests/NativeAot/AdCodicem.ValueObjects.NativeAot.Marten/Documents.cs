using System.Linq.Expressions;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Json;
using Marten;
using Marten.Linq;

namespace AdCodicem.ValueObjects.NativeAot.Documents;

/// <summary>A shop holding a value object of each family, a generic one and one written by hand.</summary>
public sealed class Shop
{
    public Guid Id { get; set; }

    public int Index { get; set; }

    public EmailAddress Email { get; set; }

    public EmailAddress? Backup { get; set; }

    public Quantity Quantity { get; set; }

    public Amount Amount { get; set; }

    public PageNumber Page { get; set; }

    public FileSize Size { get; set; }

    public Latitude Latitude { get; set; }

    public EffectiveDate Effective { get; set; }

    public OccurredAt At { get; set; }

    public Consent Consent { get; set; }

    public Grade Grade { get; set; }

    public DocumentStatus Status { get; set; }

    public DocumentNumber<PurchaseOrder> Number { get; set; }

    public HandWrittenCode Code { get; set; }
}

/// <summary>A document holding a value object nothing registered.</summary>
public sealed class Unregistered
{
    public Guid Id { get; set; }

    public UnregisteredCode Code { get; set; }
}

/// <summary>An invoice whose identity is a value object.</summary>
public sealed class Invoice
{
    public CustomerId Id { get; set; }

    public Amount Total { get; set; }
}

/// <summary>A single value object compared with a member: refused.</summary>
public sealed class ShopsAboveQuantity : ICompiledListQuery<Shop>
{
#pragma warning disable VO0048 // The shape this test pins: Marten refuses a single value object as a parameter.
    public Quantity Minimum { get; set; }
#pragma warning restore VO0048

    public Expression<Func<IMartenQueryable<Shop>, IEnumerable<Shop>>> QueryIs() => shops => shops.Where(x => x.Quantity > Minimum);
}

/// <summary>
/// The underlying value compared with an integer member, as C# compares a <c>short</c>: bound on each call, by Marten's own
/// finder of the integer member.
/// </summary>
public sealed class ShopsAboveQuantityValue : ICompiledListQuery<Shop>
{
    public int Minimum { get; set; }

    public Expression<Func<IMartenQueryable<Shop>, IEnumerable<Shop>>> QueryIs() => shops => shops.Where(x => x.Quantity.Value > Minimum);
}

/// <summary>An array of value objects: bound on each call.</summary>
public sealed class ShopsOnPages : ICompiledListQuery<Shop>
{
#pragma warning disable CA1819 // An array, as an application's query type holds one.
    public PageNumber[] Pages { get; set; } = [];
#pragma warning restore CA1819

    public Expression<Func<IMartenQueryable<Shop>, IEnumerable<Shop>>> QueryIs() => shops => shops.Where(x => Pages.Contains(x.Page));
}

/// <summary>The source-generated context the store serializes through.</summary>
[JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(Shop))]
[JsonSerializable(typeof(Unregistered))]
[JsonSerializable(typeof(Invoice))]
[JsonSerializable(typeof(List<DocumentStatus>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
internal sealed partial class MartenJsonContext : JsonSerializerContext;
