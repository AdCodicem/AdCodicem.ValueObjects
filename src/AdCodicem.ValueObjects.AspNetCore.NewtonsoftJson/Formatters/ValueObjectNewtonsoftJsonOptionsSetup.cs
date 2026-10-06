using AdCodicem.ValueObjects.NewtonsoftJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Formatters;

/// <summary>
/// Adds <see cref="ValueObjectConverter"/> to MVC's Newtonsoft.Json settings, unless the application gave them one.
/// </summary>
/// <remarks>
/// It is a post-configuration, so it runs once every configuration of <see cref="MvcNewtonsoftJsonOptions"/> has run,
/// the one <c>AddNewtonsoftJson(options =&gt; …)</c> registers included, wherever the application called it. Settings
/// that hold the converter by then were configured for value objects by the application, which chose their
/// <see cref="Newtonsoft.Json.FloatParseHandling"/>, and are left as they are; others get the converter and the two
/// settings <see cref="ValueObjectJsonSerializerSettingsExtensions.AddValueObjects"/> gives it.
/// </remarks>
internal sealed class ValueObjectNewtonsoftJsonOptionsSetup : IPostConfigureOptions<MvcNewtonsoftJsonOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, MvcNewtonsoftJsonOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.SerializerSettings;
        if (!settings.Converters.OfType<ValueObjectConverter>().Any())
        {
            settings.AddValueObjects();
        }
    }
}
