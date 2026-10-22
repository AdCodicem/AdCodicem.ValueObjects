using System.Globalization;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Serilog;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;

namespace AdCodicem.ValueObjects.UnitTests.Logging;

/// <summary>A sink keeping every event it receives, in order.</summary>
public sealed class CapturingSink : ILogEventSink
{
    /// <summary>Gets the events received.</summary>
    public List<LogEvent> Events { get; } = [];

    /// <inheritdoc />
    public void Emit(LogEvent logEvent) => Events.Add(logEvent);
}

/// <summary>
/// Logs through Serilog as an application does, a logger built for each call, and reads back what a sink would write.
/// </summary>
public static partial class SerilogCapture
{
    private static readonly CompactJsonFormatter CompactFormatter = new();

    private static readonly JsonValueFormatter ValueFormatter = new("$type");

    private static readonly MessageTemplateTextFormatter LiteralFormatter = new("{Message:lj}", CultureInfo.InvariantCulture);

    /// <summary>Gets a configuration without the package.</summary>
    public static LoggerConfiguration Bare() => new();

    /// <summary>Gets a configuration with the destructuring policy alone.</summary>
    public static LoggerConfiguration Policy() => new LoggerConfiguration().Destructure.ValueObjects();

    /// <summary>Gets a configuration with the policy and the capture of a value object logged without <c>@</c>.</summary>
    public static LoggerConfiguration OptIn()
        => new LoggerConfiguration().Destructure.ValueObjects(static options => options.CaptureAsUnderlyingValue = true);

    /// <summary>Logs one event and returns it as the sink received it.</summary>
    /// <param name="configuration">The configuration the logger is built from.</param>
    /// <param name="template">The message template.</param>
    /// <param name="values">The values of its properties.</param>
    /// <returns>The event.</returns>
    public static LogEvent Capture(LoggerConfiguration configuration, string template, params object?[] values)
    {
        var sink = new CapturingSink();
        using (var logger = configuration.WriteTo.Sink(sink).CreateLogger())
        {
            logger.Information(template, values);
        }

        return sink.Events.Should().ContainSingle().Subject;
    }

    /// <summary>Writes an event as <see cref="CompactJsonFormatter"/> does, without its timestamp.</summary>
    /// <param name="logEvent">The event.</param>
    /// <returns>The JSON line, <c>{"@mt":"…","Qty":42}</c>.</returns>
    public static string Compact(LogEvent logEvent)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        CompactFormatter.Format(logEvent, writer);
        return Timestamp().Replace(writer.ToString().Trim(), "{");
    }

    /// <summary>Writes a property value as Serilog's JSON formatters do.</summary>
    /// <param name="value">The property value.</param>
    /// <returns>Its JSON.</returns>
    public static string JsonOf(LogEventPropertyValue value)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        ValueFormatter.Format(value, writer);
        return writer.ToString();
    }

    /// <summary>Writes a property of an event as Serilog's JSON formatters do.</summary>
    /// <param name="logEvent">The event.</param>
    /// <param name="name">The name of the property.</param>
    /// <returns>Its JSON.</returns>
    public static string JsonOf(LogEvent logEvent, string name) => JsonOf(logEvent.Properties[name]);

    /// <summary>Renders the message of an event as a text sink does, <c>{Message}</c>, strings quoted.</summary>
    /// <param name="logEvent">The event.</param>
    /// <returns>The message.</returns>
    public static string Message(LogEvent logEvent) => logEvent.RenderMessage(CultureInfo.InvariantCulture);

    /// <summary>Renders the message of an event as <c>{Message:lj}</c> does, strings unquoted.</summary>
    /// <param name="logEvent">The event.</param>
    /// <returns>The message.</returns>
    public static string Literal(LogEvent logEvent)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        LiteralFormatter.Format(logEvent, writer);
        return writer.ToString();
    }

    /// <summary>Reads what a scalar property holds, so that its type can be asserted.</summary>
    /// <param name="logEvent">The event.</param>
    /// <param name="name">The name of the property.</param>
    /// <returns>The value the scalar holds.</returns>
    public static object? Scalar(LogEvent logEvent, string name)
        => logEvent.Properties[name].Should().BeOfType<ScalarValue>().Subject.Value;

    [GeneratedRegex("""^\{"@t":"[^"]*",""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Timestamp();
}
