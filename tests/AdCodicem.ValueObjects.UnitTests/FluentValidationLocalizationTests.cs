using System.Globalization;
using AdCodicem.ValueObjects.FluentValidation;
using AdCodicem.ValueObjects.Metadata;
using FluentValidation;
using FluentValidation.Resources;
using FluentValidationResult = FluentValidation.Results.ValidationResult;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The messages of <see cref="ValueObjectRuleBuilderExtensions"/> in another language: looked up in FluentValidation's
/// language manager by the key of the rule's code, with the arguments of the value object's rules as placeholders, and
/// the value object's English message where no translation exists. The language manager is global, so these tests run
/// alone, each restoring it with the cultures it changes.
/// </summary>
[Collection(nameof(FluentValidationGlobalOptions))]
public sealed class FluentValidationLocalizationTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void A_refusal_takes_the_translation_of_its_code_with_the_arguments_of_the_rules()
    {
        var validator = new InlineValidator<FluentValidationTests.ImportCommand> { v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)) };

        var failure = InFrench(() => validator.Validate(new FluentValidationTests.ImportCommand("XX"))).Errors.Should().ContainSingle().Which;

        failure.ErrorMessage.Should().Be("« Iban » : au moins 15 caractères.");
        failure.ErrorCode.Should().Be(ValueObjectErrorCodes.TooShort);
        failure.FormattedMessagePlaceholderValues.Should().Contain(new KeyValuePair<string, object>("ValueObject", "Iban"))
            .And.Contain(new KeyValuePair<string, object>("MinLength", "15"))
            .And.ContainKey("Reason");
    }

    /// <summary>
    /// A range is looked up under the key naming the one bound the type declares, then under the key of the code, and its
    /// bounds are written in the culture: through the type's own schema and through its descriptor alike.
    /// </summary>
    [Fact]
    public void A_range_takes_the_translation_naming_its_bound_and_its_bounds_in_the_culture()
    {
        var quantity = new InlineValidator<Line> { v => v.RuleFor(x => x.Quantity).MustSatisfy<Line, Quantity, short>() };
        var amount = new InlineValidator<FluentValidationTests.Transfer> { v => v.RuleFor(x => x.Amount).MustSatisfy<FluentValidationTests.Transfer, Amount, decimal>() };
        var text = new InlineValidator<Entry> { v => v.RuleFor(x => x.Text).MustParseAs(typeof(Amount)) };
        var account = Iban.Create("FR7630006000011234567890189");

        var range = InFrench(() => quantity.Validate(new Line(5000))).Errors.Should().ContainSingle().Which;
        var minimum = InFrench(() => amount.Validate(new FluentValidationTests.Transfer(-1, account))).Errors.Should().ContainSingle().Which;
        var described = InFrench(() => text.Validate(new Entry("-1", 0))).Errors.Should().ContainSingle().Which;

        range.ErrorMessage.Should().Be("« Quantity » doit être compris entre 0 et 1000.");
        range.FormattedMessagePlaceholderValues.Should().Contain(new KeyValuePair<string, object>("Minimum", "0"))
            .And.Contain(new KeyValuePair<string, object>("Maximum", "1000"));
        minimum.ErrorMessage.Should().Be("« Amount » doit valoir au moins 0.");
        described.ErrorMessage.Should().Be("« Text » doit valoir au moins 0.");
    }

    /// <summary>
    /// The UI culture chooses the translation and the culture formats its arguments, through the type's own schema and
    /// through its descriptor alike: French messages with English formats quote a decimal and the separator of the known
    /// values as English writes them.
    /// </summary>
    [Fact]
    public void The_UI_culture_chooses_the_translation_and_the_culture_formats_its_arguments()
    {
        var typed = new InlineValidator<Entry> { v => v.RuleFor(x => x.Rate).MustSatisfy<Entry, VatRate, decimal>() };
        var described = new InlineValidator<Entry> { v => v.RuleFor(x => x.Text).MustParseAs(typeof(VatRate)) };
        var entry = new Entry("7", 7m);

        Message(InFrench(() => typed.Validate(entry))).Should().Be("« Rate » doit être l'une des valeurs suivantes : 20,0; 5,5.");
        Message(InFrench(() => described.Validate(entry))).Should().Be("« Text » doit être l'une des valeurs suivantes : 20,0; 5,5.");
        Message(InCulture(French, () => typed.Validate(entry), formatting: English)).Should().Be("« Rate » doit être l'une des valeurs suivantes : 20.0, 5.5.");
        Message(InCulture(French, () => described.Validate(entry), formatting: English)).Should().Be("« Text » doit être l'une des valeurs suivantes : 20.0, 5.5.");
    }

    /// <summary>
    /// The arguments join the placeholders whatever message is chained, so a message of the application's own quotes a
    /// rule declared once.
    /// </summary>
    [Fact]
    public void A_chained_message_quotes_the_arguments_of_the_rules()
    {
        var validator = new InlineValidator<FluentValidationTests.ImportCommand>
        {
            v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Reference<PurchaseOrder>)).WithMessage("Au plus {MaxLength} : {Reason} ({ValueObject})"),
        };

        var failure = InFrench(() => validator.Validate(new FluentValidationTests.ImportCommand("PO-1042-AND-MORE"))).Errors.Should().ContainSingle().Which;

        failure.ErrorMessage.Should().Be($"Au plus 12 : {TooLongReason()} (Reference)");
        failure.ErrorCode.Should().Be(ValueObjectErrorCodes.TooLong);
    }

    /// <summary>
    /// A culture the language manager holds no translation for keeps the value object's English message, and so does a
    /// code it does not translate.
    /// </summary>
    [Fact]
    public void A_missing_translation_keeps_the_value_object_s_message()
    {
        var validator = new InlineValidator<FluentValidationTests.ImportCommand> { v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)) };

        var german = InCulture(CultureInfo.GetCultureInfo("de-DE"), () => validator.Validate(new FluentValidationTests.ImportCommand("XX")));
        var untranslated = InFrench(() => validator.Validate(new FluentValidationTests.ImportCommand("FR7630006000011234567890188")));

        german.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(MinimalApiRefusal("XX"));
        untranslated.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("The IBAN check digits are incorrect.");
    }

    [Fact]
    public void NotDefault_takes_the_translation_of_required_with_the_name_of_the_value_object()
    {
        var plain = new InlineValidator<FluentValidationTests.Transfer>
        {
            v => v.RuleFor(x => x.Account).NotDefault<FluentValidationTests.Transfer, Iban, string>(),
        };
        var chained = new InlineValidator<FluentValidationTests.Transfer>
        {
            v => v.RuleFor(x => x.Account).NotDefault<FluentValidationTests.Transfer, Iban, string>().WithMessage("{ValueObject} : {PropertyName}"),
        };
#pragma warning disable VO0010 // An uninitialized value object is what NotDefault refuses.
        var transfer = new FluentValidationTests.Transfer(10, default);
#pragma warning restore VO0010

        var translated = InFrench(() => plain.Validate(transfer)).Errors.Should().ContainSingle().Which;
        var english = InCulture(CultureInfo.GetCultureInfo("de-DE"), () => plain.Validate(transfer)).Errors.Should().ContainSingle().Which;
        var own = InFrench(() => chained.Validate(transfer)).Errors.Should().ContainSingle().Which;

        translated.ErrorMessage.Should().Be("« Account » est obligatoire (Iban).");
        translated.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        english.ErrorMessage.Should().Be("'Account' is required.");
        own.ErrorMessage.Should().Be("Iban : Account");
        InFrench(() => plain.Validate(new FluentValidationTests.Transfer(10, Iban.Create("FR7630006000011234567890189")))).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// With the language manager's cultures turned off, it reads the English translations alone, which an application
    /// may give for the keys of value objects too.
    /// </summary>
    [Fact]
    public void An_English_translation_answers_every_culture_when_the_language_manager_is_disabled()
    {
        var validator = new InlineValidator<FluentValidationTests.ImportCommand> { v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)) };
        var manager = new Translations { Enabled = false };
        manager.AddTranslation("en", ValueObjectMessages.KeyFor(ValueObjectErrorCodes.TooShort), "{PropertyName}: {MinLength} characters at least.");

        var failure = InCulture(French, () => validator.Validate(new FluentValidationTests.ImportCommand("XX")), manager).Errors.Should().ContainSingle().Which;

        failure.ErrorMessage.Should().Be("Iban: 15 characters at least.");
    }

    /// <summary>
    /// The language manager falls back to an English translation for a specific culture it holds nothing for, and for the
    /// invariant culture, before the next key is looked up; never for a neutral one, which keeps the value object's
    /// message.
    /// </summary>
    [Fact]
    public void An_English_translation_answers_a_specific_culture_and_not_a_neutral_one()
    {
        var validator = new InlineValidator<FluentValidationTests.ImportCommand> { v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)) };
        var score = new InlineValidator<Entry> { v => v.RuleFor(x => x.Text).MustParseAs(typeof(Score)) };
        var manager = new Translations();
        manager.AddTranslation("en", ValueObjectMessages.KeyFor(ValueObjectErrorCodes.TooShort), "{PropertyName}: {MinLength} characters at least.");
        manager.AddTranslation("en", ValueObjectMessages.KeyFor(ValueObjectErrorCodes.OutOfRange) + ".maximum", "{PropertyName}: {Maximum} at most.");
        var command = new FluentValidationTests.ImportCommand("XX");

        Message(InCulture(French, () => score.Validate(new Entry("200", 0)), manager)).Should().Be("Text: 100 at most.");

        Message(InCulture(CultureInfo.GetCultureInfo("de-DE"), () => validator.Validate(command), manager)).Should().Be("Iban: 15 characters at least.");
        Message(InCulture(CultureInfo.InvariantCulture, () => validator.Validate(command), manager)).Should().Be("Iban: 15 characters at least.");
        Message(InCulture(CultureInfo.GetCultureInfo("de"), () => validator.Validate(command), manager)).Should().Be(MinimalApiRefusal("XX"));
        Message(InCulture(CultureInfo.GetCultureInfo("fr"), () => validator.Validate(command), manager)).Should().Be("« Iban » : au moins 15 caractères.");
    }

    /// <summary>
    /// A tool that reads a rule's message without validating anything finds the value object's message, as it did before
    /// the language manager was read.
    /// </summary>
    [Fact]
    public void A_tool_reading_the_rule_s_message_finds_the_value_object_s_message()
    {
        var validator = new InlineValidator<FluentValidationTests.ImportCommand> { v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)) };

        var component = validator.CreateDescriptor().Rules.Single().Components.Single();

        component.GetUnformattedErrorMessage().Should().Be("{Reason}");
    }

    private static string MinimalApiRefusal(string text)
    {
        Iban.TryParse(text, CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse();

        return validation.ErrorMessage!;
    }

    private static string TooLongReason()
    {
        Reference<PurchaseOrder>.TryParse("PO-1042-AND-MORE", CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse();

        return validation.ErrorMessage!;
    }

    private static string Message(FluentValidationResult result) => result.Errors.Should().ContainSingle().Which.ErrorMessage;

    private static FluentValidationResult InFrench(Func<FluentValidationResult> validate) => InCulture(French, validate);

    /// <summary>
    /// Validates in a UI culture, which chooses the translation, and in the culture formatting the arguments, the same
    /// unless another is given, with the application's translations, and restores what it changed.
    /// </summary>
    private static FluentValidationResult InCulture(
        CultureInfo culture,
        Func<FluentValidationResult> validate,
        ILanguageManager? manager = null,
        CultureInfo? formatting = null)
    {
        var (original, ui, current) = (ValidatorOptions.Global.LanguageManager, CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture);
        try
        {
            ValidatorOptions.Global.LanguageManager = manager ?? new Translations();
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = formatting ?? culture;

            return validate();
        }
        finally
        {
            ValidatorOptions.Global.LanguageManager = original;
            CultureInfo.CurrentUICulture = ui;
            CultureInfo.CurrentCulture = current;
        }
    }

    /// <summary>A line of an order, carrying a raw quantity.</summary>
    /// <param name="Quantity">The quantity.</param>
    public sealed record Line(short Quantity);

    /// <summary>An entry carrying raw text and a raw rate.</summary>
    /// <param name="Text">The text.</param>
    /// <param name="Rate">The rate.</param>
    public sealed record Entry(string? Text, decimal Rate);

    /// <summary>The French translations of an application, as FluentValidation documents adding them.</summary>
    private sealed class Translations : LanguageManager
    {
        public Translations()
        {
            AddTranslation("fr", "ValueObject.value_object.too_short", "« {PropertyName} » : au moins {MinLength} caractères.");
            AddTranslation("fr", "ValueObject.value_object.out_of_range", "« {PropertyName} » doit être compris entre {Minimum} et {Maximum}.");
            AddTranslation("fr", "ValueObject.value_object.out_of_range.minimum", "« {PropertyName} » doit valoir au moins {Minimum}.");
            AddTranslation("fr", "ValueObject.value_object.required", "« {PropertyName} » est obligatoire ({ValueObject}).");
            AddTranslation("fr", "ValueObject.value_object.not_a_known_value", "« {PropertyName} » doit être l'une des valeurs suivantes : {KnownValues}.");
        }
    }
}
