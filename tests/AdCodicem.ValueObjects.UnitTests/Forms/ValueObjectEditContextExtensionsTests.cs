using AdCodicem.ValueObjects.Blazor;
using AdCodicem.ValueObjects.Fixtures.Blazor;
using Microsoft.AspNetCore.Components.Forms;

namespace AdCodicem.ValueObjects.UnitTests.Forms;

/// <summary>
/// The codes the inputs of value objects keep on their form's <see cref="EditContext"/>, one per field, read with
/// <see cref="ValueObjectEditContextExtensions.TryGetValueObjectErrorCode"/>.
/// </summary>
public sealed class ValueObjectEditContextExtensionsTests
{
    [Fact]
    public void A_form_no_input_refused_anything_in_holds_no_code()
    {
        var model = new Holder<Iban>();
        var form = new EditContext(model);

        form.TryGetValueObjectErrorCode(new FieldIdentifier(model, nameof(model.Value)), out var code).Should().BeFalse();
        code.Should().BeNull();
    }

    [Fact]
    public void Reading_the_code_of_no_form_throws()
    {
        var read = () => ValueObjectEditContextExtensions.TryGetValueObjectErrorCode(null!, new FieldIdentifier(new Holder<Iban>(), "Value"), out _);

        read.Should().Throw<ArgumentNullException>().WithParameterName("editContext");
    }

    [Fact]
    public void A_code_is_kept_per_field_until_it_is_cleared()
    {
        var model = new Holder<Iban>();
        var other = new Holder<Iban>();
        var form = new EditContext(model);
        var field = new FieldIdentifier(model, nameof(model.Value));
        var otherField = new FieldIdentifier(other, nameof(other.Value));

        ValueObjectEditContextExtensions.SetCode(form, field, null);
        form.TryGetValueObjectErrorCode(field, out _).Should().BeFalse();

        ValueObjectEditContextExtensions.SetCode(form, field, ValueObjectErrorCodes.TooShort);
        ValueObjectEditContextExtensions.SetCode(form, otherField, ValueObjectErrorCodes.Required);
        form.TryGetValueObjectErrorCode(field, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);

        ValueObjectEditContextExtensions.SetCode(form, field, ValueObjectErrorCodes.InvalidFormat);
        form.TryGetValueObjectErrorCode(field, out code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.InvalidFormat);

        ValueObjectEditContextExtensions.SetCode(form, field, null);
        form.TryGetValueObjectErrorCode(field, out _).Should().BeFalse();
        form.TryGetValueObjectErrorCode(otherField, out code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.Required);
    }

    [Fact]
    public void An_input_outside_a_form_has_nowhere_to_keep_a_code()
    {
        var set = () => ValueObjectEditContextExtensions.SetCode(null, new FieldIdentifier(new Holder<Iban>(), "Value"), ValueObjectErrorCodes.TooShort);

        set.Should().NotThrow();
    }
}
