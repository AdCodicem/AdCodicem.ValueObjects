using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Forms;

namespace AdCodicem.ValueObjects.Blazor;

/// <summary>
/// Reads the rule codes the inputs of value objects keep on their form's <see cref="EditContext"/>.
/// </summary>
/// <remarks>
/// An <see cref="EditContext"/> holds a field's refusal as a message alone. An <see cref="InputValueObject{TValue}"/> keeps
/// the code of the rule beside it, in <see cref="EditContext.Properties"/>, from the refusal until the next text it reads,
/// or until it is disposed: the field's equivalent of <see cref="ValueObjectErrors.ErrorCodeKey"/>, for a test, a
/// telemetry hook or a component that styles the field by rule rather than by its English message.
/// </remarks>
public static class ValueObjectEditContextExtensions
{
    /// <summary>The key of the codes in <see cref="EditContext.Properties"/>, which nothing else can store under.</summary>
    private static readonly object CodesKey = new();

    /// <summary>
    /// Gets the code of the rule that refused the text of a field, while its input shows the refusal.
    /// </summary>
    /// <param name="editContext">The form's edit context.</param>
    /// <param name="field">The field.</param>
    /// <param name="code">The code: one of <see cref="ValueObjectErrorCodes"/>, or a code of the application's own rule.</param>
    /// <returns><see langword="true"/> when the field's input shows text its value object refused.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="editContext"/> is <see langword="null"/>.</exception>
    public static bool TryGetValueObjectErrorCode(this EditContext editContext, in FieldIdentifier field, [NotNullWhen(true)] out string? code)
    {
        ArgumentNullException.ThrowIfNull(editContext);

        if (editContext.Properties.TryGetValue(CodesKey, out var codes))
        {
            return ((Dictionary<FieldIdentifier, string>)codes).TryGetValue(field, out code);
        }

        code = null;
        return false;
    }

    /// <summary>Sets or clears the code of a field.</summary>
    /// <param name="editContext">The form's edit context, or <see langword="null"/> for an input outside a form.</param>
    /// <param name="field">The field.</param>
    /// <param name="code">The code, or <see langword="null"/> to clear it.</param>
    internal static void SetCode(EditContext? editContext, in FieldIdentifier field, string? code)
    {
        if (editContext is null)
        {
            return;
        }

        if (editContext.Properties.TryGetValue(CodesKey, out var value))
        {
            var codes = (Dictionary<FieldIdentifier, string>)value;
            if (code is null)
            {
                codes.Remove(field);
            }
            else
            {
                codes[field] = code;
            }
        }
        else if (code is not null)
        {
            editContext.Properties[CodesKey] = new Dictionary<FieldIdentifier, string> { [field] = code };
        }
    }
}
