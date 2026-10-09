using AdCodicem.ValueObjects.Shared;
using Microsoft.Extensions.AI;

namespace AdCodicem.ValueObjects.AI;

/// <summary>
/// A function that checks the arguments a model sends for the value objects of the function it wraps before that
/// function binds them, and answers a refused one with a result, which <c>FunctionInvokingChatClient</c> hands the model
/// as it is, where it would hide an exception behind "Error: Function failed.".
/// </summary>
/// <remarks>
/// The parameters to check are read once, here, from the method behind the inner function, its schema and its
/// serializer options; every call then reads the arguments of those parameters alone, through the contracts the inner
/// function binds them with. An argument that passes is bound by the inner function, which validates it again.
/// </remarks>
internal sealed class ValueObjectValidatingFunction : DelegatingAIFunction
{
    private readonly ValueObjectArguments? _arguments;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectValidatingFunction"/> class.
    /// </summary>
    /// <param name="innerFunction">The function to wrap.</param>
    public ValueObjectValidatingFunction(AIFunction innerFunction)
        : base(innerFunction)
    {
        _arguments = ValueObjectArguments.For(innerFunction.UnderlyingMethod, innerFunction.JsonSchema, innerFunction.JsonSerializerOptions);
    }

    /// <inheritdoc />
    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => _arguments?.FirstRejection(arguments) is { } rejection
            ? new ValueTask<object?>(rejection.ToJsonElement())
            : base.InvokeCoreAsync(arguments, cancellationToken);
}
