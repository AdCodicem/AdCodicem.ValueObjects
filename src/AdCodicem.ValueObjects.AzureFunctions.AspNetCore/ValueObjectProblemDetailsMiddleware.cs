using System.Collections.Concurrent;
using System.Globalization;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Localization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.AzureFunctions.AspNetCore;

/// <summary>
/// Answers an HTTP trigger whose value objects the worker could not bind with the problem details the minimal API
/// package writes, carrying the code of each rule that refused a value.
/// </summary>
/// <param name="settings">The messages of the registration.</param>
/// <remarks>
/// <para>
/// The worker throws a <see cref="FunctionInputConverterException"/> that holds the converter's text alone, so each
/// value-object parameter the converter reads is parsed again from the binding data, as the minimal API filter parses its
/// parameters again. A <c>[FromBody]</c> value object refused by MVC's JSON input formatter, with the MVC package's
/// <c>AddValueObjects()</c>, surfaces as the integration's <see cref="InvalidOperationException"/> beside the codes that
/// formatter recorded on the request.
/// </para>
/// <para>
/// The answer is the invocation's result, which the integration's proxying middleware executes on the request, so it
/// reaches the client only when that middleware runs outside this one: the request is then set on the invocation by the
/// time this one starts. Otherwise a failure a value object explains, a refused body included, keeps the worker's 500,
/// and a warning says why, once.
/// </para>
/// </remarks>
internal sealed partial class ValueObjectProblemDetailsMiddleware(ValueObjectProblemDetailsSettings settings) : IFunctionsWorkerMiddleware
{
    private const string HttpTrigger = "httpTrigger";

    private readonly ConcurrentDictionary<string, bool> _httpTriggers = new(StringComparer.Ordinal);

    private int _warned;

