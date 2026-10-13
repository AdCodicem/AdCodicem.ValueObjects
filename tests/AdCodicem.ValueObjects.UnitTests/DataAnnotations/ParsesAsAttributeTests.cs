using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.Web;
using AnnotationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AdCodicem.ValueObjects.UnitTests.DataAnnotations;

/// <summary>
/// <see cref="ParsesAsAttribute"/> and <see cref="ParsesAsAttribute{TSelf, TValue}"/> through
/// <see cref="Validator"/>: text held to a value object's rules, refused with the code and the message of the rule that
/// refuses it.
/// </summary>
public class ParsesAsAttributeTests
{
    private const string Account = "FR7630006000011234567890189";

    /// <summary>The text of each rule, refused by both forms with the code and the message the value object gives it.</summary>
    public static TheoryData<Type, string, string> Refusals => new()
    {
        { typeof(Iban), "FR76", ValueObjectErrorCodes.TooShort },
        { typeof(Iban), "FR7630006000011234567890188", ValueObjectErrorCodes.InvalidFormat },
        { typeof(Iban), "FR76 3000 6000 0112 3456 7890 18!", ValueObjectErrorCodes.InvalidFormat },
        { typeof(Iban), string.Empty, ValueObjectErrorCodes.Required },
        { typeof(Quantity), "1001", ValueObjectErrorCodes.OutOfRange },
        { typeof(Quantity), string.Empty, ValueObjectErrorCodes.NotParsable },
        { typeof(Amount), "12,5", ValueObjectErrorCodes.NotParsable },
        { typeof(Amount), "-1", ValueObjectErrorCodes.OutOfRange },
        { typeof(CustomerId), "not-a-guid", ValueObjectErrorCodes.NotParsable },
        { typeof(CustomerId), Guid.Empty.ToString(), ValueObjectErrorCodes.Required },
        { typeof(CountryCode), "ZZ", ValueObjectErrorCodes.NotAKnownValue },
        { typeof(BirthDate), "1850-01-01", ValueObjectErrorCodes.OutOfRange },
        { typeof(Catalog<int>.Stock), "-1", ValueObjectErrorCodes.OutOfRange },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public void Each_rule_refuses_its_text_with_its_code_and_its_message_through_both_forms(Type type, string text, string code)
    {
        ValueObjectRegistry.TryResolve(type, out var descriptor).Should().BeTrue();
        descriptor!.TryParse(text, CultureInfo.InvariantCulture, out _, out var expected).Should().BeFalse();
        expected.ErrorCode.Should().Be(code);

        foreach (var attribute in Annotated.BothForms(type))
        {
            var refusal = Annotated.Refusal(attribute, text);

            refusal.ErrorCode.Should().Be(code, "{0} refuses {1}", attribute.GetType().Name, text);
            refusal.ErrorMessage.Should().Be(expected.ErrorMessage);
            refusal.ValueObjectType.Should().Be(type);
            refusal.MemberNames.Should().Equal(Annotated.Member);
        }
    }

    /// <summary>
    /// The attribute validates and changes nothing: the member keeps the text as it was sent, which the value object's
    /// normalization accepts.
    /// </summary>
    [Fact]
    public void Text_the_value_object_accepts_passes_and_is_left_as_it_was_sent()
    {
        var form = new TransferForm
        {
            Account = " fr76 3000 6000 0112 3456 7890 189 ",
            Beneficiary = Account,
            Quantity = "1000",
            Amount = "12.5",
            Country = "fr",
            Alternates = [Account, null, "de89 3704 0044 0532 0130 00"],
        };
        var results = new List<AnnotationResult>();

        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true).Should().BeTrue();

        results.Should().BeEmpty();
        form.Account.Should().Be(" fr76 3000 6000 0112 3456 7890 189 ");
        form.Country.Should().Be("fr");
    }

    /// <summary>
    /// A missing member is <see cref="RequiredAttribute"/>'s to report: the attribute lets <see langword="null"/>
    /// through, so a member reported missing is reported once.
    /// </summary>
    [Fact]
    public void A_null_passes_and_is_left_to_Required()
    {
        var results = new List<AnnotationResult>();
        var form = new TransferForm();

        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true).Should().BeFalse();

