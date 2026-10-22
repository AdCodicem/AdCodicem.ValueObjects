using System.Globalization;
using AdCodicem.ValueObjects.Fixtures.Untouched;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Serilog;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Templates;
using static AdCodicem.ValueObjects.UnitTests.Logging.SerilogCapture;

namespace AdCodicem.ValueObjects.UnitTests.Logging;

/// <summary>
/// <see cref="ValueObjectLoggingOptions.CaptureAsUnderlyingValue"/>: a value object logged without <c>@</c> is the value
/// it carries too, for a formatter, a filter, a template and a format in the message.
/// </summary>
public sealed class SerilogCaptureAsUnderlyingValueTests
{
    /// <summary>
    /// With the option, a value object logged without <c>@</c> is the number it carries, which Serilog's compact
    /// formatter writes as a JSON number and a text template renders as the bare number.
    /// </summary>
    [Fact]
    public void A_value_object_logged_without_at_is_its_underlying_value_in_compact_JSON_and_in_text()
    {
        var page = PageNumber.Create(42);
        var amount = Amount.Create(12.5m);

        var logged = Capture(OptIn(), "{Page} {@Destructured} {Amount} {Iban}", page, page, amount, Iban.Create("FR7630006000011234567890189"));

        Compact(logged).Should().Be(
            """{"@mt":"{Page} {@Destructured} {Amount} {Iban}","Page":42,"Destructured":42,"Amount":12.50,"Iban":"FR7630006000011234567890189"}""");
        Scalar(logged, "Page").Should().BeOfType<int>();
        Scalar(logged, "Amount").Should().BeOfType<decimal>();
        Message(logged).Should().Be("42 42 12.50 \"FR7630006000011234567890189\"");
        Literal(logged).Should().Be("42 42 12.50 FR7630006000011234567890189");

        Compact(Capture(Policy(), "{Page} {Amount}", page, amount)).Should().Be("""{"@mt":"{Page} {Amount}","Page":"42","Amount":"12.50"}""");
    }

    /// <summary>
    /// A Serilog.Expressions filter compares the number a value object carries only once it is captured as that number:
    /// without the option the event is dropped, as Serilog captured its text; with it the event is kept, as it is for a
    /// raw <see cref="int"/>, and the policy alone keeps a value object logged with <c>@</c>.
    /// </summary>
    [Fact]
    public void A_filter_comparing_a_number_keeps_an_event_carrying_a_value_object_once_it_is_its_number()
    {
        Kept(Bare()).Should().Equal("raw {X}");
        Kept(Policy()).Should().Equal("raw {X}", "destructured {@X}");
        Kept(OptIn()).Should().Equal("value object {X}", "raw {X}", "destructured {@X}");

        static IEnumerable<string> Kept(LoggerConfiguration configuration)
        {
            var sink = new CapturingSink();
            using (var logger = configuration.Filter.ByIncludingOnly("X > 10").WriteTo.Sink(sink).CreateLogger())
            {
                logger.Information("value object {X}", PageNumber.Create(42));
                logger.Information("raw {X}", 42);
                logger.Information("destructured {@X}", PageNumber.Create(42));
            }

            return sink.Events.Select(static logEvent => logEvent.MessageTemplate.Text);
        }
    }

