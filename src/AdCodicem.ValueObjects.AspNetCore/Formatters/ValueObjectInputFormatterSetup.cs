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
/// It runs after every other configuration of <see cref="MvcOptions"/>, so it finds the formatter wherever the
/// application left it, and finds none once Newtonsoft.Json has replaced it.
/// </para>
/// <para>
/// <see cref="JsonOptions.JsonSerializerOptions"/> cannot be replaced, so the formatter cannot share the application's
/// options with a setting of its own. Its options are built by the same factory as the application's, which runs every
/// configuration the application registered: the same serializer settings, in an instance of their own, whose
/// exception messages it then turns off.
/// </para>
/// </remarks>
/// <param name="jsonOptions">The factory building MVC's JSON options.</param>
/// <param name="loggerFactory">The factory of the framework formatter's logger.</param>
internal sealed class ValueObjectInputFormatterSetup(
    IOptionsFactory<JsonOptions> jsonOptions,
    ILoggerFactory loggerFactory)
    : IPostConfigureOptions<MvcOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, MvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var formatters = options.InputFormatters;
        for (var index = 0; index < formatters.Count; index++)
        {
            if (formatters[index].GetType() != typeof(SystemTextJsonInputFormatter))
            {
                continue;
            }

            var own = jsonOptions.Create(Options.DefaultName);
            var exposeMessages = own.AllowInputFormatterExceptionMessages;
            own.AllowInputFormatterExceptionMessages = false;

            formatters[index] = new ValueObjectJsonInputFormatter(
                own,
                exposeMessages,
                loggerFactory.CreateLogger<SystemTextJsonInputFormatter>());
        }
    }
}
