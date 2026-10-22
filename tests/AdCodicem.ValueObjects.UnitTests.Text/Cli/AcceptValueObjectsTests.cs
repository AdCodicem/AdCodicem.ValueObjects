using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using AdCodicem.ValueObjects.CommandLine;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// <c>AcceptValueObjects</c> on a command: every option and argument of a value object configured, in the command and its
/// subcommands, each once, leaving alone the symbols of other types, the ones whose parser the application set, and the
/// ones added after it.
/// </summary>
public sealed class AcceptValueObjectsTests
{
    private const string Electronic = "FR7630006000011234567890189";

    private const string CannotParse = "Cannot parse argument '7' for option '--quantity' as expected type 'AdCodicem.ValueObjects.UnitTests.Domain.Quantity'.";

    [Fact]
    public void Every_option_and_argument_of_a_value_object_is_read_in_the_command_and_its_subcommands_and_nothing_else()
    {
        var quantity = new Option<Quantity>("--quantity");
        var name = new Option<string>("--name");
        var verbose = new Option<bool>("--verbose");
        var iban = new Argument<Iban>("iban");
        var countries = new Option<CountryCode[]>("--country") { AllowMultipleArgumentsPerToken = true };
        var amount = new Argument<Amount?>("amount");
        var pay = new Command("pay") { iban, amount, new Command("deep") { countries } };
        var root = new RootCommand { quantity, name, verbose, pay };
        pay.SetAction(static _ => { });

        root.AcceptValueObjects().Should().BeSameAs(root);

        var accepted = root.Parse(["--quantity", "7", "--name", "x", "--verbose", "pay", "fr76 3000 6000 0112 3456 7890 189", "12.5"]);
        accepted.Errors.Should().BeEmpty();
        accepted.GetValue(quantity).Should().Be(Quantity.Create(7));
        accepted.GetValue(name).Should().Be("x");
        accepted.GetValue(verbose).Should().BeTrue();
        accepted.GetValue(iban).Should().Be(Iban.Create(Electronic));
        accepted.GetValue(amount).Should().Be(Amount.Create(12.5m));
        root.Parse(["pay", Electronic, "deep", "--country", "fr", "lu"]).GetValue(countries).Should().Equal(CountryCode.France, CountryCode.Luxembourg);

        root.Parse(["pay", "XX"]).Refusals().Should().Equal(
            new CliError("Argument 'iban': The value must be at least 15 characters long.", ValueObjectErrorCodes.TooShort, typeof(Iban)));
        root.Parse(["pay", Electronic, "deep", "--country", "fr", "de"]).Refusals().Should().Equal(
            new CliError("Option '--country', the 2nd value: The value is not one of the accepted values.", ValueObjectErrorCodes.NotAKnownValue, typeof(CountryCode)));
    }

