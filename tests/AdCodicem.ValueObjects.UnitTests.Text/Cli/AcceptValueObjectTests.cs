using System.CommandLine;
using AdCodicem.ValueObjects.CommandLine;
using AdCodicem.ValueObjects.UnitTests.TestData;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// <c>AcceptValueObject</c> on one option or argument: text read through the value object's rules into the normalized
/// instance, in every shape a symbol may hold it in, empty text, an option left out and a default value, and the misuses
/// it refuses when the symbol is configured.
/// </summary>
public sealed class AcceptValueObjectTests
{
    private const string Electronic = "FR7630006000011234567890189";

    private const string NotDescribed =
        "which is not a value object the registry describes, its nullable type, or an array, a List<T>, an IEnumerable<T> or "
        + "an IReadOnlyList<T> of one. Under native AOT, a value object written by hand, ValueObjectRegistry.Register<TSelf, "
        + "TValue>(TSelf.Schema), and a construction of a generic one, ValueObjectRegistry.Register<TSelf, TValue>(static () => "
        + "new TSelf.ValueJsonConverter()), have to be registered before AcceptValueObject or AcceptValueObjects is called.";

    private const string Unsupported =
        "a value object in a shape AcceptValueObject does not read: it reads the value object, its nullable type, or an array, a "
        + "List<T>, an IEnumerable<T> or an IReadOnlyList<T> of it.";

    public static TheoryData<string> Shapes => ["TSelf", "TSelf?", "TSelf[]", "List<TSelf>", "IEnumerable<TSelf>", "IReadOnlyList<TSelf>"];

