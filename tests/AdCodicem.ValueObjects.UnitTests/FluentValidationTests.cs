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

    /// <summary>A command carrying raw text, as an inbound message from another system would.</summary>
    /// <param name="Iban">The account number, as text.</param>
    public sealed record ImportCommand(string? Iban);
}
