using AdCodicem.ValueObjects.Identifiers;
using Microsoft.Extensions.Compliance.Classification;

namespace AdCodicem.ValueObjects.UnitTests.Domain;

// Value objects their author classifies as personal data, as an application does with its own taxonomy: the exceptions
// of the generated Create and Parse then leave the rejected value out of AttemptedValue.

/// <summary>
/// The classifications of this domain's data.
/// </summary>
public static class Taxonomy
{
    /// <summary>Gets the classification of data that identifies a person.</summary>
    public static DataClassification Personal => new("UnitTests", nameof(Personal));
}

/// <summary>
/// Classifies a value object as personal data.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class PersonalDataAttribute : DataClassificationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalDataAttribute"/> class.
    /// </summary>
    public PersonalDataAttribute()
        : base(Taxonomy.Personal)
    {
    }
}

/// <summary>
/// The number of a passport, in upper case.
/// </summary>
[PersonalData]
[ValueObject<string>(MinLength = 6, MaxLength = 9, ExplicitConversionFromValue = true)]
public readonly partial struct PassportNumber : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}

/// <summary>
/// A yearly salary, never negative.
/// </summary>
[PersonalData]
[ValueObject<decimal>(Arithmetic = true)]
public readonly partial struct Salary : IValueObjectMinimum<decimal>
{
    public static decimal Minimum => 0m;
}

/// <summary>
/// The public identifier of a patient, which is personal data as much as the patient's name.
/// </summary>
[PersonalData]
[EntityId("pat")]
public readonly partial struct PatientId;

/// <summary>
/// The name a person goes by with one kind of owner, which is personal data whatever the owner.
/// </summary>
/// <typeparam name="TOwner">The kind of owner the person goes by this name with.</typeparam>
[PersonalData]
[ValueObject<string>(MinLength = 2, MaxLength = 32)]
public readonly partial struct Pseudonym<TOwner>
    where TOwner : class;

/// <summary>
/// The name a team goes by with one kind of owner, which its author says is not sensitive.
/// </summary>
/// <typeparam name="TOwner">The kind of owner the team goes by this name with.</typeparam>
[NoDataClassification]
[ValueObject<string>(MinLength = 2, MaxLength = 32)]
public readonly partial struct TeamName<TOwner>
    where TOwner : class;
