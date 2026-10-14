using AdCodicem.ValueObjects.Grpc;
using AdCodicem.ValueObjects.UnitTests.BinarySerialization.ContractFirst;
using Google.Rpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// <see cref="ValueObjectExceptionInterceptor"/> and <see cref="ValueObjectBadRequest"/> in a contract-first service,
/// whose messages Grpc.Tools generates, between Grpc.AspNetCore and its .NET client over TestHost, and the interceptor in a
/// protobuf-net.Grpc code-first service.
/// </summary>
public sealed class ContractFirstGrpcTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    /// <summary>
    /// Without the interceptor, a <see cref="ValueObjectException"/> a handler lets escape is answered <c>UNKNOWN</c>, in
    /// each kind of call, and the rule is lost.
    /// </summary>
    [Fact]
    public async Task Without_the_interceptor_a_refusal_in_a_handler_is_answered_unknown()
    {
        await using var server = await ContractFirstServer.StartAsync(_ => { });

        foreach (var refused in await server.RefuseEveryKindAsync())
        {
            refused.StatusCode.Should().Be(StatusCode.Unknown);
            refused.Status.Detail.Should().Be("Exception was thrown by handler.");
            refused.GetRpcStatus().Should().BeNull();
        }
    }

    /// <summary>
    /// With the interceptor, the same refusal is answered <c>INVALID_ARGUMENT</c> in each kind of call, a server stream
    /// after the reply it sent, whose <c>google.rpc.ErrorInfo</c> carries the code, under the domain the registration names.
    /// </summary>
    /// <param name="domain">The domain the registration names, or <see langword="null"/> for none.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("orders.example.com")]
    public async Task With_the_interceptor_a_refusal_in_a_handler_is_answered_invalid_argument_with_its_code(string? domain)
    {
        await using var server = await ContractFirstServer.StartAsync(options =>
        {
            if (domain is null)
            {
                options.Interceptors.Add<ValueObjectExceptionInterceptor>();
            }
            else
            {
                options.Interceptors.Add<ValueObjectExceptionInterceptor>(domain);
            }
        });

        var refusals = await server.RefuseEveryKindAsync();
        refusals.Should().HaveCount(4);
        foreach (var refused in refusals)
        {
            refused.StatusCode.Should().Be(StatusCode.InvalidArgument);
            var info = refused.GetRpcStatus()!.GetDetail<ErrorInfo>()!;
            info.Domain.Should().Be(domain ?? ValueObjectExceptionInterceptor.DefaultDomain);
            info.Metadata["code"].Should().BeOneOf(ValueObjectErrorCodes.TooShort, ValueObjectErrorCodes.OutOfRange);
        }

        refusals[0].Status.Detail.Should().Be("'Iban' rejected the supplied value: The value must be at least 15 characters long.");
        var unary = refusals[0].GetRpcStatus()!.GetDetail<ErrorInfo>()!;
        unary.Reason.Should().Be("VALUE_OBJECT_TOO_SHORT");
        unary.Metadata.Should().BeEquivalentTo(new Dictionary<string, string> { ["code"] = ValueObjectErrorCodes.TooShort, ["valueObject"] = "Iban" });
        server.Watched.Should().ContainSingle("the server stream sent its reply before the refusal ended it");
    }

    /// <summary>
    /// <c>Interceptors.Add&lt;T&gt;()</c> resolves the interceptor through the services: a singleton the application
    /// registers, with a domain of its own, is the one used.
    /// </summary>
    [Fact]
    public async Task An_interceptor_registered_as_a_singleton_is_the_one_used()
    {
        await using var server = await ContractFirstServer.StartAsync(
            options => options.Interceptors.Add<ValueObjectExceptionInterceptor>(),
            services => services.AddSingleton(new ValueObjectExceptionInterceptor("singleton.example.com")));

        var refused = (await FluentActions.Awaiting(async () => await server.Client.AddAsync(new OrderLine { Account = "FR76", Page = 1 }, cancellationToken: TestContext.Current.CancellationToken))
            .Should().ThrowAsync<RpcException>()).Which;

        refused.GetRpcStatus()!.GetDetail<ErrorInfo>()!.Domain.Should().Be("singleton.example.com");
    }

    [Fact]
    public async Task An_accepted_request_goes_through_the_interceptor_untouched()
    {
        await using var server = await ContractFirstServer.StartAsync(options => options.Interceptors.Add<ValueObjectExceptionInterceptor>());

        var answer = await server.Client.AddAsync(new OrderLine { Account = ValidIban, Page = 3 }, cancellationToken: TestContext.Current.CancellationToken);

        answer.Account.Should().Be(ValidIban);
        answer.Total.Should().Be(3);
    }

    /// <summary>
    /// <see cref="ValueObjectBadRequest"/> answers every refused field of a request at once, a <c>google.rpc.BadRequest</c>
    /// naming each, and passes a request whose fields are all accepted; the interceptor leaves its exception as it is.
    /// </summary>
    [Fact]
    public async Task The_collector_answers_every_refused_field_at_once()
    {
        await using var server = await ContractFirstServer.StartAsync(options => options.Interceptors.Add<ValueObjectExceptionInterceptor>());

        var refused = (await FluentActions.Awaiting(async () => await server.Client.AddMappedAsync(
                new OrderLine { Account = "FR76", Page = 0, Accounts = { ValidIban, "FR76" + new string('1', 31) } },
                cancellationToken: TestContext.Current.CancellationToken))
            .Should().ThrowAsync<RpcException>()).Which;

        refused.StatusCode.Should().Be(StatusCode.InvalidArgument);
        refused.Status.Detail.Should().Be("The request holds values its value objects refuse.");
        var status = refused.GetRpcStatus()!;
        status.GetDetail<ErrorInfo>().Should().BeNull();
        status.GetDetail<BadRequest>()!.FieldViolations.Select(violation => (violation.Field, violation.Reason, violation.Description))
            .Should().Equal(
                ("account", "VALUE_OBJECT_TOO_SHORT", "The value must be at least 15 characters long."),
                ("page", "VALUE_OBJECT_OUT_OF_RANGE", "The value must be greater than or equal to 1."),
                ("accounts[1]", "VALUE_OBJECT_TOO_LONG", "The value must be at most 34 characters long."));

        var answer = await server.Client.AddMappedAsync(new OrderLine { Account = ValidIban, Page = 2, Accounts = { ValidIban } }, cancellationToken: TestContext.Current.CancellationToken);
        answer.Total.Should().Be(3);
    }

    /// <summary>
    /// The interceptor serves a protobuf-net.Grpc code-first service too, whose handler calls <c>Create</c>.
    /// </summary>
    [Fact]
    public async Task The_interceptor_answers_a_refusal_in_a_code_first_handler()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddCodeFirstGrpc(options => options.Interceptors.Add<ValueObjectExceptionInterceptor>("checks.example.com"));
        await using var application = builder.Build();
        application.MapGrpcService<OrderChecks>();
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = application.GetTestServer().CreateHandler() });
        var client = channel.CreateGrpcService<IOrderChecks>(ClientFactory.Create(BinderConfiguration.Create([ProtoBufMarshallerFactory.Create(RuntimeTypeModel.Create())])));

        (await client.CheckAsync(new PlainOrderLineRequest { Account = ValidIban, Quantity = 3 })).Account.Should().Be(ValidIban);
        var refused = (await FluentActions.Awaiting(async () => await client.CheckAsync(new PlainOrderLineRequest { Account = "FR76", Quantity = 3 }))
            .Should().ThrowAsync<RpcException>()).Which;

        refused.StatusCode.Should().Be(StatusCode.InvalidArgument);
        var info = refused.GetRpcStatus()!.GetDetail<ErrorInfo>()!;
        info.Domain.Should().Be("checks.example.com");
        info.Metadata["code"].Should().Be(ValueObjectErrorCodes.TooShort);
    }

    /// <summary>The contract-first service the tests serve.</summary>
    private sealed class Desk : OrderDesk.OrderDeskBase
    {
        public override Task<OrderLineAnswer> Add(OrderLine request, ServerCallContext context)
        {
            var account = Iban.Create(request.Account);
            var page = PageNumber.Create(request.Page);
            return Task.FromResult(new OrderLineAnswer { Account = account.Value, Total = page.Value });
        }

        public override Task<OrderLineAnswer> AddMapped(OrderLine request, ServerCallContext context)
        {
            var fields = new ValueObjectBadRequest();
            var account = fields.Create<Iban, string>(request.Account, "account");
            var page = fields.Create<PageNumber, int>(request.Page, "page");
            var accounts = fields.CreateAll<Iban, string>(request.Accounts, "accounts");
            fields.ThrowIfAny();
            return Task.FromResult(new OrderLineAnswer { Account = account.Value, Total = page.Value + accounts.Count });
        }

        public override async Task Watch(OrderLine request, IServerStreamWriter<OrderLineAnswer> responseStream, ServerCallContext context)
        {
            await responseStream.WriteAsync(new OrderLineAnswer { Account = "first", Total = 1 }, context.CancellationToken);
            await responseStream.WriteAsync(new OrderLineAnswer { Account = Iban.Create(request.Account).Value, Total = 2 }, context.CancellationToken);
        }

        public override async Task<OrderLineAnswer> Upload(IAsyncStreamReader<OrderLine> requestStream, ServerCallContext context)
        {
            var total = 0;
            await foreach (var line in requestStream.ReadAllAsync(context.CancellationToken))
            {
                total += PageNumber.Create(line.Page).Value;
            }

            return new OrderLineAnswer { Account = ValidIban, Total = total };
        }

        public override async Task Chat(IAsyncStreamReader<OrderLine> requestStream, IServerStreamWriter<OrderLineAnswer> responseStream, ServerCallContext context)
        {
            await foreach (var line in requestStream.ReadAllAsync(context.CancellationToken))
            {
                await responseStream.WriteAsync(new OrderLineAnswer { Account = Iban.Create(line.Account).Value, Total = 1 }, context.CancellationToken);
            }
        }
    }

    /// <summary>A contract-first server over TestHost, and its client.</summary>
    private sealed class ContractFirstServer : IAsyncDisposable
    {
        private readonly WebApplication _application;

        private readonly GrpcChannel _channel;

        private ContractFirstServer(WebApplication application)
        {
            _application = application;
            _channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = application.GetTestServer().CreateHandler() });
            Client = new OrderDesk.OrderDeskClient(_channel);
        }

        public OrderDesk.OrderDeskClient Client { get; }

        /// <summary>Gets the replies the client read from the server stream before its end.</summary>
        public List<string> Watched { get; } = [];

        public static async Task<ContractFirstServer> StartAsync(Action<global::Grpc.AspNetCore.Server.GrpcServiceOptions> configure, Action<IServiceCollection>? services = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            services?.Invoke(builder.Services);
            builder.Services.AddGrpc(configure);
            var application = builder.Build();
            application.MapGrpcService<Desk>();
            await application.StartAsync(TestContext.Current.CancellationToken);
            return new ContractFirstServer(application);
        }

        /// <summary>Refuses a value in a unary call, a server stream, a client stream and a duplex stream, in that order.</summary>
        public async Task<IReadOnlyList<RpcException>> RefuseEveryKindAsync()
        {
            var cancellation = TestContext.Current.CancellationToken;
            var refusals = new List<RpcException>();

            refusals.Add((await FluentActions.Awaiting(async () => await Client.AddAsync(new OrderLine { Account = "FR76", Page = 1 }, cancellationToken: cancellation))
                .Should().ThrowAsync<RpcException>()).Which);

            refusals.Add((await FluentActions.Awaiting(async () =>
                {
                    using var call = Client.Watch(new OrderLine { Account = "FR76" }, cancellationToken: cancellation);
                    await foreach (var answer in call.ResponseStream.ReadAllAsync(cancellation))
                    {
                        Watched.Add(answer.Account);
                    }
                })
                .Should().ThrowAsync<RpcException>()).Which);

            refusals.Add((await FluentActions.Awaiting(async () =>
                {
                    using var call = Client.Upload(cancellationToken: cancellation);
                    await call.RequestStream.WriteAsync(new OrderLine { Page = 3 }, cancellation);
                    await call.RequestStream.WriteAsync(new OrderLine { Page = -1 }, cancellation);
                    await call.RequestStream.CompleteAsync();
                    await call;
                })
                .Should().ThrowAsync<RpcException>()).Which);

            refusals.Add((await FluentActions.Awaiting(async () =>
                {
                    using var call = Client.Chat(cancellationToken: cancellation);
                    await call.RequestStream.WriteAsync(new OrderLine { Account = "FR76" }, cancellation);
                    await call.RequestStream.CompleteAsync();
                    await foreach (var answer in call.ResponseStream.ReadAllAsync(cancellation))
                    {
                        Watched.Add(answer.Account);
                    }
                })
                .Should().ThrowAsync<RpcException>()).Which);

            return refusals;
        }

        public async ValueTask DisposeAsync()
        {
            _channel.Dispose();
            await _application.DisposeAsync();
        }
    }
}

/// <summary>A code-first service whose handler maps a request over the primitives with <c>Create</c>.</summary>
[Service("orders.Checks")]
public interface IOrderChecks
{
    ValueTask<PlainOrderLineReply> CheckAsync(PlainOrderLineRequest request, CallContext context = default);
}

/// <summary>The code-first service the interceptor answers for.</summary>
public sealed class OrderChecks : IOrderChecks
{
    public ValueTask<PlainOrderLineReply> CheckAsync(PlainOrderLineRequest request, CallContext context = default)
        => new(new PlainOrderLineReply { Account = Iban.Create(request.Account ?? string.Empty).Value, Total = request.Quantity });
}
