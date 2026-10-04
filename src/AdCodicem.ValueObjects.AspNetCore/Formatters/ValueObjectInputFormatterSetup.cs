using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.AspNetCore.Formatters;

/// <summary>
/// Puts <see cref="ValueObjectJsonInputFormatter"/> in place of MVC's System.Text.Json input formatter.
/// </summary>
/// <remarks>
/// <para>
/// It runs after every configuration of <see cref="MvcOptions"/> and every post-configuration registered before it,
/// so it finds the formatter wherever the application left it, and finds none once Newtonsoft.Json has replaced it.
/// </para>
/// <para>
/// It replaces the framework's formatter alone: one of the exact type <see cref="SystemTextJsonInputFormatter"/>
/// that reads with the application's own <see cref="JsonOptions"/>, whose serializer options the framework built it
/// with. Its replacement is built with that same instance, so it reads with the same serializer options, sees any
/// change made to them or to <see cref="JsonOptions.AllowInputFormatterExceptionMessages"/> later on, and keeps the
/// media types and encodings the application gave the formatter it replaces. A formatter the application built with
/// options of its own, or derived, is left as it is.
/// </para>
/// </remarks>
/// <param name="jsonOptions">MVC's JSON options, the application's.</param>
/// <param name="loggerFactory">The factory of the framework formatter's logger.</param>
internal sealed class ValueObjectInputFormatterSetup(
    IOptions<JsonOptions> jsonOptions,
    ILoggerFactory loggerFactory)
    : IPostConfigureOptions<MvcOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, MvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var application = jsonOptions.Value;
        var formatters = options.InputFormatters;
        for (var index = 0; index < formatters.Count; index++)
        {
            if (formatters[index] is not SystemTextJsonInputFormatter framework
                || framework.GetType() != typeof(SystemTextJsonInputFormatter)
                || !ReferenceEquals(framework.SerializerOptions, application.JsonSerializerOptions))
            {
                continue;
            }

            var replacement = new ValueObjectJsonInputFormatter(
                application,
                loggerFactory.CreateLogger<SystemTextJsonInputFormatter>());

            replacement.SupportedMediaTypes.Clear();
            foreach (var mediaType in framework.SupportedMediaTypes)
            {
                replacement.SupportedMediaTypes.Add(mediaType);
            }

            replacement.SupportedEncodings.Clear();
            foreach (var encoding in framework.SupportedEncodings)
            {
                replacement.SupportedEncodings.Add(encoding);
            }

            formatters[index] = replacement;
        }
    }
}
