using Newtonsoft.Json;

namespace AdCodicem.ValueObjects.NewtonsoftJson;

/// <summary>
/// Wires value object support into <see cref="JsonSerializerSettings"/>.
/// </summary>
public static class ValueObjectJsonSerializerSettingsExtensions
{
    /// <summary>
    /// Adds <see cref="ValueObjectConverter"/> to the settings, with the two settings it reads value objects best under.
    /// </summary>
    /// <param name="settings">Settings to configure.</param>
    /// <param name="decimalReals">
    /// <see langword="true"/> to set <see cref="FloatParseHandling.Decimal"/>; <see langword="false"/> to leave
    /// <see cref="JsonSerializerSettings.FloatParseHandling"/> as the settings have it.
    /// </param>
    /// <returns>The same settings, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// Newtonsoft.Json reads each token under its settings before a converter sees it. This sets
    /// <see cref="DateParseHandling.None"/>, so that a string that looks like a date reaches the converter as the text
    /// it is, rather than as a date converted to local time, which a string or a <see cref="DateTimeOffset"/> value
    /// object cannot be read back from.
    /// </para>
    /// <para>
    /// <see cref="FloatParseHandling.Decimal"/> keeps every digit of a <see cref="decimal"/> value object, where the
    /// default reads it through a <see cref="double"/>: <c>12.50</c> would read back as 12.5, and
    /// <c>1234567890123456789.12</c> as 1234567890123456800. It applies to every number of the payload, so pass
    /// <see langword="false"/> when a payload carries a <see cref="double"/> or a <see cref="float"/> beyond the range
    /// of a <see cref="decimal"/>, about ±7.9 × 10^28, which makes the reader throw under it, or one smaller than its 28
    /// decimal places, which loses the digits past them.
    /// </para>
    /// <para>
    /// The converter is added once: a registration already in the settings is left alone.
    /// </para>
    /// </remarks>
    public static JsonSerializerSettings AddValueObjects(this JsonSerializerSettings settings, bool decimalReals = true)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.DateParseHandling = DateParseHandling.None;
        if (decimalReals)
        {
            settings.FloatParseHandling = FloatParseHandling.Decimal;
        }

        foreach (var converter in settings.Converters)
        {
            if (converter is ValueObjectConverter)
            {
                return settings;
            }
        }

        settings.Converters.Add(new ValueObjectConverter());

        return settings;
    }
}
