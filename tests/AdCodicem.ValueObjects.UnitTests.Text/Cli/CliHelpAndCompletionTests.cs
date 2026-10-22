using System.CommandLine;
using System.Globalization;
using AdCodicem.ValueObjects.CommandLine;
using AdCodicem.ValueObjects.UnitTests.TestData;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// What the help and the completions show of a value object's rules: its summary as the description, its known values as
/// completions documented by their descriptions, a closed set listed in the help and an open set named, each kept where
/// the application set its own.
/// </summary>
public sealed class CliHelpAndCompletionTests
{
    [Fact]
    public void The_help_describes_each_symbol_by_its_value_object_unless_the_application_described_it()
    {
        var iban = new Option<Iban>("--iban");
        var count = new Option<Quantity>("--count") { Description = "How many, 5 unless set.", DefaultValueFactory = static _ => Quantity.Create(5) };
        var temperature = new Option<Celsius>("--temperature") { DefaultValueFactory = static _ => Celsius.Create(21) };
        var account = new Argument<Iban>("account");
        var payee = new Argument<Iban>("payee") { Description = "Who is paid." };
        var root = new RootCommand("Pays.") { iban, count, temperature, account, payee }.AcceptValueObjects();

        var help = Lines(root.Invoke("--help").Output);

        help.Should().Contain(static line => line.StartsWith("--iban <iban>", StringComparison.Ordinal) && line.EndsWith("An International Bank Account Number, stored in its electronic form.", StringComparison.Ordinal));
        help.Should().Contain(static line => line.StartsWith("--count <count>", StringComparison.Ordinal) && line.EndsWith("How many, 5 unless set. [default: 5]", StringComparison.Ordinal));
        help.Should().Contain(static line => line.StartsWith("--temperature <temperature>", StringComparison.Ordinal) && line.EndsWith("[default: 21 °C]", StringComparison.Ordinal));
        help.Should().Contain(static line => line.StartsWith("<account>", StringComparison.Ordinal) && line.EndsWith("An International Bank Account Number, stored in its electronic form.", StringComparison.Ordinal));
        help.Should().Contain(static line => line.StartsWith("<payee>", StringComparison.Ordinal) && line.EndsWith("Who is paid.", StringComparison.Ordinal));
    }

    [Fact]
    public void A_closed_set_is_listed_in_the_help_and_an_open_set_keeps_its_name_or_the_one_the_application_gave()
    {
        var country = new Option<CountryCode>("--country");
        var currency = new Option<CurrencyCode>("--currency");
        var named = new Option<CurrencyCode>("--settlement") { HelpName = "code" };
        var page = new Argument<PageNumber>("page");
        var answer = new Argument<Answer>("answer");
        var settled = new Argument<CurrencyCode>("settled") { HelpName = "ccy" };
        var root = new RootCommand { country, currency, named, page, answer, settled }.AcceptValueObjects();

        var help = Lines(root.Invoke("--help").Output);

        help.Should().Contain(static line => line.StartsWith("--country <BE|FR|LU>", StringComparison.Ordinal));
        help.Should().Contain(static line => line.StartsWith("--currency <currency>", StringComparison.Ordinal));
        help.Should().Contain(static line => line.StartsWith("--settlement <code>", StringComparison.Ordinal));
        help.Should().Contain(static line => line.StartsWith("<page>", StringComparison.Ordinal));
        help.Should().Contain(static line => line.StartsWith("<N|Y>", StringComparison.Ordinal));
        help.Should().Contain(static line => line.StartsWith("<ccy>", StringComparison.Ordinal));
        settled.HelpName.Should().Be("ccy");
    }

    [Fact]
    public void The_known_values_complete_each_documented_by_its_description()
    {
        var country = new Option<CountryCode>("--country");
        var currency = new Option<CurrencyCode>("--currency");
        var page = new Argument<PageNumber>("page");
        var root = new RootCommand { country, currency, page }.AcceptValueObjects();

        root.Completions("--country ").Should().Equal("BE (Belgium)", "FR (France)", "LU (Luxembourg)");
        root.Completions("--country f").Should().Equal("FR (France)");
        root.Completions("--currency ").Should().Equal("EUR", "USD");
        root.Completions("--country BE ").Should().Contain("1 (The first page.)");
    }

    [Fact]
    public void Completions_of_the_application_s_own_keep_the_known_values_out()
    {
        var country = new Option<CountryCode>("--country");
        country.CompletionSources.Add("FR", "BE");
        var answer = new Argument<Answer>("answer");
        answer.CompletionSources.Add("Y");
        var root = new RootCommand { country, answer }.AcceptValueObjects();

        root.Completions("--country ").Should().Equal("BE", "FR");
        country.CompletionSources.Should().ContainSingle();
        answer.CompletionSources.Should().ContainSingle();
        country.HelpName.Should().BeNull();
    }

    [Fact]
    public void A_schema_written_by_hand_completes_a_known_value_of_another_type_and_without_details()
    {
        var loose = new Option<Declared<CliLooseNumber, int>>("--loose");
        var root = new RootCommand { loose }.AcceptValueObjects();

        root.Completions("--loose ").Should().Equal("seven");
        loose.HelpName.Should().Be("loose");
    }

    [Theory]
    [InlineData("")]
    [InlineData("fr-FR")]
    [InlineData("th-TH")]
    [InlineData("fa-IR")]
    [InlineData("ar-SA")]
    public void The_text_of_a_completion_reads_back_in_the_culture_the_symbols_read_in(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        var cutOff = new Option<CutOffDate>("--cutoff");
        var shift = new Option<ShiftStart>("--shift");
        var launch = new Option<LaunchMoment>("--launch");
        var vat = new Option<VatRate>("--vat");
        var root = new RootCommand { cutOff, shift, launch, vat }.AcceptValueObjects(options => options.Culture = culture);

        ReadsBack(root, cutOff, CutOffDate.Epoch, CutOffDate.Millennium);
        ReadsBack(root, shift, ShiftStart.Early, ShiftStart.Late);
        ReadsBack(root, launch, LaunchMoment.Launch, LaunchMoment.Relaunch);
        ReadsBack(root, vat, VatRate.Reduced, VatRate.Standard);
    }

    /// <summary>Checks that each completion of an option reads back to a known value, in the order of their labels.</summary>
    private static void ReadsBack<T>(Command root, Option<T> option, params T[] known)
    {
        var labels = root.Parse(option.Name + " ").GetCompletions(option.Name.Length + 1).Select(static item => item.Label).ToList();
        labels.Should().HaveCount(known.Length);
        var read = labels.Select(label =>
        {
            var result = root.Parse([option.Name, label]);
            result.Errors.Should().BeEmpty("'{0}' reads back", label);
            return result.GetValue(option);
        });
        read.Should().BeEquivalentTo(known);
    }

    /// <summary>Gets the lines of the help, trimmed.</summary>
    private static IReadOnlyList<string> Lines(string help) => [.. help.Split('\n').Select(static line => line.Trim())];
}
