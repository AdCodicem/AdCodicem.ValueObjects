using System.ComponentModel;
using System.Globalization;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.Shared;
using AdCodicem.ValueObjects.Wolverine.Http.Binding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Wolverine.Http;

namespace AdCodicem.ValueObjects.Wolverine.Http;

/// <summary>
/// Reads value objects for the code Wolverine generates for an endpoint, at run time or with <c>codegen write</c>; that
/// code is its only caller.
/// </summary>
/// <remarks>
/// <para>
/// Public because the generated code is compiled into the application: code written by <c>codegen write</c> calls these
/// members by name, so a change to their signatures fails that code until it is written again.
/// </para>
/// <para>
/// A refusal is recorded on the request, never thrown: <see cref="TryAnswerRefusalsAsync(HttpContext)"/>, which the
/// generated code calls once every value object is read, answers every refusal at once.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ValueObjectHttpBinding
{
    private static readonly object ItemsKey = new();

    /// <summary>Parses the text of a value object that cannot be null, recording a refusal, or an absent required value, for the request.</summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="httpContext">The request.</param>
    /// <param name="name">The name the value binds from, which a refusal is listed under.</param>
    /// <param name="text">The text, or <see langword="null"/> when the request leaves the value out.</param>
    /// <param name="required">Whether an absent value is refused: <see langword="false"/> for a parameter with a default value.</param>
    /// <returns>The value object, or the default instance, which the handler never sees, once refused.</returns>
    public static TSelf Bind<TSelf, TValue>(HttpContext httpContext, string name, string? text, bool required)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (text is null)
        {
            if (required)
            {
                Refuse(httpContext, name);
            }

            return default;
        }

        return TryBind<TSelf, TValue>(httpContext, name, text, out var value) ? value : default;
    }

    /// <summary>Parses the text of a nullable value object: null when the request leaves it out.</summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="httpContext">The request.</param>
    /// <param name="name">The name the value binds from, which a refusal is listed under.</param>
    /// <param name="text">The text, or <see langword="null"/> when the request leaves the value out.</param>
    /// <returns>The value object, or <see langword="null"/> when absent or refused.</returns>
    public static TSelf? BindNullable<TSelf, TValue>(HttpContext httpContext, string name, string? text)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return text is not null && TryBind<TSelf, TValue>(httpContext, name, text, out var value) ? value : null;
    }

    /// <summary>Parses every text of a repeated value, each refusal listed under its name.</summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="httpContext">The request.</param>
    /// <param name="name">The name the values bind from, which every refusal is listed under.</param>
    /// <param name="texts">The texts, none when the request leaves the value out.</param>
    /// <returns>The value objects, in order; an empty array when absent.</returns>
    public static TSelf[] BindArray<TSelf, TValue>(HttpContext httpContext, string name, StringValues texts)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var values = new TSelf[texts.Count];
        for (var index = 0; index < values.Length; index++)
        {
            _ = TryBind<TSelf, TValue>(httpContext, name, texts[index] ?? string.Empty, out values[index]);
        }

        return values;
    }

    /// <summary>Reads the text of a route value, or null.</summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="key">The name of the route parameter.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    public static string? Route(HttpContext httpContext, string key)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.GetRouteValue(key) as string;
    }

    /// <summary>Reads the text of a query value, the values of a repeated key joined, or null.</summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="key">The key in the query string.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    public static string? Query(HttpContext httpContext, string key)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.Request.Query.TryGetValue(key, out var values) ? values.ToString() : null;
    }

    /// <summary>Reads every value of a query key.</summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="key">The key in the query string.</param>
    /// <returns>The values, none when the key is absent.</returns>
    public static StringValues QueryValues(HttpContext httpContext, string key)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.Request.Query[key];
    }

    /// <summary>Reads the text of a header, the values of a repeated header joined, or null.</summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="key">The name of the header.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    public static string? Header(HttpContext httpContext, string key)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.Request.Headers.TryGetValue(key, out var values) ? values.ToString() : null;
    }

    /// <summary>Reads the text of a form field, or null.</summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="key">The name of the field.</param>
    /// <returns>The text, or <see langword="null"/> when the field is absent or the body is no form.</returns>
    public static async Task<string?> FormAsync(HttpContext httpContext, string key)
        => (await FormValuesAsync(httpContext, key).ConfigureAwait(false)) is { Count: > 0 } values ? values.ToString() : null;

    /// <summary>Reads every value of a form field.</summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="key">The name of the field.</param>
    /// <returns>The values, none when the field is absent or the body is no form.</returns>
    public static async Task<StringValues> FormValuesAsync(HttpContext httpContext, string key)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!httpContext.Request.HasFormContentType)
        {
            return StringValues.Empty;
        }

        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);

        return form[key];
    }

    /// <summary>Answers the request with the problem details of its refusals, if it has any.</summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>Whether the request was answered, which ends the generated handler.</returns>
    public static async Task<bool> TryAnswerRefusalsAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!httpContext.Items.TryGetValue(ItemsKey, out var listed) || listed is not ValueObjectRefusals { Count: > 0 } refusals)
        {
            return false;
        }

        await refusals.ToProblem(httpContext).ExecuteAsync(httpContext).ConfigureAwait(false);

        return true;
    }

    /// <summary>Parses a text, recording its refusal, if any, under the name.</summary>
    private static bool TryBind<TSelf, TValue>(HttpContext httpContext, string name, string text, out TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (TSelf.TryParse(text, CultureInfo.InvariantCulture, out value, out var validation))
        {
            return true;
        }

        var code = validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable;
        RefusalsOf(httpContext).Add<TSelf, TValue>(name, validation.ErrorMessage ?? $"The value is not a valid {typeof(TSelf).Name}.", code);
        ValueObjectProblemDetails.RecordErrorCode(httpContext, name, code);

        return false;
    }

    /// <summary>Records a required value the request leaves out.</summary>
    private static void Refuse(HttpContext httpContext, string name)
    {
        RefusalsOf(httpContext).AddRequired(name);
        ValueObjectProblemDetails.RecordErrorCode(httpContext, name, ValueObjectErrorCodes.Required);
    }

    /// <summary>
    /// Gets the refusals of the request, created on the first, with the messages <c>UseValueObjects</c> named.
    /// </summary>
    private static ValueObjectRefusals RefusalsOf(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(ItemsKey, out var listed) && listed is ValueObjectRefusals existing)
        {
            return existing;
        }

        var services = httpContext.RequestServices;
        var messages = services.GetService<WolverineHttpOptions>()?.Policies.OfType<ValueObjectRefusalPolicy>().FirstOrDefault()?.Messages;
        var refusals = new ValueObjectRefusals(messages, services);
        httpContext.Items[ItemsKey] = refusals;

        return refusals;
    }
}
