using System.Globalization;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// Probes one entity identifier through the visitor its descriptor accepts, which hands over its type, so that minting
/// it and reading it back are typed code the AOT compiler sees whole, reached through the constraint as an integration
/// that finds identifiers in <see cref="EntityIdRegistry"/> reaches them.
/// </summary>
internal sealed class IdentifierProbe : IEntityIdVisitor<string>
{
    public static readonly IdentifierProbe Instance = new();

    public string Visit<TId>()
        where TId : struct, IEntityId<TId>
    {
        var minted = TId.New();
        var readBack = TId.TryParse(minted.Value, CultureInfo.InvariantCulture, out var read, out _) && read.Equals(minted);

        return $"{Names.Of(typeof(TId))}, prefix {TId.Prefix}: minted {minted.Value}, {minted.Value.Length} characters of {TId.Length}, read back equal {readBack}";
    }
}
