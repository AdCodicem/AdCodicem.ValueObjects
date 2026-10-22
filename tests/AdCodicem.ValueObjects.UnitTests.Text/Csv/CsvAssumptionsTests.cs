using System.Globalization;
using AdCodicem.ValueObjects.CsvHelper;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using CsvMissingFieldException = CsvHelper.MissingFieldException;

namespace AdCodicem.ValueObjects.UnitTests.Csv;

/// <summary>
/// What CsvHelper does on its own, which the package rests on or the guide warns of: a version of CsvHelper changing one
/// of these fails here first.
/// </summary>
public sealed class CsvAssumptionsTests
{
    private const string Text = CsvOrder.Header + "\n" + CsvOrder.ValidRow + "\n";

    [Fact]
    public void Without_the_package_CsvHelper_writes_Value_columns_and_reads_default_instances_with_nothing_thrown()
    {
        Header(CsvHarness.Write([CsvOrder.Valid], setup: CsvHarness.Nothing)).Should().Contain("Value").And.NotContain(["Id", "Iban", "Quantity", "Amount", "Due", "Country"]);

        var order = CsvHarness.Read<CsvOrder>(Text, setup: CsvHarness.Nothing).Single();
        order.Iban.IsUninitialized().Should().BeTrue();
        order.Quantity.IsUninitialized().Should().BeTrue();
        new TypeConverterCache().GetConverter<Iban>().GetType().Should().Be<DefaultTypeConverter>();
    }

    [Fact]
    public void Without_the_package_a_class_map_reads_nothing_and_says_so_with_no_code()
    {
        var refusal = CsvHarness.Refusal<CsvOrder>(Text, setup: static context => context.RegisterClassMap<CsvPlainMap>());

        refusal.FirstLine().Should().Be("The conversion cannot be performed.");
        refusal.Code().Should().BeNull();
    }

    [Fact]
    public void Converters_added_after_the_first_read_of_a_context_change_nothing()
    {
        using var reader = new StringReader(Text + CsvOrder.ValidRow + "\n");
        using var csv = new CsvReader(reader, CsvHarness.Configuration());
        csv.Read();
        csv.ReadHeader();
        csv.Read();
        csv.GetRecord<CsvOrder>().Iban.IsUninitialized().Should().BeTrue();

        csv.Context.TypeConverterCache.AddValueObjects();
        csv.Read();

        csv.GetRecord<CsvOrder>().Iban.IsUninitialized().Should().BeTrue();
    }

    [Fact]
    public void A_class_map_registered_before_the_converters_keeps_the_converter_it_found()
    {
        var refusal = CsvHarness.Refusal<CsvOrder>(Text, setup: static context =>
        {
            context.RegisterClassMap<CsvPlainMap>();
            context.TypeConverterCache.AddValueObjects();
        });

        refusal.FirstLine().Should().Be("The conversion cannot be performed.");
        refusal.Code().Should().BeNull();
        CsvHarness.Read<CsvOrder>(Text, setup: static context =>
        {
            context.TypeConverterCache.AddValueObjects();
            context.RegisterClassMap<CsvPlainMap>();
        }).Single().Iban.Should().Be(CsvOrder.Valid.Iban);
    }

    [Fact]
    public void A_class_map_calling_AutoMap_with_a_culture_builds_a_context_of_its_own_which_has_no_converter()
    {
        static void Mapped(CsvContext context)
        {
            CsvHarness.Factory(context);
            context.RegisterClassMap<CsvAutoMapCultureMap>();
        }

        Header(CsvHarness.Write([CsvOrder.Valid], setup: Mapped)).Should().Contain("Value").And.NotContain(["Id", "Iban", "Quantity", "Amount", "Due", "Country"]);
        var order = CsvHarness.Read<CsvOrder>(Text, setup: Mapped).Single();
        order.Iban.IsUninitialized().Should().BeTrue();
        order.Quantity.IsUninitialized().Should().BeTrue();
    }

    [Fact]
    public void A_class_map_calling_AutoMap_over_a_context_given_the_factory_reads_when_the_reader_has_the_factory_too()
    {
        CsvHarness.Read<CsvOrder>(Text, setup: static context =>
        {
            CsvHarness.Factory(context);
            context.RegisterClassMap<CsvAutoMapContextMap>();
        }).Single().Should().BeEquivalentTo(CsvOrder.Valid);

        CsvHarness.Refusal<CsvOrder>(Text, setup: static context => context.RegisterClassMap<CsvAutoMapContextMap>())
            .FirstLine().Should().Be("The conversion cannot be performed.", "the reader's context copies its own converters into the map");
    }

    [Fact]
    public void A_map_auto_mapped_from_the_reader_context_after_the_converters_then_customized_reads()
    {
        var order = CsvHarness.Read<CsvOrder>(Text.Replace("Quantity", "Count", StringComparison.Ordinal), setup: static context =>
        {
            CsvHarness.Factory(context);
            context.AutoMap<CsvOrder>().Map(order => order.Quantity).Name("Count");
        }).Single();

        order.Should().BeEquivalentTo(CsvOrder.Valid);
    }