    [Fact]
    public void Accepted_text_reads_as_the_normalized_instance()
    {
        var iban = new Option<Iban>("--iban").AcceptValueObject();
        var reference = new Option<Reference<CliOwner>>("--reference").AcceptValueObject();
        var country = new Option<CountryCode>("--country").AcceptValueObject();
        var root = new RootCommand { iban, reference, country };

        var result = root.Parse(["--iban", "fr76 3000 6000 0112 3456 7890 189", "--reference", " po-1042 ", "--country", "be"]);

        result.Errors.Should().BeEmpty();
        result.GetValue(iban).Should().Be(Iban.Create(Electronic));
        result.GetValue(reference).Should().Be(Reference<CliOwner>.Create("PO-1042"));
        result.GetValue(country).Should().Be(CountryCode.Belgium);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void An_option_of_each_shape_reads_each_token_through_the_rules(string shape)
    {
        switch (shape)
        {
            case "TSelf":
                OptionReads<Quantity>(static value => [value], many: false);
                break;
            case "TSelf?":
                OptionReads<Quantity?>(static value => [value!.Value], many: false);
                break;
            case "TSelf[]":
                OptionReads<Quantity[]>(static value => value!, many: true);
                break;
            case "List<TSelf>":
                OptionReads<List<Quantity>>(static value => value!, many: true);
                break;
            case "IEnumerable<TSelf>":
                OptionReads<IEnumerable<Quantity>>(static value => value!, many: true);
                break;
            default:
                OptionReads<IReadOnlyList<Quantity>>(static value => value!, many: true);
                break;
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void An_argument_of_each_shape_reads_each_token_through_the_rules(string shape)
    {
        switch (shape)
        {
            case "TSelf":
                ArgumentReads<Quantity>(static value => [value], many: false);
                break;
            case "TSelf?":
                ArgumentReads<Quantity?>(static value => [value!.Value], many: false);
                break;
            case "TSelf[]":
                ArgumentReads<Quantity[]>(static value => value!, many: true);
                break;
            case "List<TSelf>":
                ArgumentReads<List<Quantity>>(static value => value!, many: true);
                break;
            case "IEnumerable<TSelf>":
                ArgumentReads<IEnumerable<Quantity>>(static value => value!, many: true);
                break;
            default:
                ArgumentReads<IReadOnlyList<Quantity>>(static value => value!, many: true);
                break;
        }
    }

    [Fact]
    public void The_call_returns_the_symbol_it_configures()
    {
        var option = new Option<Quantity>("--quantity");
        var argument = new Argument<Quantity>("quantity");

        option.AcceptValueObject().Should().BeSameAs(option);
        argument.AcceptValueObject().Should().BeSameAs(argument);
    }

    [Fact]
    public void A_symbol_whose_type_holds_no_value_object_is_refused_when_configured()
    {
        FluentActions.Invoking(static () => new Option<string>("--name").AcceptValueObject())
            .Should().Throw<InvalidOperationException>().WithMessage($"'--name' holds 'string', {NotDescribed}");
        FluentActions.Invoking(static () => new Argument<int>("count").AcceptValueObject())
            .Should().Throw<InvalidOperationException>().WithMessage($"'count' holds 'int', {NotDescribed}");
        FluentActions.Invoking(static () => new Option<HashSet<Quantity>>("--quantities").AcceptValueObject())
            .Should().Throw<InvalidOperationException>().WithMessage($"'--quantities' holds 'HashSet<Quantity>', {NotDescribed}");
    }

    [Fact]
    public void A_value_object_in_a_shape_that_is_not_read_is_refused_when_configured()
    {
        FluentActions.Invoking(static () => new Option<Quantity?[]>("--quantities").AcceptValueObject())
            .Should().Throw<InvalidOperationException>().WithMessage($"'--quantities' holds 'Quantity?[]', {Unsupported}");
        FluentActions.Invoking(static () => new Option<Quantity[,]>("--grid").AcceptValueObject())
            .Should().Throw<InvalidOperationException>().WithMessage($"'--grid' holds 'Quantity[,]', {Unsupported}");
        FluentActions.Invoking(static () => new Argument<List<Quantity?>>("quantities").AcceptValueObject())
            .Should().Throw<InvalidOperationException>().WithMessage($"'quantities' holds 'List<Quantity?>', {Unsupported}");
    }

    [Fact]
    public void A_symbol_with_a_parser_of_the_application_is_refused_rather_than_replaced()
    {
        var option = new Option<Quantity>("--quantity") { CustomParser = static _ => Quantity.Create(42) };
        var argument = new Argument<Quantity>("quantity") { CustomParser = static _ => Quantity.Create(42) };

        FluentActions.Invoking(() => option.AcceptValueObject()).Should().Throw<InvalidOperationException>()
            .WithMessage("'--quantity' already has a CustomParser, which AcceptValueObject would replace. AcceptValueObjects leaves such a symbol alone.");
        FluentActions.Invoking(() => argument.AcceptValueObject()).Should().Throw<InvalidOperationException>()
            .WithMessage("'quantity' already has a CustomParser, which AcceptValueObject would replace. AcceptValueObjects leaves such a symbol alone.");

        new RootCommand { option }.Parse(["--quantity", "5000"]).GetValue(option).Should().Be(Quantity.Create(42));
    }

    [Fact]
    public void A_null_symbol_is_refused()
    {
        FluentActions.Invoking(static () => ((Option<Quantity>)null!).AcceptValueObject()).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(static () => ((Argument<Quantity>)null!).AcceptValueObject()).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Empty_text_is_a_token_which_the_type_s_own_rule_refuses_or_accepts_and_never_null()
    {
        var iban = new Option<Iban>("--iban").AcceptValueObject();
        var quantity = new Option<Quantity>("--quantity").AcceptValueObject();
        var maybe = new Option<Quantity?>("--maybe").AcceptValueObject();
        var label = new Option<Label>("--label").AcceptValueObject();
        var root = new RootCommand { iban, quantity, maybe, label };

        root.Parse(["--iban", ""]).Refusals().Should().Equal(
            new CliError("Option '--iban': The value must not be empty.", ValueObjectErrorCodes.Required, typeof(Iban)));
        root.Parse(["--quantity", ""]).Refusals().Should().Equal(
            new CliError("Option '--quantity': The text is not a valid short.", ValueObjectErrorCodes.NotParsable, typeof(Quantity)));
        root.Parse(["--maybe", ""]).Refusals().Should().Equal(
            new CliError("Option '--maybe': The text is not a valid short.", ValueObjectErrorCodes.NotParsable, typeof(Quantity)));

        var empty = root.Parse(["--label", ""]);
        empty.Errors.Should().BeEmpty();
        empty.GetValue(label).Should().Be(Label.Create(string.Empty));
    }

    [Fact]
    public void An_option_left_out_is_null_for_the_nullable_type_and_the_default_instance_otherwise()
    {
        var quantity = new Option<Quantity>("--quantity").AcceptValueObject();
        var maybe = new Option<Quantity?>("--maybe").AcceptValueObject();
        var quantities = new Option<Quantity[]>("--quantities").AcceptValueObject();
        var readOnly = new Option<IReadOnlyList<Quantity>>("--read-only").AcceptValueObject();
        var root = new RootCommand { quantity, maybe, quantities, readOnly };

        var result = root.Parse([]);

        result.Errors.Should().BeEmpty();
        ((IValueObject<Quantity, short>)result.GetValue(quantity)).IsDefault.Should().BeTrue();
        result.GetValue(maybe).Should().BeNull();
        result.GetValue(quantities).Should().BeEmpty();
        result.GetValue(readOnly).Should().BeNull("System.CommandLine creates an empty array or list, never an IReadOnlyList<T>");
    }

    [Fact]
    public void A_required_option_left_out_is_System_CommandLine_s_error_which_carries_no_code()
    {
        var quantity = new Option<Quantity>("--quantity") { Required = true }.AcceptValueObject();

        new RootCommand { quantity }.Parse([]).Refusals().Should().Equal(new CliError("Option '--quantity' is required.", null, null));
    }

    [Fact]
    public void A_default_value_is_the_application_s_own_and_is_not_read_again_while_text_overriding_it_is()
    {
        var count = new Option<Quantity>("--count") { DefaultValueFactory = static _ => Quantity.CreateUnchecked(5000) }.AcceptValueObject();
        var root = new RootCommand { count };

        root.Parse([]).GetValue(count).Should().Be(Quantity.CreateUnchecked(5000));
        root.Parse(["--count", "7"]).GetValue(count).Should().Be(Quantity.Create(7));
        root.Parse(["--count", "5000"]).Refusals().Should().Equal(
            new CliError("Option '--count': The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)));
    }

    [Fact]
    public void An_option_given_alone_at_an_arity_the_application_allows_reads_nothing_as_one_left_out()
    {
        var quantity = new Option<Quantity>("--quantity") { Arity = ArgumentArity.ZeroOrOne }.AcceptValueObject();
        var maybe = new Option<Quantity?>("--maybe") { Arity = ArgumentArity.ZeroOrOne }.AcceptValueObject();
        var root = new RootCommand { quantity, maybe };

        var result = root.Parse(["--quantity", "--maybe"]);

        result.Errors.Should().BeEmpty();
        ((IValueObject<Quantity, short>)result.GetValue(quantity)).IsDefault.Should().BeTrue();
        result.GetValue(maybe).Should().BeNull();
    }

    [Fact]
    public void A_value_given_several_tokens_by_an_arity_of_the_application_reads_each_through_the_rules_and_keeps_the_last()
    {
        var quantity = new Option<Quantity>("--quantity") { Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true }.AcceptValueObject();
        var level = new Argument<Quantity?>("level") { Arity = ArgumentArity.ZeroOrMore }.AcceptValueObject();
        var root = new RootCommand { quantity, level };

        var accepted = root.Parse(["--quantity", "1", "2", "--", "3", "4"]);
        accepted.Errors.Should().BeEmpty();
        accepted.GetValue(quantity).Should().Be(Quantity.Create(2));
        accepted.GetValue(level).Should().Be(Quantity.Create(4));

        root.Parse(["--quantity", "1001", "2"]).Refusals().Should().Equal(
            new CliError("Option '--quantity', the 1st value: The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)));
        root.Parse(["1001", "2", "1002"]).Refusals().Should().Equal(
            new CliError("Argument 'level', the 1st value: The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)),
            new CliError("Argument 'level', the 3rd value: The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)));
    }

    [Fact]
    public void A_construction_of_a_generic_value_object_and_one_written_by_hand_are_described_by_the_registry()
    {
        var reference = new Option<Reference<CliOwner>>("--reference").AcceptValueObject();
        var ticket = new Option<Declared<CliTicket, string>>("--ticket").AcceptValueObject();
        var root = new RootCommand { reference, ticket };

        var accepted = root.Parse(["--reference", " po-7 ", "--ticket", " T-42 "]);
        accepted.Errors.Should().BeEmpty();
        accepted.GetValue(reference).Should().Be(Reference<CliOwner>.Create("PO-7"));
        accepted.GetValue(ticket).Should().Be(Declared<CliTicket, string>.Create("T-42"));

        root.Parse(["--reference", "ABCDEFGHIJKLMNOP", "--ticket", "X-1"]).Refusals().Should().Equal(
            new CliError("Option '--reference': The value must be at most 12 characters long.", ValueObjectErrorCodes.TooLong, typeof(Reference<CliOwner>)),
            new CliError("Option '--ticket': The text is not a value.", ValueObjectErrorCodes.NotParsable, typeof(Declared<CliTicket, string>)));
    }

    /// <summary>Checks that an option of a shape reads accepted text, and refuses the text its rules refuse.</summary>
    private static void OptionReads<T>(Func<T?, IEnumerable<Quantity>> values, bool many)
    {
        var option = new Option<T>("--quantity") { AllowMultipleArgumentsPerToken = true }.AcceptValueObject();
        var root = new RootCommand { option };

        var accepted = root.Parse(many ? ["--quantity", "7", "8"] : ["--quantity", "7"]);
        accepted.Errors.Should().BeEmpty();
        values(accepted.GetValue(option)).Should().Equal(many ? [Quantity.Create(7), Quantity.Create(8)] : [Quantity.Create(7)]);

        root.Parse(["--quantity", "1001"]).Refusals().Should().Equal(
            new CliError("Option '--quantity': The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)));
    }

    /// <summary>Checks that an argument of a shape reads accepted text, and refuses the text its rules refuse.</summary>
    private static void ArgumentReads<T>(Func<T?, IEnumerable<Quantity>> values, bool many)
    {
        var argument = new Argument<T>("quantity").AcceptValueObject();
        var root = new RootCommand { argument };

        var accepted = root.Parse(many ? ["7", "8"] : ["7"]);
        accepted.Errors.Should().BeEmpty();
        values(accepted.GetValue(argument)).Should().Equal(many ? [Quantity.Create(7), Quantity.Create(8)] : [Quantity.Create(7)]);

        root.Parse(["1001"]).Refusals().Should().Equal(
            new CliError("Argument 'quantity': The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)));
    }
}
