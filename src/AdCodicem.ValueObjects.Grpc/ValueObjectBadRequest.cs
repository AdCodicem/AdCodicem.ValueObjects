using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using RpcStatus = Google.Rpc.Status;

namespace AdCodicem.ValueObjects.Grpc;

/// <summary>
/// Creates the value objects a contract-first request maps its fields to, records each value they refuse as a
/// <c>google.rpc.BadRequest</c> field violation, and answers them all at once as <c>INVALID_ARGUMENT</c>.
/// </summary>
/// <remarks>
/// <para>
/// One per request: <c>var sku = request.Create&lt;Sku, string&gt;(message.Sku, "sku")</c> for each field, then
/// <see cref="ThrowIfAny"/>, which throws the <see cref="RpcException"/> <c>Google.Rpc.Status.ToRpcException()</c> builds
/// when anything was refused. Each violation's <c>field</c> is the path given, an element of a repeated field
/// <c>skus[1]</c>; its <c>reason</c> the code mapped by <see cref="ValueObjectErrorCodes.ToUpperSnakeCase"/>, left empty for
/// a code that maps to no gRPC reason; its <c>description</c> the rule's message, in English, never the value.
/// </para>
/// <para>
/// A value refused leaves the default instance where it was asked for, which no rule checked: read every field before
/// calling <see cref="ThrowIfAny"/>, and use nothing created before it returns.
/// </para>
/// </remarks>
public sealed class ValueObjectBadRequest
{
    private readonly BadRequest _badRequest = new();

    private string? _firstCode;

    /// <summary>
    /// Gets the field violations recorded so far, in the order the fields were read.
    /// </summary>
    public IReadOnlyList<BadRequest.Types.FieldViolation> Violations => _badRequest.FieldViolations;

    /// <summary>
    /// Creates a value object from a field, or records its refusal under the field's path.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type, the field's.</typeparam>
    /// <param name="value">The field's value.</param>
    /// <param name="field">The field's path in the request, <c>sku</c> or <c>order.lines[2].sku</c>.</param>
    /// <returns>The value object, or the default instance when the value is refused.</returns>
    /// <exception cref="ArgumentException"><paramref name="field"/> is <see langword="null"/>, empty or white space.</exception>
    public TSelf Create<TSelf, TValue>(TValue value, string field)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        if (TSelf.TryCreate(value, out var result, out var validation))
        {
            return result;
        }

        // A value object written by hand may fail TryCreate without a rule.
        var code = validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable;
        _firstCode ??= code;
        var violation = new BadRequest.Types.FieldViolation { Field = field, Description = validation.ErrorMessage ?? string.Empty };
        if (GrpcReasons.Of(code) is { } reason)
        {
            violation.Reason = reason;
        }

        _badRequest.FieldViolations.Add(violation);
        return result;
    }

    /// <summary>
    /// Creates a value object from each element of a repeated field, recording each refusal under the element's path.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type, the field's elements'.</typeparam>
    /// <param name="values">The field's elements.</param>
    /// <param name="field">The field's path in the request; each element's is <c>field[index]</c>.</param>
    /// <returns>The value objects, a default instance in place of each one refused.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="field"/> is <see langword="null"/>, empty or white space.</exception>
    public IReadOnlyList<TSelf> CreateAll<TSelf, TValue>(IEnumerable<TValue> values, string field)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        var created = new List<TSelf>();
        foreach (var value in values)
        {
            created.Add(Create<TSelf, TValue>(value, $"{field}[{created.Count}]"));
        }

        return created;
    }

    /// <summary>
    /// Throws, when any value was refused, an <see cref="RpcException"/> answering them all: <c>INVALID_ARGUMENT</c>, whose
    /// trailers carry a <c>google.rpc.Status</c> holding the <c>google.rpc.BadRequest</c>.
    /// </summary>
    /// <param name="message">The status message, in English, for the developer who reads it.</param>
    /// <exception cref="RpcException">A value was refused.</exception>
    public void ThrowIfAny(string message = "The request holds values its value objects refuse.")
    {
        if (_badRequest.FieldViolations.Count == 0)
        {
            return;
        }

        var exception = new RpcStatus
        {
            Code = (int)Code.InvalidArgument,
            Message = message,
            Details = { Any.Pack(_badRequest) },
        }.ToRpcException();

        // Every code is in the BadRequest; the exception carries the first, as a rule's refusal carries its own.
        exception.Data[ValueObjectErrors.ErrorCodeKey] = _firstCode;
        throw exception;
    }
}

/// <summary>
/// Maps a code to the reason gRPC's error model takes, or to nothing for a code that maps to none.
/// </summary>
internal static class GrpcReasons
{
    /// <summary>Maps a code.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The reason, or <see langword="null"/>.</returns>
    public static string? Of(string code)
    {
        try
        {
            return ValueObjectErrorCodes.ToUpperSnakeCase(code);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
