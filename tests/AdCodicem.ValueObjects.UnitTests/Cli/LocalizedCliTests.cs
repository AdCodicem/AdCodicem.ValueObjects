using System.CommandLine;
using AdCodicem.ValueObjects.CommandLine;
using AdCodicem.ValueObjects.UnitTests.Web;
using CultureScope = AdCodicem.ValueObjects.UnitTests.Forms.CultureScope;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// The messages of refusals in the application's language, read from the resource <c>UseResource&lt;T&gt;()</c> names
/// through a <see cref="System.Resources.ResourceManager"/>, in the current UI culture, the symbol as <c>{0}</c>, and the
/// English message where the resource has no entry, or a blank one.
/// </summary>
public sealed class LocalizedCliTests
{
    private const string Electronic = "FR7630006000011234567890189";

    [Fact]
    public void A_refusal_is_worded_by_the_resource_under_the_key_of_its_code_the_symbol_as_typed_quoted()
    {
        using var scope = new CultureScope("fr-FR", "fr-FR");
        var quantity = new Option<Quantity>("--quantity", "-q");
        var country = new Option<CountryCode>("--country");
        var page = new Option<PageNumber>("--page");
        var tolerance = new Option<Tolerance>("--tolerance");
        var effective = new Option<EffectiveDate>("--effective");
        var ibans = new Option<Iban[]>("--ibans") { AllowMultipleArgumentsPerToken = true };
        var iban = new Argument<Iban?>("iban");
        var root = new RootCommand { quantity, country, page, tolerance, effective, ibans, iban }
            .AcceptValueObjects(static options => options.UseResource<ValidationMessages>());

        Messages(root, "--quantity", "1001").Should().Equal("« --quantity » doit être compris entre 0 et 1000.");
        Messages(root, "-q", "1001").Should().Equal("« -q » doit être compris entre 0 et 1000.");
        Messages(root, "--country", "XX").Should().Equal("« --country » doit être l'une des valeurs suivantes : FR; BE; LU.");
        Messages(root, "--page", "0").Should().Equal("« --page » doit valoir au moins 1.");
        Messages(root, "--tolerance", "1.5").Should().Equal("« --tolerance » doit valoir au plus 1.");
        Messages(root, "--effective", "1999-12-31").Should().Equal("« --effective » doit valoir au moins 01/01/2000.");
        Messages(root, "--ibans", Electronic, "XX", "YY").Should().Equal("« --ibans[1] » : au moins 15 caractères.", "« --ibans[2] » : au moins 15 caractères.");
        Messages(root, "XX").Should().Equal("« iban » : au moins 15 caractères.");
        root.Parse(["--quantity", "three"]).Refusals().Should().Equal(
            new CliError("« --quantity » n'a pas la forme attendue.", ValueObjectErrorCodes.NotParsable, typeof(Quantity)));
    }

    [Fact]
    public void A_key_the_resource_lacks_a_culture_it_lacks_and_a_blank_entry_keep_the_English_message()
    {
        var sparse = new Option<Iban>("--iban").AcceptValueObject(static options => options.UseResource<SparseMessages>());
        var live = new Option<LiveRun>("--live").AcceptValueObject(static options => options.UseResource<ValidationMessages>());
        var quantity = new Option<Quantity>("--quantity").AcceptValueObject(static options => options.UseResource<ValidationMessages>());
        var blank = new Option<Quantity>("--count").AcceptValueObject(static options => options.UseResource<BlankMessages>());
        var root = new RootCommand { sparse, live, quantity, blank };

        using (new CultureScope("fr-FR", "fr-FR"))
        {
            Messages(root, "--iban", "XX").Should().Equal("Option '--iban': The value must be at least 15 characters long.");
            Messages(root, "--live").Should().Equal("Option '--live': Live runs are off.");
            Messages(root, "--count", "1001").Should().Equal("Option '--count': The value must be less than or equal to 1000.");
        }

        using (new CultureScope("de-DE", "de-DE"))
        {
            root.Parse(["--quantity", "1001"]).Refusals().Should().Equal(
                new CliError("Option '--quantity': The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity)));
        }
    }

    [Fact]
    public void A_template_quoting_an_argument_its_code_does_not_supply_is_the_application_s_error_to_see()
    {
        using var scope = new CultureScope("fr-FR", "fr-FR");
        var quantity = new Option<Quantity>("--quantity").AcceptValueObject(static options => options.UseResource<SparseMessages>());

        FluentActions.Invoking(() => new RootCommand { quantity }.Parse(["--quantity", "three"])).Should().Throw<FormatException>();
    }

    /// <summary>Gets the messages of the errors a command line gives, each of which carries a code.</summary>
    private static IReadOnlyList<string> Messages(Command root, params string[] args)
    {
        var refusals = root.Parse(args).Refusals();
        refusals.Should().AllSatisfy(static refusal => refusal.Code.Should().NotBeNull());
        return [.. refusals.Select(static refusal => refusal.Message)];
    }
}
