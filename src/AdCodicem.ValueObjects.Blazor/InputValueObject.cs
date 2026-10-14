using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.Blazor;

/// <summary>
/// An input for a value object: reads the text a person types through the value object's rules, binds the normalized
/// instance, shows the message of the rule that refuses it, and keeps the rule's code on the form's
/// <see cref="EditContext"/>.
/// </summary>
/// <typeparam name="TValue">The value object, or its nullable type.</typeparam>
/// <remarks>
/// <para>
/// It renders a text <c>&lt;input&gt;</c>, as <see cref="InputText"/> does, and reads its text through
/// <see cref="IValueObject{TSelf, TValue}.TryParse"/>, the overload that reports the rule. Accepted text binds the
/// instance the type creates, which the input then shows normalized. Refused text leaves the model as it was, stays in the
/// field, marked invalid, and adds the rule's message to the form's validation messages, so that
/// <see cref="ValidationMessage{TValue}"/> and <see cref="ValidationSummary"/> show it and
/// <see cref="EditContext.Validate"/> returns <see langword="false"/>; the code of the rule is read with
/// <see cref="ValueObjectEditContextExtensions.TryGetValueObjectErrorCode"/>. A message never quotes the text typed.
/// </para>
/// <para>
/// Empty text binds <see langword="null"/> to a nullable value object, which <see cref="InputBase{TValue}"/> does before
/// any parsing. On a value object that cannot be <see langword="null"/>, it is parsed as any other text: a type that
/// accepts it, such as <c>[ValueObject&lt;string&gt;(AllowEmpty = true)]</c>, binds the instance it creates, and a type
/// that refuses it is answered with <see cref="ValueObjectErrorCodes.Required"/> and the message
/// <c>[Required]</c> gives, "The {0} field is required.", <c>{0}</c> being <see cref="InputBase{TValue}.DisplayName"/>,
/// or else the name of the field.
/// </para>
/// <para>
/// The text an instance shows is the text its type reads back: the value object's own text, written in the round-trip
/// form of its underlying type (<c>2026-10-14</c> for a date), and the bare underlying value, in that same form, for a
/// type whose formatting hook writes something else. An instance equal to the default that its rules refuse, a field
/// nobody filled in, shows as empty text; one they accept, a zero they allow, shows as its value.
/// </para>
/// <para>
/// The text is read and written in <see cref="Culture"/>, or else in the current culture where the component is
/// interactive, or prerenders for interactivity, and in the invariant culture under static rendering, which is the culture
/// the framework reads a posted form in. A date, of a value object over <see cref="DateOnly"/>, <see cref="DateTime"/> or
/// <see cref="DateTimeOffset"/>, is read in the Gregorian calendar its text is written in, whatever the culture's own
/// calendar: in <c>th-TH</c> or <c>fa-IR</c>, the culture's patterns and names stay, and a year typed is a Gregorian one.
/// A statically rendered form is mapped by the framework, through <see cref="IParsable{TSelf}"/>, before any component
/// runs: a refused value is then reported with the framework's own message, which quotes the text and carries no code.
/// </para>
/// <para>
/// The value object is found in <see cref="Metadata.ValueObjectRegistry"/>, where a generated one is registered, or else, where
/// the runtime supports dynamic code, described by reflection. Under native AOT, a value object written by hand and a
/// construction of a generic one are registered by hand before the input renders; one the registry does not describe
/// fails the first render with an <see cref="InvalidOperationException"/> naming the registration.
/// </para>
/// </remarks>
public class InputValueObject<TValue> : InputBase<TValue>
{
    private ValueObjectInputText<TValue>? _text;

    /// <summary>
    /// Gets or sets the culture the text is read and written in; when not set, the current culture where the component is
    /// interactive or prerenders for interactivity, the invariant one under static rendering.
    /// </summary>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the input carries the <c>maxlength</c>, <c>minlength</c> and
    /// <c>pattern</c> attributes the value object's rules give; off by default, since a browser checks the text as it is
    /// typed, before the value object's normalizer runs.
    /// </summary>
    /// <remarks>
    /// The lengths are counted in UTF-16 code units, by the browser as by .NET. The pattern is written only when a browser,
    /// which compiles it with the <c>v</c> flag and matches it against the whole text, accepts every text the .NET pattern
    /// accepts: a pattern each of whose alternatives is anchored at both ends, with no <c>.</c>, negated class or escape,
    /// word boundary, class subtraction, surrogate, named block or category <c>C</c> or <c>Cs</c>, and nothing the shared
    /// reader of .NET patterns refuses, such as a lookaround; <c>\d</c>, <c>\w</c>, <c>\s</c> and <c>\p{…}</c> are
    /// written as the characters .NET matches them with. The attributes are written the first time an input of the type
    /// asks for them. An attribute the input is given explicitly wins over the one the rules give.
    /// </remarks>
    [Parameter]
    public bool SchemaAttributes { get; set; }

