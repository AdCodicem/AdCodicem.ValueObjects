using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace AdCodicem.ValueObjects.AspNetCore.Http.Binding;

/// <summary>
/// Answers the exception a minimal API throws for a refused value object, with <c>ThrowOnBadRequest</c> on, with the
/// problem details the filter writes.
/// </summary>
/// <remarks>
/// <para>
/// The exception handler middleware takes the endpoint and its route values off the request before it calls the
/// handlers, and hands them over in <see cref="IExceptionHandlerFeature"/>: the endpoint's marker, which the filter
/// factory filled when the endpoint was built, says which value objects to parse again.
/// </para>
/// <para>
/// The binder throws an exception wrapping another when it cannot read the body or a form, and one wrapping nothing for
/// a route, query or header value, or for a body that is missing. A wrapped exception is answered only when it carries
/// the code of a value object the serializer refused: a body the serializer cannot read at all, a form, an anti-forgery
/// token, keep the framework's answer, as they do with <c>ThrowOnBadRequest</c> off, where no filter runs for them.
/// </para>
/// </remarks>
internal sealed class ValueObjectBadRequestExceptionHandler : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest }
            || httpContext.Features.Get<IExceptionHandlerFeature>() is not { Endpoint: { } endpoint } feature
            || endpoint.Metadata.GetMetadata<ValueObjectBindingMetadata>() is not { } metadata)
        {
            return false;
        }

        var refusals = new ValueObjectRefusals();

        // A body the serializer refused: the exception it threw is wrapped, and carries the code, never the value. Its
        // message is the converter's; the framework's own message names the refused text, and is not copied. A body or a
        // form the binder could not read for another reason is not the package's to explain.
        if (exception.InnerException is not null)
        {
            if (FirstJsonException(exception) is not { } json || !ValueObjectErrors.TryGetCode(json, out var code))
            {
                return false;
            }

            refusals.Add(json.Path ?? string.Empty, json.Message, code);
        }

        metadata.Collect(httpContext.Request, feature.RouteValues, refusals);
        if (refusals.Count == 0)
        {
            return false;
        }

        await refusals.ToProblem(httpContext).ExecuteAsync(httpContext);

        return true;
    }

    private static JsonException? FirstJsonException(Exception exception)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is JsonException json)
            {
                return json;
            }
        }

        return null;
    }
}
