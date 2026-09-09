using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using FluentValidation;

namespace AdCodicem.ValueObjects.FluentValidation;

/// <summary>
/// FluentValidation rules built on the rules a value object already enforces.
/// </summary>
/// <remarks>
/// The point is to state a rule once. A command carrying a raw <c>string</c> for an IBAN should not restate the
/// length, the pattern and the check-digit rule in its validator: it should defer to the value object that owns
/// them, and report the same stable error code the rest of the system uses.
/// </remarks>
public static class ValueObjectRuleBuilderExtensions
{
    /// <summary>
    /// Requires the text to be acceptable to a value object type.
    /// </summary>
    /// <typeparam name="T">Validated object.</typeparam>
    /// <param name="ruleBuilder">Rule builder for a text member.</param>
    /// <param name="valueObjectType">Value object the text must parse into.</param>
    /// <returns>The rule, so it can be configured further.</returns>
    /// <remarks>
    /// The value object type is passed as a <see cref="Type"/> rather than a type argument so that
    /// <c>RuleFor(x =&gt; x.Iban).MustParseAs(typeof(Iban))</c> stays readable: C# cannot infer one type argument
    /// while another is given explicitly, and spelling out the validated type at every rule is noise.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> MustParseAs<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        Type valueObjectType)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);
        ArgumentNullException.ThrowIfNull(valueObjectType);

        var descriptor = Resolve(valueObjectType);

        return ruleBuilder
            .Must((instance, value, context) =>
            {
                if (value is null)
                {
                    return false;
                }

                if (descriptor.TryParse(value, CultureInfo.InvariantCulture, out _, out var validation))
                {
                    return true;
                }

                context.MessageFormatter.AppendArgument("Reason", validation.ErrorMessage);
                context.AddFailure(BuildFailure(context.PropertyPath, validation));

                return true; // The failure is already reported, with its code attached.
            })
            .WithMessage($"'{{PropertyName}}' is not a valid {valueObjectType.Name}.");
    }

    /// <summary>
    /// Requires an underlying value to satisfy the rules of a value object, without constructing it.
    /// </summary>
    /// <typeparam name="T">Validated object.</typeparam>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="ruleBuilder">Rule builder for a member holding the underlying value.</param>
    /// <returns>The rule, so it can be configured further.</returns>
    public static IRuleBuilderOptions<T, TValue> MustSatisfy<T, TSelf, TValue>(this IRuleBuilder<T, TValue> ruleBuilder)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);

        return ruleBuilder
            .Must((instance, value, context) =>
            {
                var normalized = TSelf.Normalize(value);
                var validation = TSelf.Validate(in normalized);
                if (validation.IsValid)
                {
                    return true;
                }

                context.AddFailure(BuildFailure(context.PropertyPath, validation));

                return true;
            })
            .WithMessage($"'{{PropertyName}}' is not a valid {typeof(TSelf).Name}.");
    }

    /// <summary>
    /// Requires a value object member to hold a value that actually went through validation.
    /// </summary>
    /// <typeparam name="T">Validated object.</typeparam>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="ruleBuilder">Rule builder for a value object member.</param>
    /// <returns>The rule, so it can be configured further.</returns>
    /// <remarks>
    /// Catches the one hole a struct value object cannot close by itself: an uninitialized instance, which the
    /// CLR always allows and which the analyzer only catches where it can see the code.
    /// </remarks>
    public static IRuleBuilderOptions<T, TSelf> NotDefault<T, TSelf, TValue>(this IRuleBuilder<T, TSelf> ruleBuilder)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);

        return ruleBuilder
            .Must(static value => !value.IsDefault)
            .WithErrorCode(ValueObjectErrorCodes.Required)
            .WithMessage("'{PropertyName}' is required.");
    }

    private static global::FluentValidation.Results.ValidationFailure BuildFailure(string propertyPath, ValidationResult validation)
        => new(propertyPath, validation.ErrorMessage)
        {
            ErrorCode = validation.ErrorCode,
        };

    private static ValueObjectDescriptor Resolve(Type valueObjectType)
    {
#pragma warning disable IL2026, IL3050 // Validators are wired up at start-up, never on a request path.
        if (ValueObjectRegistry.TryResolve(valueObjectType, out var descriptor))
        {
            return descriptor;
        }
#pragma warning restore IL2026, IL3050

        throw new ArgumentException($"'{valueObjectType.Name}' is not a value object.", nameof(valueObjectType));
    }
}
