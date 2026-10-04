namespace AdCodicem.ValueObjects;

/// <summary>
/// Carries the code of a violated rule across an exception that a serializer, a data access library or any other
/// integration throws in its own terms.
/// </summary>
/// <remarks>
/// <para>
/// An integration on a boundary reports a rejected value with the exception its ecosystem expects: a
/// <see cref="System.Text.Json.JsonException"/>, a <c>JsonSerializationException</c> for Newtonsoft.Json, a
/// <c>DataException</c> for Dapper. Those types have no place for a code, so the integrations store it in
/// <see cref="Exception.Data"/>, under <see cref="ErrorCodeKey"/>, and the System.Text.Json converters throw a
/// <see cref="ValueObjectJsonException"/>, which carries it as a property as well. <see cref="TryGetCode"/> reads
/// either, and a <see cref="ValueObjectException"/>, from the exception or from one it wraps.
/// </para>
/// <para>
/// An exception carries the code and the value object type, never the value it refused.
/// </para>
/// </remarks>
public static class ValueObjectErrors
{
    /// <summary>
    /// The key under which an integration stores the code of the violated rule in <see cref="Exception.Data"/>.
    /// </summary>
    /// <remarks>
    /// An integration of your own stores a code there the same way, so that <see cref="TryGetCode"/>, and anything
    /// that writes out the data of an exception, finds it: <c>exception.Data[ValueObjectErrors.ErrorCodeKey] = code</c>.
    /// </remarks>
    public const string ErrorCodeKey = "AdCodicem.ValueObjects.ErrorCode";

    /// <summary>
    /// Reads the code of the rule an exception, or one of the exceptions it wraps, carries.
    /// </summary>
    /// <param name="exception">The exception, as it was caught.</param>
    /// <param name="code">The code, or <see langword="null"/> when no exception of the chain carries one.</param>
    /// <returns><see langword="true"/> when a code was found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Each exception is read in turn, from <paramref name="exception"/> down its
    /// <see cref="Exception.InnerException"/> chain, since a framework may wrap the exception that carries the code: a
    /// minimal API answers a body it cannot read with a <c>BadHttpRequestException</c> around the
    /// <see cref="System.Text.Json.JsonException"/>, and Entity Framework Core fails <c>SaveChanges</c> with a
    /// <c>DbUpdateException</c> around the <see cref="ValueObjectException"/>. The first code found wins, read from
    /// <see cref="ValueObjectException.ErrorCode"/>, then <see cref="ValueObjectJsonException.ErrorCode"/>, then
    /// <see cref="Exception.Data"/> under <see cref="ErrorCodeKey"/>.
    /// </para>
    /// <para>
    /// An exception no value object raised carries none, and a code of your own travels as the framework's do.
    /// </para>
    /// </remarks>
    public static bool TryGetCode(Exception exception, [NotNullWhen(true)] out string? code)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (var current = exception; current is not null; current = current.InnerException)
        {
            code = current switch
            {
                ValueObjectException { ErrorCode: { Length: > 0 } fromValueObject } => fromValueObject,
                ValueObjectJsonException json => json.ErrorCode,
                _ => current.Data[ErrorCodeKey] as string,
            };

            if (!string.IsNullOrEmpty(code))
            {
                return true;
            }
        }

        code = null;
        return false;
    }
}
