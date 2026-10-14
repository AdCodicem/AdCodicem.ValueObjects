using System.Net;
using AdCodicem.ValueObjects.Grpc;
using AdCodicem.ValueObjects.NativeAot.ContractFirst;
using Google.Rpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// A contract-first gRPC service, whose messages Grpc.Tools generates, over Kestrel's cleartext HTTP/2, and the .NET client:
/// <c>ValueObjectExceptionInterceptor</c> answering a refusal a handler lets escape, in a unary call, after a reply of a
/// server stream and in a client stream, with <c>INVALID_ARGUMENT</c> and the <c>google.rpc.ErrorInfo</c> carrying its
/// code, and <c>ValueObjectBadRequest</c> answering every refused field of a request at once.
/// </summary>
internal static class ContractFirstGrpc
{
    /// <param name="report">Where each scenario is written.</param>
    /// <returns>The scenarios, run.</returns>
    public static async Task RunAsync(Report report)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        builder.Logging.ClearProviders();
        builder.Services.AddGrpc(options => options.Interceptors.Add<ValueObjectExceptionInterceptor>("native.example"));

        await using var app = builder.Build();
        app.MapGrpcService<PhoneBookService>();
        await app.StartAsync();
        try
        {
            using var channel = GrpcChannel.ForAddress(app.Urls.Single());
            var client = new PhoneBook.PhoneBookClient(channel);

            var answer = await client.AddAsync(new Entry { Phone = "+33123456789", Page = 3 });
            report.Line("grpc accepted", $"{answer.Phone} {answer.Total}");

            await Refused(report, "grpc unary Create", async () => await client.AddAsync(new Entry { Phone = "123", Page = 3 }));
            await Refused(report, "grpc server stream Create after a reply", async () =>
            {
                using var call = client.Watch(new Entry { Phone = "123" });
                var replies = new List<string>();
                await foreach (var reply in call.ResponseStream.ReadAllAsync())
                {
                    replies.Add(reply.Phone);
                }
            });
            await Refused(report, "grpc client stream Create", async () =>
            {
                using var call = client.Upload();
                await call.RequestStream.WriteAsync(new Entry { Page = 2 });
                await call.RequestStream.WriteAsync(new Entry { Page = 0 });
                await call.RequestStream.CompleteAsync();
                await call;
            });
            await Refused(report, "grpc bad request", async () => await client.AddMappedAsync(
                new Entry { Phone = "123", Page = 0, Labels = { "fine", new string('x', 201) } }));

            var mapped = await client.AddMappedAsync(new Entry { Phone = "+33123456789", Page = 2, Labels = { "fine" } });
            report.Line("grpc bad request accepted", $"{mapped.Phone} {mapped.Total}");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    /// <summary>Writes the status a call is answered with, its <c>ErrorInfo</c> and its <c>BadRequest</c>.</summary>
    private static async Task Refused(Report report, string subject, Func<Task> call)
    {
        try
        {
            await call();
            report.Fail(subject, "accepted");
        }
        catch (RpcException exception)
        {
            var status = exception.GetRpcStatus();
            var line = $"{exception.StatusCode} \"{exception.Status.Detail}\"";
            if (status?.GetDetail<ErrorInfo>() is { } info)
            {
                line += $" ErrorInfo {info.Reason} {info.Domain} {string.Join(", ", info.Metadata.OrderBy(static entry => entry.Key, StringComparer.Ordinal).Select(static entry => $"{entry.Key}={entry.Value}"))}";
            }

            if (status?.GetDetail<BadRequest>() is { } badRequest)
            {
                line += $" BadRequest {string.Join("; ", badRequest.FieldViolations.Select(static violation => $"{violation.Field} {violation.Reason}"))}";
            }

            if (exception.StatusCode != StatusCode.InvalidArgument || status is null)
            {
                report.Fail(subject, line);
            }
            else
            {
                report.Line(subject, line);
            }
        }
    }

    /// <summary>The service: each handler maps its request to value objects of the domain.</summary>
    private sealed class PhoneBookService : PhoneBook.PhoneBookBase
    {
        public override Task<EntryAnswer> Add(Entry request, ServerCallContext context)
            => Task.FromResult(new EntryAnswer { Phone = PhoneNumber.Create(request.Phone).Value, Total = PageNumber.Create(request.Page).Value });

        public override Task<EntryAnswer> AddMapped(Entry request, ServerCallContext context)
        {
            var fields = new ValueObjectBadRequest();
            var phone = fields.Create<PhoneNumber, string>(request.Phone, "phone");
            var page = fields.Create<PageNumber, int>(request.Page, "page");
            var labels = fields.CreateAll<Label, string>(request.Labels, "labels");
            fields.ThrowIfAny();
            return Task.FromResult(new EntryAnswer { Phone = phone.Value, Total = page.Value + labels.Count });
        }

        public override async Task Watch(Entry request, IServerStreamWriter<EntryAnswer> responseStream, ServerCallContext context)
        {
            await responseStream.WriteAsync(new EntryAnswer { Phone = "first", Total = 1 });
            await responseStream.WriteAsync(new EntryAnswer { Phone = PhoneNumber.Create(request.Phone).Value, Total = 2 });
        }

        public override async Task<EntryAnswer> Upload(IAsyncStreamReader<Entry> requestStream, ServerCallContext context)
        {
            var total = 0;
            await foreach (var entry in requestStream.ReadAllAsync())
            {
                total += PageNumber.Create(entry.Page).Value;
            }

            return new EntryAnswer { Phone = "+33123456789", Total = total };
        }
    }
}
