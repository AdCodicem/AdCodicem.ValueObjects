using AdCodicem.ValueObjects.FluentValidation;
using FluentValidation;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The FluentValidation rules that defer to a value object, run by FluentValidation's own validator.
/// </summary>
public class FluentValidationTests
{
    /// <summary>
    /// Whether a member is required is NotNull's or NotEmpty's to say, as everywhere in FluentValidation: a rule
    /// that also refused null reported a missing IBAN twice, the second time without a value object code.
    /// </summary>
    [Fact]
    public void A_null_text_passes_MustParseAs_and_is_left_to_NotEmpty()
    {
        var alone = new InlineValidator<ImportCommand> { v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)) };
        var required = new InlineValidator<ImportCommand>
        {
            v => v.RuleFor(x => x.Iban).NotEmpty().MustParseAs(typeof(Iban)),
        };

        alone.Validate(new ImportCommand(null)).IsValid.Should().BeTrue();
        required.Validate(new ImportCommand(null)).Errors
            .Should().ContainSingle().Which.ErrorCode.Should().Be("NotEmptyValidator");
    }

    [Fact]
    public void A_null_underlying_value_passes_MustSatisfy()
    {
        var validator = new InlineValidator<ImportCommand>
        {
            v => v.RuleFor(x => x.Iban!).MustSatisfy<ImportCommand, Iban, string>(),
        };

        validator.Validate(new ImportCommand(null)).IsValid.Should().BeTrue();
    }

    /// <summary>Normalization runs before the rules, as it does when the value object is created.</summary>
    [Fact]
    public void Text_the_value_object_accepts_passes_MustParseAs()
    {
        var validator = new InlineValidator<ImportCommand> { v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)) };

        validator.Validate(new ImportCommand("fr76 3000 6000 0112 3456 7890 189")).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("FR7630006000011234567890188", ValueObjectErrorCodes.InvalidFormat, "The IBAN check digits are incorrect.")]
    [InlineData("", ValueObjectErrorCodes.Required, "The value must not be empty.")]
    public void MustParseAs_reports_the_rule_the_value_object_owns(string text, string code, string message)
    {
        var validator = new InlineValidator<ImportCommand> { v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)) };

        var failure = validator.Validate(new ImportCommand(text)).Errors.Should().ContainSingle().Which;

        failure.PropertyName.Should().Be(nameof(ImportCommand.Iban));
        failure.ErrorCode.Should().Be(code);
        failure.ErrorMessage.Should().Be(message);
    }

    /// <summary>
    /// FluentValidation fixes a rule's error code when the rule is built, while each rule of a value object has its
    /// own. The value object's code and message are the default, and the options chained on the rule win over them, as
    /// on any rule. The value object's message stays at hand as <c>{Reason}</c>.
    /// </summary>
    [Fact]
    public void Options_chained_on_MustParseAs_replace_the_code_and_the_message_of_the_value_object()
    {
        var validator = new InlineValidator<ImportCommand>
        {
            v => v.RuleFor(x => x.Iban)
                .MustParseAs(typeof(Iban))
                .WithErrorCode("iban")
                .WithMessage("{PropertyName} '{PropertyValue}' at {PropertyPath}: {Reason}")
                .WithName("Account")
                .WithSeverity(Severity.Warning)
                .WithState(_ => 42),
        };

        var failure = validator.Validate(new ImportCommand("FR7630006000011234567890188")).Errors
            .Should().ContainSingle().Which;

        failure.PropertyName.Should().Be(nameof(ImportCommand.Iban));
        failure.ErrorCode.Should().Be("iban");
        failure.ErrorMessage.Should().Be(
            "Account 'FR7630006000011234567890188' at Iban: The IBAN check digits are incorrect.");
        failure.Severity.Should().Be(Severity.Warning);
        failure.CustomState.Should().Be(42);
        failure.AttemptedValue.Should().Be("FR7630006000011234567890188");
    }

    /// <summary>
    /// A child validator run for each element of a collection is handed the index of the element, which a message
    /// chained on the rule can quote, as on any rule.
    /// </summary>
    [Fact]
    public void A_message_chained_on_MustParseAs_quotes_the_index_of_the_collection_element()
    {
        var line = new InlineValidator<ImportCommand>
        {
            v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)).WithMessage("line {CollectionIndex}: {Reason}"),
        };
        var batch = new InlineValidator<ImportBatch> { v => v.RuleForEach(x => x.Lines).SetValidator(line) };

        var failure = batch.Validate(new ImportBatch([new("DE89370400440532013000"), new("FR7630006000011234567890188")]))
            .Errors.Should().ContainSingle().Which;

        failure.ErrorMessage.Should().Be("line 1: The IBAN check digits are incorrect.");
        failure.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    /// <summary>A message chained alone replaces the message, and the failure keeps the code of the rule it breaks.</summary>
    [Fact]
    public void A_message_chained_on_MustParseAs_keeps_the_code_of_the_rule_the_value_breaks()
    {
        var validator = new InlineValidator<ImportCommand>
        {
            v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)).WithMessage("Not an account number."),
        };

        var failure = validator.Validate(new ImportCommand("FR7630006000011234567890188")).Errors
            .Should().ContainSingle().Which;

        failure.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
        failure.ErrorMessage.Should().Be("Not an account number.");
        failure.Severity.Should().Be(Severity.Error);
        failure.CustomState.Should().BeNull();
    }

    [Fact]
    public void Options_chained_on_MustSatisfy_replace_the_code_and_the_message_of_the_value_object()
    {
        var validator = new InlineValidator<Transfer>
        {
            v => v.RuleFor(x => x.Amount)
                .MustSatisfy<Transfer, Amount, decimal>()
                .WithErrorCode("amount")
                .WithMessage("{PropertyName}: {Reason}"),
        };

        var failure = validator.Validate(new Transfer(-1m, Iban.Create("FR7630006000011234567890189"))).Errors
            .Should().ContainSingle().Which;

        failure.ErrorCode.Should().Be("amount");
        failure.ErrorMessage.Should().Be("Amount: The value must be greater than or equal to 0.");
        failure.AttemptedValue.Should().Be(-1m);
    }

    /// <summary>
    /// Empty text is the value object's to judge, as any text is: a string value object refuses it as required
    /// unless it allows empty text, and one over another type cannot parse it.
    /// </summary>
    /// <param name="type">The value object the text must parse into.</param>
    /// <param name="code">The code empty text fails with, or <see langword="null"/> when it passes.</param>
    [Theory]
    [InlineData(typeof(Iban), ValueObjectErrorCodes.Required)]
    [InlineData(typeof(CustomerId), ValueObjectErrorCodes.NotParsable)]
    [InlineData(typeof(Label), null)]
    public void Empty_text_fails_MustParseAs_as_the_value_object_refuses_it(Type type, string? code)
    {
        var validator = new InlineValidator<ImportCommand> { v => v.RuleFor(x => x.Iban).MustParseAs(type) };

        var errors = validator.Validate(new ImportCommand(string.Empty)).Errors;

        errors.Select(failure => failure.ErrorCode).Should().Equal(code is null ? [] : [code]);
    }

    /// <summary>
    /// Empty text fails NotEmpty and the value object's own rule, and both are reported under the member unless the
    /// rule chain stops at its first failure.
    /// </summary>
    [Fact]
    public void A_rule_chain_that_stops_at_its_first_failure_reports_empty_text_once()
    {
        var both = new InlineValidator<ImportCommand>
        {
            v => v.RuleFor(x => x.Iban).NotEmpty().MustParseAs(typeof(Iban)),
        };
        var stopped = new InlineValidator<ImportCommand>
        {
            v => v.RuleFor(x => x.Iban).Cascade(CascadeMode.Stop).NotEmpty().MustParseAs(typeof(Iban)),
        };

        both.Validate(new ImportCommand(string.Empty)).Errors.Select(failure => failure.ErrorCode)
            .Should().Equal("NotEmptyValidator", ValueObjectErrorCodes.Required);
        stopped.Validate(new ImportCommand(string.Empty)).Errors.Select(failure => failure.ErrorCode)
            .Should().Equal("NotEmptyValidator");
    }

    [Fact]
    public void MustParseAs_refuses_a_type_that_is_not_a_value_object_when_the_rule_is_built()
    {
        var validator = new InlineValidator<ImportCommand>();

        var build = () => validator.RuleFor(x => x.Iban).MustParseAs(typeof(string));

        build.Should().Throw<ArgumentException>()
            .Which.Should().Match<ArgumentException>(thrown =>
                thrown.ParamName == "valueObjectType" && thrown.Message.StartsWith("'String' is not a value object.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(10, true)]
    public void MustSatisfy_reports_the_code_of_the_rule_the_value_breaks(int amount, bool valid)
    {
        var validator = new InlineValidator<Transfer>
        {
            v => v.RuleFor(x => x.Amount).MustSatisfy<Transfer, Amount, decimal>(),
        };

        var result = validator.Validate(new Transfer(amount, Iban.Create("FR7630006000011234567890189")));

        result.IsValid.Should().Be(valid);
        result.Errors.Select(failure => (failure.PropertyName, failure.ErrorCode))
            .Should().Equal(valid ? [] : [(nameof(Transfer.Amount), ValueObjectErrorCodes.OutOfRange)]);
    }

    /// <summary>
    /// An instance nothing constructed - from another library's deserializer, an array element - holds no value
    /// that went through the rules.
    /// </summary>
    [Fact]
    public void NotDefault_rejects_an_uninitialized_value_object_as_required()
    {
        var validator = new InlineValidator<Transfer> { v => v.RuleFor(x => x.Account).NotDefault<Transfer, Iban, string>() };
#pragma warning disable VO0010 // The uninitialized value object is what the rule exists to catch.
        var uninitialized = new Transfer(10m, default);
#pragma warning restore VO0010

        var failure = validator.Validate(uninitialized).Errors.Should().ContainSingle().Which;

        failure.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        failure.ErrorMessage.Should().Be("'Account' is required.");
        validator.Validate(new Transfer(10m, Iban.Create("FR7630006000011234567890189"))).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// <c>IsDefault</c> tells an instance equal to the default, and over a value type a constructed zero is one: the
    /// rule refuses a valid zero as required, as its documentation says.
    /// </summary>
    [Fact]
    public void NotDefault_refuses_a_valid_zero_over_a_value_type_as_required()
    {
        var validator = new InlineValidator<Amount> { v => v.RuleFor(x => x).NotDefault<Amount, Amount, decimal>() };

        validator.Validate(Amount.Create(0m)).Errors.Should().ContainSingle()
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        validator.Validate(Amount.Create(0.01m)).IsValid.Should().BeTrue();
    }

    /// <summary>A command carrying raw text, as an inbound message from another system would.</summary>
    /// <param name="Iban">The account number, as text.</param>
    public sealed record ImportCommand(string? Iban);

    /// <summary>Commands imported together.</summary>
    /// <param name="Lines">The commands.</param>
    public sealed record ImportBatch(IReadOnlyList<ImportCommand> Lines);

    /// <summary>A command carrying a raw amount and a value object.</summary>
    /// <param name="Amount">The amount, as its underlying value.</param>
    /// <param name="Account">The account to debit.</param>
    public sealed record Transfer(decimal Amount, Iban Account);
}
