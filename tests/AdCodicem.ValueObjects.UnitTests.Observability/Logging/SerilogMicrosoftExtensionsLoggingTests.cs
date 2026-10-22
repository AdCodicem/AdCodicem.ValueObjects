using Microsoft.Extensions.Logging;
using Serilog;
using static AdCodicem.ValueObjects.UnitTests.Logging.SerilogCapture;
using MelLogger = Microsoft.Extensions.Logging.ILogger;

namespace AdCodicem.ValueObjects.UnitTests.Logging;

/// <summary>
/// With Serilog as the provider behind Microsoft.Extensions.Logging, a <c>LogInformation</c> call, a
/// <c>[LoggerMessage]</c> method and a scope go through the same capture as a Serilog call.
/// </summary>
public sealed partial class SerilogMicrosoftExtensionsLoggingTests
{
    /// <summary>
    /// With the option, a value object is its underlying value in every property Microsoft.Extensions.Logging hands
    /// Serilog; with the policy alone, only in the one logged with <c>@</c>.
    /// </summary>
    /// <param name="option">Whether <see cref="Serilog.ValueObjectLoggingOptions.CaptureAsUnderlyingValue"/> is on.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
#pragma warning disable CA1848, CA1873 // LogInformation with its arguments, as an application calls it.
    public void A_value_object_logged_through_Microsoft_Extensions_Logging_is_captured_as_through_Serilog(bool option)
    {
        var page = PageNumber.Create(42);
        var sink = new CapturingSink();
        using (var serilog = (option ? OptIn() : Policy()).WriteTo.Sink(sink).CreateLogger())
        using (var factory = LoggerFactory.Create(builder => builder.AddSerilog(serilog)))
        {
            var logger = factory.CreateLogger("Orders");
            logger.LogInformation("Placed {X} {@D}", page, page);
            Placed(logger, page, Celsius.Create(21));
            using (logger.BeginScope(new Dictionary<string, object?> { ["Scope"] = page }))
            {
                logger.LogInformation("Scoped");
            }
        }

        sink.Events.Should().HaveCount(3);
        var text = option ? "42" : "\"42\"";
        JsonOf(sink.Events[0], "X").Should().Be(text);
        JsonOf(sink.Events[0], "D").Should().Be("42");
        JsonOf(sink.Events[1], "X").Should().Be(text);
        JsonOf(sink.Events[1], "T").Should().Be(option ? "21" : "\"21 °C\"");
        Message(sink.Events[1]).Should().Be(option ? "Generated 42 21" : "Generated \"42\" \"21 °C\"");
        JsonOf(sink.Events[2], "Scope").Should().Be(text);
    }
#pragma warning restore CA1848, CA1873

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated {X} {T}")]
    private static partial void Placed(MelLogger logger, PageNumber x, Celsius t);
}
