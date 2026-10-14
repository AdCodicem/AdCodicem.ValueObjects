using AdCodicem.ValueObjects.Grpc;
using Google.Rpc;
using Grpc.Core;
using GrpcStatus = Grpc.Core.Status;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// <see cref="ValueObjectExceptionInterceptor"/>'s four server handlers called directly, as Grpc.AspNetCore calls them,
/// with continuations that return or throw.
/// </summary>
public sealed class ValueObjectExceptionInterceptorTests
{
    private static readonly ValueObjectExceptionInterceptor Interceptor = new("orders.example.com");

    [Fact]
    public async Task A_continuation_that_returns_is_answered_as_it_returns()
    {
        (await Interceptor.UnaryServerHandler<string, string>("request", null!, (request, _) => Task.FromResult(request + "!"))).Should().Be("request!");
        (await Interceptor.ClientStreamingServerHandler<string, string>(null!, null!, (_, _) => Task.FromResult("done"))).Should().Be("done");
        var streamed = false;
        await Interceptor.ServerStreamingServerHandler<string, string>("request", null!, null!, (_, _, _) => { streamed = true; return Task.CompletedTask; });
        var duplexed = false;
        await Interceptor.DuplexStreamingServerHandler<string, string>(null!, null!, null!, (_, _, _) => { duplexed = true; return Task.CompletedTask; });
        streamed.Should().BeTrue();
        duplexed.Should().BeTrue();
    }

    /// <summary>
    /// A <see cref="ValueObjectException"/> carrying a code is answered <c>INVALID_ARGUMENT</c> by each handler, its
    /// message the exception's, its <c>ErrorInfo</c> the code, the domain and the value object, and the code in the
    /// <see cref="RpcException"/>'s data.
    /// </summary>
    [Fact]
    public async Task A_refusal_is_answered_invalid_argument_by_each_handler()
    {
        var refusal = Refusal();

        foreach (var thrown in await EveryHandlerAsync(refusal))
        {
            var exception = thrown.Should().BeOfType<RpcException>().Which;
            exception.StatusCode.Should().Be(StatusCode.InvalidArgument);
            exception.Status.Detail.Should().Be(refusal.Message);
            var status = exception.GetRpcStatus()!;
            status.Message.Should().Be(refusal.Message);
            var info = status.GetDetail<ErrorInfo>()!;
            info.Reason.Should().Be("VALUE_OBJECT_OUT_OF_RANGE");
            info.Domain.Should().Be("orders.example.com");
            info.Metadata.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                ["code"] = ValueObjectErrorCodes.OutOfRange,
                ["valueObject"] = "Reference<PurchaseOrder>",
            });
            exception.ShouldCarry(ValueObjectErrorCodes.OutOfRange);
        }
    }

    [Fact]
    public async Task A_refusal_naming_no_value_object_is_answered_without_it()
    {
        var thrown = (await EveryHandlerAsync(new ValueObjectException("A value was refused.", null, ValueObjectErrorCodes.Required, null)))[0];

        thrown.Should().BeOfType<RpcException>().Which.GetRpcStatus()!.GetDetail<ErrorInfo>()!.Metadata
            .Should().ContainKey("code").And.NotContainKey("valueObject");
    }

    /// <summary>
    /// A code of the application's own that maps to no gRPC reason is answered <c>INVALID_ARGUMENT</c> all the same,
    /// without the <c>ErrorInfo</c>.
    /// </summary>
    [Fact]
    public async Task A_code_that_maps_to_no_reason_is_answered_without_details()
    {
        foreach (var thrown in await EveryHandlerAsync(new ValueObjectException("Odd.", typeof(ProtoOddCode), ProtoOddCode.Code, null)))
        {
            var exception = thrown.Should().BeOfType<RpcException>().Which;
            exception.StatusCode.Should().Be(StatusCode.InvalidArgument);
            exception.GetRpcStatus()!.Details.Should().BeEmpty();
            exception.ShouldCarry(ProtoOddCode.Code);
        }
    }

    /// <summary>
    /// What reports no broken rule goes through as it is: a <see cref="ValueObjectException"/> without a code, an
    /// <see cref="RpcException"/> the handler throws, any other exception.
    /// </summary>
    [Fact]
    public async Task Any_other_exception_goes_through_as_it_is()
    {
        Exception[] others =
        [
            new ValueObjectException("No rule."),
            new ValueObjectException("Empty rule.", typeof(Iban), string.Empty, null),
            new RpcException(new GrpcStatus(StatusCode.FailedPrecondition, "Not now.")),
            new InvalidOperationException("Broken."),
        ];

        foreach (var other in others)
        {
            foreach (var thrown in await EveryHandlerAsync(other))
            {
                thrown.Should().BeSameAs(other);
            }
        }
    }

    [Fact]
    public async Task The_domain_defaults_to_the_package_s_and_a_blank_one_is_refused()
    {
        var thrown = (await EveryHandlerAsync(Refusal(), new ValueObjectExceptionInterceptor()))[0];

        thrown.Should().BeOfType<RpcException>().Which.GetRpcStatus()!.GetDetail<ErrorInfo>()!.Domain
            .Should().Be(ValueObjectExceptionInterceptor.DefaultDomain).And.Be("adcodicem.valueobjects");
        FluentActions.Invoking(() => new ValueObjectExceptionInterceptor(" ")).Should().Throw<ArgumentException>().WithParameterName("domain");
    }

    [Fact]
    public async Task A_null_continuation_is_refused()
    {
        await FluentActions.Awaiting(() => Interceptor.UnaryServerHandler<string, string>("request", null!, null!))
            .Should().ThrowAsync<ArgumentNullException>().WithParameterName("continuation");
        await FluentActions.Awaiting(() => Interceptor.ClientStreamingServerHandler<string, string>(null!, null!, null!))
            .Should().ThrowAsync<ArgumentNullException>().WithParameterName("continuation");
        await FluentActions.Awaiting(() => Interceptor.ServerStreamingServerHandler<string, string>("request", null!, null!, null!))
            .Should().ThrowAsync<ArgumentNullException>().WithParameterName("continuation");
        await FluentActions.Awaiting(() => Interceptor.DuplexStreamingServerHandler<string, string>(null!, null!, null!, null!))
            .Should().ThrowAsync<ArgumentNullException>().WithParameterName("continuation");
    }

    private static ValueObjectException Refusal()
        => new("'Reference' rejected the supplied value: The value must be at most 12 characters long.", typeof(Reference<PurchaseOrder>), ValueObjectErrorCodes.OutOfRange, null);

    /// <summary>Runs each handler over a continuation that throws, and returns what each let out.</summary>
    private static async Task<IReadOnlyList<Exception>> EveryHandlerAsync(Exception exception, ValueObjectExceptionInterceptor? interceptor = null)
    {
        interceptor ??= Interceptor;
        return
        [
            await Caught(() => interceptor.UnaryServerHandler<string, string>("request", null!, (_, _) => throw exception)),
            await Caught(() => interceptor.ClientStreamingServerHandler<string, string>(null!, null!, (_, _) => throw exception)),
            await Caught(() => interceptor.ServerStreamingServerHandler<string, string>("request", null!, null!, (_, _, _) => throw exception)),
            await Caught(() => interceptor.DuplexStreamingServerHandler<string, string>(null!, null!, null!, (_, _, _) => throw exception)),
        ];

        static async Task<Exception> Caught(Func<Task> call)
        {
            try
            {
                await call();
            }
            catch (Exception caught)
            {
                return caught;
            }

            throw new InvalidOperationException("The handler threw nothing.");
        }
    }
}
