using System;
using System.Collections.Generic;
using System.Linq;

namespace AdCodicem.ValueObjects.Generators.Model;

/// <summary>
/// A bound of a value object the generator can read: its value, and its text as the message of a value out of range
/// quotes it.
/// </summary>
/// <param name="Value">The bound, in the form <c>LiteralFactory</c> compares a value of the underlying type in.</param>
/// <param name="Text">The bound as the generated message quotes it.</param>
internal readonly record struct DeclaredBound(IComparable Value, string Text);

/// <summary>
/// The rules of a value object the generator can evaluate on its own, which the values the author declares on the
/// type — its example, its known values — are held to before the type ever runs.
/// </summary>
/// <remarks>
/// <para>
/// They are checked in the order the generated <c>Validate</c> checks them, and a refusal carries its code and its
/// message, so that what the build reports is what the type would answer at run time. Only the first rule broken is
/// reported, as validation is fail-fast.
/// </para>
/// <para>
/// What runs only at run time is left out: a pattern, which would run the author's regular expression inside the
/// compiler, a validator hook, a bound computed by its hook rather than returned as a constant, and every rule of a type
/// that normalizes its value first, which may turn a value refused here into one it accepts.
/// </para>
/// </remarks>
internal sealed class DeclaredRules
{
    /// <summary>Gets the underlying type.</summary>
    public required UnderlyingType Underlying { get; init; }

    /// <summary>Gets a value indicating whether a string value object accepts the empty string.</summary>
    public bool AllowEmpty { get; init; }

    /// <summary>Gets the minimum length of a string value object, or -1.</summary>
    public int MinLength { get; init; } = -1;

    /// <summary>Gets the maximum length of a string value object, or -1.</summary>
    public int MaxLength { get; init; } = -1;

    /// <summary>Gets the inclusive lower bound, when the generator can read it.</summary>
    public DeclaredBound? Minimum { get; init; }

    /// <summary>Gets the inclusive upper bound, when the generator can read it.</summary>
    public DeclaredBound? Maximum { get; init; }

    /// <summary>Gets the known values of a closed value set, or <see langword="null"/> for an open one.</summary>
    public IReadOnlyList<IComparable>? ClosedSet { get; init; }

    /// <summary>Gets the name of the <see cref="StringComparison"/> member a string value object compares with.</summary>
    public string ComparisonName { get; init; } = nameof(StringComparison.Ordinal);

    /// <summary>
    /// Says which rule of the type refuses a value, if one does.
    /// </summary>
    /// <param name="value">The value, in the form <c>LiteralFactory</c> compares a value of the underlying type in.</param>
    /// <returns>The code and the message of the first rule the value breaks, or <see langword="null"/>.</returns>
    public (string Code, string Message)? Refuse(IComparable value)
    {
        if (value is string text)
        {
            if (!AllowEmpty && text.Length == 0)
            {
                return ("value_object.required", "The value must not be empty.");
            }

            if (MinLength >= 0 && text.Length < MinLength)
            {
                return ("value_object.too_short", $"The value must be at least {MinLength} characters long.");
            }

            if (MaxLength >= 0 && text.Length > MaxLength)
            {
                return ("value_object.too_long", $"The value must be at most {MaxLength} characters long.");
            }
        }

        if (Minimum is { } minimum && value.CompareTo(minimum.Value) < 0)
        {
            return ("value_object.out_of_range", $"The value must be greater than or equal to {minimum.Text}.");
        }

        if (Maximum is { } maximum && value.CompareTo(maximum.Value) > 0)
        {
            return ("value_object.out_of_range", $"The value must be less than or equal to {maximum.Text}.");
        }

        if (ClosedSet is { } known && IsKnown(value, known) == false)
        {
            return ("value_object.not_a_known_value", "The value is not one of the accepted values.");
        }

        return null;
    }

    /// <summary>
    /// Says whether a value is one of the known values of a closed set, as the generated set compares it.
    /// </summary>
    /// <remarks>
    /// A string compares under the type's comparison. The current culture is the one the application runs in, which
    /// the build machine cannot stand for: there, only text equal to a known value is known for sure, and any other is
    /// left to run time.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="known">The known values.</param>
    /// <returns>Whether it is known, or <see langword="null"/> when that cannot be told at compile time.</returns>
    private bool? IsKnown(IComparable value, IReadOnlyList<IComparable> known)
    {
        if (value is not string text)
        {
            return known.Any(candidate => value.CompareTo(candidate) == 0);
        }

        var comparer = ComparisonName switch
        {
            nameof(StringComparison.Ordinal) => StringComparer.Ordinal,
            nameof(StringComparison.OrdinalIgnoreCase) => StringComparer.OrdinalIgnoreCase,
            nameof(StringComparison.InvariantCulture) => StringComparer.InvariantCulture,
            nameof(StringComparison.InvariantCultureIgnoreCase) => StringComparer.InvariantCultureIgnoreCase,
            _ => null,
        };

        var found = known.Any(candidate => (comparer ?? StringComparer.Ordinal).Equals(text, (string)candidate));
        return found || comparer is not null ? found : null;
    }
}
