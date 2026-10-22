using AdCodicem.ValueObjects.Fixtures.Untouched;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.Serilog;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using Serilog;
using Serilog.Events;
using static AdCodicem.ValueObjects.UnitTests.Logging.SerilogCapture;

namespace AdCodicem.ValueObjects.UnitTests.Logging;

/// <summary>
/// <c>Destructure.ValueObjects()</c>: a value object logged with <c>@</c>, alone or inside what is logged with it, is the
/// value it carries, which Serilog writes as it writes that type.
/// </summary>
public sealed class SerilogDestructuringTests
{
    private static readonly Guid Customer = Guid.Parse("0192f4a0-0000-7000-8000-000000000001");

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, is logged as the bare value it carries is,
    /// under the policy with <c>@</c> and under the option either way.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_is_logged_as_the_value_it_carries(string type)
        => Samples.All[type].LogsAsItsUnderlyingValueThroughSerilog();

    /// <summary>
    /// Serilog alone reflects a value object logged with <c>@</c> into a structure of its public properties; the policy
    /// hands it the underlying value instead, a number as a JSON number.
    /// </summary>
    [Fact]
    public void A_value_object_logged_with_at_is_its_underlying_value_where_Serilog_alone_writes_a_wrapper()
    {
        const string Template = "{@Page} {@Amount} {@Customer} {@Consent} {@Iban}";
        object[] values =
        [
            PageNumber.Create(42), Amount.Create(12.5m), CustomerId.Create(Customer), Consent.Create(true),
            Iban.Create("FR7630006000011234567890189"),
        ];

        var bare = Capture(Bare(), Template, values);
        var wrapper = bare.Properties["Page"].Should().BeOfType<StructureValue>().Subject;
        wrapper.TypeTag.Should().Be(nameof(PageNumber));
        wrapper.Properties.Should().Contain(property => property.Name == "Value");

        Compact(Capture(Policy(), Template, values)).Should().Be(
            """{"@mt":"{@Page} {@Amount} {@Customer} {@Consent} {@Iban}","Page":42,"Amount":12.50,"Customer":"0192f4a0-0000-7000-8000-000000000001","Consent":true,"Iban":"FR7630006000011234567890189"}""");
    }

    /// <summary>
    /// A value object met while Serilog destructures an object, a collection, a dictionary or a tuple is its underlying
    /// value too, beside the raw values, which Serilog captures as it always does.
    /// </summary>
    [Fact]
    public void A_value_object_inside_what_is_logged_with_at_is_its_underlying_value()
    {
        var order = new LoggedOrder(PageNumber.Create(42), PageNumber.Create(5), null, Label.Create("hello"), 42);
        var logged = Capture(
            Policy(),
            "{@Order} {@Anonymous} {@Pages} {@ByName} {@Tuple}",
            order,
            new { Page = PageNumber.Create(7), Raw = 7 },
            new List<PageNumber> { PageNumber.Create(1), PageNumber.Create(2) },
            new Dictionary<string, PageNumber> { ["a"] = PageNumber.Create(1) },
            (PageNumber.Create(3), 4));

        JsonOf(logged, "Order").Should().Be("""{"Page":42,"Maybe":5,"Missing":null,"Text":"hello","Raw":42,"$type":"LoggedOrder"}""");
        JsonOf(logged, "Anonymous").Should().Be("""{"Page":7,"Raw":7}""");
        JsonOf(logged, "Pages").Should().Be("[1,2]");
        JsonOf(logged, "ByName").Should().Be("""{"a":1}""");
        JsonOf(logged, "Tuple").Should().Be("[3,4]");
    }

    /// <summary>
    /// Without <c>@</c> Serilog consults no destructuring policy: under the policy alone, a value object is still
    /// captured as the text <c>ToString()</c> writes, which is why the option exists.
    /// </summary>
    [Fact]
    public void Without_at_the_policy_alone_leaves_a_value_object_to_the_text_ToString_writes()
    {
        var logged = Capture(
            Policy(),
            "{Page} {Temperature} {Pages}",
            PageNumber.Create(42),
            Celsius.Create(21),
            new List<PageNumber> { PageNumber.Create(1), PageNumber.Create(2) });

        Compact(logged).Should().Be("""{"@mt":"{Page} {Temperature} {Pages}","Page":"42","Temperature":"21 °C","Pages":["1","2"]}""");
    }