        results.Should().ContainSingle().Which.Should().BeOfType<AnnotationResult>()
            .Which.ErrorMessage.Should().Be("The Settlement account field is required.");
        foreach (var attribute in Annotated.BothForms(typeof(Iban)))
        {
            Annotated.Check(attribute, null).Should().BeNull();
        }
    }

    /// <summary>
    /// Every member is validated with its own rules, each refused one reported under its name, with the code and the
    /// display name a host shows.
    /// </summary>
    [Fact]
    public void Each_member_of_a_model_is_reported_under_its_name_with_its_code()
    {
        var form = new TransferForm
        {
            Account = "FR76",
            Beneficiary = "FR7630006000011234567890188",
            Quantity = "1001",
            Amount = "12,5",
            Country = "ZZ",
            Alternates = [Account, "XX"],
        };
        var results = new List<AnnotationResult>();

        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true).Should().BeFalse();

        results.Should().AllBeOfType<ValueObjectValidationResult>();
        results.Cast<ValueObjectValidationResult>().Select(static result => (result.MemberNames.Single(), result.ErrorCode)).Should().Equal(
            (nameof(TransferForm.Account), ValueObjectErrorCodes.TooShort),
            (nameof(TransferForm.Beneficiary), ValueObjectErrorCodes.InvalidFormat),
            (nameof(TransferForm.Quantity), ValueObjectErrorCodes.OutOfRange),
            (nameof(TransferForm.Amount), ValueObjectErrorCodes.NotParsable),
            (nameof(TransferForm.Country), ValueObjectErrorCodes.NotAKnownValue),
            (nameof(TransferForm.Alternates), ValueObjectErrorCodes.TooShort));
    }

    /// <summary>
    /// Text is read in the invariant culture, as the binders read it, unless the attribute reads it in the current one,
    /// for a form a person types in.
    /// </summary>
    [Fact]
    public void Text_is_read_in_the_invariant_culture_unless_the_attribute_reads_the_current_one()
    {
        using var french = Annotated.Culture("fr-FR");
        var form = new TransferForm { Account = Account, Amount = "12,5", TypedAmount = "12,5" };
        var results = new List<AnnotationResult>();

        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true).Should().BeFalse();

        results.Should().ContainSingle().Which.MemberNames.Should().Equal(nameof(TransferForm.Amount));
        Annotated.Check(new ParsesAsAttribute<Amount, decimal> { UseCurrentCulture = true }, "12,5").Should().BeNull();
        Annotated.Refusal(new ParsesAsAttribute<Amount, decimal> { UseCurrentCulture = true }, "12.5").ErrorCode
            .Should().Be(ValueObjectErrorCodes.NotParsable, "12.5 is no French decimal");
        Annotated.Check(new ParsesAsAttribute(typeof(Amount)), "12.5").Should().BeNull();
    }

    /// <summary>
    /// The current culture reads the text and formats the rule's arguments, and the current UI culture chooses the
    /// message, each apart from the other: under a French culture and an English UI, <c>12,5</c> is an amount and
    /// <c>12.5</c> is not, and the message of a resource holding French alone is the rule's English one; under the
    /// reverse, <c>12.5</c> is an amount, and the French message quotes the bounds as English writes them.
    /// </summary>
    [Fact]
    public void The_culture_reads_the_text_and_the_UI_culture_chooses_the_message_each_apart()
    {
        var typed = new ParsesAsAttribute(typeof(Amount)) { UseCurrentCulture = true };
        var born = Localized(typeof(BirthDate));
        ValueObjectRegistry.TryGet(typeof(BirthDate), out var descriptor).Should().BeTrue();
        descriptor!.TryParse("1850-01-01", CultureInfo.InvariantCulture, out _, out var english).Should().BeFalse();

        using (Annotated.Culture("fr-FR", "en-US"))
        {
            Annotated.Check(typed, "12,5").Should().BeNull("the culture is French");
            Annotated.Refusal(typed, "12.5").ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
            Annotated.Refusal(born, "1850-01-01").ErrorMessage.Should().Be(english.ErrorMessage, "the resource holds no English message");
        }

        using (Annotated.Culture("en-US", "fr-FR"))
        {
            Annotated.Check(typed, "12.5").Should().BeNull("the culture is English");
            Annotated.Refusal(born, "1850-01-01").ErrorMessage.Should().Be("« Account » doit être compris entre 1/1/1900 et 12/31/2100.");
        }
    }

    public static TheoryData<object> Collections => new()
    {
        new[] { Account, "XX", "FR76" },
        new List<string?> { null, Account, "XX" },
        ImmutableArray.Create(Account, "XX"),
        new HashSet<string>(StringComparer.Ordinal) { "XX" },
    };

    /// <summary>
    /// Each element of a collection of text is checked, a <see langword="null"/> one skipped, and the first refused is
    /// reported under the member's own name, once, whatever the host prefixes it with.
    /// </summary>
    [Theory]
    [MemberData(nameof(Collections))]
    public void The_first_refused_element_of_a_collection_is_reported_under_the_members_name(object texts)
    {
        foreach (var attribute in Annotated.BothForms(typeof(Iban)))
        {
            var refusal = Annotated.Refusal(attribute, texts, "Alternates");

            refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.TooShort);
            refusal.ErrorMessage.Should().Be("The value must be at least 15 characters long.");
            refusal.MemberNames.Should().Equal("Alternates");
        }
    }

    /// <summary>
    /// The elements are checked in their order and the first refused one is reported, whatever rule refuses the ones
    /// after it.
    /// </summary>
    /// <param name="first">The first element refused.</param>
    /// <param name="second">An element refused after it, by another rule.</param>
    /// <param name="code">The code of the first one's rule.</param>
    /// <param name="message">The message of the first one's rule.</param>
    [Theory]
    [InlineData("XX", "FR7630006000011234567890188", ValueObjectErrorCodes.TooShort, "The value must be at least 15 characters long.")]
    [InlineData("FR7630006000011234567890188", "XX", ValueObjectErrorCodes.InvalidFormat, "The IBAN check digits are incorrect.")]
    public void The_first_refused_element_is_reported_whatever_rule_refuses_the_next(string first, string second, string code, string message)
    {
        foreach (var attribute in Annotated.BothForms(typeof(Iban)))
        {
            var refusal = Annotated.Refusal(attribute, new[] { Account, first, second }, "Alternates");

            refusal.ErrorCode.Should().Be(code);
            refusal.ErrorMessage.Should().Be(message);
        }
    }

    /// <summary>
    /// An immutable array left at its default, as an absent member of that type is left, passes as <see langword="null"/>
    /// does rather than throwing out of its enumerator, and <see cref="RequiredAttribute"/>, which refuses
    /// <see langword="null"/> alone, lets it through too; an empty one, and one holding text, are checked as any
    /// collection is.
    /// </summary>
    [Fact]
    public void A_default_immutable_array_passes_as_an_absent_member_does()
    {
        foreach (var attribute in Annotated.BothForms(typeof(Iban)))
        {
            Annotated.Check(attribute, default(ImmutableArray<string>)).Should().BeNull();
            Annotated.Check(attribute, ImmutableArray<string>.Empty).Should().BeNull();
        }

        var options = new ImmutableAccounts();
        var results = new List<AnnotationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true).Should().BeTrue();
        results.Should().BeEmpty();

        options.Accounts = [Account, "XX"];
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true).Should().BeFalse();
        results.Should().ContainSingle().Which.Should().BeOfType<ValueObjectValidationResult>()
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.TooShort);
    }

    [Fact]
    public void A_collection_of_accepted_text_and_null_elements_passes()
    {
        foreach (var attribute in Annotated.BothForms(typeof(Iban)))
        {
            Annotated.Check(attribute, new List<string?> { null, Account }).Should().BeNull();
            Annotated.Check(attribute, Array.Empty<string>()).Should().BeNull();
        }
    }

    /// <summary>
    /// A construction of a generic value object, which no generated registration lists: the <see cref="Type"/> form has
    /// the registry describe it by reflection, the first time a value is validated, and the generic form reads its static
    /// members.
    /// </summary>
    [Fact]
    public void A_construction_of_a_generic_value_object_is_described_the_first_time_a_value_is_validated()
    {
        var attribute = new ParsesAsAttribute(typeof(Reference<FirstConstruction>));
        ValueObjectRegistry.TryGet(typeof(Reference<FirstConstruction>), out _).Should().BeFalse("nothing asked for it yet");

        Annotated.Refusal(attribute, "PO-1042-0001-X").ErrorCode.Should().Be(ValueObjectErrorCodes.TooLong);
        ValueObjectRegistry.TryGet(typeof(Reference<FirstConstruction>), out _).Should().BeTrue("the attribute had it described");
        Annotated.Check(attribute, "po-1042").Should().BeNull();

        var generic = new ParsesAsAttribute<Reference<SecondConstruction>, string>();
        Annotated.Refusal(generic, "PO-1042-0001-X").ErrorCode.Should().Be(ValueObjectErrorCodes.TooLong);
        Annotated.Check(generic, "po-1042").Should().BeNull();
        ValueObjectRegistry.TryGet(typeof(Reference<SecondConstruction>), out _).Should().BeFalse("the generic form asks no registry");
    }

    /// <summary>
    /// A value object written by hand and registered by nothing is described by reflection under the
    /// <see cref="Type"/> form, and read through its static members under the generic form.
    /// </summary>
    [Fact]
    public void A_value_object_written_by_hand_is_held_to_its_rules_through_both_forms()
    {
        var attribute = new ParsesAsAttribute(typeof(HandWrittenTag<ParsesAsAttributeTests>));
        var generic = new ParsesAsAttribute<HandWrittenTag<ParsesAsAttributeTests>, string>();

        foreach (var form in (ParsesAsAttribute[])[attribute, generic])
        {
            Annotated.Refusal(form, string.Empty).ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
            Annotated.Check(form, "tag").Should().BeNull();
        }
    }

    /// <summary>
    /// A parser written by hand may refuse without a code, which both forms report as not parsable, and with a code but
    /// no message, which DataAnnotations would replace with its own, the code lost: its default message stands in, and the
    /// code is kept.
    /// </summary>
    [Fact]
    public void A_refusal_written_by_hand_without_a_code_or_a_message_keeps_a_code_and_gets_a_message()
    {
        foreach (var attribute in (ParsesAsAttribute[])[new ParsesAsAttribute<SilentCode, string>(), new ParsesAsAttribute(typeof(SilentCode))])
        {
            var silent = Annotated.Refusal(attribute, string.Empty);
            silent.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
            silent.ErrorMessage.Should().Be("The text could not be parsed as SilentCode.");

            var quiet = Annotated.Refusal(attribute, "quiet");
            quiet.ErrorCode.Should().Be(SilentCode.QuietCode);
            quiet.ErrorMessage.Should().Be("The field Account is invalid.");
        }
    }

    [Fact]
    public void A_message_the_attribute_declares_replaces_the_rules_and_keeps_its_code()
    {
        var declared = new ParsesAsAttribute(typeof(Iban)) { ErrorMessage = "{0} is not an IBAN we take." };
        var named = new ParsesAsAttribute<Iban, string>
        {
            ErrorMessageResourceType = typeof(AnnotationMessages),
            ErrorMessageResourceName = nameof(AnnotationMessages.RefusedAccount),
        };

        var fromText = Annotated.Refusal(declared, "FR76");
        var fromResource = Annotated.Refusal(named, "FR7630006000011234567890188");

        fromText.ErrorMessage.Should().Be("Account is not an IBAN we take.");
        fromText.ErrorCode.Should().Be(ValueObjectErrorCodes.TooShort);
        fromResource.ErrorMessage.Should().Be("Account is not an account we hold.");
        fromResource.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    /// <summary>
    /// DataAnnotations refuses a resource named without its type, and a message beside a resource, as it refuses them on
    /// any attribute, when the message is formatted.
    /// </summary>
    [Fact]
    public void A_message_DataAnnotations_cannot_format_is_refused_as_on_any_attribute()
    {
        var nameAlone = new ParsesAsAttribute(typeof(Iban)) { ErrorMessageResourceName = nameof(AnnotationMessages.RefusedAccount) };
        var both = new ParsesAsAttribute(typeof(Iban)) { ErrorMessage = "{0}", ErrorMessageResourceType = typeof(AnnotationMessages) };

        FluentActions.Invoking(() => Annotated.Check(nameAlone, "FR76")).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Annotated.Check(both, "FR76")).Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// A resource type named alone has the message looked up by the code of the rule, under the keys of
    /// <see cref="ValueObjectMessages.KeysFor"/>, the member and the rule's arguments quoted, in the current UI culture,
    /// formatted in the current culture: the resource an application hands MVC's <c>UseResource&lt;T&gt;()</c>.
    /// </summary>
    [Fact]
    public void A_resource_named_alone_gives_the_message_of_each_code_in_the_current_culture()
    {
        using var french = Annotated.Culture("fr-FR");

        Annotated.Refusal(Localized(typeof(Iban)), "FR76").ErrorMessage.Should().Be("« Account » : au moins 15 caractères.");
        Annotated.Refusal(Localized(typeof(Quantity)), "1001").ErrorMessage.Should().Be("« Account » doit être compris entre 0 et 1000.");
        Annotated.Refusal(Localized(typeof(Amount)), "-1").ErrorMessage.Should().Be("« Account » doit valoir au moins 0.");
        Annotated.Refusal(Localized(typeof(CountryCode)), "ZZ").ErrorMessage
            .Should().Be("« Account » doit être l'une des valeurs suivantes : FR; BE; LU.");
        Annotated.Refusal(Localized(typeof(Percentage)), "100.5").ErrorMessage.Should().Be("« Account » doit être compris entre 0 et 100.");

        var generic = new ParsesAsAttribute<Quantity, short> { ErrorMessageResourceType = typeof(ValidationMessages) };
        var refusal = Annotated.Refusal(generic, "1001", "Quantité");
        refusal.ErrorMessage.Should().Be("« Quantité » doit être compris entre 0 et 1000.");
        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>The display name of a member is the one a resource quotes, as any other message of the model.</summary>
    [Fact]
    public void A_resource_named_alone_quotes_the_display_name_of_the_member()
    {
        using var french = Annotated.Culture("fr-FR");
        var form = new LocalizedForm { Account = "FR76", Born = "1850-01-01" };
        var results = new List<AnnotationResult>();

        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true).Should().BeFalse();

        results.Select(static result => result.ErrorMessage).Should().Equal(
            "« Compte de règlement » : au moins 15 caractères.",
            "« Born » doit être compris entre 01/01/1900 et 31/12/2100.");
    }

    /// <summary>
    /// A resource with no neutral culture, read in a culture it lacks, throws as the framework's localizer finds; the
    /// English message of the rule stands.
    /// </summary>
    [Fact]
    public void A_resource_holding_no_message_for_the_culture_keeps_the_rules_English_message()
    {
        using var english = Annotated.Culture("en-US");

        var refusal = Annotated.Refusal(Localized(typeof(Iban)), "FR76");

        refusal.ErrorMessage.Should().Be("The value must be at least 15 characters long.");
        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.TooShort);
    }

    /// <summary>
    /// A key the resource lacks keeps the English message, and the bare key of a range serves a type of one bound when
    /// the resource lacks the key of that bound, as for MVC; a template quoting an argument the code does not supply is the
    /// application's error to see.
    /// </summary>
    [Fact]
    public void A_sparse_resource_falls_back_key_by_key_and_a_malformed_template_throws()
    {
        using var french = Annotated.Culture("fr-FR");
        var sparse = new ParsesAsAttribute(typeof(Amount)) { ErrorMessageResourceType = typeof(SparseMessages) };
        var country = new ParsesAsAttribute(typeof(CountryCode)) { ErrorMessageResourceType = typeof(SparseMessages) };

        Annotated.Refusal(sparse, "-1", "total").ErrorMessage.Should().Be("« total » : de 0 à .");
        Annotated.Refusal(country, "ZZ").ErrorMessage.Should().Be("The value is not one of the accepted values.");
        FluentActions.Invoking(() => Annotated.Check(sparse, "abc")).Should().Throw<FormatException>();
    }

    /// <summary>
    /// A resource of the neutral culture answers a key it lacks with nothing rather than an exception, read in a culture
    /// it holds no satellite for: the next key is looked up, the key of a range serving a type of one bound when the
    /// resource lacks the key of that bound, and the English message stands when it lacks every key of the code.
    /// </summary>
    [Fact]
    public void A_neutral_resource_lacking_the_key_of_a_bound_gives_the_key_of_the_range()
    {
        using var french = Annotated.Culture("fr-FR");
        var amount = new ParsesAsAttribute(typeof(Amount)) { ErrorMessageResourceType = typeof(NeutralMessages) };
        var iban = new ParsesAsAttribute(typeof(Iban)) { ErrorMessageResourceType = typeof(NeutralMessages) };

        Annotated.Refusal(amount, "-1", "Total").ErrorMessage.Should().Be("Total is out of range, from 0.");
        Annotated.Refusal(iban, "FR76").ErrorMessage.Should().Be("The value must be at least 15 characters long.");
    }

    /// <summary>
    /// <see cref="ValidationAttribute.Validate(object?, string)"/> formats DataAnnotations' message on its own, which a
    /// resource named alone would make it refuse: it gets DataAnnotations' default one.
    /// </summary>
    [Fact]
    public void The_message_DataAnnotations_formats_on_its_own_is_its_default_for_a_resource_named_alone()
    {
        var localized = Localized(typeof(Iban));
        var plain = new ParsesAsAttribute(typeof(Iban)) { ErrorMessage = "{0} is refused." };

#pragma warning disable CS0618 // The legacy overload is the one that formats the message itself.
        FluentActions.Invoking(() => localized.Validate("FR76", "Legacy")).Should().Throw<ValidationException>()
            .WithMessage("The field Legacy is invalid.");
#pragma warning restore CS0618
        localized.FormatErrorMessage("Legacy").Should().Be("The field Legacy is invalid.");
        plain.FormatErrorMessage("Legacy").Should().Be("Legacy is refused.");
        new ParsesAsAttribute(typeof(Iban)).FormatErrorMessage("Legacy").Should().Be("The field Legacy is invalid.");
    }

    /// <summary>
    /// Validated with no context, as <see cref="ValidationAttribute.IsValid(object?)"/> does, a refusal names the value
    /// object, without the arity of a generic one.
    /// </summary>
    [Fact]
    public void Validated_without_a_context_text_is_refused_under_the_value_objects_name()
    {
        new ParsesAsAttribute(typeof(Iban)).IsValid("FR76").Should().BeFalse();
        new ParsesAsAttribute(typeof(Iban)).IsValid(Account).Should().BeTrue();
        new ParsesAsAttribute<Reference<PurchaseOrder>, string> { ErrorMessage = "{0}" }.IsValid("PO-1042-0001-X").Should().BeFalse();

        var probe = new ContextFreeProbe(typeof(Reference<PurchaseOrder>));
        probe.Refuse("PO-1042-0001-X").Should().BeOfType<ValueObjectValidationResult>()
            .Which.Should().Match<ValueObjectValidationResult>(static result => result.ErrorMessage == "Reference refused." && !result.MemberNames.Any());
    }

    /// <summary>
    /// A member that holds no text is a misuse, reported naming the member and its type, never its value.
    /// </summary>
    [Fact]
    public void A_member_that_holds_no_text_is_refused_naming_its_type_and_never_its_value()
    {
        foreach (var attribute in Annotated.BothForms(typeof(Iban)))
        {
            var refusal = FluentActions.Invoking(() => Annotated.Check(attribute, 4242, "Count")).Should().Throw<InvalidOperationException>().Which;

            refusal.Message.Should().Be(
                "[ParsesAs] holds text to the rules of 'Iban', and 'Count' is of type 'Int32': declare it string, or a collection of string.");
            refusal.Message.Should().NotContain("4242");
        }
    }

    /// <summary>
    /// A type that is no value object is refused when a value is first validated, not when the attribute is created,
    /// which anything reading a member's attributes does; a type nothing names is refused at once.
    /// </summary>
    [Fact]
    public void A_type_that_is_no_value_object_is_refused_when_a_value_is_validated()
    {
        var attribute = new ParsesAsAttribute(typeof(string));

        attribute.ValueObjectType.Should().Be<string>();
        FluentActions.Invoking(() => Annotated.Check(attribute, "text")).Should().Throw<InvalidOperationException>()
            .WithMessage("[ParsesAs] names 'String', which is not a value object the registry describes. Under native AOT, *"
                         + "have to be registered before a value is validated, or named through [ParsesAs<TSelf, TValue>].");
        FluentActions.Invoking(() => new ParsesAsAttribute(null!)).Should().Throw<ArgumentNullException>().WithParameterName("valueObjectType");
    }

    /// <summary>
    /// <c>typeof(Iban?)</c> names the value object itself, which the attribute, its refusals and its messages report,
    /// never <see cref="Nullable{T}"/>.
    /// </summary>
    [Fact]
    public void A_nullable_type_names_its_value_object()
    {
        var attribute = new ParsesAsAttribute(typeof(Iban?));

        attribute.ValueObjectType.Should().Be<Iban>();
        var refusal = Annotated.Refusal(attribute, "FR76");
        refusal.ValueObjectType.Should().Be<Iban>();
        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.TooShort);
        new ContextFreeProbe(typeof(Iban?)).Refuse("FR76").Should().BeOfType<ValueObjectValidationResult>()
            .Which.ErrorMessage.Should().Be("Iban refused.");
        FluentActions.Invoking(() => Annotated.Check(attribute, 4242, "Count")).Should().Throw<InvalidOperationException>()
            .WithMessage("[ParsesAs] holds text to the rules of 'Iban', *");
    }

    /// <summary>
    /// The <see cref="Type"/> form finds the descriptor once and keeps it: an attribute a host keeps, as MVC,
    /// <c>AddValidation()</c> and <c>Validator</c> keep theirs, pays for the lookup once.
    /// </summary>
    [Fact]
    public void The_descriptor_is_found_once_and_kept()
    {
        var attribute = new ParsesAsAttribute(typeof(Reference<KeptConstruction>));

        Annotated.Check(attribute, "po-1").Should().BeNull();
        Annotated.Refusal(attribute, "PO-1042-0001-X").ErrorCode.Should().Be(ValueObjectErrorCodes.TooLong);

        ValueObjectRegistry.TryGet(typeof(Reference<KeptConstruction>), out var registered).Should().BeTrue();
        registered!.ValueObjectType.Should().Be<Reference<KeptConstruction>>();
    }

    /// <summary>
    /// The boxed path's codes, guarded at the attribute as <c>DescriptorTests</c> guards them at the descriptor: a wrong
    /// check digit and a validator hook report their own rule, never a flattened <c>not_parsable</c>.
    /// </summary>
    [Fact]
    public void The_Type_form_reports_the_rule_a_validator_hook_owns_not_a_flattened_one()
    {
        var iban = Annotated.Refusal(new ParsesAsAttribute(typeof(Iban)), "FR7630006000011234567890188");
        var customer = Annotated.Refusal(new ParsesAsAttribute(typeof(CustomerId)), Guid.Empty.ToString());

        iban.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
        iban.ErrorMessage.Should().Be("The IBAN check digits are incorrect.");
        customer.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        customer.ErrorMessage.Should().Be("A customer identifier must not be empty.");
    }

    private static ParsesAsAttribute Localized(Type type) => new(type) { ErrorMessageResourceType = typeof(ValidationMessages) };

    /// <summary>A model whose messages come from the resource, one member with a display name.</summary>
    private sealed class LocalizedForm
    {
        [ParsesAs(typeof(Iban), ErrorMessageResourceType = typeof(ValidationMessages))]
        [Display(Name = "Compte de règlement")]
        public string? Account { get; set; }

        [ParsesAs(typeof(BirthDate), ErrorMessageResourceType = typeof(ValidationMessages))]
        public string? Born { get; set; }
    }

    /// <summary>
    /// Options holding the text of accounts in an immutable array, which a binder leaves at its default when the
    /// configuration holds none.
    /// </summary>
    private sealed class ImmutableAccounts
    {
        [Required]
        [ParsesAs(typeof(Iban))]
        public ImmutableArray<string> Accounts { get; set; }
    }

    /// <summary>Calls the attribute with no context, as <see cref="ValidationAttribute.IsValid(object?)"/> does.</summary>
    private sealed class ContextFreeProbe(Type type) : ParsesAsAttribute(type)
    {
        public AnnotationResult? Refuse(string text)
        {
            ErrorMessage = "{0} refused.";

            return IsValid(text, null!);
        }
    }

    private sealed class FirstConstruction;

    private sealed class SecondConstruction;

    private sealed class KeptConstruction;
}
