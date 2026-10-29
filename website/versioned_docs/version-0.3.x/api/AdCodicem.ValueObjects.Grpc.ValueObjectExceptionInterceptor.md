# Class ValueObjectExceptionInterceptor {#AdCodicem_ValueObjects_Grpc_ValueObjectExceptionInterceptor}

Namespace: [AdCodicem.ValueObjects.Grpc](AdCodicem.ValueObjects.Grpc.md)  
Assembly: AdCodicem.ValueObjects.Grpc.dll  

Answers a <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> that escapes a gRPC service method with <code>INVALID_ARGUMENT</code> and a
<code>google.rpc.ErrorInfo</code> carrying the rule's code, rather than <code>UNKNOWN</code>.

```csharp
public sealed class ValueObjectExceptionInterceptor : Interceptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[Interceptor](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/Interceptors/Interceptor.cs) ← 
[ValueObjectExceptionInterceptor](AdCodicem.ValueObjects.Grpc.ValueObjectExceptionInterceptor.md)

#### Inherited Members

[Interceptor.BlockingUnaryCall<TRequest, TResponse\>\(TRequest, ClientInterceptorContext<TRequest, TResponse\>, Interceptor.BlockingUnaryCallContinuation<TRequest, TResponse\>\)](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/Interceptors/Interceptor.cs), 
[Interceptor.AsyncUnaryCall<TRequest, TResponse\>\(TRequest, ClientInterceptorContext<TRequest, TResponse\>, Interceptor.AsyncUnaryCallContinuation<TRequest, TResponse\>\)](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/Interceptors/Interceptor.cs), 
[Interceptor.AsyncServerStreamingCall<TRequest, TResponse\>\(TRequest, ClientInterceptorContext<TRequest, TResponse\>, Interceptor.AsyncServerStreamingCallContinuation<TRequest, TResponse\>\)](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/Interceptors/Interceptor.cs), 
[Interceptor.AsyncClientStreamingCall<TRequest, TResponse\>\(ClientInterceptorContext<TRequest, TResponse\>, Interceptor.AsyncClientStreamingCallContinuation<TRequest, TResponse\>\)](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/Interceptors/Interceptor.cs), 
[Interceptor.AsyncDuplexStreamingCall<TRequest, TResponse\>\(ClientInterceptorContext<TRequest, TResponse\>, Interceptor.AsyncDuplexStreamingCallContinuation<TRequest, TResponse\>\)](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/Interceptors/Interceptor.cs), 
[Interceptor.UnaryServerHandler<TRequest, TResponse\>\(TRequest, ServerCallContext, UnaryServerMethod<TRequest, TResponse\>\)](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/Interceptors/Interceptor.cs), 
[Interceptor.ClientStreamingServerHandler<TRequest, TResponse\>\(IAsyncStreamReader<TRequest\>, ServerCallContext, ClientStreamingServerMethod<TRequest, TResponse\>\)](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/Interceptors/Interceptor.cs), 
[Interceptor.ServerStreamingServerHandler<TRequest, TResponse\>\(TRequest, IServerStreamWriter<TResponse\>, ServerCallContext, ServerStreamingServerMethod<TRequest, TResponse\>\)](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/Interceptors/Interceptor.cs), 
[Interceptor.DuplexStreamingServerHandler<TRequest, TResponse\>\(IAsyncStreamReader<TRequest\>, IServerStreamWriter<TResponse\>, ServerCallContext, DuplexStreamingServerMethod<TRequest, TResponse\>\)](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/Interceptors/Interceptor.cs), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
A contract-first service maps the <code>string</code> and <code>int32</code> fields of its messages to value objects in its
handlers; when the mapping calls <code>Create</code> on a value the value object refuses, Grpc.AspNetCore answers the
exception with <code>UNKNOWN</code>, "Exception was thrown by handler.", and the rule is lost. Registered with
<code>AddGrpc(options =&gt; options.Interceptors.Add&lt;ValueObjectExceptionInterceptor&gt;())</code>, or
<code>AddCodeFirstGrpc</code>'s for a protobuf-net.Grpc service, this interceptor turns such an exception, one that carries a
code, into an <xref href="Grpc.Core.RpcException" data-throw-if-not-resolved="false"></xref> whose trailers carry a <code>google.rpc.Status</code> holding a
<code>google.rpc.ErrorInfo</code>: its <code>reason</code> is the code mapped by <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.ToUpperSnakeCase(System.String)" data-throw-if-not-resolved="false"></xref>,
its <code>domain</code> the one given, and its <code>metadata</code> holds the <code>code</code> and the <code>valueObject</code>. The status
message is the exception's, which, for one the library throws, names the type and the rule, never the value. A code
that maps to no gRPC reason is answered <code>INVALID_ARGUMENT</code> without the <code>ErrorInfo</code>.
</p>
<p>
Every other exception goes through as it is, an <xref href="Grpc.Core.RpcException" data-throw-if-not-resolved="false"></xref> thrown by the handler included. A request
value refused while Grpc.AspNetCore reads the message, before any interceptor runs, never reaches this one: for a
protobuf-net.Grpc service, <code>ValueObjectBinderConfiguration.ForServer</code> of <code>AdCodicem.ValueObjects.ProtobufNet</code>
answers it.
</p>

## Constructors

### ValueObjectExceptionInterceptor\(string\) {#AdCodicem_ValueObjects_Grpc_ValueObjectExceptionInterceptor__ctor_System_String_}

Answers a <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> that escapes a gRPC service method with <code>INVALID_ARGUMENT</code> and a
<code>google.rpc.ErrorInfo</code> carrying the rule's code, rather than <code>UNKNOWN</code>.

```csharp
public ValueObjectExceptionInterceptor(string domain = "adcodicem.valueobjects")
```

#### Parameters

`domain` [string](https://learn.microsoft.com/dotnet/api/system.string)

The <code>google.rpc.ErrorInfo.domain</code> of a refusal, the service's name for instance.

#### Remarks

<p>
A contract-first service maps the <code>string</code> and <code>int32</code> fields of its messages to value objects in its
handlers; when the mapping calls <code>Create</code> on a value the value object refuses, Grpc.AspNetCore answers the
exception with <code>UNKNOWN</code>, "Exception was thrown by handler.", and the rule is lost. Registered with
<code>AddGrpc(options =&gt; options.Interceptors.Add&lt;ValueObjectExceptionInterceptor&gt;())</code>, or
<code>AddCodeFirstGrpc</code>'s for a protobuf-net.Grpc service, this interceptor turns such an exception, one that carries a
code, into an <xref href="Grpc.Core.RpcException" data-throw-if-not-resolved="false"></xref> whose trailers carry a <code>google.rpc.Status</code> holding a
<code>google.rpc.ErrorInfo</code>: its <code>reason</code> is the code mapped by <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.ToUpperSnakeCase(System.String)" data-throw-if-not-resolved="false"></xref>,
its <code>domain</code> the one given, and its <code>metadata</code> holds the <code>code</code> and the <code>valueObject</code>. The status
message is the exception's, which, for one the library throws, names the type and the rule, never the value. A code
that maps to no gRPC reason is answered <code>INVALID_ARGUMENT</code> without the <code>ErrorInfo</code>.
</p>
<p>
Every other exception goes through as it is, an <xref href="Grpc.Core.RpcException" data-throw-if-not-resolved="false"></xref> thrown by the handler included. A request
value refused while Grpc.AspNetCore reads the message, before any interceptor runs, never reaches this one: for a
protobuf-net.Grpc service, <code>ValueObjectBinderConfiguration.ForServer</code> of <code>AdCodicem.ValueObjects.ProtobufNet</code>
answers it.
</p>

## Fields

### DefaultDomain {#AdCodicem_ValueObjects_Grpc_ValueObjectExceptionInterceptor_DefaultDomain}

The <code>google.rpc.ErrorInfo.domain</code> a refusal is answered with when the application names none.

```csharp
public const string DefaultDomain = "adcodicem.valueobjects"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### ClientStreamingServerHandler<TRequest, TResponse\>\(IAsyncStreamReader<TRequest\>, ServerCallContext, ClientStreamingServerMethod<TRequest, TResponse\>\) {#AdCodicem_ValueObjects_Grpc_ValueObjectExceptionInterceptor_ClientStreamingServerHandler__2_Grpc_Core_IAsyncStreamReader___0__Grpc_Core_ServerCallContext_Grpc_Core_ClientStreamingServerMethod___0___1__}

