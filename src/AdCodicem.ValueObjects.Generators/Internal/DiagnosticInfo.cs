using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AdCodicem.ValueObjects.Generators.Internal;

/// <summary>
/// An equatable stand-in for <see cref="Diagnostic"/>, which cannot be cached by the incremental pipeline
/// because it holds on to symbols and syntax nodes.
/// </summary>
internal readonly record struct DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> Arguments)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params string[] arguments)
        => new(descriptor, LocationInfo.From(location), EquatableArray<string>.From(arguments));

    public Diagnostic ToDiagnostic()
        => Diagnostic.Create(Descriptor, Location?.ToLocation(), [.. Arguments]);
}

/// <summary>
/// An equatable stand-in for <see cref="Microsoft.CodeAnalysis.Location"/>.
/// </summary>
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public static LocationInfo? From(Location? location)
        => location is null || location.SourceTree is null
            ? null
            : new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);

    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);
}