    [Fact]
    public void A_recursive_option_is_read_through_its_rules_by_the_walk_of_the_command_declaring_it()
    {
        var quantity = new Option<Quantity>("--quantity") { Recursive = true };
        var root = new RootCommand { quantity, new Command("sub") }.AcceptValueObjects();

        root.Parse(["sub", "--quantity", "7"]).GetValue(quantity).Should().Be(Quantity.Create(7));
        root.Parse(["sub", "--quantity", "1001"]).Refusals().Should().Equal(
            new CliError("Option '--quantity': The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)));

        var elsewhere = new Option<Quantity>("--quantity") { Recursive = true };
        var sub = new Command("sub");
        var other = new RootCommand { elsewhere, sub };
        sub.AcceptValueObjects();

        var unread = other.Parse(["sub", "--quantity", "7"]);
        FluentActions.Invoking(() => unread.GetValue(elsewhere)).Should().Throw<InvalidOperationException>().WithMessage(CannotParse);
    }

    [Fact]
    public void A_symbol_and_a_subcommand_shared_by_two_commands_are_configured_once()
    {
        var country = new Option<CountryCode>("--country");
        var shared = new Command("shared") { new Option<CountryCode>("--origin") };
        var root = new RootCommand { new Command("a") { country, shared }, new Command("b") { country, shared } };

        root.AcceptValueObjects();
        root.AcceptValueObjects();

        country.CompletionSources.Should().ContainSingle();
        shared.Options.Single().CompletionSources.Should().ContainSingle();
        shared.Validators.Should().ContainSingle();
        root.Validators.Should().ContainSingle();
        root.Parse(["b", "--country", "be"]).GetValue(country).Should().Be(CountryCode.Belgium);
        root.Parse(["a", "shared", "--origin", "XX"]).Refusals().Should().ContainSingle().Which.Code.Should().Be(ValueObjectErrorCodes.NotAKnownValue);
    }

    [Fact]
    public void A_command_reached_again_through_its_own_subcommands_is_walked_once()
    {
        var quantity = new Option<Quantity>("--quantity");
        var inner = new Command("inner");
        var outer = new Command("outer") { quantity, inner };
        inner.Add(outer);

        new RootCommand { outer }.AcceptValueObjects();

        quantity.CustomParser.Should().NotBeNull();
    }

    [Fact]
    public void A_symbol_configured_already_keeps_its_settings_and_one_with_a_parser_of_the_application_or_another_type_keeps_it()
    {
        var amount = new Option<Amount>("--amount").AcceptValueObject(static options => options.Culture = CultureInfo.GetCultureInfo("fr-FR"));
        var quantity = new Option<Quantity>("--quantity") { CustomParser = static _ => Quantity.Create(42) };
        var iban = new Argument<Iban>("iban") { CustomParser = static _ => Iban.Create(Electronic) };
        var note = new Argument<string>("note");
        var root = new RootCommand { amount, quantity, iban, note }.AcceptValueObjects();

        var result = root.Parse(["--amount", "12,5", "--quantity", "5000", "XX", "hello"]);

        result.Errors.Should().BeEmpty();
        result.GetValue(amount).Should().Be(Amount.Create(12.5m));
        result.GetValue(quantity).Should().Be(Quantity.Create(42));
        result.GetValue(iban).Should().Be(Iban.Create(Electronic));
        result.GetValue(note).Should().Be("hello");
    }

    [Fact]
    public void A_symbol_added_after_the_walk_is_left_to_System_CommandLine()
    {
        var root = new RootCommand().AcceptValueObjects();
        var quantity = new Option<Quantity>("--quantity");
        root.Add(quantity);

        var result = root.Parse(["--quantity", "7"]);

        result.Errors.Should().BeEmpty();
        FluentActions.Invoking(() => result.GetValue(quantity)).Should().Throw<InvalidOperationException>().WithMessage(CannotParse);
    }

    [Fact]
    public void A_value_object_in_a_shape_that_is_not_read_is_refused_by_the_walk()
    {
        FluentActions.Invoking(static () => new RootCommand { new Option<Quantity?[]>("--quantities") }.AcceptValueObjects())
            .Should().Throw<InvalidOperationException>().WithMessage("'--quantities' holds 'Quantity?[]', a value object in a shape AcceptValueObject does not read*");
        FluentActions.Invoking(static () => new RootCommand { new Command("sub") { new Argument<Quantity?[]>("quantities") } }.AcceptValueObjects())
            .Should().Throw<InvalidOperationException>().WithMessage("'quantities' holds 'Quantity?[]', a value object in a shape AcceptValueObject does not read*");
        FluentActions.Invoking(static () => ((RootCommand)null!).AcceptValueObjects()).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void An_option_left_out_is_named_by_its_name_where_nothing_was_typed()
    {
        var count = new Option<Quantity>("--count", "-c") { DefaultValueFactory = static _ => Quantity.Create(5) };
        var typed = new Option<Quantity>("--quantity", "-q");
        var result = new RootCommand { count, typed }.Parse(["-q", "7"]);

        ValueObjectSymbol.NameOf(result.GetResult(count)!).Should().Be("--count");
        ValueObjectSymbol.NameOf(result.GetResult(typed)!).Should().Be("-q");
        result.GetResult(count)!.Implicit.Should().BeTrue();
        result.GetResult(typed).Should().BeOfType<OptionResult>();
    }
}
