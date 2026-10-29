---
title: Answer refused values in gRPC services
sidebar_label: gRPC services
slug: /how-to/grpc
description: Collect every field a contract-first request maps to a value object, answer the refused ones at once as INVALID_ARGUMENT carrying a google.rpc.BadRequest, and answer a ValueObjectException that escapes any service method with INVALID_ARGUMENT and a google.rpc.ErrorInfo carrying its rule code, rather than UNKNOWN.
---

# Answer refused values in gRPC services

```bash
dotnet add package AdCodicem.ValueObjects.Grpc
```

```csharp skip
builder.Services.AddGrpc(options => options.Interceptors.Add<ValueObjectExceptionInterceptor>());

public override Task<AddReply> Add(AddRequest request, ServerCallContext context)
{
    var fields = new ValueObjectBadRequest();
    var sku = fields.Create<Sku, string>(request.Sku, "sku");
    var quantity = fields.Create<Quantity, int>(request.Quantity, "quantity");
    var skus = fields.CreateAll<Sku, string>(request.Skus, "skus");
    fields.ThrowIfAny();
    // ...
}
```

A contract-first service, whose messages Grpc.Tools generates from a `.proto`, never carries a value object in a message:
its fields are `string` and `int32`, and its handlers map them to value objects. When the mapping calls `Create` on a
value a value object refuses, Grpc.AspNetCore answers the `ValueObjectException` that escapes as it answers any exception
of a handler: `UNKNOWN`, "Exception was thrown by handler.", and the rule is lost. The package gives the handler two
ways to answer `INVALID_ARGUMENT` with the rule's code instead, in gRPC's rich error model, which a client in any
language reads. It depends on Grpc.StatusProto alone, and serves a [protobuf-net.Grpc code-first
service](protobuf-net.md#code-first-grpc) as well.

## Every refused field at once

`ValueObjectBadRequest` creates the value object each field maps to, through `TryCreate`, and records each value refused,
so that one answer names every field the client got wrong:

```csharp skip
var fields = new ValueObjectBadRequest();
var sku = fields.Create<Sku, string>(request.Sku, "sku");
var quantity = fields.Create<Quantity, int>(request.Quantity, "quantity");
var skus = fields.CreateAll<Sku, string>(request.Skus, "skus");   // skus[0], skus[1], …
fields.ThrowIfAny();
```

`ThrowIfAny` throws nothing when every value was accepted. Otherwise it throws the `RpcException` Grpc.StatusProto's
`ToRpcException()` builds: `INVALID_ARGUMENT`, "The request holds values its value objects refuse." unless another
message is given, whose trailers carry a `google.rpc.Status` holding a `google.rpc.BadRequest`, one field violation per
value refused, in the order the fields were read:

| Field | Value |
| --- | --- |
| `field` | the path given, `sku`, `order.lines[2].sku`; an element of a repeated field `skus[1]` |
| `reason` | the code in upper snake case, `VALUE_OBJECT_TOO_LONG` ([a code for gRPC](../reference/errors.md#a-code-for-grpc)); empty for a code that maps to none |
| `description` | the rule's message, in English, never the value |

A value object written by hand whose `TryCreate` refuses without a code is recorded as `VALUE_OBJECT_NOT_PARSABLE`.
`Violations` lists what was recorded so far, and the exception carries the code of the first one in its `Data`, every
code being in the `BadRequest`. A field refused leaves the default instance where it was asked for, which no rule
checked: read every field, then call `ThrowIfAny`, and use nothing created before it returns.

## A refusal that escapes a handler

`ValueObjectExceptionInterceptor` answers a `ValueObjectException` that carries a code, one a `Create`, a `Parse` or an
explicit conversion throws, with `INVALID_ARGUMENT`, whose trailers carry a `google.rpc.Status` holding a
`google.rpc.ErrorInfo`:

```csharp skip
builder.Services.AddGrpc(options => options.Interceptors.Add<ValueObjectExceptionInterceptor>());                         // adcodicem.valueobjects
builder.Services.AddGrpc(options => options.Interceptors.Add<ValueObjectExceptionInterceptor>("orders.example.com"));   // a domain of the service's own
builder.Services.AddCodeFirstGrpc(options => options.Interceptors.Add<ValueObjectExceptionInterceptor>());              // protobuf-net.Grpc
```

| Field | Value |
| --- | --- |
| `reason` | the code in upper snake case, `VALUE_OBJECT_INVALID_FORMAT` |
| `domain` | the domain given, `adcodicem.valueobjects` by default |
| `metadata` | `code`, the rule's code; `valueObject`, the value object as C# names it, `Reference<PurchaseOrder>`, when the exception names one |

The status message is the exception's, which for the library's own names the type and the rule, never the value: "'Sku'
rejected the supplied value: The value does not match the expected format.". A code that maps to no gRPC reason is
answered `INVALID_ARGUMENT` without the `ErrorInfo`. On the server, the `RpcException` carries the code in its `Data`.

It serves unary methods and the three kinds of stream: a refusal after a server stream has sent replies ends the stream
with the status, after them. Every other exception goes through as it is, an `RpcException` the handler throws, the one
`ThrowIfAny` throws among them, and a `ValueObjectException` without a code. A request value refused while Grpc.AspNetCore
reads the message, before any interceptor runs, never reaches it: for a protobuf-net.Grpc service,
[`ValueObjectBinderConfiguration.ForServer`](protobuf-net.md#code-first-grpc) of `AdCodicem.ValueObjects.ProtobufNet`
answers it.

`Interceptors.Add<ValueObjectExceptionInterceptor>()` creates the interceptor through the services, so a singleton the
application registers, `new ValueObjectExceptionInterceptor("orders.example.com")`, is the one used.

## Without the package

The same answer takes about thirty lines in a service, and each service must map the code the same way, which is what
`ValueObjectErrorCodes.ToUpperSnakeCase` is for:

```csharp skip
private static TSelf Map<TSelf, TValue>(TValue value, string field, BadRequest violations)
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    if (TSelf.TryCreate(value, out var result, out var validation))
    {
        return result;
    }

    violations.FieldViolations.Add(new BadRequest.Types.FieldViolation
    {
        Field = field,
        Reason = ValueObjectErrorCodes.ToUpperSnakeCase(validation.ErrorCode!),
        Description = validation.ErrorMessage,
    });
    return result;
}

// After every field:
if (violations.FieldViolations.Count > 0)
{
    throw new Google.Rpc.Status { Code = (int)Code.InvalidArgument, Message = "…", Details = { Any.Pack(violations) } }.ToRpcException();
}
```

`ToUpperSnakeCase` throws an `ArgumentException` for a code that maps to no valid reason, a code of the application's own
that starts with a digit, say; the package leaves the reason empty there instead.

## On the client

Grpc.StatusProto reads the details off the exception, in .NET as Google's libraries read them in other languages:

```csharp skip
catch (RpcException exception) when (exception.StatusCode == StatusCode.InvalidArgument)
{
    var status = exception.GetRpcStatus();
    foreach (var violation in status?.GetDetail<BadRequest>()?.FieldViolations ?? [])
    {
        // violation.Field: "skus[1]", violation.Reason: "VALUE_OBJECT_TOO_LONG"
    }

    var info = status?.GetDetail<ErrorInfo>();   // info.Metadata["code"]: value_object.invalid_format
}
```

## Native AOT

The package is AOT-compatible, and the native AOT application CI publishes serves a contract-first service with the
interceptor and the collector over Kestrel, the native binary answering as the JIT does.

## Not covered

- **`localized_message`.** The descriptions and the status message stay in English; a service that answers in its
  users' language looks the message up by code ([a message in the application's
  language](../reference/errors.md#a-message-in-the-applications-language)) and builds its own details.
- **protovalidate** and other field options: the rules stay in the value objects, checked in the handler.
- **gRPC-Web, gRPC JSON transcoding**: not run.
