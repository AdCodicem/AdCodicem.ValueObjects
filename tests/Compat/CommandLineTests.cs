using System.CommandLine;
using System.Globalization;
using AdCodicem.ValueObjects.CommandLine;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// The System.CommandLine package as packed, built against System.CommandLine 2.0, on 3.0, the line the next major of
/// .NET ships with: options and arguments of value objects read through their rules, a refusal a parse error carrying the
/// rule's code, an invocation answering it on standard error with exit code 1, and the messages of a resource.
/// </summary>
public sealed class CommandLineTests
{
    private const string Electronic = "FR7630006000011234567890189";

    [Fact]
    public void The_package_runs_on_the_next_major_of_System_CommandLine()
        => typeof(RootCommand).Assembly.GetName().Version!.Major.Should().Be(3);

    [Fact]
    public void Options_and_arguments_read_their_text_through_the_rules_and_refuse_with_a_coded_parse_error()
    {
        var iban = new Option<Iban>("--iban") { Required = true };
        var quantity = new Option<Quantity?>("--quantity");
        var countries = new Option<CountryCode[]>("--country") { AllowMultipleArgumentsPerToken = true };
        var reference = new Argument<Reference<PurchaseOrder>>("reference");
        var root = new RootCommand { iban, quantity, countries, reference }.AcceptValueObjects();

        var accepted = root.Parse(["--iban", "fr76 3000 6000 0112 3456 7890 189", "--country", "fr", "lu", "--", " po-1042 "]);
        accepted.Errors.Should().BeEmpty();
        accepted.GetValue(iban).Should().Be(Iban.Create(Electronic));
        accepted.GetValue(quantity).Should().BeNull();
        accepted.GetValue(countries).Should().Equal(CountryCode.France, CountryCode.Luxembourg);
        accepted.GetValue(reference).Should().Be(Reference<PurchaseOrder>.Create("PO-1042"));

        var refused = root.Parse(["--iban", "XX", "--quantity", "500", "--country", "fr", "de", "--", "ABCDEFGHIJKLMNOP"]);
        refused.Errors.Select(static error => (error.Message, error.TryGetValueObjectErrorCode(out var code) ? code : null)).Should().BeEquivalentTo(
        [
            ("Option '--iban': The value must be at least 15 characters long.", ValueObjectErrorCodes.TooShort),
            ("Option '--quantity': The value must be less than or equal to 100.", ValueObjectErrorCodes.OutOfRange),
            ("Option '--country', the 2nd value: The value is not one of the accepted values.", ValueObjectErrorCodes.NotAKnownValue),
            ("Argument 'reference': The value must be at most 12 characters long.", ValueObjectErrorCodes.TooLong),
        ]);
        refused.Errors[0].TryGetValueObjectType(out var type).Should().BeTrue();
        type.Should().Be<Iban>();
    }

    [Fact]
    public void An_invocation_writes_a_refusal_to_standard_error_and_returns_1_without_running_the_action()
    {
        var quantity = new Option<Quantity>("--quantity");
        var ran = false;
        var root = new RootCommand { quantity }.AcceptValueObjects();
        root.SetAction(_ => ran = true);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = root.Parse(["--quantity", "500"]).Invoke(new InvocationConfiguration { Output = output, Error = error });

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("Option '--quantity': The value must be less than or equal to 100.").And.NotContain("500");
        ran.Should().BeFalse();
    }

    [Fact]
    public void A_refusal_is_worded_in_French_by_the_application_s_resource()
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            var quantity = new Option<Quantity>("--quantity");
            var ibans = new Option<Iban[]>("--ibans") { AllowMultipleArgumentsPerToken = true };
            var country = new Option<CountryCode>("--country");
            var root = new RootCommand { quantity, ibans, country }.AcceptValueObjects(static options => options.UseResource<Messages>());

            root.Parse(["--quantity", "500", "--ibans", Electronic, "XX", "--country", "DE"]).Errors.Select(static error => error.Message).Should().BeEquivalentTo(
                "« --quantity » doit être compris entre 1 et 100.",
                "« --ibans[1] » : au moins 15 caractères.",
                "« --country » doit être l'une des valeurs suivantes : FR; BE; LU.");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }
}
