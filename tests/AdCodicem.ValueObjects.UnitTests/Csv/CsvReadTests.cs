using System.Globalization;
using AdCodicem.ValueObjects.CsvHelper;
using AdCodicem.ValueObjects.UnitTests.Forms;
using AdCodicem.ValueObjects.UnitTests.TestData;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace AdCodicem.ValueObjects.UnitTests.Csv;

/// <summary>
/// Value objects read from CSV fields through their rules: the normalized instance, and every refusal CsvHelper's own
/// <see cref="TypeConverterException"/>, carrying the rule's code and naming no field.
/// </summary>
public sealed class CsvReadTests
{
    private const string Text = CsvOrder.Header + "\n" + CsvOrder.ValidRow + "\n";

    public static TheoryData<int, string, string> Refused => new()
    {
        { 1, "XX", ValueObjectErrorCodes.TooShort },
        { 1, "FR76300060000112345678901891234567890", ValueObjectErrorCodes.TooLong },
        { 1, "FR76300060000112345678901!9", ValueObjectErrorCodes.InvalidFormat },
        { 2, "1001", ValueObjectErrorCodes.OutOfRange },
        { 2, "-1", ValueObjectErrorCodes.OutOfRange },
        { 2, "abc", ValueObjectErrorCodes.NotParsable },
        { 3, "-0.01", ValueObjectErrorCodes.OutOfRange },
        { 3, "\"12,5\"", ValueObjectErrorCodes.NotParsable },
        { 4, "2101-01-01", ValueObjectErrorCodes.OutOfRange },
        { 4, "tomorrow", ValueObjectErrorCodes.NotParsable },
        { 0, "nope", ValueObjectErrorCodes.NotParsable },
        { 0, "00000000-0000-0000-0000-000000000000", ValueObjectErrorCodes.Required },
        { 5, "XX", ValueObjectErrorCodes.NotAKnownValue },
        { 6, "1001", ValueObjectErrorCodes.OutOfRange },
    };

    [Fact]
    public void An_auto_mapped_order_reads_each_value_object_from_its_column()
        => CsvHarness.Read<CsvOrder>(Text).Should().ContainSingle().Which.Should().BeEquivalentTo(CsvOrder.Valid);

    [Fact]
    public void AddValueObject_reads_what_the_factory_reads_without_the_registry()
    {
        static void Typed(CsvContext context) => context.TypeConverterCache
            .AddValueObject<CustomerId, Guid>()
            .AddValueObject<Iban, string>()
            .AddValueObject<Quantity, short>()
            .AddValueObject<Amount, decimal>()
            .AddValueObject<BirthDate, DateOnly>()
            .AddValueObject<CountryCode, string>();

        CsvHarness.Read<CsvOrder>(Text, setup: Typed).Should().ContainSingle().Which.Should().BeEquivalentTo(CsvOrder.Valid);
    }

    [Fact]
    public void A_field_reads_as_the_normalized_instance()
    {
        var order = CsvHarness.Read<CsvOrder>(Row((1, "fr76 3000 6000 0112 3456 7890 189"), (5, "be"), (6, " 7 "))).Single();

        order.Iban.Should().Be(Iban.Create("FR7630006000011234567890189"));
        order.Country.Should().Be(CountryCode.Belgium);
        order.Optional.Should().Be(Quantity.Create(7));
    }

    [Theory]
    [MemberData(nameof(Refused))]
    public void A_refused_field_is_a_TypeConverterException_naming_the_rule_and_carrying_its_code(int column, string field, string code)
    {
        var name = CsvOrder.Header.Split(',')[column];
        var refusal = CsvHarness.Refusal<CsvOrder>(Row((column, field)));
        var text = field.Trim('"');

        refusal.Code().Should().Be(code);
        refusal.FirstLine().Should().Be($"The field is not a valid {Expected(column, text)}");
        refusal.Text.Should().Be(text);
        refusal.MemberMapData.Member!.Name.Should().Be(name);
        refusal.MemberMapData.Names.Should().Equal(name);
        refusal.Context!.Parser!.Row.Should().Be(2);
        if (text.Length > 3)
        {
            refusal.FirstLine().Should().NotContain(text, "the message never quotes the field");
        }
    }

