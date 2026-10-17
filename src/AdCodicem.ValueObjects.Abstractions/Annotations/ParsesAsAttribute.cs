using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using AnnotationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AdCodicem.ValueObjects.Annotations;

/// <summary>
/// Requires text to be accepted by a value object, and reports the message and the code of the rule that refuses it.
/// </summary>
/// <remarks>
/// <para>
/// A DTO, an options class or a form model holding the text a value object is made from later states no rule of its own:
/// <c>[ParsesAs(typeof(Iban))] public string? Account { get; set; }</c> holds the text to the rules the type declares,
/// its normalization included, wherever DataAnnotations runs: <c>Validator</c>, MVC, <c>AddValidation()</c>, options
/// validation and <c>ObservableValidator</c>. It validates and changes nothing: the member keeps the text as it was sent.
/// </para>
/// <para>
/// A <see langword="null"/> passes, so that <see cref="RequiredAttribute"/> says whether the member must be there, and so
/// does an <see cref="ImmutableArray{T}"/> left at its default, which is how an absent member of that type is left. Empty
/// text is the value object's to judge, as any text is. A <see cref="string"/> is parsed in the invariant culture, as the
/// binders read text, unless <see cref="UseCurrentCulture"/> says otherwise; each element of an
/// <see cref="IEnumerable{T}"/> of <see cref="string"/> is parsed in turn, a <see langword="null"/> element skipped, and
/// the first refused is reported under the member's own name. A member of any other type throws
/// <see cref="InvalidOperationException"/>, a misuse <c>VO0039</c> reports at compile time.
/// </para>
/// <para>
/// A refusal is a <see cref="ValueObjectValidationResult"/>, which carries the code. Its message is the English message of
/// the rule unless the attribute declares one: <see cref="ValidationAttribute.ErrorMessage"/>, or a resource with its
/// name, give DataAnnotations' single message, <c>{0}</c> the member; <see cref="ValidationAttribute.ErrorMessageResourceType"/>
/// alone looks the message up by the code of the rule, under the keys of
/// <see cref="ValueObjectMessages.KeysFor(ValueObjectSchema, string)"/>, with the arguments of the rule from <c>{1}</c>,
/// in the current UI culture, and keeps the English message for a key the resource lacks. The message never quotes the
/// text.
/// </para>
/// <para>
/// This form names the value object by its <see cref="Type"/>, and finds its descriptor the first time a value is
/// validated: registered, or described by reflection where the runtime supports dynamic code. Under native AOT, a value
/// object written by hand and a construction of a generic one are found only once registered by hand, or through the type
/// arguments of <see cref="ParsesAsAttribute{TSelf, TValue}"/>, which needs no registry.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class ParsesAsAttribute : ValidationAttribute
{
    private ValueObjectDescriptor? _descriptor;

    /// <summary>
    /// Names the value object the text must be accepted by, which the registry describes the first time a value is
    /// validated.
    /// </summary>
    /// <param name="valueObjectType">The value object type, or its nullable type, which names the same value object.</param>
    /// <exception cref="ArgumentNullException"><paramref name="valueObjectType"/> is <see langword="null"/>.</exception>
    public ParsesAsAttribute(Type valueObjectType)
    {
        ArgumentNullException.ThrowIfNull(valueObjectType);

        ValueObjectType = Nullable.GetUnderlyingType(valueObjectType) ?? valueObjectType;
    }

    /// <summary>
    /// Gets the value object the text must be accepted by: the type named, or the value object a nullable type names,
    /// <c>Iban</c> for <c>typeof(Iban?)</c>, as a refusal reports it.
    /// </summary>
    public Type ValueObjectType { get; }

    /// <summary>
    /// Gets or sets whether the text is read in the current culture rather than the invariant one.
    /// </summary>
    /// <remarks>
    /// The binders, the configuration binder and <c>MustParseAs</c> read the invariant culture, which refuses
    /// <c>12,5</c>. A form a person types in, in French, may set it, and the application then converts the text in the
    /// same culture when it reads it.
    /// </remarks>
    public bool UseCurrentCulture { get; set; }

    /// <summary>Gets the rules the arguments of a message are read from.</summary>
    private protected virtual ValueObjectSchema Schema => Descriptor.Schema;

    /// <summary>Gets the descriptor of the value object, found the first time a value is validated.</summary>
    private ValueObjectDescriptor Descriptor
        => _descriptor ??= ValueObjectValidationAttributes.Resolve(ValueObjectType, "[ParsesAs] names", ", or named through [ParsesAs<TSelf, TValue>]");

    /// <inheritdoc />
    /// <remarks>
    /// The message DataAnnotations formats on its own, outside a validation, as <see cref="ValidationAttribute.Validate(object?, string)"/>
    /// does: with <see cref="ValidationAttribute.ErrorMessageResourceType"/> alone, which DataAnnotations would refuse,
    /// its default message.
    /// </remarks>
    public override string FormatErrorMessage(string name)
        => ValueObjectValidationAttributes.NamesResourceAlone(this, out _)
            ? ValueObjectValidationAttributes.InvalidMessage(name)
            : base.FormatErrorMessage(name);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The member is neither text nor a collection of text, or the type is not a value object the registry describes.
    /// </exception>
    protected override AnnotationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var provider = UseCurrentCulture ? CultureInfo.CurrentCulture : CultureInfo.InvariantCulture;
        switch (value)
        {
            case null:
            case ImmutableArray<string> { IsDefault: true }:
                return AnnotationResult.Success;
            case string text:
                return TryParse(text, provider, out var validation) ? AnnotationResult.Success : Refuse(validation, validationContext);
            case IEnumerable<string?> texts:
                foreach (var element in texts)
                {
                    if (element is not null && !TryParse(element, provider, out var refused))
                    {
                        return Refuse(refused, validationContext);
                    }
                }

                return AnnotationResult.Success;
            default:
                throw new InvalidOperationException(
                    $"[ParsesAs] holds text to the rules of '{ValueObjectType.Name}', and "
                    + $"'{ValueObjectValidationAttributes.DisplayName(validationContext, ValueObjectType)}' is of type "
                    + $"'{value.GetType().Name}': declare it string, or a collection of string.");
        }
    }

    /// <summary>
    /// Parses text as the value object, reporting why it is refused.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="provider">The culture the text is read in.</param>
    /// <param name="validation">Why the text is refused, always with a code.</param>
    /// <returns><see langword="true"/> when the text is accepted.</returns>
    private protected virtual bool TryParse(string text, IFormatProvider provider, out ValidationResult validation)
        => Descriptor.TryParse(text, provider, out _, out validation);

    /// <summary>
    /// Gets the arguments a message for a code quotes, formatted in the current culture.
    /// </summary>
    /// <param name="errorCode">The code of the rule.</param>
    /// <returns>The arguments, in the order of their positions from <c>{1}</c>.</returns>
    private protected virtual IReadOnlyList<KeyValuePair<string, object?>> Arguments(string errorCode)
        => ValueObjectMessages.ArgumentsFor(Descriptor, errorCode, CultureInfo.CurrentCulture);

    /// <summary>
    /// Builds the result of a refusal: the code of the rule, and the message the attribute declares.
    /// </summary>
    private ValueObjectValidationResult Refuse(ValidationResult validation, ValidationContext? context)
    {
        var code = validation.ErrorCode!;
        var name = ValueObjectValidationAttributes.DisplayName(context, ValueObjectType);

        string message;
        if (ValueObjectValidationAttributes.NamesResourceAlone(this, out var resource))
        {
            message = ValueObjectResourceMessages.Find(resource, ValueObjectMessages.KeysFor(Schema, code), () => Arguments(code), name)
                      ?? validation.ErrorMessage!;
        }
        else if (CustomMessage)
        {
            message = FormatErrorMessage(name);
        }
        else
        {
            message = validation.ErrorMessage!;
        }

        // A rule written by hand may give an empty message, which DataAnnotations would replace with its own, code lost.
        if (message.Length == 0)
        {
            message = ValueObjectValidationAttributes.InvalidMessage(name);
        }

        return new ValueObjectValidationResult(message, ValueObjectValidationAttributes.MemberNames(context), ValueObjectType, code);
    }

    /// <summary>Gets whether the attribute declares DataAnnotations' single message, or the resource it is read from.</summary>
    private bool CustomMessage
        => !string.IsNullOrEmpty(ErrorMessage) || ErrorMessageResourceType is not null || !string.IsNullOrEmpty(ErrorMessageResourceName);
}

