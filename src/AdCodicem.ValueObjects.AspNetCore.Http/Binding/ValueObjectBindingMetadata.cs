using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AdCodicem.ValueObjects.AspNetCore.Http.Binding;

/// <summary>
/// Marks an endpoint covered by
/// <see cref="ValueObjectEndpointConventionBuilderExtensions.WithValueObjectProblemDetails"/>, and holds the value
/// objects it binds from text, for the filter and for the exception handler, which finds the endpoint in the feature the
/// exception handler middleware hands it.
/// </summary>
internal sealed class ValueObjectBindingMetadata
{
    /// <summary>The value objects the endpoint binds from text, in the order the binder describes them.</summary>
    private ValueObjectParameter[] _parameters = [];

    /// <summary>
    /// Whether the binder of the endpoint refuses no empty text, as the Request Delegate Generator does: it binds empty
    /// query text without parsing it, and takes an empty header for an absent one.
    /// </summary>
    private bool _skipsEmptyText;

    /// <summary>Records what the filter factory found, once the binder has described the parameters.</summary>
    /// <param name="parameters">The value objects bound from text.</param>
    /// <param name="skipsEmptyText">Whether the binder refuses no empty text.</param>
    public void Describe(ValueObjectParameter[] parameters, bool skipsEmptyText)
    {
        _parameters = parameters;
        _skipsEmptyText = skipsEmptyText;
    }

    /// <summary>Parses the text of each value object again, and adds the refusals to the list.</summary>
    /// <param name="request">The request that failed to bind.</param>
    /// <param name="routeValues">Its route values, which the exception handler middleware takes off the request.</param>
    /// <param name="refusals">The refusals of the request.</param>
    public void Collect(HttpRequest request, RouteValueDictionary? routeValues, ValueObjectRefusals refusals)
    {
        foreach (var parameter in _parameters)
        {
            parameter.Collect(request, routeValues, _skipsEmptyText, refusals);
        }
    }
}