    [Fact]
    public void Without_raw_data_in_messages_the_field_is_hidden_from_Text_as_CsvHelper_hides_it()
    {
        var configuration = CsvHarness.Configuration() with { ExceptionMessagesContainRawData = false };

        var refusal = CsvHarness.Refusal<CsvOrder>(Row((2, "1001")), configuration);

        refusal.Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
        refusal.Text.Should().Be("Hidden because ExceptionMessagesContainRawData is false.");
        refusal.Message.Should().NotContain("1001").And.Contain("Hidden because ExceptionMessagesContainRawData is false.");
        CsvHarness.Refusal<CsvOrder>(Row((2, "1001"))).Message.Should().Contain("1001", "CsvHelper adds the raw record otherwise");
    }

    [Theory]
    [InlineData(1, "Iban")]
    [InlineData(2, "Quantity")]
    [InlineData(0, "CustomerId")]
    public void An_empty_field_is_required_whatever_the_parser_says_of_no_text(int column, string type)
    {
        var refusal = CsvHarness.Refusal<CsvOrder>(Row((column, string.Empty)));

        refusal.Code().Should().Be(ValueObjectErrorCodes.Required);
        refusal.FirstLine().Should().Be($"An empty field cannot be read as {type}; declare the member as a nullable {type}? instead.");
        refusal.Text.Should().BeEmpty();
        Quantity.TryParse(string.Empty, CultureInfo.InvariantCulture, out _, out var parsed).Should().BeFalse();
        parsed.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable, "the parser alone would not call it missing");
    }

    [Fact]
    public void A_field_reaches_the_rules_as_CsvHelper_hands_it_over_its_spaces_included()
    {
        CsvHarness.Read<CsvLine<Label>>("Line,Value\n1, Hello \n").Single().Value.Should().Be(Label.Create(" Hello "));
        CsvHarness.Read<CsvLine<Label>>("Line,Value\n1, Hello \n", CsvHarness.Configuration() with { TrimOptions = TrimOptions.Trim })
            .Single().Value.Should().Be(Label.Create("Hello"), "CsvHelper's TrimOptions trim it");

        var refusal = CsvHarness.Refusal<CsvLine<Quantity>>("Line,Value\n1,  \n");
        refusal.Code().Should().Be(ValueObjectErrorCodes.NotParsable, "a field of spaces is not empty");
        refusal.Text.Should().Be("  ");
    }

    [Fact]
    public void A_type_accepting_empty_text_reads_an_empty_field()
        => CsvHarness.Read<CsvRow<Label>>("Value\n\"\"\n").Single().Value.Should().Be(Label.Create(string.Empty));

    [Fact]
    public void A_nullable_member_reads_an_empty_field_and_each_of_its_null_values_as_null()
    {
        CsvHarness.Read<CsvOrder>(Text).Single().Optional.Should().BeNull();

        static void Nulls(CsvContext context)
        {
            CsvHarness.Factory(context);
            context.TypeConverterOptionsCache.GetOptions<Quantity?>().NullValues.Add("NULL");
        }

        CsvHarness.Read<CsvOrder>(Row((6, "NULL")), setup: Nulls).Single().Optional.Should().BeNull();
        CsvHarness.Refusal<CsvOrder>(Row((6, "1001")), setup: Nulls).Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void The_converter_named_on_a_nullable_member_reads_an_empty_field_and_each_of_its_null_values_as_null()
    {
        // Named through [TypeConverter] or a class map's TypeConverter<T>(), the converter gets the field CsvHelper's
        // NullableConverter would have answered: with nothing registered, so that the named converter alone reads it.
        static void Nulls(CsvContext context)
        {
            context.TypeConverterOptionsCache.GetOptions<Quantity?>().NullValues.Add("NULL");
            context.TypeConverterOptionsCache.GetOptions<Label?>().NullValues.Add("NULL");
        }

        static void Mapped<TMap>(CsvContext context)
            where TMap : ClassMap
        {
            Nulls(context);
            context.RegisterClassMap<TMap>();
        }

        const string Text = "Line,Value\n1,\n2,NULL\n3,7\n";
        CsvHarness.Read<CsvAttributedOptional>(Text.Replace("Value", "Optional", StringComparison.Ordinal), setup: Nulls)
            .Select(static row => row.Optional).Should().Equal(null, null, Quantity.Create(7));
        CsvHarness.Read<CsvLine<Quantity?>>(Text, setup: Mapped<CsvNamedOptionalMap<Quantity, short>>)
            .Select(static row => row.Value).Should().Equal(null, null, Quantity.Create(7));
        CsvHarness.Read<CsvLine<Label?>>("Line,Value\n1,\n2,NULL\n3,x\n", setup: Mapped<CsvNamedOptionalMap<Label, string>>)
            .Select(static row => row.Value).Should().Equal([null, null, Label.Create("x")], "text its type accepts is a null value first");

        CsvHarness.Refusal<CsvAttributedOptional>("Line,Optional\n1,1001\n", setup: Nulls).Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
        CsvHarness.Refusal<CsvLine<Quantity?>>("Line,Value\n1,1001\n", setup: Mapped<CsvNamedOptionalMap<Quantity, short>>)
            .Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
        CsvHarness.Write([new CsvAttributedOptional { Line = 1 }, new CsvAttributedOptional { Line = 2, Optional = Quantity.Create(7) }], setup: Nulls)
            .Should().Be("Line,Optional\n1,NULL\n2,7\n");
    }

    [Fact]
    public void A_null_value_of_a_value_object_that_is_not_nullable_is_read_as_text()
    {
        static void Nulls(CsvContext context)
        {
            CsvHarness.Factory(context);
            context.TypeConverterOptionsCache.GetOptions<Iban>().NullValues.Add("NULL");
            context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("NULL");
        }

        var refusal = CsvHarness.Refusal<CsvOrder>(Row((1, "NULL")), setup: Nulls);

        refusal.Code().Should().Be(ValueObjectErrorCodes.TooShort);
        refusal.Text.Should().Be("NULL");
    }

    [Fact]
    public void A_field_missing_from_a_short_row_is_required_when_missing_fields_are_allowed()
    {
        var configuration = CsvHarness.Configuration() with { MissingFieldFound = null };

        var refusal = CsvHarness.Refusal<CsvOrder>(
            CsvOrder.Header + "\n0f8fad5b-d9cb-469f-a165-70867728950e,FR7630006000011234567890189,3\n",
            configuration);

        refusal.Code().Should().Be(ValueObjectErrorCodes.Required);
        refusal.FirstLine().Should().Be("An empty field cannot be read as Amount; declare the member as a nullable Amount? instead.");
        refusal.Text.Should().BeEmpty("CsvHelper's own converters hand a missing field over as empty text");
        refusal.MemberMapData.Member!.Name.Should().Be(nameof(CsvOrder.Amount));
        CsvHarness.Read<CsvOrder>(CsvOrder.Header + "\n" + CsvOrder.ValidRow.TrimEnd(',') + "\n", configuration)
            .Single().Optional.Should().BeNull("a nullable member missing from a short row reads null");
    }

    [Fact]
    public void A_missing_column_is_CsvHelper_header_validation_a_nullable_one_included_unless_optional()
    {
        FluentActions.Invoking(static () => CsvHarness.Read<CsvOrder>("Id,Quantity,Amount,Due,Country,Optional\n"))
            .Should().Throw<HeaderValidationException>().Which.Code().Should().BeNull();
        FluentActions.Invoking(static () => CsvHarness.Read<CsvOrder>("Id,Iban,Quantity,Amount,Due,Country\n"))
            .Should().Throw<HeaderValidationException>().WithMessage("Header with name 'Optional'[0] was not found.*");

        var row = CsvHarness.Read<CsvOptionalColumn>("Iban\nFR7630006000011234567890189\n").Single();
        row.Iban.Should().Be(Iban.Create("FR7630006000011234567890189"));
        row.Optional.Should().BeNull();
    }

    [Fact]
    public void An_import_reports_each_refused_row_with_its_member_and_code_and_keeps_the_others()
    {
        List<string> refused = [];
        var configuration = CsvHarness.Configuration() with
        {
            ReadingExceptionOccurred = args =>
            {
                if (args.Exception is TypeConverterException refusal && refusal.Code() is { } code)
                {
                    refused.Add($"row {refusal.Context!.Parser!.Row} {refusal.MemberMapData.Names[0]} {code}");
                    return false;
                }

                return true;
            },
        };
        var text = string.Join(
            "\n",
            CsvOrder.Header,
            Values((2, "1001")),
            CsvOrder.ValidRow,
            Values((1, "XX")),
            Values((5, "XX")),
            string.Empty);

        var kept = CsvHarness.Read<CsvOrder>(text, configuration);

        kept.Should().ContainSingle().Which.Should().BeEquivalentTo(CsvOrder.Valid);
        refused.Should().Equal(
            "row 2 Quantity value_object.out_of_range",
            "row 4 Iban value_object.too_short",
            "row 5 Country value_object.not_a_known_value");
    }

    [Fact]
    public void A_positional_record_reads_through_its_constructor_and_names_a_refused_parameter_in_Names()
    {
        CsvHarness.Read<CsvOrderRecord>("Iban,Quantity,Optional\nFR7630006000011234567890189,3,\n").Should().Equal(
            new CsvOrderRecord(Iban.Create("FR7630006000011234567890189"), Quantity.Create(3), null));

        var refusal = CsvHarness.Refusal<CsvOrderRecord>("Iban,Quantity,Optional\nFR7630006000011234567890189,1001,\n");

        refusal.Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
        refusal.MemberMapData.Member.Should().BeNull("CsvHelper maps a parameter with no member");
        refusal.MemberMapData.Names.Should().Equal("Quantity");
    }

    [Fact]
    public async Task GetRecordsAsync_reads_and_refuses_as_GetRecords_does()
    {
        using (var reader = new StringReader(Text))
        using (var csv = new CsvReader(reader, CsvHarness.Configuration()))
        {
            csv.Context.TypeConverterCache.AddValueObjects();
            var orders = await csv.GetRecordsAsync<CsvOrder>(TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);
            orders.Should().ContainSingle().Which.Should().BeEquivalentTo(CsvOrder.Valid);
        }

        using var refusedReader = new StringReader(Row((2, "1001")));
        using var refusedCsv = new CsvReader(refusedReader, CsvHarness.Configuration());
        refusedCsv.Context.TypeConverterCache.AddValueObjects();
        await FluentActions.Awaiting(async () => await refusedCsv.GetRecordsAsync<CsvOrder>(TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<TypeConverterException>().Where(static refusal => refusal.Code() == ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void GetField_reads_and_refuses_one_field_and_TryGetField_answers_false()
    {
        using var reader = new StringReader("FR7630006000011234567890189,1001,3,\n");
        using var csv = new CsvReader(reader, CsvHarness.Configuration() with { HasHeaderRecord = false });
        csv.Context.TypeConverterCache.AddValueObjects();
        csv.Read().Should().BeTrue();

        csv.GetField<Iban>(0).Should().Be(Iban.Create("FR7630006000011234567890189"));
        csv.GetField<Quantity?>(2).Should().Be(Quantity.Create(3));
        csv.GetField<Quantity?>(3).Should().BeNull();
        FluentActions.Invoking(() => csv.GetField<Quantity>(1)).Should().Throw<TypeConverterException>()
            .Which.Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
        csv.TryGetField<Quantity>(1, out var quantity).Should().BeFalse();
        quantity.IsUninitialized().Should().BeTrue();
    }

    [Fact]
    public void CsvHelper_options_other_than_the_culture_are_not_read()
    {
        static void Options(CsvContext context)
        {
            CsvHarness.Factory(context);
            context.TypeConverterOptionsCache.GetOptions<bool>().BooleanTrueValues.Add("yes");
            context.TypeConverterOptionsCache.GetOptions<Consent>().BooleanTrueValues.Add("yes");
            context.TypeConverterOptionsCache.GetOptions<Amount>().NumberStyles = NumberStyles.Number;
        }

        CsvHarness.Read<CsvRow<bool>>("Value\nyes\n", setup: Options).Single().Value.Should().BeTrue("CsvHelper's own column reads it");
        CsvHarness.Refusal<CsvRow<Consent>>("Value\nyes\n", setup: Options).Code().Should().Be(ValueObjectErrorCodes.NotParsable);
        CsvHarness.Refusal<CsvRow<Amount>>("Value\n\"1,000.5\"\n", setup: Options).Code().Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    [Fact]
    public void A_rule_refusing_with_an_empty_message_is_named_without_one()
    {
        var refusal = CsvHarness.Refusal<CsvRow<UnexplainedCode>>("Value\nquiet\n", setup: static context => context.TypeConverterCache.AddValueObject<UnexplainedCode, string>());

        refusal.Code().Should().Be(UnexplainedCode.QuietCode);
        refusal.FirstLine().Should().Be("The field is not a valid UnexplainedCode.");
    }

    [Fact]
    public void A_parser_refusing_without_a_reason_is_not_parsable_and_named_without_a_message()
    {
        var refusal = CsvHarness.Refusal<CsvRow<UnexplainedCode>>("Value\nunexplained\n", setup: static context => context.TypeConverterCache.AddValueObject<UnexplainedCode, string>());

        refusal.Code().Should().Be(ValueObjectErrorCodes.NotParsable);
        refusal.FirstLine().Should().Be("The field is not a valid UnexplainedCode.", "the shared reading names the type by its runtime name, which a generic one would show");
    }

    [Fact]
    public void A_default_used_on_conversion_failure_replaces_a_refusal_where_CsvHelper_converters_would_return_it()
    {
        Read(new CsvQuantityMap(static map => map.Default(Quantity.Create(1), useOnConversionFailure: true)), "1001")
            .Should().Be(Quantity.Create(1));
        ReadOptional(new CsvOptionalQuantityMap(static map => map.Default(Quantity.Create(2), useOnConversionFailure: true)), "1001")
            .Should().Be(Quantity.Create(2));
        ReadOptional(new CsvOptionalQuantityMap(static map => map.Default((Quantity?)null, useOnConversionFailure: true)), "1001")
            .Should().BeNull();
    }

    [Fact]
    public void A_field_its_rules_accept_is_read_whatever_the_default_used_on_conversion_failure()
    {
        Read(new CsvQuantityMap(static map => map.Default(Quantity.Create(1), useOnConversionFailure: true)), "5")
            .Should().Be(Quantity.Create(5));
        ReadOptional(new CsvOptionalQuantityMap(static map => map.Default((Quantity?)null, useOnConversionFailure: true)), "5")
            .Should().Be(Quantity.Create(5));
    }

    [Fact]
    public void A_default_CsvHelper_converters_would_refuse_on_conversion_failure_leaves_the_coded_refusal()
    {
        Refusal(new CsvQuantityMap(static map => map.Default("7", useOnConversionFailure: true))).Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
        Refusal(new CsvQuantityMap(static map => map.Default((string?)null, useOnConversionFailure: true))).Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
        Refusal(new CsvQuantityMap(static map => map.Default(Quantity.Create(1)))).Code().Should().Be(ValueObjectErrorCodes.OutOfRange);

        using var reader = new StringReader("1001\n");
        using var csv = new CsvReader(reader, CsvHarness.Configuration() with { HasHeaderRecord = false });
        csv.Read();
        var data = new MemberMapData(null) { UseDefaultOnConversionFailure = true, Default = Quantity.Create(1) };
        FluentActions.Invoking(() => new ValueObjectTypeConverter<Quantity, short>().ConvertFromString("1001", csv, data))
            .Should().Throw<TypeConverterException>().Which.Code().Should().Be(ValueObjectErrorCodes.OutOfRange, "no default was set");

        static TypeConverterException Refusal(CsvQuantityMap map)
            => CsvHarness.Refusal<CsvRow<Quantity>>("Value\n1001\n", setup: context => Map(context, map));
    }

    [Fact]
    public void An_empty_field_takes_the_default_as_it_is_and_a_default_written_as_text_goes_through_the_rules()
    {
        Read(new CsvQuantityMap(static map => map.Default(Quantity.Create(1))), "\"\"").Should().Be(Quantity.Create(1));

        var row = CsvHarness.Read<CsvDefaulted>("Country,Wrong\n,lu\n").Single();
        row.Country.Should().Be(CountryCode.Belgium);
        row.Wrong.Should().Be(CountryCode.Luxembourg);

        var refusal = CsvHarness.Refusal<CsvDefaulted>("Country,Wrong\nFR,\n");
        refusal.Code().Should().Be(ValueObjectErrorCodes.NotAKnownValue);
        refusal.Text.Should().Be("XX");
    }

    /// <summary>Gets the message <c>TryParse</c> gives the text of a column of <see cref="CsvOrder"/>, its type first.</summary>
    private static string Expected(int column, string text)
    {
        var culture = CultureInfo.InvariantCulture;
        var (type, validation) = column switch
        {
            0 => ("CustomerId", CustomerId.TryParse(text, culture, out _, out var refused) ? default : refused),
            1 => ("Iban", Iban.TryParse(text, culture, out _, out var refused) ? default : refused),
            2 or 6 => ("Quantity", Quantity.TryParse(text, culture, out _, out var refused) ? default : refused),
            3 => ("Amount", Amount.TryParse(text, culture, out _, out var refused) ? default : refused),
            4 => ("BirthDate", BirthDate.TryParse(text, culture, out _, out var refused) ? default : refused),
            _ => ("CountryCode", CountryCode.TryParse(text, culture, out _, out var refused) ? default : refused),
        };

        validation.IsValid.Should().BeFalse("{0} refuses '{1}'", type, text);
        return $"{type}: {validation.ErrorMessage}";
    }

    /// <summary>Writes the text of an order, header included, whose columns are those of the valid row but the ones given.</summary>
    private static string Row(params (int Column, string Field)[] fields) => CsvOrder.Header + "\n" + Values(fields) + "\n";

    /// <summary>Writes the valid row with the columns given replaced.</summary>
    private static string Values(params (int Column, string Field)[] fields)
    {
        var values = CsvOrder.ValidRow.Split(',');
        foreach (var (column, field) in fields)
        {
            values[column] = field;
        }

        return string.Join(',', values);
    }

    /// <summary>Reads one quantity through a class map.</summary>
    private static Quantity Read(CsvQuantityMap map, string field)
        => CsvHarness.Read<CsvRow<Quantity>>($"Value\n{field}\n", setup: context => Map(context, map)).Single().Value;

    /// <summary>Reads one nullable quantity through a class map.</summary>
    private static Quantity? ReadOptional(CsvOptionalQuantityMap map, string field)
        => CsvHarness.Read<CsvRow<Quantity?>>($"Value\n{field}\n", setup: context => Map(context, map)).Single().Value;

    /// <summary>Gives a context the factory, then registers a class map.</summary>
    private static void Map(CsvContext context, ClassMap map)
    {
        CsvHarness.Factory(context);
        context.RegisterClassMap(map);
    }
}
