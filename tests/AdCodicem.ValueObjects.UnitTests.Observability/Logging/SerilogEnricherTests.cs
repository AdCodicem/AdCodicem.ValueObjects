using AdCodicem.ValueObjects.Serilog;
using Serilog.Events;
using static AdCodicem.ValueObjects.UnitTests.Logging.SerilogCapture;

namespace AdCodicem.ValueObjects.UnitTests.Logging;

/// <summary>
/// The enricher the option adds, called directly: it replaces a value object captured as a scalar with its underlying
/// value wherever an event holds one, and rebuilds nothing that holds none.
/// </summary>
public sealed class SerilogEnricherTests
{
    private static readonly ScalarValue Raw = new(7);

    private static ScalarValue Page(int value) => new(PageNumber.Create(value));

    /// <summary>A value Serilog captured that holds no value object is handed back as it is.</summary>
    [Fact]
    public void A_value_holding_no_value_object_is_handed_back_as_it_is()
    {
        LogEventPropertyValue[] values =
        [
            Raw,
            new StructureValue([new LogEventProperty("A", Raw)], "Tagged"),
            new SequenceValue([Raw, new SequenceValue([Raw])]),
            new DictionaryValue([new(new ScalarValue("a"), Raw)]),
            new OtherValue(),
        ];

        foreach (var value in values)
        {
            ValueObjectEnricher.Unwrap(value).Should().BeSameAs(value);
        }
    }

    /// <summary>A scalar holding a value object becomes the scalar of its underlying value.</summary>
    [Fact]
    public void A_scalar_holding_a_value_object_becomes_its_underlying_value()
        => ValueObjectEnricher.Unwrap(Page(42)).Should().BeOfType<ScalarValue>().Which.Value.Should().Be(42);

    /// <summary>
    /// A structure is rebuilt with every property holding a value object replaced, the others kept as they were, in
    /// their order, under the same type tag.
    /// </summary>
    [Fact]
    public void A_structure_is_rebuilt_with_its_value_objects_replaced_and_everything_else_kept()
    {
        var both = new StructureValue([new LogEventProperty("A", Page(1)), new LogEventProperty("B", Page(2))], "Pair");
        var second = new StructureValue([new LogEventProperty("A", Raw), new LogEventProperty("B", Page(2)), new LogEventProperty("C", Raw)], "Trio");

        var unwrappedBoth = ValueObjectEnricher.Unwrap(both).Should().BeOfType<StructureValue>().Subject;
        var unwrappedSecond = ValueObjectEnricher.Unwrap(second).Should().BeOfType<StructureValue>().Subject;

        unwrappedBoth.TypeTag.Should().Be("Pair");
        unwrappedBoth.Properties.Select(static property => (property.Name, ((ScalarValue)property.Value).Value))
            .Should().Equal(("A", (object?)1), ("B", 2));
        unwrappedSecond.TypeTag.Should().Be("Trio");
        unwrappedSecond.Properties.Select(static property => property.Name).Should().Equal("A", "B", "C");
        unwrappedSecond.Properties[0].Should().BeSameAs(second.Properties[0]);
        ((ScalarValue)unwrappedSecond.Properties[1].Value).Value.Should().Be(2);
        unwrappedSecond.Properties[2].Should().BeSameAs(second.Properties[2]);
    }

    /// <summary>A sequence is rebuilt with every element holding a value object replaced, the others kept.</summary>
    [Fact]
    public void A_sequence_is_rebuilt_with_its_value_objects_replaced_and_everything_else_kept()
    {
        var both = new SequenceValue([Page(1), Page(2)]);
        var second = new SequenceValue([Raw, Page(2), Raw]);

        var unwrappedBoth = ValueObjectEnricher.Unwrap(both).Should().BeOfType<SequenceValue>().Subject;
        var unwrappedSecond = ValueObjectEnricher.Unwrap(second).Should().BeOfType<SequenceValue>().Subject;

        unwrappedBoth.Elements.Select(static element => ((ScalarValue)element).Value).Should().Equal(1, 2);
        unwrappedSecond.Elements[0].Should().BeSameAs(Raw);
        ((ScalarValue)unwrappedSecond.Elements[1]).Value.Should().Be(2);
        unwrappedSecond.Elements[2].Should().BeSameAs(Raw);
    }

    /// <summary>
    /// A dictionary is rebuilt with every key and every value holding a value object replaced, the entries before the
    /// first one changed kept as they were, and the order of its entries kept.
    /// </summary>
    [Fact]
    public void A_dictionary_is_rebuilt_with_its_value_object_keys_and_values_replaced_and_everything_else_kept()
    {
        var first = new KeyValuePair<ScalarValue, LogEventPropertyValue>(new ScalarValue("first"), Raw);
        var dictionary = new DictionaryValue(
        [
            first,
            new(new ScalarValue("value"), Page(2)),
            new(Page(3), Raw),
            new(Page(4), Page(5)),
            new(new ScalarValue("last"), Raw),
        ]);

        var unwrapped = ValueObjectEnricher.Unwrap(dictionary).Should().BeOfType<DictionaryValue>().Subject;

        unwrapped.Elements.Select(static element => (element.Key.Value, ((ScalarValue)element.Value).Value))
            .Should().Equal(("first", (object?)7), ("value", 2), (3, 7), (4, 5), ("last", 7));
        unwrapped.Elements.Should().ContainKey(first.Key).WhoseValue.Should().BeSameAs(Raw);
    }