    /// <inheritdoc />
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (!_httpTriggers.GetOrAdd(context.FunctionId, static (_, definition) => IsHttpTrigger(definition), context.FunctionDefinition))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Set by the integration's proxying middleware before it calls the middleware inside it, this one among them.
        var httpContext = context.GetHttpContext();
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (FunctionInputConverterException)
        {
            if (httpContext is null)
            {
                WarnOnce(context);
                throw;
            }

            if (await ExplainAsync(context, httpContext, body: null).ConfigureAwait(false) is not { } problem)
            {
                throw;
            }

            Answer(context, problem);
        }
        catch (InvalidOperationException exception) when (ValueObjectProblemDetails.GetErrorCodes(context.GetHttpContext()).Count > 0)
        {
            // MVC records the codes on the request, which the integration sets on the invocation before the body is bound:
            // it is there now on either side of the integration, and was there at start only if the integration runs
            // outside this middleware.
            if (httpContext is null)
            {
                WarnOnce(context);
                throw;
            }

            if (await ExplainAsync(context, httpContext, exception).ConfigureAwait(false) is not { } problem)
            {
                throw;
            }

            Answer(context, problem);
        }
    }

    /// <summary>Tells whether a function is triggered by HTTP, as the integration's proxying middleware tells it.</summary>
    internal static bool IsHttpTrigger(FunctionDefinition definition)
        => definition.InputBindings.Values.Any(static binding => string.Equals(binding.Type, HttpTrigger, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Collects the refusals of the invocation, in the culture of the request when the application named messages, and
    /// returns the problem that reports them, or <see langword="null"/> when no value object explains the failure.
    /// </summary>
    private async Task<ValidationProblem?> ExplainAsync(FunctionContext context, HttpContext httpContext, InvalidOperationException? body)
    {
        var messages = settings.Messages;
        if (messages?.ResourceType is null)
        {
            return Collect(context, httpContext, body, messages);
        }

        // The invocation runs on the host's call, not on the request's: the culture UseRequestLocalization() chose, if the
        // application added it to the integration's pipeline, is on the request alone.
        if (httpContext.Features.Get<IRequestCultureFeature>() is { } requested)
        {
            var current = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
            CultureInfo.CurrentCulture = requested.RequestCulture.Culture;
            CultureInfo.CurrentUICulture = requested.RequestCulture.UICulture;
            try
            {
                return Collect(context, httpContext, body, messages);
            }
            finally
            {
                (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = current;
            }
        }

        // Otherwise the application's RequestLocalizationOptions choose the culture from the request, as the framework's
        // middleware would, and, registered or not, fall back to the current culture.
        ValidationProblem? problem = null;
        var localization = new RequestLocalizationMiddleware(
            _ =>
            {
                problem = Collect(context, httpContext, body, messages);
                return Task.CompletedTask;
            },
            httpContext.RequestServices.GetRequiredService<IOptions<RequestLocalizationOptions>>(),
            httpContext.RequestServices.GetRequiredService<ILoggerFactory>());
        await localization.Invoke(httpContext).ConfigureAwait(false);

        return problem;
    }

    /// <summary>Parses each value-object parameter the converter reads again, and lists the body's refusal first.</summary>
    private static ValidationProblem? Collect(FunctionContext context, HttpContext httpContext, InvalidOperationException? body, ValueObjectMessageOptions? messages)
    {
        var refusals = new ValueObjectFunctionRefusals(messages, httpContext.RequestServices);
        if (body is not null)
        {
            // MVC's messages, joined by the integration in its exception, beside the codes MVC's formatter recorded: one
            // with System.Text.Json, which stops at the first refusal.
            string[] lines = [.. body.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            foreach (var (member, code) in ValueObjectProblemDetails.GetErrorCodes(httpContext))
            {
                foreach (var line in lines)
                {
                    refusals.Add(member, line, code);
                }
            }
        }

        var data = context.BindingContext.BindingData;
        foreach (var parameter in context.FunctionDefinition.Parameters)
        {
            if (!ValueObjectBindingText.IsReadAsText(parameter.Properties, typeof(ValueObjectInputConverter))
                || !ValueObjectParameterCheck.TryGet(parameter.Type, out var check))
            {
                continue;
            }

            if (data.TryGetValue(parameter.Name, out var source) && source is string text)
            {
                check.Check(text, parameter.Name, refusals);
            }
            else if (source is null && !parameter.HasDefaultValue && !parameter.IsReferenceOrNullableType)
            {
                refusals.AddRequired(parameter.Name);
            }
        }

        if (refusals.Count == 0)
        {
            return null;
        }

        LogAnswered(
            context.GetLogger(typeof(ValueObjectProblemDetailsMiddleware).FullName!),
            context.FunctionDefinition.Name,
            string.Join(", ", refusals.Codes.Select(static refusal => $"{refusal.Key} ({refusal.Value})")));

        return refusals.ToProblem(httpContext);
    }

    /// <summary>Sets the problem as the invocation's result, which the integration's proxying middleware executes.</summary>
    private static void Answer(FunctionContext context, ValidationProblem problem) => context.GetInvocationResult().Value = problem;

    private void WarnOnce(FunctionContext context)
    {
        if (Interlocked.Exchange(ref _warned, 1) == 0)
        {
            LogOutsideIntegration(context.GetLogger(typeof(ValueObjectProblemDetailsMiddleware).FullName!), context.FunctionDefinition.Name);
        }
    }

    [LoggerMessage(1, LogLevel.Information, "Function '{FunctionName}' answered 400 for refused value objects: {Refusals}.", EventName = "ValueObjectsRefused")]
    private static partial void LogAnswered(ILogger logger, string functionName, string refusals);

    [LoggerMessage(
        2,
        LogLevel.Warning,
        "UseValueObjectProblemDetails() found no HttpContext when the HTTP trigger '{FunctionName}' started: it answers the HTTP triggers of the ASP.NET Core integration only, from inside that integration's middleware. Call it after ConfigureFunctionsWebApplication(), or inside ConfigureFunctionsWebApplication(worker => ...); until then, a refused value object keeps the worker's 500.",
        EventName = "ValueObjectProblemDetailsOutsideIntegration")]
    private static partial void LogOutsideIntegration(ILogger logger, string functionName);
}
