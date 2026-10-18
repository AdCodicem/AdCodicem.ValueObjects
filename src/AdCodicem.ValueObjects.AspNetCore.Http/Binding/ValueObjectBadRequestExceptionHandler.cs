using System.Globalization;
using System.Text.Json;
using AdCodicem.ValueObjects.Shared;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

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
/// <para>
/// The messages are read in the culture of the request, which <c>UseRequestLocalization()</c> sets: in the usual order,
/// where <c>UseExceptionHandler()</c> comes first, that middleware has restored the server's culture by the time the
/// exception reaches the handler, and the handler applies the culture its <see cref="IRequestCultureFeature"/> names
/// again while it answers.
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

        // The exception handler middleware runs before the request localization middleware in the usual order, and the
        // culture that middleware set has been restored by the time the exception reaches it: the messages are read in the
        // request's culture again, which the feature that middleware left on the request still names.
        var current = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        if (httpContext.Features.Get<IRequestCultureFeature>() is { } requested)
        {
            CultureInfo.CurrentCulture = requested.RequestCulture.Culture;
            CultureInfo.CurrentUICulture = requested.RequestCulture.UICulture;
        }

        try
        {
            var refusals = new ValueObjectRefusals(metadata.Messages, httpContext.RequestServices);

            // A body the serializer refused: the exception it threw is wrapped, and carries the code, never the value. Its
            // message is the converter's; the framework's own message names the refused text, and is not copied. A body or
            // a form the binder could not read for another reason is not the package's to explain.
            if (exception.InnerException is not null)
            {
                if (FirstJsonException(exception) is not { } json || !ValueObjectErrors.TryGetCode(json, out var code))
                {
                    return false;
                }

                refusals.AddBody(json, code);
            }

            metadata.Collect(httpContext.Request, feature.RouteValues, refusals);
            if (refusals.Count == 0)
            {
                return false;
            }

            await refusals.ToProblem(httpContext).ExecuteAsync(httpContext);

            return true;
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = current;
        }
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
