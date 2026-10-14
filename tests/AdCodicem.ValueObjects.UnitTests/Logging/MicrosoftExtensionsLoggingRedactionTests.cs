using AdCodicem.ValueObjects.Fixtures.Redaction;
using AdCodicem.ValueObjects.Fixtures.WithoutJson;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using static AdCodicem.ValueObjects.UnitTests.Logging.SerilogCapture;
using MelLogger = Microsoft.Extensions.Logging.ILogger;

namespace AdCodicem.ValueObjects.UnitTests.Logging;

/// <summary>
/// What an application logging through Microsoft.Extensions.Telemetry's redaction writes of a value object, through the
/// <c>[LoggerMessage]</c> methods Microsoft.Gen.Logging writes in the Redaction fixture: the behaviour <c>VO0045</c> and
/// <c>VO0046</c> warn about, at run time.
/// </summary>
/// <remarks>
/// Two of these pin a defect of the logging generator. When an update makes one fail, the logging generator now reads
/// what it did not, and the diagnostic warning about it can step back.
/// </remarks>
#pragma warning disable CA1873 // The value objects created as arguments are what each test logs.
public sealed class MicrosoftExtensionsLoggingRedactionTests
{
    private const string Address = "jane@example.com";

    private static readonly DataClassificationSet Contact = new(RedactionTaxonomy.Contact);

    /// <summary>A classification on the parameter, as the code fix of <c>VO0045</c> writes it, sends the value to the redactor.</summary>
    [Fact]
    public void A_classified_parameter_is_erased()
        => Tags(logger => RedactionLog.Classified(logger, ContactEmail.Create(Address)))["Email"].Should().BeEmpty();

    /// <summary>
    /// A classification on the type alone is not read: the value object is written in clear, whatever redactor the
    /// application configures. When this fails, <c>VO0045</c> can step back.
    /// </summary>
    [Fact]
    public void A_classification_on_the_type_alone_is_not_read()
        => Tags(logger => RedactionLog.TypeOnly(logger, ContactEmail.Create(Address)))["Email"].Should().Be(Address);

    /// <summary><c>[NoDataClassification]</c> logs a classified value object in clear, as it says.</summary>
    [Fact]
    public void NoDataClassification_logs_in_clear()
        => Tags(logger => RedactionLog.OnPurpose(logger, ContactEmail.Create(Address)))["Email"].Should().Be(Address);

    /// <summary>
    /// <c>[LogProperties]</c> keeps the classification of a property holding a value object of another assembly, and
    /// <c>Transitive</c>, which expands it into its <c>Value</c>, drops it. When the second fails, <c>VO0046</c> can step
    /// back.
    /// </summary>
    [Fact]
    public void LogProperties_keeps_the_classification_of_a_property_and_Transitive_drops_it()
    {
        var shipment = new Shipment(Sku.Create("sku-1"));

        Tags(logger => RedactionLog.Expanded(logger, shipment))["shipment.Code"].Should().BeEmpty();
        Tags(logger => RedactionLog.Transitive(logger, shipment))["shipment.Code.Value"].Should().Be("SKU-1");
    }

    /// <summary>
    /// The redactor reads a value object as its text, so the HMAC redactor hashes it as its underlying value, the hash an
    /// application correlates with across its logs; a value object with a formatting hook is hashed as the hook's text.
    /// </summary>
    [Fact]
    public void The_HMAC_redactor_hashes_a_value_object_as_its_text()
    {
        var capture = new CapturingProvider();
        using var services = new ServiceCollection()
            .AddLogging(builder => builder.EnableRedaction().AddProvider(capture))
            .AddRedaction(redaction => redaction.SetHmacRedactor(
                options =>
                {
                    options.Key = Convert.ToBase64String(new byte[32]);
                    options.KeyId = 1;
                },
                Contact))
            .BuildServiceProvider();

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Redaction");
        RedactionLog.Classified(logger, ContactEmail.Create(Address));
        RedactionLog.Text(logger, Address);
        RedactionLog.Hook(logger, Temperature.Create(21));
        RedactionLog.Reading(logger, "21 °C");
        RedactionLog.Reading(logger, "21");

        var hashes = capture.Tags.Select(tags => tags.Single(tag => tag.Key != "{OriginalFormat}").Value).ToList();
        hashes.Should().AllSatisfy(hash => hash.Should().StartWith("1:"));
        hashes[0].Should().Be(hashes[1]);
        hashes[2].Should().Be(hashes[3]).And.NotBe(hashes[4]);
    }

    /// <summary>
    /// Serilog, the provider behind Microsoft.Extensions.Logging, receives what the redaction wrote: the parameter the code
    /// fix classifies is erased, and the one classified on its type alone is in clear.
    /// </summary>
    [Fact]
    public void Serilog_behind_Microsoft_Extensions_Logging_receives_the_redacted_value()
    {
        var sink = new CapturingSink();
        using (var serilog = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger())
        using (var services = new ServiceCollection()
                   .AddLogging(builder => builder.EnableRedaction().AddSerilog(serilog))
                   .AddRedaction(redaction => redaction.SetRedactor<ErasingRedactor>(Contact))
                   .BuildServiceProvider())
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Redaction");
            RedactionLog.Classified(logger, ContactEmail.Create(Address));
            RedactionLog.TypeOnly(logger, ContactEmail.Create(Address));
        }

        sink.Events.Select(logEvent => JsonOf(logEvent, "Email")).Should().Equal("\"\"", $"\"{Address}\"");
    }

    /// <summary>Logs through a factory redacting the fixture's classification with the erasing redactor, and returns the tags.</summary>
    private static Dictionary<string, string?> Tags(Action<MelLogger> log)
    {
        var capture = new CapturingProvider();
        using var services = new ServiceCollection()
            .AddLogging(builder => builder.EnableRedaction().AddProvider(capture))
            .AddRedaction(redaction => redaction.SetRedactor<ErasingRedactor>(Contact))
            .BuildServiceProvider();

        log(services.GetRequiredService<ILoggerFactory>().CreateLogger("Redaction"));

        return capture.Tags.Should().ContainSingle().Subject;
    }

    /// <summary>A provider keeping the tags of every entry, each as the text a sink would write.</summary>
    private sealed class CapturingProvider : ILoggerProvider
    {
        public List<Dictionary<string, string?>> Tags { get; } = [];

        public MelLogger CreateLogger(string categoryName) => new Capturing(Tags);

        public void Dispose()
        {
        }

        private sealed class Capturing(List<Dictionary<string, string?>> tags) : MelLogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => tags.Add(((IReadOnlyList<KeyValuePair<string, object?>>)state!)
                    .ToDictionary(tag => tag.Key, tag => tag.Value?.ToString(), StringComparer.Ordinal));
        }
    }
}
#pragma warning restore CA1873