    /// <summary>A dictionary whose first entry holds a value object as its key alone is rebuilt from that entry on.</summary>
    [Fact]
    public void A_dictionary_whose_first_key_alone_is_a_value_object_is_rebuilt()
    {
        var dictionary = new DictionaryValue([new(Page(3), Raw), new(new ScalarValue("b"), new SequenceValue([Page(1)]))]);

        var unwrapped = ValueObjectEnricher.Unwrap(dictionary).Should().BeOfType<DictionaryValue>().Subject;

        unwrapped.Elements.Keys.Select(static key => key.Value).Should().Equal(3, "b");
        ((ScalarValue)((SequenceValue)unwrapped.Elements.Values.Last()).Elements[0]).Value.Should().Be(1);
    }

    /// <summary>
    /// A key holding a value object is kept as it is when another key of the dictionary already is its underlying value,
    /// whether that key comes before it or after it, or is another value object's unwrapped first: a dictionary holds no
    /// two equal keys, and its values are replaced all the same.
    /// </summary>
    [Fact]
    public void A_key_whose_underlying_value_another_key_already_is_is_kept()
    {
        var rawFirst = new DictionaryValue([new(new ScalarValue(1), Page(2)), new(Page(1), Raw)]);
        var rawLast = new DictionaryValue([new(Page(1), Page(2)), new(new ScalarValue(1), Raw)]);
        var twoValueObjects = new DictionaryValue([new(Page(1), Page(2)), new(new ScalarValue(Celsius.Create(1)), Raw), new(Page(3), Raw)]);

        Entries(rawFirst).Should().Equal(((object?)1, (object?)2), (PageNumber.Create(1), 7));
        Entries(rawLast).Should().Equal(((object?)PageNumber.Create(1), (object?)2), (1, 7));
        Entries(twoValueObjects).Should().Equal(((object?)1, (object?)2), (Celsius.Create(1), 7), (3, 7));

        static IEnumerable<(object? Key, object? Value)> Entries(DictionaryValue dictionary)
            => ValueObjectEnricher.Unwrap(dictionary).Should().BeOfType<DictionaryValue>().Subject.Elements
                .Select(static element => (element.Key.Value, ((ScalarValue)element.Value).Value));
    }

    /// <summary>
    /// A value object several levels deep is replaced too: a structure holding a sequence of structures, and a sequence
    /// holding a structure that holds a sequence, are rebuilt on every level leading to one.
    /// </summary>
    [Fact]
    public void A_value_object_several_levels_deep_is_replaced_on_every_level()
    {
        var line = new StructureValue([new LogEventProperty("Qty", Page(1))], "Line");
        var structure = new StructureValue([new LogEventProperty("Lines", new SequenceValue([line]))], "Basket");
        var sequence = new SequenceValue([new StructureValue([new LogEventProperty("Pages", new SequenceValue([Page(2)]))], "Book")]);

        JsonOf(ValueObjectEnricher.Unwrap(structure)).Should().Be("""{"Lines":[{"Qty":1,"$type":"Line"}],"$type":"Basket"}""");
        JsonOf(ValueObjectEnricher.Unwrap(sequence)).Should().Be("""[{"Pages":[2],"$type":"Book"}]""");
    }

    /// <summary>
    /// The enricher replaces only the properties of an event that hold a value object, in place, and leaves an event
    /// holding none as it found it.
    /// </summary>
    [Fact]
    public void The_enricher_replaces_only_the_properties_holding_a_value_object()
    {
        var raw = new LogEventProperty("Raw", Raw);
        var nested = new LogEventProperty("Nested", new SequenceValue([Raw]));
        var untouched = new LogEvent(DateTimeOffset.UnixEpoch, LogEventLevel.Information, null, MessageTemplate.Empty, [raw, nested]);
        var touched = new LogEvent(
            DateTimeOffset.UnixEpoch,
            LogEventLevel.Information,
            null,
            MessageTemplate.Empty,
            [raw, new LogEventProperty("Page", Page(42)), new LogEventProperty("Pages", new SequenceValue([Page(1)]))]);

        ValueObjectEnricher.Instance.Enrich(untouched, null!);
        ValueObjectEnricher.Instance.Enrich(touched, null!);

        untouched.Properties["Raw"].Should().BeSameAs(Raw);
        untouched.Properties["Nested"].Should().BeSameAs(nested.Value);
        touched.Properties.Keys.Should().Equal("Raw", "Page", "Pages");
        touched.Properties["Raw"].Should().BeSameAs(Raw);
        ((ScalarValue)touched.Properties["Page"]).Value.Should().Be(42);
        ((ScalarValue)((SequenceValue)touched.Properties["Pages"]).Elements[0]).Value.Should().Be(1);
    }

    /// <summary>The enricher refuses a missing event.</summary>
    [Fact]
    public void The_enricher_refuses_a_missing_event()
        => FluentActions.Invoking(static () => ValueObjectEnricher.Instance.Enrich(null!, null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("logEvent");

    /// <summary>The policy refuses nothing and hands back nothing for a value that is no value object.</summary>
    [Fact]
    public void The_policy_declines_a_value_that_is_no_value_object()
    {
        ValueObjectDestructuringPolicy.Instance.TryDestructure(42, null!, out var declined).Should().BeFalse();
        declined.Should().BeNull();
        ValueObjectDestructuringPolicy.Instance.TryDestructure(PageNumber.Create(42), null!, out var unwrapped).Should().BeTrue();
        unwrapped.Should().BeOfType<ScalarValue>().Which.Value.Should().Be(42);
    }

    /// <summary>A kind of value an application's own enricher may add, which the enricher does not know.</summary>
    private sealed class OtherValue : LogEventPropertyValue
    {
        public override void Render(TextWriter output, string? format = null, IFormatProvider? formatProvider = null)
            => output.Write("other");
    }
}