/// <summary>
/// Requires text to be accepted by a value object named at compile time, through its static members, with no registry.
/// </summary>
/// <typeparam name="TSelf">The value object type.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
/// <remarks>
/// It checks and reports as <see cref="ParsesAsAttribute"/> does, through <see cref="IValueObject{TSelf, TValue}.TryParse"/>
/// and <see cref="IValueObject{TSelf, TValue}.Schema"/>, so that a value object written by hand, or a construction of a
/// generic one, needs no registration under native AOT. It derives from <see cref="ParsesAsAttribute"/>, so that a
/// document generator finds both forms as one, and reads <see cref="ParsesAsAttribute.ValueObjectType"/>.
/// </remarks>
public sealed class ParsesAsAttribute<TSelf, TValue> : ParsesAsAttribute
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Names the value object through the type arguments.
    /// </summary>
    public ParsesAsAttribute()
        : base(typeof(TSelf))
    {
    }

    /// <inheritdoc />
    private protected override ValueObjectSchema Schema => TSelf.Schema;

    /// <inheritdoc />
    private protected override bool TryParse(string text, IFormatProvider provider, out ValidationResult validation)
    {
        if (TSelf.TryParse(text, provider, out _, out validation))
        {
            return true;
        }

        // A parser written by hand may refuse without a code, which the descriptor's parser gives it too.
        if (validation.IsValid)
        {
            validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, $"The text could not be parsed as {typeof(TSelf).Name}.");
        }

        return false;
    }

    /// <inheritdoc />
    private protected override IReadOnlyList<KeyValuePair<string, object?>> Arguments(string errorCode)
        => ValueObjectMessages.ArgumentsFor<TSelf, TValue>(errorCode, CultureInfo.CurrentCulture);
}
