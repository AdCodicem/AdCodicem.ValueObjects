using System.CommandLine;
using System.Globalization;
using AdCodicem.ValueObjects.CommandLine;
using CultureScope = AdCodicem.ValueObjects.UnitTests.Forms.CultureScope;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// The culture text is read in: the invariant one unless the application names another, whatever the current culture,
/// and a date in the Gregorian calendar whatever the culture's own.
/// </summary>
public sealed class CliCultureTests
{
    [Fact]
    public void The_invariant_culture_is_the_default_whatever_the_current_one_and_refuses_a_group_separator()
    {
        using var scope = new CultureScope("fr-FR", "fr-FR");
        var amount = new Option<Amount>("--amount");
        var birth = new Option<BirthDate>("--birth");
        var root = new RootCommand { amount, birth }.AcceptValueObjects();

        root.Parse(["--amount", "12,5"]).Refusals().Should().Equal(
            new CliError("Option '--amount': The text is not a valid decimal.", ValueObjectErrorCodes.NotParsable, typeof(Amount)));
        root.Parse(["--amount", "12.5"]).GetValue(amount).Should().Be(Amount.Create(12.5m));
        root.Parse(["--birth", "03/10/1990"]).GetValue(birth).Should().Be(BirthDate.Create(new DateOnly(1990, 3, 10)));
    }

    [Fact]
    public void A_culture_the_application_names_reads_its_numbers_and_dates_as_its_people_write_them()
    {
        var amount = new Option<Amount>("--amount");
        var birth = new Option<BirthDate>("--birth");
        var root = new RootCommand { amount, birth }.AcceptValueObjects(static options => options.Culture = CultureInfo.GetCultureInfo("fr-FR"));

        root.Parse(["--amount", "12,5"]).GetValue(amount).Should().Be(Amount.Create(12.5m));
        root.Parse(["--amount", "12.5"]).Refusals().Should().Equal(
            new CliError("Option '--amount': The text is not a valid decimal.", ValueObjectErrorCodes.NotParsable, typeof(Amount)));
        root.Parse(["--birth", "03/10/1990"]).GetValue(birth).Should().Be(BirthDate.Create(new DateOnly(1990, 10, 3)));
    }

    [Theory]
    [InlineData("th-TH")]
    [InlineData("fa-IR")]
    public void A_date_is_read_in_the_Gregorian_calendar_whatever_the_culture_s_own(string name)
    {
        var birth = new Option<BirthDate>("--birth");
        var recorded = new Option<RecordedAt>("--recorded");
        var occurred = new Option<OccurredAt>("--occurred");
        var root = new RootCommand { birth, recorded, occurred }.AcceptValueObjects(options => options.Culture = CultureInfo.GetCultureInfo(name));

        var result = root.Parse(["--birth", "1990-10-14", "--recorded", "2024-05-06T07:08:09", "--occurred", "2024-05-06T07:08:09+02:00"]);

        result.Errors.Should().BeEmpty();
        result.GetValue(birth).Should().Be(BirthDate.Create(new DateOnly(1990, 10, 14)));
        result.GetValue(recorded).Value.Year.Should().Be(2024);
        result.GetValue(occurred).Value.Year.Should().Be(2024);
    }
}