    /// <summary>
    /// A value object written by hand, a type carrying the marker alone and a construction of a generic value object are
    /// all logged with <c>@</c> as what <see cref="IValueObject.GetBoxedValue"/> hands back, with nothing registered.
    /// </summary>
    [Fact]
    public void Every_type_carrying_the_marker_is_logged_with_at_as_the_value_it_hands_back_with_nothing_registered()
    {
        var link = new Uri("https://example.com/a");
        var logged = Capture(
            Policy(),
            "{@Counter} {@Link} {@Unregistered} {@Class} {@Selfless} {@Marker} {@Reference} {@Standing}",
            HandWrittenCounter.Create(5),
            HandWrittenLink.Create(link),
            UnregisteredCode.Create("abc"),
            new ClassBackedValue(),
            new SelflessValue(),
            new MarkerOnlyValue(),
            Reference<PurchaseOrder>.Create("po-1"),
            Standing<PolicyCompetitor>.Create(5));

        Compact(logged).Should().Be(
            """{"@mt":"{@Counter} {@Link} {@Unregistered} {@Class} {@Selfless} {@Marker} {@Reference} {@Standing}","Counter":5,"Link":"https://example.com/a","Unregistered":"abc","Class":"class","Selfless":"selfless","Marker":null,"Reference":"PO-1","Standing":5}""");
        Scalar(logged, "Link").Should().BeSameAs(link);
        JsonOf(logged, "Link").Should().Be(JsonOf(Capture(Bare(), "{@Link}", link), "Link"));
    }

    /// <summary>
    /// Serilog destructures a bare <see cref="Int128"/> or <see cref="UInt128"/>, which it knows as no scalar, into an
    /// empty structure, losing the number; a value object over one keeps it, written as its digits in a string, as
    /// Serilog writes the bare number logged without <c>@</c>.
    /// </summary>
    [Fact]
    public void A_value_object_over_a_128_bit_integer_keeps_the_number_Serilog_drops_from_a_bare_one()
    {
        var balance = LedgerBalance.Create(Int128.Parse("1000000000000000000000", System.Globalization.CultureInfo.InvariantCulture));
        var fingerprint = Fingerprint.Create(UInt128.MaxValue);

        JsonOf(Capture(Bare(), "{@D}", balance.Value), "D").Should().Be("""{"$type":"Int128"}""");
        JsonOf(Capture(Bare(), "{@D}", fingerprint.Value), "D").Should().Be("""{"$type":"UInt128"}""");
        JsonOf(Capture(Bare(), "{X}", balance.Value), "X").Should().Be("\"1000000000000000000000\"");

        JsonOf(Capture(Policy(), "{@D}", balance), "D").Should().Be("\"1000000000000000000000\"");
        JsonOf(Capture(Policy(), "{@D}", fingerprint), "D").Should().Be("\"340282366920938463463374607431768211455\"");
    }

    /// <summary>
    /// The policy hands Serilog the value itself, which it writes whole: <c>ToMaximumStringLength</c> shortens a bare
    /// string logged with <c>@</c>, not a value object over one.
    /// </summary>
    [Fact]
    public void A_value_object_over_text_is_logged_whole_under_a_maximum_string_length()
    {
        var label = Label.Create("abcdefghijklmnopqrstuvwxyz");

        JsonOf(Capture(Policy().Destructure.ToMaximumStringLength(10), "{@D}", label), "D").Should().Be("\"abcdefghijklmnopqrstuvwxyz\"");
        JsonOf(Capture(Policy().Destructure.ToMaximumStringLength(10), "{@D}", label.Value), "D").Should().Be("\"abcdefghi…\"");
    }

    /// <summary>
    /// Serilog asks its scalar types before any destructuring policy: a value object the application makes a scalar
    /// without the option, which unwraps it afterwards, is written through <c>ToString()</c>, with <c>@</c> too.
    /// </summary>
    [Fact]
    public void A_value_object_made_a_scalar_without_the_option_is_written_as_its_text()
        => JsonOf(Capture(Policy().Destructure.AsScalar<PageNumber>(), "{@D}", PageNumber.Create(42)), "D").Should().Be("\"42\"");

    /// <summary>
    /// Serilog runs destructuring policies in the order they were declared, and asks its scalar types before any: an
    /// application's own transformation of a value object, a mask, applies when declared before the package's policy,
    /// not after it, and never under the option, which makes every registered value object a scalar, nested ones
    /// included.
    /// </summary>
    [Fact]
    public void An_application_s_own_destructuring_of_a_value_object_applies_only_before_the_policy_and_never_under_the_option()
    {
        var iban = Iban.Create("FR7630006000011234567890189");
        var masked = Mask(iban);
        masked.Should().NotContain("1234567890");

        Logged(Bare().Destructure.ByTransforming<Iban>(Mask).Destructure.ValueObjects()).Should().Equal(masked, masked);
        Logged(Policy().Destructure.ByTransforming<Iban>(Mask)).Should().Equal(iban.Value, iban.Value);
        Logged(Bare().Destructure.ByTransforming<Iban>(Mask).Destructure.ValueObjects(static options => options.CaptureAsUnderlyingValue = true))
            .Should().Equal(iban.Value, iban.Value);

        IEnumerable<object?> Logged(LoggerConfiguration configuration)
        {
            var logged = Capture(configuration, "{@Iban} {@Holder}", iban, new { Iban = iban });
            var holder = logged.Properties["Holder"].Should().BeOfType<StructureValue>().Subject;
            return [Scalar(logged, "Iban"), holder.Properties.Should().ContainSingle().Which.Value.Should().BeOfType<ScalarValue>().Subject.Value];
        }

        static string Mask(Iban value) => value.ToString(Iban.Formats.Masked, null);
    }

