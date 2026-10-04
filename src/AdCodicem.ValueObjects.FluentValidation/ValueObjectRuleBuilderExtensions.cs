using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.Metadata;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Results;

namespace AdCodicem.ValueObjects.FluentValidation;

/// <summary>
/// FluentValidation rules built on the rules a value object already enforces.
/// </summary>
/// <remarks>
/// <para>
/// The point is to state a rule once. A command carrying a raw <c>string</c> for an IBAN should not restate the
/// length, the pattern and the check-digit rule in its validator: it should defer to the value object that owns
/// them, and report the same stable error code the rest of the system uses.
/// </para>
/// <para>
/// A failure of <see cref="MustParseAs{T}"/> or <see cref="MustSatisfy{T, TSelf, TValue}"/> carries, by default, the
/// code and the message of the value object's rule that refused the value. The options chained on either replace
/// them as they would on any rule: <c>WithErrorCode</c>, <c>WithMessage</c>, <c>WithSeverity</c>, <c>WithState</c>
/// and <c>WithName</c>. A message template can quote the value object's own message as <c>{Reason}</c>, besides the
/// usual <c>{PropertyName}</c>, <c>{PropertyValue}</c>, <c>{PropertyPath}</c> and, in a child validator run for each
/// element of a collection, <c>{CollectionIndex}</c>. The global options apply as well: the default severity, and the
/// <c>OnFailureCreated</c> callback.
/// </para>
/// </remarks>
public static class ValueObjectRuleBuilderExtensions
{
    /// <summary>
    /// The message a failure carries unless one is chained: the message of the value object's rule.
    /// </summary>
    private const string ReasonTemplate = "{Reason}";

    /// <summary>
    /// The key under which FluentValidation hands a child validator, run for each element of a collection, the index of
    /// the element.
    /// </summary>
    private const string CollectionIndexKey = "__FV_CollectionIndex";