    /// <summary>Gets or sets the associated <see cref="ElementReference"/>.</summary>
    [DisallowNull]
    public ElementReference? Element { get; protected set; }

    /// <summary>Gets or sets the services the messages of the application's resource are read through.</summary>
    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    private ValueObjectInputText<TValue> Text => _text ??= ValueObjectInputText<TValue>.Resolve();

    /// <summary>
    /// Gets the culture the text is read and written in: a static form is posted back in the invariant culture, which the
    /// framework's form mapping reads it in, and an interactive one, prerendered or not, in the current culture.
    /// </summary>
    private CultureInfo EffectiveCulture
        => Culture ?? (RendererInfo.IsInteractive || AssignedRenderMode is not null ? CultureInfo.CurrentCulture : CultureInfo.InvariantCulture);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var text = CurrentValueAsString;
        builder.OpenElement(0, "input");
        if (SchemaAttributes)
        {
            // Before the attributes the input is given, which win over them.
            builder.AddMultipleAttributes(1, Text.SchemaAttributes);
        }

        builder.AddMultipleAttributes(2, AdditionalAttributes);
        if (!string.IsNullOrEmpty(NameAttributeValue))
        {
            builder.AddAttribute(3, "name", NameAttributeValue);
        }

        if (!string.IsNullOrEmpty(CssClass))
        {
            builder.AddAttribute(4, "class", CssClass);
        }

        builder.AddAttribute(5, "value", text);
        builder.AddAttribute(6, "onchange", EventCallback.Factory.CreateBinder<string?>(this, Change, text));
        builder.SetUpdatesAttributeName("value");
        builder.AddElementReferenceCapture(7, reference => Element = reference);
        builder.CloseElement();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="InputBase{TValue}"/> formats the model's value only while no refused text shows, so the code of an
    /// earlier refusal is cleared here too, whichever way the text was set. A subclass that sets
    /// <see cref="InputBase{TValue}.CurrentValueAsString"/> to empty text on a nullable value object reaches no member of
    /// the input before the form notifies the change, which still reads the earlier code; the change event clears it first.
    /// </remarks>
    protected override string? FormatValueAsString(TValue? value)
    {
        ValueObjectEditContextExtensions.SetCode(EditContext, FieldIdentifier, null);

        return Text.Format(value, EffectiveCulture);
    }

    /// <inheritdoc />
    protected override bool TryParseValueFromString(
        string? value,
        [MaybeNullWhen(false)] out TValue result,
        [NotNullWhen(false)] out string? validationErrorMessage)
    {
        if (Text.TryParse(value, EffectiveCulture, out result, out var code, out var message))
        {
            ValueObjectEditContextExtensions.SetCode(EditContext, FieldIdentifier, null);
            validationErrorMessage = null;
            return true;
        }

        var member = DisplayName ?? FieldIdentifier.FieldName;
        validationErrorMessage = Localize(code, member) ?? InputMessages.For(message, member);
        ValueObjectEditContextExtensions.SetCode(EditContext, FieldIdentifier, code);
        return false;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        ValueObjectEditContextExtensions.SetCode(EditContext, FieldIdentifier, null);

        base.Dispose(disposing);
    }

    /// <summary>
    /// Takes the text of a change, clearing the code of an earlier refusal first: empty text bound to a nullable value
    /// object is never parsed.
    /// </summary>
    private void Change(string? text)
    {
        ValueObjectEditContextExtensions.SetCode(EditContext, FieldIdentifier, null);
        CurrentValueAsString = text;
    }

    /// <summary>Looks the message of a refusal up in the resource the application names, if it names one.</summary>
    private string? Localize(string code, string member)
        => Services.GetService<IOptions<ValueObjectInputOptions>>()?.Value.ResourceType is { } resource
            ? Text.Localize(resource, Services, code, member)
            : null;
}
