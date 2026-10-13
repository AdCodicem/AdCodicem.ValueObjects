using AdCodicem.ValueObjects.IntegrationTests.Fixtures;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Queue triggers of the Azure Functions isolated worker, in the real host, on Azurite: a message's binding data bound to
/// value objects through their rules, the identifier and the insertion time the host sends as JSON among them, beside the
/// message itself, which its binding and the worker's serializer read through the generated JSON converter, or the
/// value object converter as raw text when the parameter names it. Each function writes what it bound to the
/// <c>results</c> queue; a message it cannot bind goes to the poison queue at its first failure.
/// </summary>
[Collection(nameof(FunctionsHosts))]
public sealed class FunctionsQueueTests(FunctionsHostFixture host)
{
    private const string Guid = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

    /// <summary>
    /// The identifier, the insertion time and the dequeue count of a message, the time sent as a JSON string literal,
    /// bind to value objects beside the message, which the worker's serializer reads; a member the message's value object
    /// refuses fails the message through the generated JSON converter, with its code.
    /// </summary>
    [Fact]
    public async Task A_messages_binding_data_binds_to_value_objects_beside_the_message()
    {
        var orders = host.Queue("orders");
        await orders.SendMessageAsync($$"""{"OrderId":"{{Guid}}","Customer":"ABC-12345","Quantity":7}""", TestContext.Current.CancellationToken);
        await orders.SendMessageAsync($$"""{"OrderId":"{{Guid}}","Customer":"ABC-12345","Quantity":500}""", TestContext.Current.CancellationToken);

        (await host.TakeAsync("results", 1)).Should().Equal(
            $"QueuePoco OrderSubmitted({Guid}, 'ABC-12345', 7) id=set insertionTime=set dequeueCount=1");
        (await host.TakeAsync("orders-poison", 1)).Should().ContainSingle().Which.Should().Contain("\"Quantity\":500");
        (await host.AfterLogsAsync()).Should().Contain("ValueObjectJsonException: The value is not a valid Quantity: The value must be less than or equal to 100.");
    }

    /// <summary>
    /// The host writes a message's number through a double, so a small one reaches the binding data in exponent form,
    /// <c>1E-07</c>, which a decimal's <c>TryParse</c> refuses: a value object over <see cref="decimal"/> reads it as the
    /// worker's JSON conversion did, and its rules then judge the number, one they refuse failing the message with the
    /// rule's code.
    /// </summary>
    [Fact]
    public async Task A_number_the_host_writes_in_exponent_form_binds_a_decimal_value_object()
    {
        var rates = host.Queue("rates");
        await rates.SendMessageAsync("""{"Rate":0.0000001,"Amount":12.5}""", TestContext.Current.CancellationToken);
        await rates.SendMessageAsync("""{"Rate":1,"Amount":1E+21}""", TestContext.Current.CancellationToken);

        (await host.TakeAsync("results", 1)).Should().Equal("QueueRate rate=0.0000001 amount=12.5");
        (await host.TakeAsync("rates-poison", 1)).Should().ContainSingle().Which.Should().Contain("1E+21");
        (await host.AfterLogsAsync()).Should().Contain(
            "System.FormatException: The value is not a valid Amount: The value must be less than or equal to 1000.25. (value_object.out_of_range)");
    }

    /// <summary>
    /// A message that is a bare value object is the binding's to read, as JSON: a JSON string binds, raw text fails, the
    /// value object converter leaving the payload alone.
    /// </summary>
    [Fact]
    public async Task A_bare_value_object_message_is_read_as_JSON_by_its_binding()
    {
        var codes = host.Queue("codes");
        await codes.SendMessageAsync("\"ABC-12345\"", TestContext.Current.CancellationToken);
        await codes.SendMessageAsync("ABC-12345", TestContext.Current.CancellationToken);

        (await host.TakeAsync("results", 1)).Should().Equal("QueueJson CustomerCode('ABC-12345')");
        (await host.TakeAsync("codes-poison", 1)).Should().Equal("ABC-12345");
    }

    /// <summary>
    /// A parameter naming the value object converter with <c>[InputConverter]</c> reads the message as raw text through
    /// the value object's rules, a JSON string once decoded; a refusal fails the message with the code at the end of the
    /// text the worker keeps, which never holds the refused value.
    /// </summary>
    [Fact]
    public async Task A_parameter_naming_the_converter_reads_a_raw_message_through_the_rules()
    {
        var raw = host.Queue("rawcodes");
        await raw.SendMessageAsync("ABC-12345", TestContext.Current.CancellationToken);
        await raw.SendMessageAsync("\"XYZ-678\"", TestContext.Current.CancellationToken);
        await raw.SendMessageAsync("lowercase refusal", TestContext.Current.CancellationToken);

        (await host.TakeAsync("results", 2)).Should().BeEquivalentTo("QueueRaw CustomerCode('ABC-12345')", "QueueRaw CustomerCode('XYZ-678')");
        (await host.TakeAsync("rawcodes-poison", 1)).Should().Equal("lowercase refusal");
        var logs = await host.AfterLogsAsync();
        logs.Should().Contain("System.FormatException: The value is not a valid CustomerCode: The value does not match the expected format. (value_object.invalid_format)");
        logs.Should().NotContain("lowercase refusal");
    }
}
