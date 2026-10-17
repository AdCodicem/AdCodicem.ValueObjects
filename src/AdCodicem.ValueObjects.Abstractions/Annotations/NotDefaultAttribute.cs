using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using AnnotationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AdCodicem.ValueObjects.Annotations;

/// <summary>
/// Requires a value-object member not to hold the instance equal to <c>default(TSelf)</c>, which
/// <see cref="RequiredAttribute"/> lets through, since a struct is never <see langword="null"/>.
/// </summary>
/// <remarks>
/// <para>
/// A <see langword="null"/> passes, so that an absent <c>TSelf?</c> is <see cref="RequiredAttribute"/>'s to judge; a
/// <c>TSelf?</c> holding the default instance is refused. The check reads
/// <see cref="IValueObject{TSelf, TValue}.IsDefault"/> through the descriptor of the value object's type, so that over a
/// value type a constructed zero, <c>OrderId.Create(Guid.Empty)</c>, is refused too, as FluentValidation's
/// <c>NotDefault</c> refuses it. A member that is no value object throws <see cref="InvalidOperationException"/>, a misuse
/// <c>VO0041</c> reports at compile time, and so does a collection of value objects, whose elements are not checked.
/// </para>
/// <para>
/// A refusal is a <see cref="ValueObjectValidationResult"/> carrying <see cref="ValueObjectErrorCodes.Required"/>, with
/// <see cref="RequiredAttribute"/>'s message, <c>The {0} field is required.</c>, so that an answer reads the same
/// whichever refused. <see cref="ValidationAttribute.ErrorMessage"/>, or a resource with its name, replace it as on any
/// attribute; <see cref="ValidationAttribute.ErrorMessageResourceType"/> alone looks the message up under
/// <c>ValueObject.value_object.required</c>, <c>{0}</c> the member, as FluentValidation's <c>NotDefault</c> does.
/// </para>
/// <para>
/// The type is found in the registry, or described by reflection where the runtime supports dynamic code: under native
/// AOT, a value object written by hand and a construction of a generic one are registered by hand first.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class NotDefaultAttribute : ValidationAttribute
{
    /// <summary>How each value object type tells its default instance, closed over the type once, through its descriptor.</summary>
    private static readonly ConcurrentDictionary<Type, Func<object, bool>> DefaultChecks = new();

    /// <summary>
    /// Creates the attribute, whose message is <see cref="RequiredAttribute"/>'s, <c>The {0} field is required.</c>.
    /// </summary>
    public NotDefaultAttribute()
        : base(ValueObjectValidationAttributes.RequiredMessage)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// The message DataAnnotations formats on its own, outside a validation: with
    /// <see cref="ValidationAttribute.ErrorMessageResourceType"/> alone, which DataAnnotations would refuse,
    /// <see cref="RequiredAttribute"/>'s.
    /// </remarks>
    public override string FormatErrorMessage(string name)
        => ValueObjectValidationAttributes.NamesResourceAlone(this, out _)
            ? ValueObjectValidationAttributes.RequiredMessageFor(name)
            : base.FormatErrorMessage(name);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The member is no value object, or its type is not a value object the registry describes.
    /// </exception>
    protected override AnnotationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return AnnotationResult.Success;
        }

        var type = value.GetType();
        if (value is not IValueObject)
        {
            throw new InvalidOperationException(
                $"[NotDefault] checks a value object, and '{ValueObjectValidationAttributes.DisplayName(validationContext, type)}' "
                + $"is of type '{type.Name}'.");
        }

        if (!DefaultChecks.GetOrAdd(type, static key => ValueObjectValidationAttributes.Resolve(key, "[NotDefault] checks", string.Empty).Accept(DefaultCheck.Instance))(value))
        {
            return AnnotationResult.Success;
        }

        var name = ValueObjectValidationAttributes.DisplayName(validationContext, type);
        var message = ValueObjectValidationAttributes.NamesResourceAlone(this, out var resource)
            ? ValueObjectResourceMessages.Find(resource, [ValueObjectMessages.KeyFor(ValueObjectErrorCodes.Required)], static () => [], name)
              ?? ValueObjectValidationAttributes.RequiredMessageFor(name)
            : FormatErrorMessage(name);

        return new ValueObjectValidationResult(message, ValueObjectValidationAttributes.MemberNames(validationContext), type, ValueObjectErrorCodes.Required);
    }

    /// <summary>
    /// Closes the check of the default instance over the type arguments a descriptor hands back, with no
    /// <see cref="Type.MakeGenericType(Type[])"/>.
    /// </summary>
    private sealed class DefaultCheck : IValueObjectVisitor<Func<object, bool>>
    {
        public static readonly DefaultCheck Instance = new();

        public Func<object, bool> Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => static boxed => ((TSelf)boxed).IsDefault;
    }
}