    [Fact]
    public void A_class_map_naming_the_converter_of_a_member_reads_with_nothing_registered()
    {
        var order = CsvHarness.Read<CsvOrder>("Iban,Quantity\nFR7630006000011234567890189,3\n", setup: static context => context.RegisterClassMap<CsvNamedMap>()).Single();

        order.Iban.Should().Be(CsvOrder.Valid.Iban);
        order.Quantity.Should().Be(CsvOrder.Valid.Quantity);
        CsvHarness.Refusal<CsvOrder>("Iban,Quantity\nXX,3\n", setup: static context => context.RegisterClassMap<CsvNamedMap>())
            .Code().Should().Be(ValueObjectErrorCodes.TooShort);
    }

    [Fact]
    public void The_converter_named_by_an_attribute_reads_and_writes_with_nothing_registered_even_through_AutoMap_with_a_culture()
    {
        var row = new CsvAttributed { Iban = CsvOrder.Valid.Iban };

        CsvHarness.Write([row], setup: CsvHarness.Nothing).Should().Be("Iban\nFR7630006000011234567890189\n");
        CsvHarness.Read<CsvAttributed>("Iban\nfr76 3000 6000 0112 3456 7890 189\n", setup: CsvHarness.Nothing).Single().Iban.Should().Be(row.Iban);
        CsvHarness.Refusal<CsvAttributed>("Iban\nXX\n", setup: CsvHarness.Nothing).Code().Should().Be(ValueObjectErrorCodes.TooShort);

        var map = new DefaultClassMap<CsvAttributed>();
        map.AutoMap(CultureInfo.InvariantCulture);
        map.MemberMaps.Single().Data.TypeConverter.Should().BeOfType<ValueObjectTypeConverter<Iban, string>>();
    }

    [Fact]
    public void A_converter_writing_null_drops_the_field_which_the_reader_then_misses()
    {
        var text = CsvHarness.Write([CsvOrder.Valid], setup: static context =>
        {
            CsvHarness.Factory(context);
            context.TypeConverterCache.AddConverter<Quantity?>(new NullWriting());
        });

        text.Should().Be(CsvOrder.Header + "\n" + CsvOrder.ValidRow.TrimEnd(',') + "\n");
        FluentActions.Invoking(() => CsvHarness.Read<CsvOrder>(text)).Should().Throw<CsvMissingFieldException>();
    }

    [Fact]
    public void CsvHelper_skips_a_one_column_row_of_empty_text_as_a_blank_line_unless_told_not_to()
    {
        var text = CsvHarness.Write([new CsvRow<Quantity?> { Value = null }, new CsvRow<Quantity?> { Value = Quantity.Create(3) }]);

        text.Should().Be("Value\n\n3\n");
        CsvHarness.Read<CsvRow<Quantity?>>(text).Select(static row => row.Value).Should().Equal(Quantity.Create(3));
        CsvHarness.Read<CsvRow<Quantity?>>(text, CsvHarness.Configuration() with { IgnoreBlankLines = false })
            .Select(static row => row.Value).Should().Equal(null, Quantity.Create(3));
    }

    [Fact]
    public void CsvHelper_hands_a_converter_named_on_a_positional_record_parameter_no_type_so_an_empty_nullable_one_is_required()
    {
        var refusal = CsvHarness.Refusal<CsvAttributedOptionalRecord>("Line,Optional\n1,\n", setup: CsvHarness.Nothing);

        refusal.Code().Should().Be(ValueObjectErrorCodes.Required);
        refusal.MemberMapData.Member.Should().BeNull();
        refusal.MemberMapData.Type.Should().Be<string>("CsvHelper copies no parameter type into the map data");
        CsvHarness.Read<CsvOrderRecord>("Iban,Quantity,Optional\nFR7630006000011234567890189,3,\n").Single().Optional
            .Should().BeNull("the converter the cache gives a nullable parameter is inside CsvHelper's NullableConverter");
    }

    [Fact]
    public void CsvHelper_reads_an_empty_field_of_a_nullable_member_as_null_whatever_its_null_values()
    {
        static void Nulls(CsvContext context)
        {
            CsvHarness.Factory(context);
            context.TypeConverterOptionsCache.GetOptions<Label?>().NullValues.Add("NULL");
        }

        CsvLine<Label?>[] rows = [new() { Line = 1, Value = Label.Create(string.Empty) }, new() { Line = 2 }];

        var text = CsvHarness.Write(rows);
        text.Should().Be("Line,Value\n1,\n2,\n");
        CsvHarness.Read<CsvLine<Label?>>(text).Select(static row => row.Value).Should().Equal((Label?)null, null);

        var nulls = CsvHarness.Write(rows, setup: Nulls);
        nulls.Should().Be("Line,Value\n1,\n2,NULL\n");
        CsvHarness.Read<CsvLine<Label?>>(nulls, setup: Nulls).Select(static row => row.Value).Should().Equal((Label?)null, null);
        CsvHarness.Read<CsvLine<Label?>>("Line,Value\n1,\"\"\n", setup: Nulls).Single().Value.Should().BeNull("quoting changes nothing");
    }

