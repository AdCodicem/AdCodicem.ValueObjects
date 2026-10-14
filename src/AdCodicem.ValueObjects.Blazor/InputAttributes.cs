using System.Globalization;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Blazor;

/// <summary>The HTML attributes an input takes from the rules of a value object.</summary>
internal static class InputAttributes
{
    /// <summary>
    /// Gets the <c>maxlength</c>, <c>minlength</c> and <c>pattern</c> a schema gives, the pattern only when a browser reads
    /// it as .NET does.
    /// </summary>
    /// <param name="schema">The rules of the value object.</param>
    /// <returns>The attributes, in that order.</returns>
    public static IReadOnlyDictionary<string, object> From(ValueObjectSchema schema)
    {
        var attributes = new Dictionary<string, object>(StringComparer.Ordinal);
        if (schema.MaxLength is { } maximum)
        {
            attributes["maxlength"] = maximum.ToString(CultureInfo.InvariantCulture);
        }

        if (schema.MinLength is { } minimum)
        {
            attributes["minlength"] = minimum.ToString(CultureInfo.InvariantCulture);
        }

        if (schema.Pattern is { } pattern && EcmaScriptPattern.TryWrite(pattern, out var written))
        {
            attributes["pattern"] = written;
        }

        return attributes;
    }
}
