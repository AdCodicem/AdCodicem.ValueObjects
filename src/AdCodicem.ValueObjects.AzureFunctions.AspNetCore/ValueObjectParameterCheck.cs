using System.Collections.Concurrent;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.AzureFunctions.AspNetCore;

/// <summary>
/// Parses the text of one value-object parameter again, as the converter parsed it, to learn which rule refused it.
/// </summary>
internal abstract class ValueObjectParameterCheck
{
    private static readonly ConcurrentDictionary<Type, ValueObjectParameterCheck> Checks = new();

    /// <summary>Parses the text, and adds its refusal, if any, to the list.</summary>
    /// <param name="text">The text the worker held for the parameter.</param>
    /// <param name="member">The name of the parameter, which the refusal is listed under.</param>
    /// <param name="refusals">The refusals of the invocation.</param>
    public abstract void Check(string text, string member, ValueObjectRefusals refusals);

    /// <summary>Finds the check of a value object, keeping it only once the type is described.</summary>
    /// <param name="type">The parameter's type, possibly nullable.</param>
    /// <param name="check">The check, when the type is a value object the registry describes.</param>
    /// <returns>Whether a check was found.</returns>
    public static bool TryGet(Type type, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ValueObjectParameterCheck? check)
    {
        if (Checks.TryGetValue(type, out check))
        {
            return true;
        }

        if (!ValueObjectBindingText.TryDescribe(type, out var descriptor))
        {
            return false;
        }

        check = Checks.GetOrAdd(type, descriptor.Accept(For.Instance));
        return true;
    }

    /// <summary>Creates the check of the value object a descriptor stands for, closed through the visitor.</summary>
    private sealed class For : IValueObjectVisitor<ValueObjectParameterCheck>
    {
        public static readonly For Instance = new();

        /// <summary>Creates the check.</summary>
        /// <typeparam name="TSelf">Value object type.</typeparam>
        /// <typeparam name="TValue">Underlying value type.</typeparam>
        /// <returns>The check of the value object.</returns>
        public ValueObjectParameterCheck Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new ValueObjectParameterCheck<TSelf, TValue>();
    }
}

/// <summary>
/// Parses the text of a value-object parameter through <see cref="ValueObjectBindingText.TryParse{TSelf, TValue}"/>.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
internal sealed class ValueObjectParameterCheck<TSelf, TValue> : ValueObjectParameterCheck
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <inheritdoc />
    public override void Check(string text, string member, ValueObjectRefusals refusals)
    {
        if (!ValueObjectBindingText.TryParse<TSelf, TValue>(text, out _, out var validation))
        {
            refusals.Add<TSelf, TValue>(
                member,
                validation.ErrorMessage ?? $"The value is not a valid {TypeNames.Of(typeof(TSelf))}.",
                validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable);
        }
    }
}
