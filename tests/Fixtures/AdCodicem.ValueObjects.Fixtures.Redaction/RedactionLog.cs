using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.Fixtures.Redaction;

/// <summary>
/// Logging methods Microsoft.Gen.Logging writes, which hand each classified tag to the redactor the application
/// configures for its classification, and every other tag as it is.
/// </summary>
public static partial class RedactionLog
{
    /// <summary>Logs an email address classified on the parameter, as <c>VO0045</c>'s code fix writes it.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="email">The address, redacted.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Classified {Email}")]
    public static partial void Classified(ILogger logger, [ContactData] ContactEmail email);

#pragma warning disable VO0045 // The defect the redaction tests pin: a classification on the type alone is not read.
    /// <summary>Logs an email address classified on its type alone, which the logging generator writes in clear.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="email">The address, in clear.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Type only {Email}")]
    public static partial void TypeOnly(ILogger logger, ContactEmail email);
#pragma warning restore VO0045

    /// <summary>Logs an email address in clear on purpose, which <c>[NoDataClassification]</c> states.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="email">The address, in clear.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "On purpose {Email}")]
    public static partial void OnPurpose(ILogger logger, [NoDataClassification] ContactEmail email);

    /// <summary>Logs a value object with a formatting hook, classified on the parameter.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="temperature">The temperature, redacted.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Hook {Temperature}")]
    public static partial void Hook(ILogger logger, [ContactData] Temperature temperature);

    /// <summary>Logs the properties of a shipment, its code classified on its property.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="shipment">The shipment, its code redacted.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Expanded")]
    public static partial void Expanded(ILogger logger, [LogProperties] Shipment shipment);

#pragma warning disable VO0046, EXTEXP0003 // The defect the redaction tests pin: Transitive drops the property's classification.
    /// <summary>Logs the properties of a shipment transitively, which writes its code's value in clear.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="shipment">The shipment, its code in clear.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Transitive")]
    public static partial void Transitive(ILogger logger, [LogProperties(Transitive = true)] Shipment shipment);
#pragma warning restore VO0046, EXTEXP0003

    /// <summary>Logs an email address as text, classified on the parameter, under the tag a value object's goes to.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="email">The address, redacted.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Text {Email}")]
    public static partial void Text(ILogger logger, [ContactData] string email);

    /// <summary>Logs a temperature as text, classified on the parameter, under the tag a value object's goes to.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="temperature">The temperature, redacted.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Reading {Temperature}")]
    public static partial void Reading(ILogger logger, [ContactData] string temperature);
}
