using System.Buffers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Formatters;

/// <summary>
/// Puts <see cref="ValueObjectNewtonsoftJsonInputFormatter"/> in place of MVC's Newtonsoft.Json input formatter.
/// </summary>
/// <remarks>
/// <para>
/// It runs after every configuration of <see cref="MvcOptions"/>, the one <c>AddNewtonsoftJson()</c> adds its formatters
/// in included, and every post-configuration registered before it, so it finds the formatter wherever the application
/// left it, and finds none when MVC reads with System.Text.Json.
/// </para>
/// <para>
/// It replaces the framework's formatter alone: one of the exact type <see cref="NewtonsoftJsonInputFormatter"/> that
/// reads with the application's own <see cref="MvcNewtonsoftJsonOptions.SerializerSettings"/>, which the framework built
/// it with. Its replacement is built as the framework builds it, with those settings, the same
/// <see cref="MvcNewtonsoftJsonOptions"/>, the pools the framework takes from the services and the
/// <see cref="MvcOptions"/> being configured, and keeps the media types and encodings the application gave the formatter
/// it replaces. A formatter the application built over settings of its own, or derived, the JSON Patch formatter among
/// them, is left as it is.
/// </para>
/// </remarks>
/// <param name="jsonOptions">MVC's Newtonsoft.Json options, the application's.</param>
/// <param name="loggerFactory">The factory of the framework formatter's logger.</param>
/// <param name="charPool">The pool of the buffers the reader reads into, the framework formatter's.</param>
/// <param name="objectPoolProvider">The provider of the pool of serializers, the framework formatter's.</param>
internal sealed class ValueObjectNewtonsoftJsonInputFormatterSetup(
    IOptions<MvcNewtonsoftJsonOptions> jsonOptions,
    ILoggerFactory loggerFactory,
    ArrayPool<char> charPool,
    ObjectPoolProvider objectPoolProvider)
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
            if (formatters[index] is not NewtonsoftJsonInputFormatter framework
                || framework.GetType() != typeof(NewtonsoftJsonInputFormatter)
                || !ReferenceEquals(ValueObjectNewtonsoftJsonInputFormatter.SettingsOf(framework), application.SerializerSettings))
            {
                continue;
            }

            var replacement = new ValueObjectNewtonsoftJsonInputFormatter(
                loggerFactory.CreateLogger<NewtonsoftJsonInputFormatter>(),
                application.SerializerSettings,
                charPool,
                objectPoolProvider,
                options,
                application);

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
