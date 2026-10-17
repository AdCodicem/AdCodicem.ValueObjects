using System.CommandLine;
using System.CommandLine.Parsing;
using AdCodicem.ValueObjects.CommandLine;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// What a refused value becomes: a parse error naming the symbol and giving the rule's message, never the text, the rule's
/// code and the value object type kept beside it, and, on an invocation, the message on standard error, the help and exit
/// code 1 without the action running.
/// </summary>
public sealed class CliRefusalTests
{
    private const string Electronic = "FR7630006000011234567890189";

    [Fact]
    public void Each_rule_refuses_with_its_message_after_the_option_and_its_code_beside_the_error()
    {
        var iban = new Option<Iban>("--iban").AcceptValueObject();
        var quantity = new Option<Quantity>("--quantity").AcceptValueObject();
        var page = new Option<PageNumber>("--page").AcceptValueObject();
        var country = new Option<CountryCode>("--country").AcceptValueObject();
        var customer = new Option<CustomerId>("--customer").AcceptValueObject();
        var root = new RootCommand { iban, quantity, page, country, customer };

        Refusal(root, "--iban", "XX").Should().Be(new CliError("Option '--iban': The value must be at least 15 characters long.", ValueObjectErrorCodes.TooShort, typeof(Iban)));
        Refusal(root, "--iban", "FR76-NOT-AN-IBAN!!!!").Should().Be(new CliError("Option '--iban': The value does not match the expected format.", ValueObjectErrorCodes.InvalidFormat, typeof(Iban)));
        Refusal(root, "--iban", "FR7630006000011234567890188").Should().Be(new CliError("Option '--iban': The IBAN check digits are incorrect.", ValueObjectErrorCodes.InvalidFormat, typeof(Iban)));
        Refusal(root, "--iban", Electronic + "0123456789012").Should().Be(new CliError("Option '--iban': The value must be at most 34 characters long.", ValueObjectErrorCodes.TooLong, typeof(Iban)));
        Refusal(root, "--quantity", "1001").Should().Be(new CliError("Option '--quantity': The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)));
        Refusal(root, "--page", "0").Should().Be(new CliError("Option '--page': The value must be greater than or equal to 1.", ValueObjectErrorCodes.OutOfRange, typeof(PageNumber)));
        Refusal(root, "--quantity", "three").Should().Be(new CliError("Option '--quantity': The text is not a valid short.", ValueObjectErrorCodes.NotParsable, typeof(Quantity)));
        Refusal(root, "--country", "XX").Should().Be(new CliError("Option '--country': The value is not one of the accepted values.", ValueObjectErrorCodes.NotAKnownValue, typeof(CountryCode)));
        Refusal(root, "--customer", Guid.Empty.ToString()).Should().Be(new CliError("Option '--customer': A customer identifier must not be empty.", ValueObjectErrorCodes.Required, typeof(CustomerId)));
    }

    [Fact]
    public void An_argument_is_named_by_its_name_and_an_option_by_the_alias_typed()
    {
        var quantity = new Option<Quantity>("--quantity", "-q").AcceptValueObject();
        var iban = new Argument<Iban>("iban").AcceptValueObject();
        var root = new RootCommand { quantity, iban };

        root.Parse(["XX", "-q", "1001"]).Refusals().Should().Equal(
            new CliError("Option '-q': The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)),
            new CliError("Argument 'iban': The value must be at least 15 characters long.", ValueObjectErrorCodes.TooShort, typeof(Iban)));
    }

    [Fact]
    public void Each_refused_element_of_a_collection_is_an_error_of_its_own_under_its_position()
    {
        var ibans = new Option<Iban[]>("--ibans") { AllowMultipleArgumentsPerToken = true }.AcceptValueObject();
        var quantities = new Argument<List<Quantity>>("quantities").AcceptValueObject();
        var root = new RootCommand { ibans, quantities };

        root.Parse(["1", "1001", "2", "--ibans", Electronic, "XX", "DE89370400440532013000", "YY"]).Refusals().Should().Equal(
            new CliError("Option '--ibans', the 2nd value: The value must be at least 15 characters long.", ValueObjectErrorCodes.TooShort, typeof(Iban)),
            new CliError("Option '--ibans', the 4th value: The value must be at least 15 characters long.", ValueObjectErrorCodes.TooShort, typeof(Iban)),
            new CliError("Argument 'quantities', the 2nd value: The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)));

        root.Parse(["--ibans", "XX"]).Refusals().Should().Equal(
            new CliError("Option '--ibans': The value must be at least 15 characters long.", ValueObjectErrorCodes.TooShort, typeof(Iban)));
    }

    [Fact]
    public void The_position_of_a_refused_element_is_an_English_ordinal_counted_from_one()
    {
        var quantities = new Argument<Quantity[]>("quantities").AcceptValueObject();
        var root = new RootCommand { quantities };

        var refusals = root.Parse([.. Enumerable.Repeat("1001", 124)]).Refusals();

        refusals.Should().HaveCount(124);
        int[] counts = [1, 2, 3, 4, 10, 11, 12, 13, 14, 20, 21, 22, 23, 24, 100, 101, 102, 103, 104, 111, 112, 113, 121, 122, 123, 124];
        string[] ordinals = ["1st", "2nd", "3rd", "4th", "10th", "11th", "12th", "13th", "14th", "20th", "21st", "22nd", "23rd", "24th", "100th", "101st",
            "102nd", "103rd", "104th", "111th", "112th", "113th", "121st", "122nd", "123rd", "124th"];
        counts.Select(count => refusals[count - 1].Message).Should().Equal(
            ordinals.Select(static ordinal => $"Argument 'quantities', the {ordinal} value: The value must be less than or equal to 1000."));
    }

    [Fact]
    public void A_parser_refusing_without_a_reason_or_with_an_empty_message_still_names_the_symbol_and_carries_a_code()
    {
        var code = new Option<CliUnexplainedCode>("--code").AcceptValueObject();
        var codes = new Argument<CliUnexplainedCode[]>("codes").AcceptValueObject();
        var root = new RootCommand { code, codes };

        root.Parse(["--code", "unexplained"]).Refusals().Should().Equal(
            new CliError("Option '--code': The text could not be parsed as CliUnexplainedCode.", ValueObjectErrorCodes.NotParsable, typeof(CliUnexplainedCode)));
        root.Parse(["--code", "quiet"]).Refusals().Should().Equal(
            new CliError("Option '--code' is invalid.", CliUnexplainedCode.QuietCode, typeof(CliUnexplainedCode)));
        root.Parse(["loud", "quiet"]).Refusals().Should().Equal(
            new CliError("Argument 'codes', the 2nd value, is invalid.", CliUnexplainedCode.QuietCode, typeof(CliUnexplainedCode)));
    }

    [Fact]
    public void An_error_of_System_CommandLine_s_own_carries_no_code_nor_type()
    {
        var quantity = new Option<Quantity>("--quantity") { Required = true }.AcceptValueObject();
        var root = new RootCommand { quantity };

        var errors = root.Parse(["extra"]).Errors;

        errors.Select(static error => error.Message).Should().BeEquivalentTo("Option '--quantity' is required.", "Unrecognized command or argument 'extra'.");
        errors.Should().AllSatisfy(static error =>
        {
            error.TryGetValueObjectErrorCode(out var code).Should().BeFalse();
            code.Should().BeNull();
            error.TryGetValueObjectType(out var type).Should().BeFalse();
            type.Should().BeNull();
        });
    }

    [Fact]
    public void A_null_error_is_refused()
    {
        FluentActions.Invoking(static () => ((ParseError)null!).TryGetValueObjectErrorCode(out _)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(static () => ((ParseError)null!).TryGetValueObjectType(out _)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetValue_on_a_refused_symbol_throws_System_CommandLine_s_exception_with_the_message_and_no_code()
    {
        var iban = new Option<Iban>("--iban").AcceptValueObject();
        var result = new RootCommand { iban }.Parse(["--iban", "XX"]);

        var exception = FluentActions.Invoking(() => result.GetValue(iban)).Should().Throw<InvalidOperationException>()
            .WithMessage("Option '--iban': The value must be at least 15 characters long.").Which;
        ValueObjectErrors.TryGetCode(exception, out _).Should().BeFalse();
    }

    [Fact]
    public void Each_parse_keeps_the_code_of_its_own_errors()
    {
        var quantity = new Option<Quantity>("--quantity").AcceptValueObject();
        var iban = new Option<Iban>("--iban").AcceptValueObject();
        var root = new RootCommand { quantity, iban };

        var first = root.Parse(["--quantity", "1001"]);
        var second = root.Parse(["--iban", "XX"]);

        first.Refusals().Should().Equal(new CliError("Option '--quantity': The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)));
        second.Refusals().Should().Equal(new CliError("Option '--iban': The value must be at least 15 characters long.", ValueObjectErrorCodes.TooShort, typeof(Iban)));
    }

    [Fact]
    public void The_lines_of_a_response_file_are_read_through_the_rules_too()
    {
        var quantity = new Option<Quantity>("--quantity").AcceptValueObject();
        var iban = new Option<Iban>("--iban").AcceptValueObject();
        var root = new RootCommand { quantity, iban };
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(file, ["--quantity 1001", "--iban XX"]);

            root.Parse([$"@{file}"]).Refusals().Should().Equal(
                new CliError("Option '--quantity': The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)),
                new CliError("Option '--iban': The value must be at least 15 characters long.", ValueObjectErrorCodes.TooShort, typeof(Iban)));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void An_invocation_writes_a_refusal_to_standard_error_shows_the_help_and_returns_1_without_running_the_action()
    {
        var iban = new Option<Iban>("--iban") { Required = true };
        var quantity = new Option<Quantity?>("--quantity");
        var ran = new List<string>();
        var root = new RootCommand("Places an order.") { iban, quantity }.AcceptValueObjects();
        root.SetAction(result => ran.Add($"{result.GetValue(iban)} {result.GetValue(quantity)}"));

        var (exitCode, output, error) = root.Invoke("--quantity", "1001", "--iban", "XX");

        exitCode.Should().Be(1);
        error.Should().Contain("Option '--iban': The value must be at least 15 characters long.")
            .And.Contain("Option '--quantity': The value must be less than or equal to 1000.")
            .And.NotContain("XX").And.NotContain("1001");
        output.Should().Contain("Places an order.").And.Contain("--iban <iban>");
        ran.Should().BeEmpty();

        root.Invoke("--iban", "fr76 3000 6000 0112 3456 7890 189", "--quantity", "3").ExitCode.Should().Be(0);
        ran.Should().Equal($"{Electronic} 3");
    }

    /// <summary>Gets the one error a command line gives.</summary>
    private static CliError Refusal(Command root, string option, string text) => root.Parse([option, text]).Refusals().Should().ContainSingle().Which;
}
