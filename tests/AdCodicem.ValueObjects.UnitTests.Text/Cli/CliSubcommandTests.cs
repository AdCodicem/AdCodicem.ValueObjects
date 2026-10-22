using System.CommandLine;
using AdCodicem.ValueObjects.CommandLine;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// The options and arguments of the commands above an invoked subcommand, which System.CommandLine reads only when the
/// action asks for them: read through their rules while the subcommand is validated, each refusal a parse error with its
/// code, and the action not run.
/// </summary>
public sealed class CliSubcommandTests
{
    private const string Electronic = "FR7630006000011234567890189";

    private static readonly CliError QuantityTooLarge = new("Option '--quantity': The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity));

    private static readonly CliError IbanTooShort = new("Argument 'iban': The value must be at least 15 characters long.", ValueObjectErrorCodes.TooShort, typeof(Iban));

    [Fact]
    public void A_parent_s_option_and_argument_are_read_through_their_rules_when_a_subcommand_is_invoked()
    {
        var quantity = new Option<Quantity>("--quantity");
        var iban = new Argument<Iban>("iban");
        var ran = new List<string>();
        var sub = new Command("sub");
        var root = new RootCommand { quantity, iban, sub }.AcceptValueObjects();
        root.SetAction(static _ => { });
        sub.SetAction(result => ran.Add($"{result.GetValue(quantity)} {result.GetValue(iban)}"));

        root.Parse(["--quantity", "1001", "XX", "sub"]).Refusals().Should().Equal(QuantityTooLarge, IbanTooShort);

        var (exitCode, output, error) = root.Invoke("--quantity", "1001", "XX", "sub");
        exitCode.Should().Be(1);
        error.Should().Contain(QuantityTooLarge.Message).And.Contain(IbanTooShort.Message);
        output.Should().Contain("Usage:");
        ran.Should().BeEmpty();

        root.Parse(["sub"]).Errors.Should().BeEmpty();
        root.Invoke("--quantity", "7", "fr76 3000 6000 0112 3456 7890 189", "sub").ExitCode.Should().Be(0);
        ran.Should().Equal($"7 {Electronic}");
    }

    [Fact]
    public void Each_command_above_a_nested_subcommand_has_its_symbols_read_of_every_shape()
    {
        var quantity = new Option<Quantity?>("--quantity");
        var countries = new Option<List<CountryCode>>("--country") { AllowMultipleArgumentsPerToken = true };
        var iban = new Argument<Iban>("iban");
        var amounts = new Argument<Amount[]>("amounts") { Arity = ArgumentArity.ZeroOrMore };
        var deep = new Command("deep") { new Argument<Iban>("account") };
        var pay = new Command("pay") { countries, iban, deep };
        var root = new Command("app") { quantity, amounts, pay }.AcceptValueObjects();
        deep.SetAction(static _ => { });

        root.Parse(["--quantity", "1001", "12.5", "-1", "pay", "XX", "--country", "fr", "de", "deep", Electronic]).Refusals().Should().Equal(
            new CliError("Option '--country', the 2nd value: The value is not one of the accepted values.", ValueObjectErrorCodes.NotAKnownValue, typeof(CountryCode)),
            IbanTooShort,
            QuantityTooLarge,
            new CliError("Argument 'amounts', the 2nd value: The value must be greater than or equal to 0.", ValueObjectErrorCodes.OutOfRange, typeof(Amount)));

        var accepted = root.Parse(["--quantity", "7", "12.5", "pay", Electronic, "--country", "fr", "deep", Electronic]);
        accepted.Errors.Should().BeEmpty();
        accepted.GetValue(quantity).Should().Be(Quantity.Create(7));
        accepted.GetValue(amounts).Should().Equal(Amount.Create(12.5m));
        accepted.GetValue(countries).Should().Equal(CountryCode.France);
        accepted.GetValue(iban).Should().Be(Iban.Create(Electronic));
    }

    [Fact]
    public void A_refused_value_above_is_listed_alone_as_System_CommandLine_then_leaves_the_subcommand_s_own_symbols()
    {
        var quantity = new Option<Quantity>("--quantity");
        var inner = new Option<Quantity>("--inner") { Required = true };
        var sub = new Command("sub") { inner };
        var root = new RootCommand { quantity, sub }.AcceptValueObjects();

        root.Parse(["--quantity", "1001", "sub", "--inner", "5000"]).Refusals().Should().Equal(QuantityTooLarge);
        root.Parse(["--quantity", "1001", "sub"]).Refusals().Should().Equal(QuantityTooLarge);
        root.Parse(["--quantity", "7", "sub", "--inner", "5000"]).Refusals().Should().Equal(
            new CliError("Option '--inner': The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)));
    }

    [Fact]
    public void A_parent_s_symbol_System_CommandLine_validates_itself_is_left_to_it()
    {
        var recursive = new Option<Quantity>("--quantity") { Recursive = true };
        var count = new Option<Quantity>("--count") { DefaultValueFactory = static _ => Quantity.Create(5) };
        var name = new Option<string>("--name");
        var level = new Argument<Quantity>("level") { DefaultValueFactory = static _ => Quantity.Create(1) };
        level.Validators.Add(static result =>
        {
            if (result.Tokens is [{ Value: "13" }])
            {
                result.AddError("Not 13.");
            }
        });
        var sub = new Command("sub");
        var root = new RootCommand { recursive, count, name, level, sub }.AcceptValueObjects();

        // Read here as well, a recursive option and an option with a default value would be refused twice for their arity.
        root.Parse(["sub", "--quantity", "1", "--quantity", "2"]).Refusals().Should().Equal(
            new CliError("Option '--quantity' expects a single argument but 2 were provided.", null, null));
        root.Parse(["--count", "1", "--count", "2", "sub"]).Refusals().Should().Equal(
            new CliError("Option '--count' expects a single argument but 2 were provided.", null, null));

        // Read here as well, an argument with a default value would skip its validators, which only System.CommandLine runs.
        root.Parse(["13", "sub"]).Refusals().Should().Equal(new CliError("Not 13.", null, null));

        var accepted = root.Parse(["--name", "x", "7", "sub"]);
        accepted.Errors.Should().BeEmpty();
        accepted.GetValue(level).Should().Be(Quantity.Create(7));
        accepted.GetValue(count).Should().Be(Quantity.Create(5));
    }

    [Fact]
    public void A_parent_s_symbol_configured_alone_is_read_once_a_walk_reached_the_subcommand()
    {
        var quantity = new Option<Quantity>("--quantity").AcceptValueObject();
        var iban = new Argument<Iban>("iban").AcceptValueObject();
        var sub = new Command("sub");
        var root = new Command("app") { quantity, iban, sub };
        sub.SetAction(static _ => { });

        var unread = root.Parse(["--quantity", "1001", "XX", "sub"]);
        unread.Errors.Should().BeEmpty();
        FluentActions.Invoking(() => unread.GetValue(quantity)).Should().Throw<InvalidOperationException>().WithMessage(QuantityTooLarge.Message);

        sub.AcceptValueObjects();

        root.Parse(["--quantity", "1001", "XX", "sub"]).Refusals().Should().Equal(QuantityTooLarge, IbanTooShort);
        sub.Validators.Should().ContainSingle();
        root.Validators.Should().BeEmpty();
    }
}
