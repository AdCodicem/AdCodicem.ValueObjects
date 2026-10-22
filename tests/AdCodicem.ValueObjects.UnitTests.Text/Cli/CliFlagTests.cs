using System.CommandLine;
using AdCodicem.ValueObjects.CommandLine;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// The option of a value object over <see cref="bool"/>, a flag as System.CommandLine's own <see cref="bool"/> option is:
/// given alone it reads <see langword="true"/>, through the type's rules, its default value factory serving it left out,
/// and a token after it is its value.
/// </summary>
public sealed class CliFlagTests
{
    private const string Electronic = "FR7630006000011234567890189";

    [Fact]
    public void A_flag_reads_true_given_alone_and_its_value_in_either_syntax()
    {
        var consent = new Option<Consent>("--consent").AcceptValueObject();
        var maybe = new Option<Consent?>("--maybe").AcceptValueObject();
        var root = new RootCommand { consent, maybe };

        Read(root, consent, "--consent").Should().Be(Consent.Create(true));
        Read(root, consent, "--consent", "false").Should().Be(Consent.Create(false));
        Read(root, consent, "--consent:false").Should().Be(Consent.Create(false));
        Read(root, maybe, "--maybe").Should().Be(Consent.Create(true));
        Read(root, maybe, "--maybe:False").Should().Be(Consent.Create(false));

        root.Parse(["--consent", "maybe"]).Refusals().Should().Equal(
            new CliError("Option '--consent': The text is not a valid bool.", ValueObjectErrorCodes.NotParsable, typeof(Consent)));

        var absent = root.Parse([]);
        ((IValueObject<Consent, bool>)absent.GetValue(consent)).IsDefault.Should().BeTrue();
        absent.GetValue(maybe).Should().BeNull();
    }

    [Fact]
    public void A_flag_s_default_value_factory_serves_it_left_out_and_in_the_help_but_not_given_alone()
    {
        var consent = new Option<Consent>("--consent") { DefaultValueFactory = static _ => Consent.Create(false) }.AcceptValueObject();
        var maybe = new Option<Consent?>("--maybe") { DefaultValueFactory = static _ => Consent.Create(false) }.AcceptValueObject();
        var root = new RootCommand { consent, maybe };

        Read(root, consent, "--consent").Should().Be(Consent.Create(true));
        Read(root, consent).Should().Be(Consent.Create(false));
        Read(root, consent, "--consent", "false").Should().Be(Consent.Create(false));
        Read(root, maybe, "--maybe").Should().Be(Consent.Create(true));
        Read(root, maybe).Should().Be(Consent.Create(false));
        consent.GetDefaultValue().Should().Be(Consent.Create(false));
    }

    [Fact]
    public void A_default_value_factory_set_after_the_call_serves_the_flag_given_alone_as_System_CommandLine_calls_it()
    {
        var consent = new Option<Consent>("--consent").AcceptValueObject();
        consent.DefaultValueFactory = static _ => Consent.Create(false);

        Read(new RootCommand { consent }, consent, "--consent").Should().Be(Consent.Create(false));
    }

    [Fact]
    public void A_flag_its_rules_refuse_is_refused_given_alone_with_their_code()
    {
        var live = new Option<LiveRun>("--live").AcceptValueObject();
        var staged = new Option<LiveRun>("--staged") { DefaultValueFactory = static _ => LiveRun.Create(false) }.AcceptValueObject();
        var terms = new Option<TermsAccepted>("--terms").AcceptValueObject();
        var root = new RootCommand { live, staged, terms };

        root.Parse(["--live", "--staged"]).Refusals().Should().Equal(
            new CliError("Option '--live': Live runs are off.", LiveRun.Forbidden, typeof(LiveRun)),
            new CliError("Option '--staged': Live runs are off.", LiveRun.Forbidden, typeof(LiveRun)));
        Read(root, live, "--live", "false").Should().Be(LiveRun.Create(false));
        Read(root, terms, "--terms").Should().Be(TermsAccepted.Accepted);
        root.Parse(["--terms", "false"]).Refusals().Should().Equal(
            new CliError("Option '--terms': The value is not one of the accepted values.", ValueObjectErrorCodes.NotAKnownValue, typeof(TermsAccepted)));
    }

    [Fact]
    public void An_arity_the_application_set_is_kept_but_one_value_cannot_be_told_from_System_CommandLine_s_default()
    {
        var zero = new Option<Consent>("--consent") { Arity = ArgumentArity.Zero }.AcceptValueObject();
        var exactlyOne = new Option<Consent>("--agreed") { Arity = ArgumentArity.ExactlyOne }.AcceptValueObject();
        var root = new RootCommand { zero, exactlyOne };

        zero.Arity.Should().Be(ArgumentArity.Zero);
        exactlyOne.Arity.Should().Be(ArgumentArity.ZeroOrOne);
        Read(root, zero, "--consent").Should().Be(Consent.Create(true));
        root.Parse(["--consent", "false"]).Refusals().Should().Equal(new CliError("Unrecognized command or argument 'false'.", null, null));
    }

    [Fact]
    public void A_token_right_after_the_flag_is_its_value_so_a_positional_argument_there_is_refused()
    {
        var iban = new Argument<Iban>("iban");
        var consent = new Option<Consent>("--consent");
        var root = new Command("app") { iban, consent }.AcceptValueObjects();

        root.Parse(["--consent", Electronic]).Refusals().Should().Equal(
            new CliError("Option '--consent': The text is not a valid bool.", ValueObjectErrorCodes.NotParsable, typeof(Consent)),
            new CliError("Required argument missing for command: 'app'.", null, null));
        Read(root, consent, Electronic, "--consent").Should().Be(Consent.Create(true));
    }

    [Fact]
    public void Neither_an_argument_nor_a_collection_of_a_value_object_over_bool_is_a_flag()
    {
        var consent = new Argument<Consent>("consent").AcceptValueObject();
        var consents = new Option<Consent[]>("--consents") { AllowMultipleArgumentsPerToken = true }.AcceptValueObject();
        var root = new Command("app") { consent, consents };

        consents.Arity.Should().Be(ArgumentArity.OneOrMore);
        root.Parse([]).Refusals().Should().Equal(new CliError("Required argument missing for command: 'app'.", null, null));
        Read(root, consent, "true").Should().Be(Consent.Create(true));
        Read(root, consents, "true", "--consents", "true", "false").Should().Equal(Consent.Create(true), Consent.Create(false));
    }

    /// <summary>Gets the value of an option a command line gives, which no error refuses.</summary>
    private static T? Read<T>(Command root, Option<T> option, params string[] args)
    {
        var result = root.Parse(args);
        result.Errors.Should().BeEmpty();
        return result.GetValue(option);
    }

    /// <summary>Gets the value of an argument a command line gives, which no error refuses.</summary>
    private static T? Read<T>(Command root, Argument<T> argument, params string[] args)
    {
        var result = root.Parse(args);
        result.Errors.Should().BeEmpty();
        return result.GetValue(argument);
    }
}
