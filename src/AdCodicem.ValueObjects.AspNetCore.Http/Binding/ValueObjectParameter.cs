using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AdCodicem.ValueObjects.AspNetCore.Http.Binding;

/// <summary>Where the binder reads the text of a value object.</summary>
internal enum ValueObjectParameterSource
{
    /// <summary>A route value.</summary>
    Route,

    /// <summary>The query string.</summary>
    Query,

    /// <summary>A header.</summary>
    Header,
}

/// <summary>
/// A value object an endpoint binds from text, with what it takes to read that text again and say which rule refused it.
/// </summary>
/// <param name="name">The name it binds from, which the refusal is listed under.</param>
/// <param name="source">Where the binder reads it.</param>
/// <param name="isOptional">Whether the binder accepts its absence.</param>
/// <param name="isArray">Whether it is an array, bound from every value of the query string under its name.</param>
/// <param name="skipsEmptyElements">
/// Whether an empty element of the array binds as <see langword="null"/>, as it does for an array of nullable value
/// objects under the reflection-based binding.
/// </param>
/// <param name="check">The check of its value object, closed over its type.</param>
internal sealed class ValueObjectParameter(
    string name,
    ValueObjectParameterSource source,
    bool isOptional,
    bool isArray,
    bool skipsEmptyElements,
    ValueObjectTextCheck check)
{
    /// <summary>Reads the text again, and adds the refusal of the value object, if any, to the list.</summary>
    /// <param name="request">The request that failed to bind.</param>
    /// <param name="routeValues">Its route values.</param>
    /// <param name="skipsEmptyText">
    /// Whether the binder refuses no empty text, as the Request Delegate Generator does: it takes an empty header for an
    /// absent one, and binds empty query text without a refusal, <see langword="null"/> to a nullable value object and the
    /// default instance to one that cannot be <see langword="null"/>.
    /// </param>
    /// <param name="refusals">The refusals of the request.</param>
    public void Collect(HttpRequest request, RouteValueDictionary? routeValues, bool skipsEmptyText, ValueObjectRefusals refusals)
    {
        if (isArray)
        {
            foreach (var element in request.Query[name])
            {
                if (!string.IsNullOrEmpty(element) || !(skipsEmptyElements || skipsEmptyText))
                {
                    check.Check(element, name, refusals);
                }
            }

            return;
        }

        var text = source switch
        {
            ValueObjectParameterSource.Route => routeValues?[name] as string,
            ValueObjectParameterSource.Query => (string?)request.Query[name],
            _ => (string?)request.Headers[name],
        };

        if (text is null || (text.Length == 0 && skipsEmptyText && source == ValueObjectParameterSource.Header))
        {
            if (!isOptional)
            {
                refusals.AddRequired(name);
            }
        }
        else if (text.Length > 0 || !skipsEmptyText)
        {
            check.Check(text, name, refusals);
        }
    }
}
