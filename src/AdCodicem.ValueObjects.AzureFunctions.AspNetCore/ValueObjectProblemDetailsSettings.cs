using AdCodicem.ValueObjects.AspNetCore;

namespace AdCodicem.ValueObjects.AzureFunctions.AspNetCore;

/// <summary>
/// The messages the middleware answers with: one instance per service collection, which the first registration adds and
/// the last registration naming messages sets.
/// </summary>
internal sealed class ValueObjectProblemDetailsSettings
{
    /// <summary>Gets or sets the messages, or <see langword="null"/> for the English ones of the rules.</summary>
    public ValueObjectMessageOptions? Messages { get; set; }
}
