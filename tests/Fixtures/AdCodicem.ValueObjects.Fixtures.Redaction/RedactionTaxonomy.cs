using Microsoft.Extensions.Compliance.Classification;

namespace AdCodicem.ValueObjects.Fixtures.Redaction;

/// <summary>
/// The classification of the fixture's personal data, under names of its own: the unit suite imports its domain, whose
/// <c>Taxonomy</c> and <c>PersonalDataAttribute</c> a test of this fixture would otherwise meet twice.
/// </summary>
public static class RedactionTaxonomy
{
    /// <summary>Gets the classification of a way to contact a person.</summary>
    public static DataClassification Contact => new("Fixtures", nameof(Contact));
}

/// <summary>
/// Classifies a value, a parameter or a property as a way to contact a person: parameterless, and valid wherever
/// <see cref="DataClassificationAttribute"/> is, as Microsoft.Gen.Logging needs it.
/// </summary>
public sealed class ContactDataAttribute : DataClassificationAttribute
{
    /// <summary>Initializes a new instance of the <see cref="ContactDataAttribute"/> class.</summary>
    public ContactDataAttribute()
        : base(RedactionTaxonomy.Contact)
    {
    }
}
