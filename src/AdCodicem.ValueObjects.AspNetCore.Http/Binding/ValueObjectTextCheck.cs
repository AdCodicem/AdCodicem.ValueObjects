using System.Globalization;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.AspNetCore.Http.Binding;

/// <summary>
/// Parses the text of one value object again, as the binder did, to learn which rule refused it.
/// </summary>
internal abstract class ValueObjectTextCheck
{
    /// <summary>Parses the text, and adds its refusal, if any, to the list.</summary>
    /// <param name="text">The text the binder read, <see langword="null"/> parsed as empty.</param>
    /// <param name="member">The name the refusal is listed under.</param>
    /// <param name="refusals">The refusals of the request.</param>
    public abstract void Check(string? text, string member, ValueObjectRefusals refusals);

    /// <summary>
    /// Creates the check of the value object a descriptor stands for, closed over the type arguments it hands back, at
    /// compile time rather than by reflection.
    /// </summary>
    internal sealed class For : IValueObjectVisitor<ValueObjectTextCheck>
    {
        public static readonly For Instance = new();

        /// <summary>Creates the check.</summary>
        /// <typeparam name="TSelf">Value object type.</typeparam>
        /// <typeparam name="TValue">Underlying value type.</typeparam>
        /// <returns>The check of the value object.</returns>
        public ValueObjectTextCheck Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new ValueObjectTextCheck<TSelf, TValue>();
    }
}

/// <summary>
/// Parses the text of a value object through <c>TSelf.TryParse</c>, the four-argument overload that reports the rule, in
/// the invariant culture the binder parses in.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// A refusal without a reason, which a parser written by hand may return, is reported as MVC's model binder reports it:
/// "The value is not a valid …", with <see cref="ValueObjectErrorCodes.NotParsable"/>.
/// </remarks>
internal sealed class ValueObjectTextCheck<TSelf, TValue> : ValueObjectTextCheck
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <inheritdoc />
    public override void Check(string? text, string member, ValueObjectRefusals refusals)
    {
        if (!TSelf.TryParse(text, CultureInfo.InvariantCulture, out _, out var validation))
        {
            refusals.Add(
                member,
                validation.ErrorMessage ?? $"The value is not a valid {typeof(TSelf).Name}.",
                validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable);
        }
    }
}
