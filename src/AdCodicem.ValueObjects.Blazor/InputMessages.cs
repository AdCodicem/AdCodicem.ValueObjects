using System.Globalization;
using System.Text;

namespace AdCodicem.ValueObjects.Blazor;

/// <summary>The messages of a refusal whose rule gives none to show.</summary>
internal static class InputMessages
{
    /// <summary><c>[Required]</c>'s message, which <c>[NotDefault]</c> refuses with too, for a field emptied.</summary>
    private static readonly CompositeFormat Required = CompositeFormat.Parse("The {0} field is required.");

    /// <summary>DataAnnotations' default message, for a rule that gives an empty one, as <c>[ParsesAs]</c> shows it.</summary>
    private static readonly CompositeFormat Invalid = CompositeFormat.Parse("The field {0} is invalid.");

    /// <summary>Gets the message an input shows for a refusal, in the current culture.</summary>
    /// <param name="message">
    /// The message of the rule, empty when it gave none, or <see langword="null"/> for empty text its type refuses.
    /// </param>
    /// <param name="member">The name quoted as <c>{0}</c>.</param>
    /// <returns>The message.</returns>
    public static string For(string? message, string member) => message switch
    {
        null => string.Format(CultureInfo.CurrentCulture, Required, member),
        "" => string.Format(CultureInfo.CurrentCulture, Invalid, member),
        _ => message,
    };
}
