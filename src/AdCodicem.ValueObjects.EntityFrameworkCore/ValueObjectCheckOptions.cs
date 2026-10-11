namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Chooses the rules <see cref="ValueObjectConventionExtensions.AddValueObjectCheckConstraints"/> writes as CHECK
/// constraints.
/// </summary>
public sealed class ValueObjectCheckOptions
{
    /// <summary>
    /// Gets or sets the rules written as CHECK constraints: lengths, bounds and closed value sets, unless one is removed.
    /// </summary>
    public ValueObjectChecks Rules { get; set; } =
        ValueObjectChecks.Lengths | ValueObjectChecks.Bounds | ValueObjectChecks.KnownValues;
}