    /// <summary>
    /// Requires the text to be acceptable to a value object type.
    /// </summary>
    /// <typeparam name="T">Validated object.</typeparam>
    /// <param name="ruleBuilder">Rule builder for a text member.</param>
    /// <param name="valueObjectType">Value object the text must parse into.</param>
    /// <returns>The rule, so it can be configured further.</returns>
    /// <remarks>
    /// <para>
    /// A <see langword="null"/> passes: whether the member is required is <c>NotNull</c>'s or <c>NotEmpty</c>'s to
    /// say, as everywhere in FluentValidation. Empty text is the value object's to judge, as any text is: a string
    /// value object refuses it as required unless it allows empty text, and one over another type cannot parse it.
    /// </para>
    /// <para>
    /// The value object type is passed as a <see cref="Type"/> rather than a type argument so that
    /// <c>RuleFor(x =&gt; x.Iban).MustParseAs(typeof(Iban))</c> stays readable: C# cannot infer one type argument
    /// while another is given explicitly, and spelling out the validated type at every rule is noise.
    /// </para>
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> MustParseAs<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        Type valueObjectType)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);
        ArgumentNullException.ThrowIfNull(valueObjectType);

        var descriptor = Resolve(valueObjectType);
        var options = new ChainedOptions<T, string?>();

        return ruleBuilder
            .Must((instance, value, context) =>
            {
                if (value is null || descriptor.TryParse(value, CultureInfo.InvariantCulture, out _, out var validation))
                {
                    return true;
                }

                options.Report(context, value, validation);

                return true; // The failure is already reported, with the code of the rule it breaks.
            })
            .WithMessage(ReasonTemplate)
            .Configure(options.Capture);
    }

    /// <summary>
    /// Requires an underlying value to satisfy the rules of a value object, without constructing it.
    /// </summary>
    /// <typeparam name="T">Validated object.</typeparam>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="ruleBuilder">Rule builder for a member holding the underlying value.</param>
    /// <returns>The rule, so it can be configured further.</returns>
    /// <remarks>
    /// A <see langword="null"/> passes: whether the member is required is <c>NotNull</c>'s or <c>NotEmpty</c>'s to
    /// say, as everywhere in FluentValidation.
    /// </remarks>
    public static IRuleBuilderOptions<T, TValue> MustSatisfy<T, TSelf, TValue>(this IRuleBuilder<T, TValue> ruleBuilder)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);

        var options = new ChainedOptions<T, TValue>();

        return ruleBuilder
            .Must((instance, value, context) =>
            {
                if (value is null)
                {
                    return true;
                }

                var normalized = TSelf.Normalize(value);
                var validation = TSelf.Validate(in normalized);
                if (validation.IsValid)
                {
                    return true;
                }

                options.Report(context, value, validation);

                return true; // The failure is already reported, with the code of the rule it breaks.
            })
            .WithMessage(ReasonTemplate)
            .Configure(options.Capture);
    }

    /// <summary>
    /// Requires a value object member not to equal <c>default(TSelf)</c>, through
    /// <see cref="IValueObject{TSelf, TValue}.IsDefault"/>.
    /// </summary>
    /// <typeparam name="T">Validated object.</typeparam>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="ruleBuilder">Rule builder for a value object member.</param>
    /// <returns>The rule, so it can be configured further.</returns>
    /// <remarks>
    /// <para>
    /// Catches the one hole a struct value object cannot close by itself: an uninitialized instance, which the
    /// CLR always allows and which the analyzer only catches where it can see the code.
    /// </para>
    /// <para>
    /// Over a value type, a constructed instance holding the type's zero (<c>0</c>, <see cref="Guid.Empty"/>, …) equals
    /// the default too, so the rule refuses a valid zero as <c>value_object.required</c>. Keep it for a value object
    /// over <see cref="string"/>, or over a value type whose rules refuse its zero anyway.
    /// </para>
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

    /// <summary>
    /// Finds the descriptor of a value object: the one registered, or, where the runtime supports dynamic code, one the
    /// registry describes by reflection.
    /// </summary>
    /// <param name="valueObjectType">The value object type.</param>
    /// <returns>Its descriptor.</returns>
    /// <exception cref="ArgumentException">The type is not a value object the registry describes.</exception>
    /// <remarks>
    /// A <c>#pragma</c> silences the analyzers that run with the compiler, never the trimmer or the native AOT compiler,
    /// which read the compiled code: they reported the call to <see cref="ValueObjectRegistry.TryResolve"/> in every
    /// application published with native AOT that called <c>MustParseAs</c>. Under native AOT, where the reflection that
    /// call needs is not available, it is not made: a generated value object is registered already, and a construction
    /// of a generic one is registered by hand.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Only reached for a value object nothing registered: one written by hand, or a construction of a "
                        + "generic one, whose type the rule names, which keeps it. Under native AOT, where dynamic code is "
                        + "not supported, the registered descriptor is the only one.")]
    private static ValueObjectDescriptor Resolve(Type valueObjectType)
    {
        if (ValueObjectRegistry.TryGet(valueObjectType, out var descriptor)
            || (RuntimeFeature.IsDynamicCodeSupported && ValueObjectRegistry.TryResolve(valueObjectType, out descriptor)))
        {
            return descriptor;
        }

        throw new ArgumentException(
            $"'{valueObjectType.Name}' is not a value object. Under native AOT, a construction of a generic value object "
            + "has to be registered before a rule names it.",
            nameof(valueObjectType));
    }

    /// <summary>
    /// Reports a refusal with the options chained on the rule, falling back on the value object's code and message.
    /// </summary>
    /// <typeparam name="T">Validated object.</typeparam>
    /// <typeparam name="TProperty">Validated member.</typeparam>
    /// <remarks>
    /// <para>
    /// FluentValidation gives a rule one error code, fixed when the rule is built, while each rule of a value object
    /// has its own. The failure is therefore built here, as FluentValidation builds it from the rule: the code
    /// chained with <c>WithErrorCode</c> when there is one, else the value object's; the message template of the rule,
    /// <c>{Reason}</c> unless another is chained, formatted with the placeholders FluentValidation prepares for any
    /// failure, the index of a collection element included; the severity chained, else the global one; the state
    /// chained, if any; and the global <c>OnFailureCreated</c> callback last. A message builder set on the rule through
    /// <c>Configure</c> is the one thing left out: FluentValidation lets it be set, but not read back.
    /// </para>
    /// <para>
    /// The rule is built once and validated concurrently, so the component is only read here, never written.
    /// </para>
    /// </remarks>
    private sealed class ChainedOptions<T, TProperty>
    {
        private IValidationRule<T, TProperty>? _rule;

        private RuleComponent<T, TProperty>? _component;

        /// <summary>
        /// Captures the rule and the component holding the options chained on it, once, when the rule is built.
        /// </summary>
        /// <param name="rule">The rule.</param>
        public void Capture(IValidationRule<T, TProperty> rule)
        {
            _rule = rule;
            _component = (RuleComponent<T, TProperty>)rule.Current;
        }

        /// <summary>
        /// Reports the refusal of a value.
        /// </summary>
        /// <param name="context">Validation context.</param>
        /// <param name="value">The value refused.</param>
        /// <param name="validation">Why the value object refused it.</param>
        public void Report(ValidationContext<T> context, TProperty value, ValidationResult validation)
        {
            var rule = _rule!;
            var component = _component!;

            var formatter = context.MessageFormatter
                .AppendPropertyName(context.DisplayName)
                .AppendPropertyValue(value)
                .AppendArgument("PropertyPath", context.PropertyPath)
                .AppendArgument("Reason", validation.ErrorMessage);

            // A child validator run for each element of a collection is handed the index of the element.
            if (context.RootContextData.TryGetValue(CollectionIndexKey, out var index)
                && !formatter.PlaceholderValues.ContainsKey("CollectionIndex"))
            {
                formatter.AppendArgument("CollectionIndex", index);
            }

            var failure = new ValidationFailure(context.PropertyPath, component.GetErrorMessage(context, value), value)
            {
                ErrorCode = component.ErrorCode ?? validation.ErrorCode,
                FormattedMessagePlaceholderValues = new(formatter.PlaceholderValues),
                Severity = component.SeverityProvider is { } severity ? severity(context, value) : ValidatorOptions.Global.Severity,
            };

            if (component.CustomStateProvider is { } state)
            {
                failure.CustomState = state(context, value);
            }

            context.AddFailure(ValidatorOptions.Global.OnFailureCreated is { } created
                ? created(failure, context, value, rule, component)
                : failure);
        }
    }
}
