using System.CommandLine;
using System.Globalization;
using AdCodicem.ValueObjects.CommandLine;
using AdCodicem.ValueObjects.UnitTests.Web;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>The options of the System.CommandLine symbols: their defaults, their checks, and when they are read.</summary>
public sealed class ValueObjectArgumentOptionsTests
{
    [Fact]
    public void The_defaults_read_in_the_invariant_culture_with_the_English_messages()
    {
        var options = new ValueObjectArgumentOptions();

        options.Culture.Should().BeSameAs(CultureInfo.InvariantCulture);
        options.ResourceType.Should().BeNull();
    }

    [Fact]
    public void A_resource_is_named_by_its_type_and_the_options_chain()
    {
        var options = new ValueObjectArgumentOptions();

        options.UseResource<ValidationMessages>().Should().BeSameAs(options);
        options.ResourceType.Should().Be<ValidationMessages>();
        options.UseResource<SparseMessages>().Should().BeSameAs(options);
        options.ResourceType.Should().Be<SparseMessages>();
    }

    [Fact]
    public void A_null_culture_or_resource_is_refused()
    {
        var options = new ValueObjectArgumentOptions();

        FluentActions.Invoking(() => options.Culture = null!).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => options.UseResource(null!)).Should().Throw<ArgumentNullException>().WithParameterName("resourceType");
    }

    [Fact]
    public void The_options_are_read_when_the_call_returns_and_changing_them_afterwards_changes_nothing()
    {
        ValueObjectArgumentOptions? kept = null;
        var amount = new Option<Amount>("--amount").AcceptValueObject(options => kept = options);
        kept!.Culture = CultureInfo.GetCultureInfo("fr-FR");

        new RootCommand { amount }.Parse(["--amount", "12,5"]).Refusals().Should().ContainSingle()
            .Which.Code.Should().Be(ValueObjectErrorCodes.NotParsable);
    }
}