Server-side handler for intercepting client streaming call.

```csharp
public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation) where TRequest : class where TResponse : class
```

#### Parameters

`requestStream` [IAsyncStreamReader](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/IAsyncStreamReader.cs)<TRequest\>

The request stream of the incoming invocation.

`context` [ServerCallContext](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/ServerCallContext.cs)

An instance of <xref href="Grpc.Core.ServerCallContext" data-throw-if-not-resolved="false"></xref> representing
the context of the invocation.

`continuation` [ClientStreamingServerMethod](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/ServerMethods.cs)<TRequest, TResponse\>

A delegate that asynchronously proceeds with the invocation, calling
the next interceptor in the chain, or the service request handler,
in case of the last interceptor and return the response value of
the RPC. The interceptor can choose to call it zero or more times
at its discretion.

#### Returns

 [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task\-1)<TResponse\>

A future representing the response value of the RPC. The interceptor
can simply return the return value from the continuation intact,
or an arbitrary response value as it sees fit. The interceptor has
the ability to wrap or substitute the request stream when calling
the continuation.

#### Type Parameters

`TRequest` 

Request message type for this method.

`TResponse` 

Response message type for this method.

### DuplexStreamingServerHandler<TRequest, TResponse\>\(IAsyncStreamReader<TRequest\>, IServerStreamWriter<TResponse\>, ServerCallContext, DuplexStreamingServerMethod<TRequest, TResponse\>\) {#AdCodicem_ValueObjects_Grpc_ValueObjectExceptionInterceptor_DuplexStreamingServerHandler__2_Grpc_Core_IAsyncStreamReader___0__Grpc_Core_IServerStreamWriter___1__Grpc_Core_ServerCallContext_Grpc_Core_DuplexStreamingServerMethod___0___1__}

