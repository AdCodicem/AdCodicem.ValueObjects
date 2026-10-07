using AdCodicem.ValueObjects.CompatTests;
using AdCodicem.ValueObjects.MessagePack;
using MessagePack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// MessagePack's analyzer comes with the package and fails the build of a [MessagePackObject] type holding a value object
// of the same assembly (MsgPack003): the guide's answer, one attribute per value object.
[assembly: MessagePackAssumedFormattable(typeof(CustomerId))]
[assembly: MessagePackAssumedFormattable(typeof(Iban))]
[assembly: MessagePackAssumedFormattable(typeof(Quantity))]
[assembly: MessagePackAssumedFormattable(typeof(CountryCode))]
[assembly: MessagePackAssumedFormattable(typeof(Reference<PurchaseOrder>))]

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>A consignment of value objects, as an application declares one for MessagePack.</summary>
[MessagePackObject]
public sealed class Consignment
{
    [Key(0)]
    public CustomerId Customer { get; set; }

    [Key(1)]
    public Iban Account { get; set; }

    [Key(2)]
    public Quantity Quantity { get; set; }

    [Key(3)]
    public Quantity? Spare { get; set; }

    [Key(4)]
    public Reference<PurchaseOrder> Order { get; set; }

    [Key(5)]
    public Dictionary<CountryCode, Quantity> Stock { get; set; } = [];
}

/// <summary>The same consignment, of the primitives the value objects replace.</summary>
[MessagePackObject]
public sealed class PrimitiveConsignment
{
    [Key(0)]
    public Guid Customer { get; set; }

    [Key(1)]
    public string Account { get; set; } = string.Empty;

    [Key(2)]
    public int Quantity { get; set; }

    [Key(3)]
    public int? Spare { get; set; }

    [Key(4)]
    public string Order { get; set; } = string.Empty;

    [Key(5)]
    public Dictionary<string, int> Stock { get; set; } = [];
}

/// <summary>A hub taking and returning value objects.</summary>
public sealed class ConsignmentHub : Hub
{
    public string Ship(Iban account, Quantity quantity) => $"{account.Value} x {quantity.Value}";

    public Quantity Next(Quantity quantity) => Quantity.Create(quantity.Value + 1);
}

/// <summary>
/// MessagePack and SignalR's MessagePack hub protocol of the next major with AdCodicem.ValueObjects.MessagePack, the
/// package's floor of MessagePack resolved as an application without central package management resolves it, and
/// MessagePack's analyzer and source generator running in that SDK's compiler.
/// </summary>
public sealed class MessagePackTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard.WithValueObjects();

    /// <summary>
    /// The protocol of the next major runs on the MessagePack 3 the package asks for, which lifts the 2.5 the protocol
    /// asks for.
    /// </summary>
    [Fact]
    public void The_hub_protocol_of_the_next_major_runs_on_MessagePack_3()
    {
        typeof(MessagePackSerializer).Assembly.GetName().Version!.Major.Should().Be(3);
        typeof(MessagePackHubProtocolOptions).Assembly.GetName().Version!.Major.Should().Be(11);
    }

    [Fact]
    public void A_consignment_of_value_objects_is_written_as_the_same_consignment_of_primitives()
    {
        var customer = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        var consignment = new Consignment
        {
            Customer = CustomerId.Create(customer),
            Account = Iban.Create(ValidIban),
            Quantity = Quantity.Create(3),
            Spare = null,
            Order = Reference<PurchaseOrder>.Create("po-1"),
            Stock = new() { [CountryCode.France] = Quantity.Create(7) },
        };
        var primitives = new PrimitiveConsignment
        {
            Customer = customer,
            Account = ValidIban,
            Quantity = 3,
            Spare = null,
            Order = "PO-1",
            Stock = new() { ["FR"] = 7 },
        };

        var written = MessagePackSerializer.Serialize(consignment, Options, TestContext.Current.CancellationToken);

        written.Should().Equal(MessagePackSerializer.Serialize(primitives, MessagePackSerializerOptions.Standard, TestContext.Current.CancellationToken));
        var read = MessagePackSerializer.Deserialize<Consignment>(written, Options, TestContext.Current.CancellationToken);
        read.Customer.Should().Be(consignment.Customer);
        read.Account.Should().Be(consignment.Account);
        read.Quantity.Should().Be(consignment.Quantity);
        read.Spare.Should().BeNull();
        read.Order.Should().Be(consignment.Order);
        read.Stock.Should().Equal(consignment.Stock);
    }

    [Fact]
    public void A_value_the_type_refuses_is_refused_with_its_code_through_MessagePack_s_wrapping()
    {
        var refused = MessagePackSerializer.Serialize(
            new PrimitiveConsignment { Customer = Guid.NewGuid(), Account = ValidIban, Quantity = 500, Order = "PO-1" },
            MessagePackSerializerOptions.Standard,
            TestContext.Current.CancellationToken);

        var thrown = FluentActions.Invoking(() => MessagePackSerializer.Deserialize<Consignment>(refused, Options, TestContext.Current.CancellationToken))
            .Should().Throw<MessagePackSerializationException>().Which;

        ValueObjectErrors.TryGetCode(thrown, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task A_hub_and_a_client_both_wired_exchange_value_objects_and_refuse_one_the_type_rejects()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR().AddMessagePackProtocol(static options => options.UseValueObjects());
        await using var application = builder.Build();
        application.MapHub<ConsignmentHub>("/hub");
        await application.StartAsync(TestContext.Current.CancellationToken);
        var server = application.GetTestServer();

        await using var client = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .AddMessagePackProtocol(static options => options.UseValueObjects())
            .Build();
        await client.StartAsync(TestContext.Current.CancellationToken);

        (await client.InvokeAsync<string>("Ship", Iban.Create(ValidIban), Quantity.Create(3), TestContext.Current.CancellationToken))
            .Should().Be($"{ValidIban} x 3");
        (await client.InvokeAsync<Quantity>("Next", Quantity.Create(3), TestContext.Current.CancellationToken))
            .Should().Be(Quantity.Create(4));
        await FluentActions.Awaiting(() => client.InvokeAsync<string>("Ship", "FR76", 3, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<HubException>().WithMessage("Failed to invoke 'Ship' due to an error on the server.");

        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
