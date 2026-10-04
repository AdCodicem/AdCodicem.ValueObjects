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
    /// <param name="exception">The exception.</param>
    /// <param name="code">The code it must carry.</param>
    public static void ShouldCarry(this Exception exception, string? code)
    {
        code.Should().NotBeNullOrEmpty("a refusal carries a code");
        ValueObjectErrors.TryGetCode(exception, out var read).Should().BeTrue("{0} carries a code", exception.GetType().Name);
        read.Should().Be(code);

        if (exception is not (ValueObjectException or ValueObjectJsonException))
        {
            exception.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(code);
        }
    }

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
