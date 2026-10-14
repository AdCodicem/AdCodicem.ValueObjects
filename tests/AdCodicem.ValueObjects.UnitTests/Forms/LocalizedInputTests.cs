using AdCodicem.ValueObjects.Blazor;
using AdCodicem.ValueObjects.Fixtures.Blazor;
using AdCodicem.ValueObjects.UnitTests.Web;
using Bunit;

namespace AdCodicem.ValueObjects.UnitTests.Forms;

/// <summary>
/// The messages of refusals in the application's language, read by code from <c>Web/ValidationMessages.fr.resx</c> once
/// <c>AddValueObjectInputs(options =&gt; options.UseResource&lt;T&gt;())</c> names it: <c>{0}</c> the input's
/// <c>DisplayName</c>, or else its field's name, the arguments the type's rules supply in the current culture, and the
/// English message kept for a key the resource lacks.
/// </summary>
public sealed class LocalizedInputTests
{
    [Fact]
    public void A_refusal_is_answered_in_the_language_of_the_current_UI_culture()
    {
        using (FormsHarness.Culture("fr-FR"))
        {
            using var context = Localized<ValidationMessages>();
            var iban = context.Form(new Holder<Iban>(), static parameters => parameters.Add(static form => form.DisplayName, "IBAN"));

            iban.Type("XX");
            iban.Messages.Should().Equal("« IBAN » : au moins 15 caractères.");
            iban.Code.Should().Be(ValueObjectErrorCodes.TooShort);

            iban.Type(string.Empty);
            iban.Messages.Should().Equal("« IBAN » est obligatoire.");
            iban.Code.Should().Be(ValueObjectErrorCodes.Required);

            var optional = context.Form(new Holder<Iban?>(), static parameters => parameters.Add(static form => form.DisplayName, "IBAN"));
            optional.Type("XX");
            optional.Messages.Should().Equal("« IBAN » : au moins 15 caractères.");

            var quantity = context.Form(new Holder<Quantity>());
            quantity.Type("1001");
            quantity.Messages.Should().Equal("« Value » doit être compris entre 0 et 1000.");
            quantity.Code.Should().Be(ValueObjectErrorCodes.OutOfRange);

            var country = context.Form(new Holder<CountryCode>(), static parameters => parameters.Add(static form => form.DisplayName, "Pays"));
            country.Type("XX");
            country.Messages.Should().Equal("« Pays » doit être l'une des valeurs suivantes : FR; BE; LU.");
        }
    }

    [Fact]
    public void The_bounds_a_message_quotes_are_written_in_the_current_culture()
    {
        using (FormsHarness.Culture("fr-FR"))
        {
            using var context = Localized<ValidationMessages>();

            var born = context.Form(new Holder<BirthDate>(), static parameters => parameters.Add(static form => form.DisplayName, "Naissance"));
            born.Type("01/01/1850");
            born.Messages.Should().Equal("« Naissance » doit être compris entre 01/01/1900 et 31/12/2100.");

            var mass = context.Form(new Holder<Mass>(), static parameters => parameters.Add(static form => form.DisplayName, "Masse"));
            mass.Type("1E+40");
            mass.Messages.Should().Equal("« Masse » doit être compris entre 9,1E-31 et 2E+32.");
        }
    }

    [Fact]
    public void Another_language_and_a_key_the_resource_lacks_keep_the_English_message()
    {
        using (FormsHarness.Culture("fr-FR", uiCulture: "en-US"))
        {
            using var context = Localized<ValidationMessages>();
            var iban = context.Form(new Holder<Iban>());

            iban.Type("XX");

            iban.Messages.Should().Equal("The value must be at least 15 characters long.");
        }

        using (FormsHarness.Culture("fr-FR"))
        {
            using var context = Localized<SparseMessages>();
            var iban = context.Form(new Holder<Iban>());
            var quantity = context.Form(new Holder<Quantity>(), static parameters => parameters.Add(static form => form.DisplayName, "Quantité"));

            iban.Type("XX");
            quantity.Type("1001");

            iban.Messages.Should().Equal("The value must be at least 15 characters long.");
            quantity.Messages.Should().Equal("« Quantité » : de 0 à 1000.");
        }
    }

    [Fact]
    public void A_message_of_the_application_s_own_rule_with_no_key_keeps_its_text()
    {
        using (FormsHarness.Culture("fr-FR"))
        {
            using var context = Localized<ValidationMessages>();
            var code = context.Form(new Holder<UnexplainedCode>(), static parameters => parameters.Add(static form => form.DisplayName, "Code"));

            code.Type("quiet");

            code.Messages.Should().Equal("The field Code is invalid.");
            code.Code.Should().Be(UnexplainedCode.QuietCode);
        }
    }

    private static BunitContext Localized<TResource>()
        => FormsHarness.Context(services: static services => services.AddValueObjectInputs(static options => options.UseResource<TResource>()));
}
