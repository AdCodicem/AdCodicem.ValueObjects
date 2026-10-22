using System.CommandLine;
using AdCodicem.ValueObjects.CommandLine;
using CultureScope = AdCodicem.ValueObjects.UnitTests.Forms.CultureScope;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// What System.CommandLine does that the package rests on or works around, pinned, so that a version changing one fails
/// here first: its converters hold no value object, it gives the flag syntax to <see cref="bool"/> alone, validators run
/// before any parser, an option's errors are listed under the option, a parse error refuses a blank message, a parent
/// command's symbols are converted only when asked for, and a command's validator adding an error stops its validation.
/// </summary>
public sealed class ParserAssumptionsTests
{
    [Fact]
    public void Without_the_package_a_valid_value_parses_with_no_error_and_GetValue_then_throws()
    {
        var quantity = new Option<Quantity>("--quantity");
        var root = new RootCommand { quantity };

        var result = root.Parse(["--quantity", "3"]);

        result.Errors.Should().BeEmpty();
        FluentActions.Invoking(() => result.GetValue(quantity)).Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot parse argument '3' for option '--quantity' as expected type 'AdCodicem.ValueObjects.UnitTests.Domain.Quantity'.");
        ((IValueObject<Quantity, short>)root.Parse([]).GetValue(quantity)).IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Without_the_package_a_value_object_over_bool_is_no_flag()
    {
        var consent = new Option<Consent>("--consent");

        new RootCommand { consent }.Parse(["--consent"]).Errors.Select(static error => error.Message).Should().Equal("Required argument missing for option: '--consent'.");
        consent.Arity.Should().Be(ArgumentArity.ExactlyOne);
    }

    [Fact]
    public void A_validator_runs_before_the_parser_on_the_text_as_typed_and_answers_without_a_code()
    {
        var country = new Option<CountryCode>("--country").AcceptValueObject().AcceptOnlyFromAmong("BE", "FR");

        var error = new RootCommand { country }.Parse(["--country", "be"]).Errors.Should().ContainSingle().Which;

        error.Message.Should().StartWith("Argument 'be' not recognized. Must be one of:");
        error.TryGetValueObjectErrorCode(out _).Should().BeFalse();
    }

    [Fact]
    public void The_errors_of_an_option_s_argument_are_listed_under_the_option()
    {
        var quantity = new Option<Quantity>("--quantity").AcceptValueObject();

        var result = new RootCommand { quantity }.Parse(["--quantity", "1001"]);

        result.GetResult(quantity)!.Errors.Should().ContainSingle().Which.Should().BeSameAs(result.Errors.Single());
    }

    [Fact]
    public void A_parse_error_refuses_a_blank_message()
    {
        var quantity = new Option<int>("--quantity");
        quantity.Validators.Add(static result => result.AddError(" "));

        FluentActions.Invoking(() => new RootCommand { quantity }.Parse(["--quantity", "1"])).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void An_option_given_alone_whose_type_is_not_bool_reads_its_default_value_not_its_parser()
    {
        var parsed = new List<int>();
        var count = new Option<int>("--count")
        {
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = static _ => 5,
            CustomParser = result =>
            {
                parsed.Add(result.Tokens.Count);
                return 7;
            },
        };

        new RootCommand { count }.Parse(["--count"]).GetValue(count).Should().Be(5);
        parsed.Should().BeEmpty();
    }

    [Fact]
    public void Its_own_decimal_option_reads_a_group_separator_in_the_current_culture()
    {
        using var scope = new CultureScope(string.Empty, string.Empty);
        var amount = new Option<decimal>("--amount");

        new RootCommand { amount }.Parse(["--amount", "12,5"]).GetValue(amount).Should().Be(125m);
    }

    [Fact]
    public void A_parent_command_s_option_and_argument_are_converted_only_when_GetValue_asks_for_them()
    {
        var count = new Option<int>("--count");
        var level = new Argument<int>("level");
        var sub = new Command("sub");
        var root = new Command("app") { count, level, sub };
        sub.SetAction(static _ => { });

        var result = root.Parse(["--count", "abc", "def", "sub"]);

        result.Errors.Should().BeEmpty();
        FluentActions.Invoking(() => result.GetValue(count)).Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot parse argument 'abc' for option '--count' as expected type 'System.Int32'.");
        FluentActions.Invoking(() => result.GetValue(level)).Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot parse argument 'def' for command 'app' as expected type 'System.Int32'.");
    }

    [Fact]
    public void A_command_whose_validator_added_an_error_leaves_its_own_options_and_arguments_unvalidated()
    {
        var inner = new Option<int>("--inner") { Required = true };
        var sub = new Command("sub") { inner };
        sub.Validators.Add(static result => result.AddError("Refused by the command."));

        new RootCommand { sub }.Parse(["sub"]).Errors.Select(static error => error.Message).Should().Equal("Refused by the command.");
    }

    [Fact]
    public void An_option_left_out_calls_no_parser()
    {
        var parsed = 0;
        var quantity = new Option<Quantity>("--quantity") { CustomParser = _ => Quantity.Create((short)++parsed) };

        new RootCommand { quantity }.Parse([]).GetResult(quantity).Should().BeNull();
        parsed.Should().Be(0);
    }
}
