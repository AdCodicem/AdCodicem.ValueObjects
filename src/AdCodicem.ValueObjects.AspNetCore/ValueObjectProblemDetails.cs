using Microsoft.AspNetCore.Http;

namespace AdCodicem.ValueObjects.AspNetCore;

/// <summary>
/// Carries the violated rule from the model binder to the problem details response.
/// </summary>
/// <remarks>
/// <c>ModelStateDictionary</c> only holds a message, and a message is not something a client can branch on. The
/// stable error code rides alongside it on the request, and
/// <see cref="ValueObjectMvcExtensions.AddValueObjectProblemDetails"/> folds it into the RFC 9457 body as an
/// <c>errorCodes</c> extension.
/// </remarks>
public static class ValueObjectProblemDetails
{
    /// <summary>
    /// The extension member added to the problem details body, mapping each rejected member to its error code.
    /// </summary>
    public const string ExtensionName = "errorCodes";

    private static readonly object ItemsKey = new();

    /// <summary>
    /// Records the code of the rule a bound value violated.
    /// </summary>
    /// <param name="httpContext">Current request.</param>
    /// <param name="memberName">Name of the rejected model member.</param>
    /// <param name="errorCode">Stable code of the violated rule.</param>
    public static void RecordErrorCode(HttpContext? httpContext, string memberName, string errorCode)
    {
        if (httpContext is null)
        {
            return;
        }

        if (httpContext.Items.TryGetValue(ItemsKey, out var existing) && existing is Dictionary<string, string> codes)
        {
            codes[memberName] = errorCode;
            return;
        }

        httpContext.Items[ItemsKey] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [memberName] = errorCode,
        };
    }

    /// <summary>
    /// Gets the codes recorded for the current request.
    /// </summary>
    /// <param name="httpContext">Current request.</param>
    /// <returns>The recorded codes, or an empty dictionary.</returns>
    public static IReadOnlyDictionary<string, string> GetErrorCodes(HttpContext? httpContext)
        => httpContext?.Items.TryGetValue(ItemsKey, out var existing) == true && existing is Dictionary<string, string> codes
            ? codes
            : new Dictionary<string, string>(StringComparer.Ordinal);
}
