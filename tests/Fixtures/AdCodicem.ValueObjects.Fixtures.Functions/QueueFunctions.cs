using AdCodicem.ValueObjects.AzureFunctions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;

namespace AdCodicem.ValueObjects.Fixtures.Functions;

/// <summary>
/// Queue triggers: value objects bound from a message's binding data, the identifier and the insertion time the host
/// sends as JSON and the numbers it writes through a double among them, beside the message itself, which its binding and the worker's serializer read, or which the
/// value object converter reads as raw text when the parameter names it.
/// </summary>
public static class QueueFunctions
{
    [Function("QueuePoco")]
    [QueueOutput("results")]
    public static string QueuePoco([QueueTrigger("orders")] OrderSubmitted message, OrderId id, Timestamp? insertionTime, Quantity dequeueCount)
        => $"QueuePoco OrderSubmitted({message.OrderId.Value}, '{message.Customer.Value}', {message.Quantity.Value}) id={(id.Value == Guid.Empty ? "empty" : "set")} insertionTime={(insertionTime is null ? "null" : "set")} dequeueCount={dequeueCount.Value}";

    [Function("QueueRate")]
    [QueueOutput("results")]
    public static string QueueRate([QueueTrigger("rates")] Priced message, Rate rate, Amount amount)
        => FormattableString.Invariant($"QueueRate rate={rate.Value} amount={amount.Value}");

    [Function("QueueJson")]
    [QueueOutput("results")]
    public static string QueueJson([QueueTrigger("codes")] CustomerCode code) => $"QueueJson CustomerCode('{code.Value}')";

    [Function("QueueRaw")]
    [QueueOutput("results")]
    public static string QueueRaw([QueueTrigger("rawcodes")][InputConverter(typeof(ValueObjectInputConverter))] CustomerCode code)
        => $"QueueRaw CustomerCode('{code.Value}')";
}
