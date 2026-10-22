using AdCodicem.ValueObjects.Blazor;
using AdCodicem.ValueObjects.Fixtures.Blazor;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;

namespace AdCodicem.ValueObjects.UnitTests.Forms;

/// <summary>
/// <c>[NotDefault]</c> under <see cref="DataAnnotationsValidator"/> beside the input of the same value object: the guard of a
/// field nobody touched, which DataAnnotations names by its member and the input by its <c>DisplayName</c>.
/// </summary>
public sealed class AnnotatedFormTests
{
    [Fact]
    public void An_untouched_field_is_refused_on_submit_by_NotDefault()
    {
        using var context = FormsHarness.Context();
        var form = context.Render<AnnotatedForm>();

        form.Find("form").Submit();

        form.Instance.Invalid.Should().Be(1);
        form.Instance.Context.GetValidationMessages().Should().Equal("The Account field is required.");
        form.Instance.Context.TryGetValueObjectErrorCode(Field(form), out _).Should().BeFalse();
    }

    [Fact]
    public void Refused_text_in_an_untouched_field_shows_both_messages_under_it()
    {
        using var context = FormsHarness.Context();
        var form = context.Render<AnnotatedForm>();

        form.Find("input").Change(string.Empty);

        form.Instance.Context.GetValidationMessages(Field(form)).Should().BeEquivalentTo("The IBAN field is required.", "The Account field is required.");
        form.Instance.Context.TryGetValueObjectErrorCode(Field(form), out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.Required);

        form.Find("input").Change("fr76 3000 6000 0112 3456 7890 189");
        form.Find("form").Submit();
        form.Instance.Valid.Should().Be(1);
        form.Instance.Context.GetValidationMessages().Should().BeEmpty();
    }

    private static FieldIdentifier Field(IRenderedComponent<AnnotatedForm> form) => new(form.Instance.Model, nameof(AnnotatedOrder.Account));
}
