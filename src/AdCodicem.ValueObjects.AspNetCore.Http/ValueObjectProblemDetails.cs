using Microsoft.AspNetCore.Http;

namespace AdCodicem.ValueObjects.AspNetCore;

/// <summary>
/// Names the problem details member that maps each rejected member to the code of the rule it broke, and carries the
/// violated rule from the MVC model binder to the problem details response.
/// </summary>
/// <remarks>
/// <para>
/// <c>ModelStateDictionary</c> only holds a message, and a message is not something a client can branch on. The
/// stable error code rides alongside it on the request, and <c>AddValueObjectProblemDetails()</c> on
/// <c>ApiBehaviorOptions</c>, in <c>AdCodicem.ValueObjects.AspNetCore</c>, folds it into the RFC 9457 body as an
/// <c>errorCodes</c> extension. A minimal API endpoint covered by
/// <see cref="Http.ValueObjectEndpointConventionBuilderExtensions.WithValueObjectProblemDetails"/> writes the same
/// member, so both answer a refused value object with one shape.
/// </para>
/// <para>
/// The type lives in <c>AdCodicem.ValueObjects.AspNetCore.Http</c>, which every application on ASP.NET Core can
/// reference, native AOT included, and keeps its namespace: <c>AdCodicem.ValueObjects.AspNetCore</c> references that
/// package and forwards the type, so code compiled against either assembly finds it.
/// </para>
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
    /// <remarks>
    /// A member recorded again keeps its first code, the one beside the first of its errors: MVC binds every element of a
    /// query array under the array's name, and a JSON body may repeat a key, so a member can be refused more than once.
    /// A minimal API's problem details keep the first code the same way.
    /// </remarks>
    public static void RecordErrorCode(HttpContext? httpContext, string memberName, string errorCode)
    {
        if (httpContext is null)
        {
            return;
        }

        if (httpContext.Items.TryGetValue(ItemsKey, out var existing) && existing is Dictionary<string, string> codes)
        {
            codes.TryAdd(memberName, errorCode);
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