Server-side handler for intercepting bidirectional streaming calls.

```csharp
public override Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation) where TRequest : class where TResponse : class
```

#### Parameters

`requestStream` [IAsyncStreamReader](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/IAsyncStreamReader.cs)<TRequest\>

The request stream of the incoming invocation.

`responseStream` [IServerStreamWriter](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/IServerStreamWriter.cs)<TResponse\>

The response stream of the incoming invocation.

`context` [ServerCallContext](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/ServerCallContext.cs)

An instance of <xref href="Grpc.Core.ServerCallContext" data-throw-if-not-resolved="false"></xref> representing
the context of the invocation.

`continuation` [DuplexStreamingServerMethod](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/ServerMethods.cs)<TRequest, TResponse\>

A delegate that asynchronously proceeds with the invocation, calling
the next interceptor in the chain, or the service request handler,
in case of the last interceptor and the interceptor can choose to
call it zero or more times at its discretion. The interceptor has
the ability to wrap or substitute the request and response streams
when calling the continuation.

#### Returns

 [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

#### Type Parameters

`TRequest` 

Request message type for this method.

`TResponse` 

Response message type for this method.

### ServerStreamingServerHandler<TRequest, TResponse\>\(TRequest, IServerStreamWriter<TResponse\>, ServerCallContext, ServerStreamingServerMethod<TRequest, TResponse\>\) {#AdCodicem_ValueObjects_Grpc_ValueObjectExceptionInterceptor_ServerStreamingServerHandler__2___0_Grpc_Core_IServerStreamWriter___1__Grpc_Core_ServerCallContext_Grpc_Core_ServerStreamingServerMethod___0___1__}

Server-side handler for intercepting server streaming call.

```csharp
public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation) where TRequest : class where TResponse : class
```

#### Parameters

`request` TRequest

The request value of the incoming invocation.

`responseStream` [IServerStreamWriter](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/IServerStreamWriter.cs)<TResponse\>

The response stream of the incoming invocation.

`context` [ServerCallContext](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/ServerCallContext.cs)

An instance of <xref href="Grpc.Core.ServerCallContext" data-throw-if-not-resolved="false"></xref> representing
the context of the invocation.

`continuation` [ServerStreamingServerMethod](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/ServerMethods.cs)<TRequest, TResponse\>

A delegate that asynchronously proceeds with the invocation, calling
the next interceptor in the chain, or the service request handler,
in case of the last interceptor and the interceptor can choose to
call it zero or more times at its discretion. The interceptor has
the ability to wrap or substitute the request value and the response stream
when calling the continuation.

#### Returns

 [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

#### Type Parameters

`TRequest` 

Request message type for this method.

`TResponse` 

Response message type for this method.

### UnaryServerHandler<TRequest, TResponse\>\(TRequest, ServerCallContext, UnaryServerMethod<TRequest, TResponse\>\) {#AdCodicem_ValueObjects_Grpc_ValueObjectExceptionInterceptor_UnaryServerHandler__2___0_Grpc_Core_ServerCallContext_Grpc_Core_UnaryServerMethod___0___1__}

Server-side handler for intercepting an incoming unary call.

```csharp
public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation) where TRequest : class where TResponse : class
```

#### Parameters

`request` TRequest

The request value of the incoming invocation.

`context` [ServerCallContext](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/ServerCallContext.cs)

An instance of <xref href="Grpc.Core.ServerCallContext" data-throw-if-not-resolved="false"></xref> representing
the context of the invocation.

`continuation` [UnaryServerMethod](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/ServerMethods.cs)<TRequest, TResponse\>

A delegate that asynchronously proceeds with the invocation, calling
the next interceptor in the chain, or the service request handler,
in case of the last interceptor and return the response value of
the RPC. The interceptor can choose to call it zero or more times
at its discretion.

#### Returns

 [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task\-1)<TResponse\>

A future representing the response value of the RPC. The interceptor
can simply return the return value from the continuation intact,
or an arbitrary response value as it sees fit.

#### Type Parameters

`TRequest` 

Request message type for this method.

`TResponse` 

Response message type for this method.

