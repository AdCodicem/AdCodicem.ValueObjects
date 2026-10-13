using HotChocolate.Types;

namespace AdCodicem.ValueObjects.Fixtures.GraphQL;

/// <summary>A line of an order, whose fields are value objects.</summary>
/// <param name="Sku">The product.</param>
/// <param name="Count">How many.</param>
public sealed record Line(Sku Sku, ItemCount Count);

/// <summary>The queries HotChocolate's source generator binds at compile time.</summary>
[QueryType]
public static partial class CatalogQueries
{
    /// <summary>Echoes a line.</summary>
    /// <param name="sku">The product.</param>
    /// <param name="count">How many.</param>
    /// <returns>The line.</returns>
    public static Line Line(Sku sku, ItemCount count) => new(sku, count);

    /// <summary>Echoes a list of products.</summary>
    /// <param name="skus">The products.</param>
    /// <returns>The products.</returns>
    public static IReadOnlyList<Sku> Skus(IReadOnlyList<Sku> skus) => skus;

    /// <summary>Echoes a product that may be missing.</summary>
    /// <param name="sku">The product.</param>
    /// <returns>The product.</returns>
    public static Sku? Find(Sku? sku) => sku;
}