    /// <summary>A Serilog.Expressions template sees the type of the underlying value with the option, text without it.</summary>
    [Fact]
    public void An_expression_template_sees_the_type_of_the_underlying_value()
    {
        Typed(OptIn()).Should().Be("System.Int32 42");
        Typed(Policy()).Should().Be("System.String 42");

        static string Typed(LoggerConfiguration configuration)
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            new ExpressionTemplate("{TypeOf(X)} {X}").Format(Capture(configuration, "{X}", PageNumber.Create(42)), writer);
            return writer.ToString();
        }
    }

    /// <summary>
    /// A value object whose formatting hook writes more than the number is logged as the number, to which a format in
    /// the template applies; stringification, <c>{$T}</c>, writes the hook's text. A named format of a value object over
    /// text, a mask included, is a format of the string it carries, which Serilog ignores, with the option or not.
    /// </summary>
    [Fact]
    public void A_value_object_with_a_formatting_hook_is_logged_as_its_underlying_value_to_which_formats_apply()
    {
        var temperature = Celsius.Create(21);
        var floor = Floor.Create(3);
        var iban = Iban.Create("FR7630006000011234567890189");

        var logged = Capture(OptIn(), "{T} {T:000} {$S} {F} {$G} {Iban:M}", temperature, temperature, temperature, floor, floor, iban);

        JsonOf(logged, "T").Should().Be("21");
        JsonOf(logged, "F").Should().Be("3");
        Message(logged).Should().Be("21 021 \"21 °C\" 3 \"floor 3\" \"FR7630006000011234567890189\"");
        Message(Capture(Policy(), "{T:000} {Iban:M}", temperature, iban)).Should().Be("\"21 °C\" \"FR7630006000011234567890189\"");
        Message(Capture(OptIn(), "{Iban}", iban.ToString(Iban.Formats.Masked, null))).Should().NotContain("1234567890");
    }

    /// <summary>
    /// With the option, a value object Serilog captures as a scalar inside an object destructured with <c>@</c>, a list,
    /// an array, a dictionary's values and keys is replaced with its underlying value, and what surrounds it is kept: the
    /// type tag of a structure among it.
    /// </summary>
    [Fact]
    public void Every_value_object_an_event_holds_is_replaced_wherever_it_is()
    {
        var order = new LoggedOrder(PageNumber.Create(42), PageNumber.Create(5), null, Label.Create("hello"), 42);
        var byTemperature = new Dictionary<Celsius, int> { [Celsius.Create(21)] = 1 };
        var logged = Capture(
            OptIn().Destructure.AsDictionary<Dictionary<Celsius, int>>(),
            "{@Order} {List} {Array} {ByName} {ByTemperature}",
            order,
            new List<PageNumber> { PageNumber.Create(42), PageNumber.Create(42) },
            new[] { PageNumber.Create(1), PageNumber.Create(2) },
            new Dictionary<string, PageNumber> { ["a"] = PageNumber.Create(1) },
            byTemperature);

        JsonOf(logged, "Order").Should().Be("""{"Page":42,"Maybe":5,"Missing":null,"Text":"hello","Raw":42,"$type":"LoggedOrder"}""");
        JsonOf(logged, "List").Should().Be("[42,42]");
        JsonOf(logged, "Array").Should().Be("[1,2]");
        JsonOf(logged, "ByName").Should().Be("""{"a":1}""");
        JsonOf(logged, "ByTemperature").Should().Be("""{"21":1}""");
        JsonOf(Capture(Policy().Destructure.AsDictionary<Dictionary<Celsius, int>>(), "{D}", byTemperature), "D").Should().Be("""{"21 °C":1}""");
    }

    /// <summary>
    /// With the option, a value object several levels deep is replaced too: in the structures a sequence of an object
    /// destructured with <c>@</c> holds, in a dictionary's structures, and in a sequence of sequences logged without
    /// <c>@</c>. The policy alone gives the same object with <c>@</c>, Serilog meeting each value object itself.
    /// </summary>
    [Fact]
    public void A_value_object_several_levels_deep_is_replaced_too()
    {
        var basket = new LoggedBasket(
            [new LoggedLine(PageNumber.Create(1)), new LoggedLine(PageNumber.Create(2))],
            new Dictionary<string, LoggedLine> { ["a"] = new(PageNumber.Create(3)) },
            [[PageNumber.Create(4)]]);
        const string Expected = """{"Lines":[{"Qty":1,"$type":"LoggedLine"},{"Qty":2,"$type":"LoggedLine"}],"ByName":{"a":{"Qty":3,"$type":"LoggedLine"}},"Nested":[[4]],"$type":"LoggedBasket"}""";

        var logged = Capture(OptIn(), "{@Basket} {Nested}", basket, new List<List<PageNumber>> { new() { PageNumber.Create(5) } });

        JsonOf(logged, "Basket").Should().Be(Expected);
        JsonOf(logged, "Nested").Should().Be("[[5]]");
        JsonOf(Capture(Policy(), "{@Basket}", basket), "Basket").Should().Be(Expected);
    }

    /// <summary>
    /// A dictionary whose keys meet once unwrapped, a value object over 1 beside a raw 1, keeps the key the other already
    /// is, and every other value object of the event is still replaced: the enricher does not throw, which Serilog would
    /// swallow, leaving them all their text.
    /// </summary>
    [Fact]
    public void Keys_that_meet_once_unwrapped_leave_the_rest_of_the_event_replaced()
    {
        var logged = Capture(
            OptIn().Destructure.AsDictionary<Dictionary<object, int>>(),
            "{D} {P}",
            new Dictionary<object, int> { [PageNumber.Create(1)] = 1, [1] = 2 },
            PageNumber.Create(42));

        Scalar(logged, "P").Should().Be(42);
        logged.Properties["D"].Should().BeOfType<DictionaryValue>().Which.Elements.Keys.Select(static key => key.Value)
            .Should().Equal(PageNumber.Create(1), 1);
        JsonOf(logged, "D").Should().Be("""{"1":1,"1":2}""");
    }

    /// <summary>
    /// The option makes a scalar of every value object registered when the method runs: a construction of a generic
    /// value object that nothing registered or resolved before then, and a value object written by hand that nothing
    /// registered, are captured as their text until the application makes them scalars, which the enricher then unwraps.
    /// </summary>
    [Fact]
    public void A_value_object_nothing_registered_is_its_underlying_value_once_the_application_makes_it_a_scalar()
    {
        var standing = Standing<OptInCompetitor>.Create(5);
        var counter = HandWrittenCounter.Create(5);

        Compact(Capture(OptIn(), "{Standing} {Unregistered}", standing, UnregisteredCode.Create("abc")))
            .Should().Be("""{"@mt":"{Standing} {Unregistered}","Standing":"5","Unregistered":"abc"}""");
        var scalar = OptIn().Destructure.AsScalar<Standing<OptInCompetitor>>().Destructure.AsScalar<HandWrittenCounter>();
        var logged = Capture(scalar, "{Standing} {Counter}", standing, counter);
        Compact(logged).Should().Be("""{"@mt":"{Standing} {Counter}","Standing":5,"Counter":5}""");
        Scalar(logged, "Standing").Should().BeOfType<int>();
    }

    /// <summary>
    /// A construction of a generic value object registered by hand before the method runs, as native AOT asks, or
    /// resolved by then, is in the registry, and the option makes it a scalar as any other: it is its underlying value
    /// with nothing more declared.
    /// </summary>
    [Fact]
    public void A_construction_registered_or_resolved_before_the_method_runs_is_its_underlying_value()
    {
        ValueObjectRegistry.Register<Standing<RegisteredCompetitor>, int>(static () => new Standing<RegisteredCompetitor>.ValueJsonConverter());
        ValueObjectRegistry.TryResolve(typeof(Standing<ResolvedCompetitor>), out _).Should().BeTrue();

        var logged = Capture(OptIn(), "{Registered} {Resolved}", Standing<RegisteredCompetitor>.Create(5), Standing<ResolvedCompetitor>.Create(6));

        Compact(logged).Should().Be("""{"@mt":"{Registered} {Resolved}","Registered":5,"Resolved":6}""");
        Scalar(logged, "Registered").Should().BeOfType<int>();
        Scalar(logged, "Resolved").Should().BeOfType<int>();
    }

    /// <summary>
    /// The enricher replaces what the enrichers before it added: a property pushed on the log context is the underlying
    /// value when <c>Enrich.FromLogContext()</c> comes first, and the value object's text when it comes after, even
    /// pushed to be destructured. A property given through <c>ForContext</c> is added by a child logger, whose enrichers
    /// run before the root's, and is the underlying value either way.
    /// </summary>
    /// <param name="contextFirst">Whether <c>Enrich.FromLogContext()</c> is called before the method.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_property_an_enricher_adds_is_its_underlying_value_when_that_enricher_comes_first(bool contextFirst)
    {
        var page = PageNumber.Create(42);
        var configuration = new LoggerConfiguration();
        if (contextFirst)
        {
            configuration = configuration.Enrich.FromLogContext();
        }

        configuration = configuration.Destructure.ValueObjects(static options => options.CaptureAsUnderlyingValue = true);
        if (!contextFirst)
        {
            configuration = configuration.Enrich.FromLogContext();
        }

        var sink = new CapturingSink();
        using (var logger = configuration.WriteTo.Sink(sink).CreateLogger())
        {
            using (LogContext.PushProperty("Context", page))
            using (LogContext.PushProperty("Destructured", page, destructureObjects: true))
            {
                logger.Information("pushed");
            }

            logger.ForContext("Child", page).Information("child");
        }

        var expected = contextFirst ? "42" : "\"42\"";
        JsonOf(sink.Events[0], "Context").Should().Be(expected);
        JsonOf(sink.Events[0], "Destructured").Should().Be(expected);
        JsonOf(sink.Events[1], "Child").Should().Be("42");
    }

    /// <summary>A competitor no other test ranks, so that the construction is one nothing has registered.</summary>
    private sealed class OptInCompetitor;

    /// <summary>A competitor whose construction only this class registers, by hand.</summary>
    private sealed class RegisteredCompetitor;

    /// <summary>A competitor whose construction only this class resolves.</summary>
    private sealed class ResolvedCompetitor;
}

/// <summary>A line of a basket, holding a value object.</summary>
/// <param name="Qty">A value object.</param>
public sealed record LoggedLine(PageNumber Qty);

/// <summary>An object holding value objects several levels deep.</summary>
/// <param name="Lines">A list of objects holding a value object.</param>
/// <param name="ByName">A dictionary of objects holding a value object.</param>
/// <param name="Nested">A list of lists of value objects.</param>
public sealed record LoggedBasket(List<LoggedLine> Lines, Dictionary<string, LoggedLine> ByName, List<List<PageNumber>> Nested);
