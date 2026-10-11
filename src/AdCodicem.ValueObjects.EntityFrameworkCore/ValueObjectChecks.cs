namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// The rules of a value object that <see cref="ValueObjectConventionExtensions.AddValueObjectCheckConstraints"/> writes as
/// CHECK constraints, combined as flags.
/// </summary>
/// <remarks>
/// Each rule is written only where the database refuses no value the value object accepts: a constraint stricter than the
/// type would fail a write the application validated. A rule only code checks, the checksum of an
/// <see cref="IValueObjectValidator{TValue}"/>, has no SQL form and stays in the application.
/// </remarks>
[Flags]
public enum ValueObjectChecks
{
    /// <summary>
    /// No rule.
    /// </summary>
    None = 0,

    /// <summary>
    /// The minimum length of text, <c>MinLength</c>, counted so that the database never counts fewer characters than .NET
    /// does. The maximum length stays the size of the column.
    /// </summary>
    Lengths = 1,

    /// <summary>
    /// The bounds of <see cref="IValueObjectMinimum{TValue}"/> and <see cref="IValueObjectMaximum{TValue}"/>, where the
    /// column orders values as .NET does.
    /// </summary>
    Bounds = 2,

    /// <summary>
    /// The known values of a closed value set, where every value the type accepts is stored as one of them.
    /// </summary>
    KnownValues = 4,
}
