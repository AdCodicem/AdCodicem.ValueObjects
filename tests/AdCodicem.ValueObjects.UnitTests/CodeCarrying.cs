namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Asserts the code an exception carries, where <see cref="ValueObjectErrors.TryGetCode"/> reads it.
/// </summary>
internal static class CodeCarrying
{
    /// <summary>
    /// Asserts that the exception carries the code, in its data under <see cref="ValueObjectErrors.ErrorCodeKey"/> for
    /// an exception of another ecosystem, and as <see cref="ValueObjectErrors.TryGetCode"/> reads it.
    /// </summary>
    /// <remarks>
    /// A DataAnnotations <see cref="System.ComponentModel.DataAnnotations.ValidationException"/>, which DataAnnotations
    /// raises itself, carries it on its <see cref="ValueObjectValidationResult"/> alone, as an exception wrapping one does.
    /// </remarks>
    /// <param name="exception">The exception.</param>
    /// <param name="code">The code it must carry.</param>
    public static void ShouldCarry(this Exception exception, string? code)
    {
        code.Should().NotBeNullOrEmpty("a refusal carries a code");
        ValueObjectErrors.TryGetCode(exception, out var read).Should().BeTrue("{0} carries a code", exception.GetType().Name);
        read.Should().Be(code);

        if (exception is not (ValueObjectException or ValueObjectJsonException) && !Annotated(exception))
        {
            exception.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(code);
        }
    }

    /// <summary>Tells whether an exception is, or wraps, a DataAnnotations refusal of a value object.</summary>
    private static bool Annotated(Exception? exception)
        => exception is System.ComponentModel.DataAnnotations.ValidationException { ValidationResult: ValueObjectValidationResult }
           || (exception?.InnerException is { } inner && Annotated(inner));

    /// <summary>Gets the code the value object rejects its default value with.</summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <returns>The code.</returns>
    public static string? CodeOfDefault<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
#pragma warning disable VO0010 // The uninitialized instance is what the writers refuse.
        var value = default(TSelf).Value;
#pragma warning restore VO0010

        return TSelf.Validate(in value).ErrorCode;
    }
}
