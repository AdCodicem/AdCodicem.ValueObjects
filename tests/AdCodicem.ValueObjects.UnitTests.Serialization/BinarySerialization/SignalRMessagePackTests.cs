using System.Buffers;
using System.Collections.Concurrent;
using AdCodicem.ValueObjects.MessagePack;
using MessagePack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static AdCodicem.ValueObjects.UnitTests.BinarySerialization.MessagePackWire;
using EventId = Microsoft.Extensions.Logging.EventId;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// <see cref="ValueObjectHubProtocolExtensions.UseValueObjects"/>: SignalR's MessagePack hub protocol, which writes a
/// value object as an empty map unless it is wired, read in memory as a server reads an invocation, then between a hub
/// and the .NET client over TestHost.
/// </summary>
public sealed class SignalRMessagePackTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    private const string BindingFailure =
        "Failed to invoke 'Add' due to an error on the server. InvalidDataException: Error binding arguments. Make sure "
        + "that the types of the provided values match the types of the hub method being invoked.";

    /// <summary>
    /// The combination the package ships: MessagePack 3, which lifts the 2.5 the hub protocol asks for, under the hub
    /// protocol of ASP.NET Core 10.
    /// </summary>
    [Fact]
    public void The_hub_protocol_of_ASP_NET_Core_10_runs_on_MessagePack_3()
    {
        typeof(MessagePackSerializer).Assembly.GetName().Version!.Major.Should().Be(3);
        typeof(MessagePackHubProtocolOptions).Assembly.GetName().Version!.Major.Should().Be(10);
    }

    [Fact]
    public void Without_the_helper_a_value_object_travels_as_an_empty_map_and_the_hub_receives_its_default()
    {
        var (wire, parsed) = RoundTrip(new MessagePackHubProtocolOptions(), Iban.Create(ValidIban), Quantity.Create(3));

        wire.Should().Contain("""
                              "Add",[{},{}]
                              """);
        var arguments = parsed.Should().BeOfType<InvocationMessage>().Which.Arguments;
        ((IValueObject<Iban, string>)arguments[0]!).IsDefault.Should().BeTrue();
        ((Quantity)arguments[1]!).Value.Should().Be(0);
    }

    [Fact]
    public void With_the_helper_a_value_object_travels_as_its_value_and_the_hub_receives_it()
    {
        var (wire, parsed) = RoundTrip(new MessagePackHubProtocolOptions().UseValueObjects(), Iban.Create(ValidIban), Quantity.Create(3));

        wire.Should().Contain($$"""
                                "Add",["{{ValidIban}}",3]
                                """);
        parsed.Should().BeOfType<InvocationMessage>().Which.Arguments
            .Should().Equal(Iban.Create(ValidIban), Quantity.Create(3));
    }

    [Fact]
    public void An_argument_the_type_refuses_fails_the_binding_with_its_code()
    {
        var (_, parsed) = RoundTrip(new MessagePackHubProtocolOptions().UseValueObjects(), "FR76", (short)3);

        var failure = parsed.Should().BeOfType<InvocationBindingFailureMessage>().Which.BindingFailure.SourceException;
        failure.Should().BeOfType<InvalidDataException>().Which.Message.Should().StartWith("Error binding arguments.");
        Code(failure).Should().Be(ValueObjectErrorCodes.TooShort);
    }

    [Fact]
    public void A_trusted_protocol_reads_an_argument_the_type_refuses()
    {
        var (_, parsed) = RoundTrip(new MessagePackHubProtocolOptions().UseValueObjects(trusted: true), ValidIban, (short)5000);

        ((Quantity)parsed.Should().BeOfType<InvocationMessage>().Which.Arguments[1]!).Value.Should().Be(5000);
    }

    [Fact]
    public async Task A_hub_and_a_client_both_wired_exchange_value_objects_as_their_values()
    {
        await using var hub = await HubApplication.StartAsync(detailedErrors: false);
        await using var client = hub.Connect(wired: true);
        var shipped = new TaskCompletionSource<Iban>();
        using var subscription = client.On<Iban>("Shipped", iban => shipped.TrySetResult(iban));
        await client.StartAsync(TestContext.Current.CancellationToken);

        (await client.InvokeAsync<string>("Add", Iban.Create(ValidIban), Quantity.Create(3), TestContext.Current.CancellationToken))
            .Should().Be($"{ValidIban} x 3");
        (await client.InvokeAsync<Quantity>("Twice", Quantity.Create(3), TestContext.Current.CancellationToken))
            .Should().Be(Quantity.Create(6));
        (await client.InvokeAsync<string>("Maybe", (Quantity?)null, TestContext.Current.CancellationToken)).Should().Be("none");
        (await client.InvokeAsync<string>("Maybe", (Quantity?)Quantity.Create(4), TestContext.Current.CancellationToken)).Should().Be("4");
        await client.InvokeAsync("Ship", Iban.Create(ValidIban), TestContext.Current.CancellationToken);
        (await shipped.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken))
            .Should().Be(Iban.Create(ValidIban));
    }

    /// <summary>
    /// An argument the value object refuses fails the invocation before the hub method runs: the client is told the
    /// invocation failed, never the rule, and the server logs the binding failure at <see cref="LogLevel.Debug"/> with
    /// an exception whose chain carries the code.
    /// </summary>
    /// <param name="detailedErrors">Whether the hub sends the client the message of the exception.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_argument_the_type_refuses_fails_the_invocation_and_the_server_logs_its_code(bool detailedErrors)
    {
        await using var hub = await HubApplication.StartAsync(detailedErrors);
        await using var client = hub.Connect(wired: true);
        await client.StartAsync(TestContext.Current.CancellationToken);

        var refusal = await FluentActions.Awaiting(() => client.InvokeAsync<string>("Add", "FR76", (short)3, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<HubException>();

        refusal.Which.Message.Should().Be(detailedErrors ? BindingFailure : "Failed to invoke 'Add' due to an error on the server.");
        Code(refusal.Which).Should().BeNull("the rule's code never reaches the client");
        var logged = hub.Logged.Should().ContainSingle(entry => entry.EventName == "InvalidHubParameters").Which;
        logged.Level.Should().Be(LogLevel.Debug);
        logged.Category.Should().Be("Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher");
        Code(logged.Exception!).Should().Be(ValueObjectErrorCodes.TooShort);
    }

    [Fact]
    public async Task A_result_the_type_refuses_fails_the_invocation_on_the_client()
    {
        await using var hub = await HubApplication.StartAsync(detailedErrors: false);
        await using var client = hub.Connect(wired: true);
        await client.StartAsync(TestContext.Current.CancellationToken);

        // The hub doubles 600 past Quantity's maximum through CreateUnchecked, which the client reads strictly.
        var refusal = await FluentActions.Awaiting(() => client.InvokeAsync<Quantity>("Twice", Quantity.Create(600), TestContext.Current.CancellationToken))
            .Should().ThrowAsync<HubException>();

        refusal.Which.Message.Should().StartWith("Error trying to deserialize result to Quantity.");
        Code(refusal.Which).Should().BeNull();
    }

    [Fact]
    public async Task A_client_without_the_helper_is_refused_rather_than_read_as_defaults()
    {
        await using var hub = await HubApplication.StartAsync(detailedErrors: false);
        await using var client = hub.Connect(wired: false);
        await client.StartAsync(TestContext.Current.CancellationToken);

        await FluentActions.Awaiting(() => client.InvokeAsync<string>("Add", Iban.Create(ValidIban), Quantity.Create(3), TestContext.Current.CancellationToken))
            .Should().ThrowAsync<HubException>().WithMessage("Failed to invoke 'Add' due to an error on the server.");

        Code(hub.Logged.Should().ContainSingle(entry => entry.EventName == "InvalidHubParameters").Which.Exception!)
            .Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    [Fact]
    public async Task A_value_object_the_type_rejects_is_refused_before_the_client_sends_it()
    {
        await using var hub = await HubApplication.StartAsync(detailedErrors: false);
        await using var client = hub.Connect(wired: true);
        await client.StartAsync(TestContext.Current.CancellationToken);

#pragma warning disable VO0010 // The uninitialized instance is what the client must refuse to send.
        var refusal = await FluentActions.Awaiting(() => client.InvokeAsync<string>("Add", default(Iban), Quantity.Create(3), TestContext.Current.CancellationToken))
            .Should().ThrowAsync<MessagePackSerializationException>();
#pragma warning restore VO0010

        refusal.Which.InnerException.Should().BeOfType<MessagePackSerializationException>()
            .Which.Message.Should().StartWith("The value to write is not a valid Iban: ");
        Code(refusal.Which).Should().Be(ValueObjectErrorCodes.Required);
        hub.Logged.Should().NotContain(entry => entry.EventName == "InvalidHubParameters", "nothing was sent");
        client.State.Should().Be(HubConnectionState.Connected);
        (await client.InvokeAsync<string>("Add", Iban.Create(ValidIban), Quantity.Create(3), TestContext.Current.CancellationToken))
            .Should().Be($"{ValidIban} x 3");
    }

    /// <summary>
    /// A hub method returning a value object its type rejects fails to write the completion: the server logs it with the
    /// code and closes the connection, which the client reports.
    /// </summary>
    [Fact]
    public async Task A_result_the_type_rejects_closes_the_connection_and_the_server_logs_its_code()
    {
        await using var hub = await HubApplication.StartAsync(detailedErrors: false);
        await using var client = hub.Connect(wired: true);
        var closed = new TaskCompletionSource();
        client.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
        await client.StartAsync(TestContext.Current.CancellationToken);

        await FluentActions.Awaiting(() => client.InvokeAsync<PageNumber>("Blank", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<HubException>()
            .WithMessage("The server closed the connection with the following error: Connection closed with an error.");

        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var logged = hub.Logged.Should().ContainSingle(entry => entry.EventName == "FailedWritingMessage").Which;
        logged.Level.Should().Be(LogLevel.Error);
        logged.Category.Should().Be("Microsoft.AspNetCore.SignalR.HubConnectionContext");
        Code(logged.Exception!).Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// SignalR's options set <see cref="MessagePackSecurity.UntrustedData"/>, under which MessagePack refuses a dictionary
    /// keyed by a value object.
    /// </summary>
    [Fact]
    public async Task A_dictionary_keyed_by_a_value_object_is_refused_under_the_hub_protocol_s_own_security()
    {
        await using var hub = await HubApplication.StartAsync(detailedErrors: false);
        await using var client = hub.Connect(wired: true);
        await client.StartAsync(TestContext.Current.CancellationToken);
        var stock = new Dictionary<Iban, Quantity> { [Iban.Create(ValidIban)] = Quantity.Create(4) };

        await FluentActions.Awaiting(() => client.InvokeAsync<int>("Stock", stock, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<HubException>().WithMessage("Failed to invoke 'Stock' due to an error on the server.");

        var logged = hub.Logged.Should().ContainSingle(entry => entry.EventName == "InvalidHubParameters").Which.Exception!;
        Chain(logged).Should().ContainSingle(exception => exception is TypeAccessException)
            .Which.Message.Should().EndWith(typeof(Iban).FullName);

        static IEnumerable<Exception> Chain(Exception exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                yield return current;
            }
        }
    }

    /// <summary>
    /// Writes an invocation of <c>Add</c> with the hub protocol, as a client does, and parses it back, as a server does.
    /// </summary>
    private static (string Wire, HubMessage Parsed) RoundTrip(MessagePackHubProtocolOptions options, params object?[] arguments)
    {
        var protocol = new MessagePackHubProtocol(Options.Create(options));
        var buffer = new ArrayBufferWriter<byte>();
        protocol.WriteMessage(new InvocationMessage("Add", arguments), buffer);
        var bytes = buffer.WrittenMemory.ToArray();

        // A message is prefixed with its length, one byte for one this short.
        var wire = MessagePackSerializer.ConvertToJson(bytes.AsMemory(1));
        var sequence = new ReadOnlySequence<byte>(bytes);
        protocol.TryParseMessage(ref sequence, new AddBinder(), out var parsed).Should().BeTrue();

        return (wire, parsed!);
    }

    /// <summary>
    /// Tells the protocol the parameters of <c>Add</c>, as a server's dispatcher does.
    /// </summary>
    private sealed class AddBinder : IInvocationBinder
    {
        public IReadOnlyList<Type> GetParameterTypes(string methodName) => [typeof(Iban), typeof(Quantity)];

        public Type GetReturnType(string invocationId) => typeof(object);

        public Type GetStreamItemType(string streamId) => typeof(object);
    }

    /// <summary>
    /// A hub over TestHost, its protocol wired, and what it logs.
    /// </summary>
    private sealed class HubApplication : IAsyncDisposable
    {
        private readonly WebApplication _application;

        private HubApplication(WebApplication application, LogCapture logs)
        {
            _application = application;
            Logs = logs;
        }

        /// <summary>Gets what SignalR logged on the server, Debug included.</summary>
        public IReadOnlyCollection<LogEntry> Logged => Logs.Entries;

        private LogCapture Logs { get; }

        public static async Task<HubApplication> StartAsync(bool detailedErrors)
        {
            var logs = new LogCapture();
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(logs);
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
            builder.Services
                .AddSignalR(options => options.EnableDetailedErrors = detailedErrors)
                .AddMessagePackProtocol(static options => options.UseValueObjects());

            var application = builder.Build();
            application.MapHub<OrderHub>("/hub");
            await application.StartAsync(TestContext.Current.CancellationToken);

            return new HubApplication(application, logs);
        }

        /// <summary>
        /// Builds a .NET client of the hub, over long polling, which TestHost serves.
        /// </summary>
        /// <param name="wired">Whether the client's protocol is wired too.</param>
        /// <returns>The connection, not started.</returns>
        public HubConnection Connect(bool wired)
        {
            var server = _application.GetTestServer();

            return new HubConnectionBuilder()
                .WithUrl(new Uri(server.BaseAddress, "hub"), options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
                .AddMessagePackProtocol(options =>
                {
                    if (wired)
                    {
                        options.UseValueObjects();
                    }
                })
                .Build();
        }

        public async ValueTask DisposeAsync()
        {
            await _application.StopAsync(TestContext.Current.CancellationToken);
            await _application.DisposeAsync();
        }
    }

    /// <summary>One entry SignalR logged.</summary>
    private sealed record LogEntry(string Category, LogLevel Level, string? EventName, Exception? Exception);

    /// <summary>Keeps what SignalR logs on the server.</summary>
    private sealed class LogCapture : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyCollection<LogEntry> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

        public void Dispose()
        {
        }

        private sealed class Logger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => category.StartsWith("Microsoft.AspNetCore.SignalR", StringComparison.Ordinal);

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue(new LogEntry(category, logLevel, eventId.Name, exception));
        }
    }
}

/// <summary>
/// A hub taking and returning value objects.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "SignalR invokes the public instance methods of a hub.")]
public sealed class OrderHub : Hub
{
    public string Add(Iban account, Quantity quantity) => $"{account.Value} x {quantity.Value}";

    public Quantity Twice(Quantity quantity) => Quantity.CreateUnchecked((short)(quantity.Value * 2));

    public string Maybe(Quantity? quantity) => quantity?.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";

    public int Stock(Dictionary<Iban, Quantity> stock) => stock.Count;

    public Task Ship(Iban account) => Clients.Caller.SendAsync("Shipped", account);

#pragma warning disable VO0010 // What a hub returns for a value it never set, which the server must refuse to write.
    public PageNumber Blank() => default;
#pragma warning restore VO0010
}
