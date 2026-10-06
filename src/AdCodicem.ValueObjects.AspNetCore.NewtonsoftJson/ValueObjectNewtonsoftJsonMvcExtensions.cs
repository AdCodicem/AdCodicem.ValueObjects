using AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Formatters;
using AdCodicem.ValueObjects.NewtonsoftJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson;

/// <summary>
/// Wires value object support into an MVC application whose request bodies Newtonsoft.Json reads.
/// </summary>
public static class ValueObjectNewtonsoftJsonMvcExtensions
{
    /// <summary>
    /// Registers value object model binding and serialization on an MVC builder that reads with Newtonsoft.Json, and
    /// records the code of the rule a value refused inside a request body broke.
    /// </summary>
    /// <param name="builder">MVC builder, on which <c>AddNewtonsoftJson()</c> is called, before or after this.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// It calls <see cref="ValueObjectMvcExtensions.AddValueObjects(IMvcBuilder)"/> first, so that value objects bind
    /// from the route, the query string and the headers, and one call sets everything up; an application that calls
    /// that method as well, before or after, behaves the same.
    /// </para>
    /// <para>
    /// It adds <see cref="ValueObjectConverter"/> to MVC's <see cref="MvcNewtonsoftJsonOptions.SerializerSettings"/>,
    /// with <see cref="DateParseHandling.None"/> and <see cref="FloatParseHandling.Decimal"/>, as
    /// <see cref="ValueObjectJsonSerializerSettingsExtensions.AddValueObjects"/> does, once every configuration of those
    /// options has run, and only when the settings hold no such converter yet: settings the application gave one are its
    /// own, and are left as it left them. Without the converter, Newtonsoft.Json reads a
    /// value object through its type converter, whose refusal names no rule and quotes the refused text.
    /// </para>
    /// <para>
    /// Those settings are the ones MVC writes every response with as well, and they apply to every member of every
    /// body. An application that ran without the converter changes its wire: a numeric or boolean value object is
    /// answered <c>7</c> or <c>true</c>, where the type converter wrote <c>"7"</c> or <c>"True"</c>, and a boolean
    /// value object sent as a string is refused. A member of type <see cref="object"/> or <c>JToken</c> keeps a string
    /// that looks like a date as a string, and reads a number with a fraction as a <see cref="decimal"/>, which refuses
    /// one beyond its range.
    /// </para>
    /// <para>
    /// It then puts a formatter of its own in place of MVC's Newtonsoft.Json input formatter, at its index: one deriving
    /// from it, built over the same serializer settings and the same <see cref="MvcNewtonsoftJsonOptions"/>, with the
    /// media types and encodings the application gave the formatter it replaces. It reads a body as the framework's
    /// does, and records the code of each value object the body refuses under the key of its model state error, so that
    /// <see cref="ValueObjectMvcExtensions.AddValueObjectProblemDetails"/> puts it in the response beside the message.
    /// Newtonsoft.Json reads on past a refused member of an object whose properties it sets, so each refused member
    /// keeps its own code, until the model state holds as many errors as
    /// <see cref="MvcOptions.MaxModelValidationErrors"/> lets it. The model state, the logs and the exceptions that
    /// propagate are the framework's, whatever <see cref="MvcNewtonsoftJsonOptions.AllowInputFormatterExceptionMessages"/>
    /// says when the request is read. A Newtonsoft.Json formatter the application built over settings of its own, or
    /// derived, the JSON Patch formatter among them, is left as it is, and records no code.
    /// </para>
    /// <para>
    /// The swap is a post-configuration of <see cref="MvcOptions"/>. In a post-configuration registered after this call,
    /// <c>InputFormatters.RemoveType&lt;NewtonsoftJsonInputFormatter&gt;()</c> finds this formatter in its place, which
    /// is not of that exact type: remove there every formatter that is a
    /// <see cref="Microsoft.AspNetCore.Mvc.Formatters.NewtonsoftJsonInputFormatter"/> but neither a
    /// <see cref="Microsoft.AspNetCore.Mvc.Formatters.NewtonsoftJsonPatchInputFormatter"/>, which derives from it and
    /// which that call keeps, nor a formatter the application derived itself.
    /// </para>
    /// <para>
    /// It does not call <c>AddNewtonsoftJson()</c>, which is the application's choice of serializer. Without it MVC
    /// reads bodies with System.Text.Json, whose formatter <see cref="ValueObjectMvcExtensions.AddValueObjects(IMvcBuilder)"/>
    /// already replaces, and this call amounts to that one.
    /// </para>
    /// </remarks>
    public static IMvcBuilder AddValueObjectsNewtonsoftJson(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddValueObjects();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Transient<IPostConfigureOptions<MvcNewtonsoftJsonOptions>, ValueObjectNewtonsoftJsonOptionsSetup>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Transient<IPostConfigureOptions<MvcOptions>, ValueObjectNewtonsoftJsonInputFormatterSetup>());

        return builder;
    }
}
