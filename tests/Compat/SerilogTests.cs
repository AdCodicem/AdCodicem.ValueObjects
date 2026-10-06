using System.Globalization;
using AdCodicem.ValueObjects.Serilog;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// Logging through Serilog with AdCodicem.ValueObjects.Serilog on the next major, the package's floor of Serilog
/// resolved as an application without central package management resolves it: a value object is the value it carries,
/// written as Serilog writes that type.
/// </summary>
public sealed class SerilogTests
{
    private static readonly JsonValueFormatter Formatter = new("$type");

    /// <summary>
    /// The policy logs a value object with <c>@</c>, alone or inside an object, as its underlying value: a number as a
    /// JSON number, a construction of a generic value object nothing registered included.
    /// </summary>
    [Fact]
    public void A_value_object_logged_with_at_is_its_underlying_value()
    {
        var customer = CustomerId.Create(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"));
        var payment = new Payment { Id = PaymentId.New(), Account = Iban.Create("FR7630006000011234567890189"), Amount = Amount.Create(12.5m) };

        var logged = Capture(
            new LoggerConfiguration().Destructure.ValueObjects(),
            "{@Quantity} {@Amount} {@Customer} {@Email} {@Reference} {@Payment} {Text}",
            Quantity.Create(42),
            Amount.Create(12.5m),
            customer,
            EmailAddress.Create("Ada@Example.com"),
            Reference<PurchaseOrder>.Create("po-1"),
            payment,
            Quantity.Create(42));

        Json(logged, "Quantity").Should().Be("42");
        Json(logged, "Amount").Should().Be("12.50");
        Json(logged, "Customer").Should().Be("\"0f8fad5b-d9cb-469f-a165-70867728950e\"");
        Json(logged, "Email").Should().Be("\"ada@example.com\"");
        Json(logged, "Reference").Should().Be("\"PO-1\"");
        Json(logged, "Payment").Should().Be($$"""{"Id":"{{payment.Id}}","Account":"FR7630006000011234567890189","Amount":12.50,"$type":"Payment"}""");
        Json(logged, "Text").Should().Be("\"42\"", "without @ and without the option, Serilog captures a value object's text");
    }

    /// <summary>
    /// With the option and the domain's assembly named, a value object logged without <c>@</c> is its underlying value
    /// too, a scalar of its underlying type.
    /// </summary>
    [Fact]
    public void With_the_option_a_value_object_logged_without_at_is_its_underlying_value()
    {
        var configuration = new LoggerConfiguration().Destructure.ValueObjects(static options =>
        {
            options.CaptureAsUnderlyingValue = true;
            options.Assemblies.Add(typeof(Quantity).Assembly);
        });

        var logged = Capture(configuration, "{Quantity} {Amount} {Quantities}", Quantity.Create(42), Amount.Create(12.5m), new List<Quantity> { Quantity.Create(1), Quantity.Create(2) });

        Json(logged, "Quantity").Should().Be("42");
        logged.Properties["Quantity"].Should().BeOfType<ScalarValue>().Which.Value.Should().BeOfType<int>();
        Json(logged, "Amount").Should().Be("12.50");
        Json(logged, "Quantities").Should().Be("[1,2]");
        logged.RenderMessage(CultureInfo.InvariantCulture).Should().Be("42 12.50 [1, 2]");
    }

    private static LogEvent Capture(LoggerConfiguration configuration, string template, params object?[] values)
    {
        var sink = new CapturingSink();
        using (var logger = configuration.WriteTo.Sink(sink).CreateLogger())
        {
            logger.Information(template, values);
        }

        return sink.Events.Should().ContainSingle().Subject;
    }

    private static string Json(LogEvent logEvent, string name)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        Formatter.Format(logEvent.Properties[name], writer);
        return writer.ToString();
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