    /// <summary>
    /// Serilog captures a dictionary as one only when its key is of a type it knows as a scalar; a dictionary keyed by a
    /// value object is a list of pairs, unless the application declares it a dictionary, which the policy then makes
    /// possible with <c>@</c>, its keys being scalars.
    /// </summary>
    [Fact]
    public void A_dictionary_keyed_by_a_value_object_is_a_dictionary_once_the_application_declares_it_one()
    {
        var byPage = new Dictionary<PageNumber, int> { [PageNumber.Create(7)] = 1 };

        JsonOf(Capture(Policy(), "{@D}", byPage), "D").Should().Be("""[{"Key":7,"Value":1,"$type":"KeyValuePair`2"}]""");
        JsonOf(Capture(Policy().Destructure.AsDictionary<Dictionary<PageNumber, int>>(), "{@D}", byPage), "D").Should().Be("""{"7":1}""");
        JsonOf(Capture(Bare().Destructure.AsDictionary<Dictionary<PageNumber, int>>(), "{@D}", byPage), "D")
            .Should().Be("\"Capturing the property value threw an exception: InvalidCastException\"");
    }

    /// <summary>
    /// A default instance, which no rule accepted, is logged as the default of its underlying type, under the policy and
    /// under the option: nothing reads a log back into a value object.
    /// </summary>
    [Fact]
    public void A_default_instance_is_logged_as_the_default_of_its_underlying_type()
    {
        object[] defaults = [Uninitialized<PageNumber>(), Uninitialized<PhoneNumber>(), Uninitialized<Consent>()];

        foreach (var configuration in new Func<LoggerConfiguration>[] { Policy, OptIn })
        {
            var logged = Capture(configuration(), "{@Page} {@Phone} {@Consent}", defaults);
            Compact(logged).Should().Be("""{"@mt":"{@Page} {@Phone} {@Consent}","Page":0,"Phone":"","Consent":false}""");
        }

        Compact(Capture(OptIn(), "{Page} {Phone} {Consent}", defaults))
            .Should().Be("""{"@mt":"{Page} {Phone} {Consent}","Page":0,"Phone":"","Consent":false}""");
    }

    /// <summary>
    /// A value object whose formatting hook writes more than the number is logged as the number: the hook is not what a
    /// log carries, and stringification, <c>{$T}</c>, is what writes its text.
    /// </summary>
    [Fact]
    public void A_value_object_with_a_formatting_hook_is_logged_with_at_as_its_underlying_value()
    {
        var temperature = Celsius.Create(21);

        var logged = Capture(Policy(), "{@T} {$S}", temperature, temperature);

        JsonOf(logged, "T").Should().Be("21");
        JsonOf(logged, "S").Should().Be("\"21 °C\"");
        Message(logged).Should().Be("21 \"21 °C\"");
    }

    /// <summary>
    /// <see cref="AnyEntityId"/> is no value object: Serilog destructures it with <c>@</c> as a struct of its public
    /// properties, and captures it without <c>@</c> as its text, the identifier.
    /// </summary>
    [Fact]
    public void An_identifier_of_any_kind_is_no_value_object_and_is_logged_as_its_text_without_at()
    {
        var account = AccountId.New();
        var any = AnyEntityId.Parse(account.Value, null);

        var logged = Capture(OptIn(), "{@Destructured} {Text}", any, any);

        logged.Properties["Destructured"].Should().BeOfType<StructureValue>().Which.TypeTag.Should().Be(nameof(AnyEntityId));
        Scalar(logged, "Text").Should().Be(account.Value);
    }

    /// <summary>Called twice, with and without the option, the method keeps one behaviour: the option's.</summary>
    [Fact]
    public void Calling_the_method_again_changes_nothing_it_already_set_up()
    {
        var configuration = new LoggerConfiguration()
            .Destructure.ValueObjects()
            .Destructure.ValueObjects(static options => options.CaptureAsUnderlyingValue = true)
            .Destructure.ValueObjects(static options => options.CaptureAsUnderlyingValue = true);

        Compact(Capture(configuration, "{X} {@D}", PageNumber.Create(42), PageNumber.Create(42)))
            .Should().Be("""{"@mt":"{X} {@D}","X":42,"D":42}""");
    }

    /// <summary>An instance no rule accepted, as an array holds it before anything is stored in it.</summary>
    private static T Uninitialized<T>()
        where T : struct
    {
        var array = new T[1];
        return array[0];
    }

    /// <summary>A competitor no other test ranks, so that the construction is one nothing has registered.</summary>
    private sealed class PolicyCompetitor;
}

/// <summary>An object destructured with the value objects it holds.</summary>
/// <param name="Page">A value object.</param>
/// <param name="Maybe">A nullable value object holding one.</param>
/// <param name="Missing">A nullable value object holding none.</param>
/// <param name="Text">A value object over text.</param>
/// <param name="Raw">A raw value.</param>
public sealed record LoggedOrder(PageNumber Page, PageNumber? Maybe, PageNumber? Missing, Label Text, int Raw);