    [Fact]
    public void CsvHelper_leaves_a_collection_member_out_of_an_auto_map_with_a_header()
        => CsvHarness.Write([new CsvList<Quantity> { Line = 1, Values = [Quantity.Create(3)] }]).Should().Be("Line\n1\n");

    [Fact]
    public void CsvHelper_collection_converters_write_each_element_as_its_ToString_without_the_package()
    {
#pragma warning disable VO0010 // An account nobody filled in, which the package alone would refuse to write.
        CsvList<Iban> accounts = new() { Line = 1, Values = [CsvOrder.Valid.Iban, default] };
#pragma warning restore VO0010

        CsvHarness.Write([accounts], setup: Listed<Iban>).Should().Be("Line,Values\n1,FR7630006000011234567890189,\n");
        FluentActions.Invoking(static () => CsvHarness.Write([new CsvRow<Iban>()])).Should().Throw<TypeConverterException>()
            .Which.Code().Should().Be(ValueObjectErrorCodes.Required, "the package refuses the same instance in a column of its own");

        var temperatures = CsvHarness.Write([new CsvList<Celsius> { Line = 1, Values = [Celsius.Create(21), Celsius.Create(22)] }], setup: Listed<Celsius>);
        temperatures.Should().Be("Line,Values\n1,21 °C,22 °C\n", "a formatting hook's text, which its parser refuses");
        CsvHarness.Read<CsvList<Celsius>>(temperatures, setup: Listed<Celsius>).Single().Values.Should().BeEmpty();
    }

    [Fact]
    public void CsvHelper_collection_converters_read_by_name_stop_at_the_first_refused_element_with_nothing_thrown()
    {
        CsvHarness.Read<CsvList<Quantity>>("Line,Values,Values\n1,3,1001\n", setup: Listed<Quantity>).Single().Values.Should().Equal(Quantity.Create(3));
        CsvHarness.Read<CsvList<Quantity>>("Line,Values,Values\n1,1001,3\n", setup: Listed<Quantity>).Single().Values.Should().BeEmpty();

        CsvHarness.Refusal<CsvList<Quantity>>("1,3,1001\n", CsvHarness.Configuration() with { HasHeaderRecord = false }, Listed<Quantity>)
            .Code().Should().Be(ValueObjectErrorCodes.OutOfRange, "read by index, each element goes through the package's converter");
    }

    [Fact]
    public void CsvHelper_maps_a_value_object_as_a_record_by_its_public_properties_and_reads_none_as_one()
    {
        CsvHarness.Write([CsvOrder.Valid.Iban]).Should().Be("CountryCode,Value\nFR,FR7630006000011234567890189\n");
        FluentActions.Invoking(static () => CsvHarness.Read<Iban>("Iban\nFR7630006000011234567890189\n"))
            .Should().Throw<ReaderException>().WithMessage("No members are mapped for type '*.Iban'.*");
    }

    [Fact]
    public void CsvHelper_reads_a_decimal_with_its_group_separator_which_the_value_object_refuses()
    {
        CsvHarness.Read<CsvRow<decimal>>("Value\n\"12,5\"\n", setup: CsvHarness.Nothing).Single().Value.Should().Be(125m);
        CsvHarness.Refusal<CsvRow<Amount>>("Value\n\"12,5\"\n").Code().Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    [Fact]
    public void CsvHelper_converters_hide_the_field_from_Text_when_raw_data_is_kept_out_of_messages()
    {
        var configuration = CsvHarness.Configuration() with { ExceptionMessagesContainRawData = false };

        CsvHarness.Refusal<CsvRow<int>>("Value\nabc\n", configuration, CsvHarness.Nothing).Text
            .Should().Be("Hidden because ExceptionMessagesContainRawData is false.");
        CsvHarness.Refusal<CsvRow<int>>("Value\nabc\n", setup: CsvHarness.Nothing).Text.Should().Be("abc");
    }

    [Fact]
    public void CsvHelper_takes_options_for_a_member_by_its_exact_type()
    {
        var context = new CsvContext(CsvHarness.Configuration());
        context.TypeConverterOptionsCache.GetOptions<int>().CultureInfo = CultureInfo.GetCultureInfo("fr-FR");

        context.TypeConverterOptionsCache.GetOptions<int?>().CultureInfo.Should().BeNull();
    }

    /// <summary>Gives a context the package's factory, then the class map of a collection of value objects.</summary>
    private static void Listed<T>(CsvContext context)
    {
        CsvHarness.Factory(context);
        context.RegisterClassMap<CsvListMap<T>>();
    }

    /// <summary>Gets the columns of the header a text starts with.</summary>
    private static string[] Header(string text) => text[..text.IndexOf('\n', StringComparison.Ordinal)].Split(',');

    /// <summary>A converter writing <see langword="null"/>, as one written by hand may for a nullable member holding nothing.</summary>
    private sealed class NullWriting : DefaultTypeConverter
    {
        public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
            => value is null ? null : base.ConvertToString(value, row, memberMapData);
    }
}
