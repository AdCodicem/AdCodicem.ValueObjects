using AdCodicem.ValueObjects.CompatTests.ContractFirst;
using AdCodicem.ValueObjects.Grpc;
using Google.Rpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// AdCodicem.ValueObjects.Grpc on the next major: a contract-first service of Grpc.AspNetCore over TestHost 11, whose
/// handlers map its fields to value objects, a refusal answered through the interceptor and through the collector.
/// </summary>
public sealed class GrpcTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    [Fact]
    public async Task A_contract_first_service_answers_refused_values_with_invalid_argument_and_their_codes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddGrpc(options => options.Interceptors.Add<ValueObjectExceptionInterceptor>("compat.example.com"));
        await using var application = builder.Build();
        application.MapGrpcService<DispatchService>();
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = application.GetTestServer().CreateHandler() });
        var client = new Dispatch.DispatchClient(channel);
        var cancellation = TestContext.Current.CancellationToken;

        (await client.SendAsync(new Consignee { Account = ValidIban, Quantity = 3 }, cancellationToken: cancellation)).Quantity.Should().Be(3);

        var escaped = (await FluentActions.Awaiting(async () => await client.SendAsync(new Consignee { Account = "FR76", Quantity = 3 }, cancellationToken: cancellation))
            .Should().ThrowAsync<RpcException>()).Which;
        escaped.StatusCode.Should().Be(StatusCode.InvalidArgument);
        var info = escaped.GetRpcStatus()!.GetDetail<ErrorInfo>()!;
        info.Reason.Should().Be("VALUE_OBJECT_TOO_SHORT");
        info.Domain.Should().Be("compat.example.com");
        info.Metadata["valueObject"].Should().Be("Iban");

        var collected = (await FluentActions.Awaiting(async () => await client.SendMappedAsync(new Consignee { Account = "FR76", Quantity = 500 }, cancellationToken: cancellation))
            .Should().ThrowAsync<RpcException>()).Which;
        collected.StatusCode.Should().Be(StatusCode.InvalidArgument);
        collected.GetRpcStatus()!.GetDetail<BadRequest>()!.FieldViolations.Select(violation => (violation.Field, violation.Reason))
            .Should().Equal(("account", "VALUE_OBJECT_TOO_SHORT"), ("quantity", "VALUE_OBJECT_OUT_OF_RANGE"));
    }

    /// <summary>The service, whose handlers map a consignee to value objects.</summary>
    private sealed class DispatchService : Dispatch.DispatchBase
    {
        public override Task<Receipt> Send(Consignee request, ServerCallContext context)
            => Task.FromResult(new Receipt { Account = Iban.Create(request.Account).Value, Quantity = Quantity.Create(request.Quantity).Value });

        public override Task<Receipt> SendMapped(Consignee request, ServerCallContext context)
        {
            var fields = new ValueObjectBadRequest();
            var account = fields.Create<Iban, string>(request.Account, "account");
            var quantity = fields.Create<Quantity, int>(request.Quantity, "quantity");
            fields.ThrowIfAny();
            return Task.FromResult(new Receipt { Account = account.Value, Quantity = quantity.Value });
        }
    }
}
