using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Serilog;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// Every value object the registry holds, logged through Serilog with and without <c>@</c>, under the destructuring
/// policy of <c>Destructure.ValueObjects()</c> and under its option, beside the bare value it carries.
/// </summary>
/// <remarks>
/// Serilog's build targets turn object destructuring off under <c>PublishTrimmed</c>, which <c>PublishAot</c> implies, and
/// the project turns it off for the JIT run too: an object logged with <c>@</c> is its <c>ToString()</c> in both, and a
/// bare <see cref="Int128"/> its text.
/// </remarks>
internal static class Logging
{
    private static readonly JsonValueFormatter Formatter = new("$type");

    /// <param name="report">Where each scenario is written.</param>
    /// <param name="minted">An identifier minted earlier, the one text an identifier accepts.</param>
    public static void Run(Report report, OrderId minted)
    {
        foreach (var descriptor in ValueObjectRegistry.GetRegistered().OrderBy(d => Names.Of(d.ValueObjectType), StringComparer.Ordinal))
        {
            descriptor.Accept(new SerilogProbe(report, minted.Value));
        }

        // Registered by nothing: the policy needs no registration, and the option captures it as its text.
        var unregistered = UnregisteredCode.Create("abc");
        report.Line("serilog UnregisteredCode", $"policy {Logged(Policy(), "{@D}", unregistered)}; opt-in {Logged(OptIn(), "{X}", unregistered)}");

        // Collections are captured element by element, which the option replaces.
        var quantities = new List<Quantity> { Quantity.Create(1), Quantity.Create(2) };
        var amounts = new Dictionary<string, Amount> { ["a"] = Amount.Create(12.345m) };
        report.Line("serilog collections", $"opt-in {Logged(OptIn(), "{L} {D}", quantities, amounts)}; policy {Logged(Policy(), "{@L} {@D}", quantities, amounts)}");

        // Serilog destructures no object here, so the record is the text ToString() writes.
        report.Line("serilog object", $"policy {Logged(Policy(), "{@P}", new Page(PageNumber.First))}");

        // A default instance is the default of its underlying type.
        report.Line("serilog default", $"opt-in {Logged(OptIn(), "{X} {@D}", Uninitialized<PageNumber>(), Uninitialized<EmailAddress>())}");
    }

    /// <summary>The option, with the domain's assembly named, as an application configuring its logger first does.</summary>
    internal static LoggerConfiguration OptIn()
        => new LoggerConfiguration().Destructure.ValueObjects(static options =>
        {
            options.CaptureAsUnderlyingValue = true;
            options.Assemblies.Add(typeof(Quantity).Assembly);
        });

    internal static LoggerConfiguration Policy() => new LoggerConfiguration().Destructure.ValueObjects();

    internal static LogEvent Capture(LoggerConfiguration configuration, string template, params object?[] values)
    {
        var sink = new CapturingSink();
        using (var logger = configuration.WriteTo.Sink(sink).CreateLogger())
        {
            logger.Information(template, values);
        }

        return sink.Events.Single();
    }

    internal static string Json(LogEventPropertyValue value)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        Formatter.Format(value, writer);
        return writer.ToString();
    }

    private static string Logged(LoggerConfiguration configuration, string template, params object?[] values)
        => string.Join(" ", Capture(configuration, template, values).Properties.Select(property => $"{property.Key}={Json(property.Value)}"));

    private static T Uninitialized<T>()
        where T : struct
    {
        var array = new T[1];
        return array[0];
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    /// <summary>
    /// Logs one value object of each type through the visitor its descriptor accepts, and fails the run when it is not
    /// the scalar of its underlying type, written as the bare value is.
    /// </summary>
    private sealed class SerilogProbe(Report report, string minted) : IValueObjectVisitor<bool>
    {
        public bool Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            var subject = $"serilog {Names.Of(typeof(TSelf))}";
            TSelf? sample = null;
            foreach (var text in Underlying.Texts(typeof(TValue)).Append(minted))
            {
                if (TSelf.TryParse(text, CultureInfo.InvariantCulture, out var parsed, out _))
                {
                    sample = parsed;
                    break;
                }
            }

            if (sample is not { } value)
            {
                report.Fail(subject, "accepts none of the texts it is probed with");
                return false;
            }

            var bare = Capture(new LoggerConfiguration(), "{X} {@D}", value.Value, value.Value);
            var policy = Capture(Policy(), "{X} {@D}", value, value);
            var optIn = Capture(OptIn(), "{X} {@D}", value, value);
            report.Line(subject, $"bare X={Json(bare.Properties["X"])} D={Json(bare.Properties["D"])}; policy X={Json(policy.Properties["X"])} D={Json(policy.Properties["D"])}; opt-in X={Json(optIn.Properties["X"])} D={Json(optIn.Properties["D"])}");

            Expect(subject, "policy D", policy.Properties["D"], bare.Properties["D"], value.Value);
            Expect(subject, "opt-in X", optIn.Properties["X"], bare.Properties["X"], value.Value);
            Expect(subject, "opt-in D", optIn.Properties["D"], bare.Properties["D"], value.Value);
            return true;
        }

        private void Expect<TValue>(string subject, string what, LogEventPropertyValue logged, LogEventPropertyValue bare, TValue value)
        {
            if (logged is not ScalarValue { Value: TValue scalar } || !EqualityComparer<TValue>.Default.Equals(scalar, value) || Json(logged) != Json(bare))
            {
                report.Fail(subject, $"{what} is {Json(logged)}, not the {typeof(TValue).Name} {Json(bare)}");
            }
        }
    }
}
