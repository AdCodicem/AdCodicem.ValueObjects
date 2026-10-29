# Class ValueObjectBinderConfiguration {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectBinderConfiguration}

Namespace: [AdCodicem.ValueObjects.ProtobufNet](AdCodicem.ValueObjects.ProtobufNet.md)  
Assembly: AdCodicem.ValueObjects.ProtobufNet.dll  

Builds the protobuf-net.Grpc binder configuration of a code-first server whose messages hold value objects.

```csharp
public static class ValueObjectBinderConfiguration
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectBinderConfiguration](AdCodicem.ValueObjects.ProtobufNet.ValueObjectBinderConfiguration.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Fields

### DefaultDomain {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectBinderConfiguration_DefaultDomain}

The <code>google.rpc.ErrorInfo.domain</code> a refusal is answered with when the application names none.

```csharp
public const string DefaultDomain = "adcodicem.valueobjects"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### ForServer\(TypeModel, string\) {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectBinderConfiguration_ForServer_ProtoBuf_Meta_TypeModel_System_String_}

Builds a binder configuration for a code-first server: payloads go through the model, and a request value a value
object refuses is answered <code>INVALID_ARGUMENT</code> with the rule's code.

```csharp
[RequiresDynamicCode("protobuf-net.Grpc binds a code-first service and closes its marshallers by reflection at run time.")]
public static BinderConfiguration ForServer(TypeModel model, string domain = "adcodicem.valueobjects")
```

#### Parameters

`model` TypeModel

The model the value objects are registered on, <xref href="ProtoBuf.Meta.RuntimeTypeModel.Default" data-throw-if-not-resolved="false"></xref> or the application's own.

`domain` [string](https://learn.microsoft.com/dotnet/api/system.string)

The <code>google.rpc.ErrorInfo.domain</code> of a refusal, the service's name for instance.

#### Returns

 BinderConfiguration

The configuration, which <code>AddCodeFirstGrpc()</code> reads from the service collection.

#### Remarks

<p>
Grpc.AspNetCore reads a request before any interceptor runs, and answers an exception its deserializer throws with
<code>UNKNOWN</code>, "Exception was thrown by handler.". The marshaller of this configuration turns an exception that
carries a rule's code, under <xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>, into an <xref href="Grpc.Core.RpcException" data-throw-if-not-resolved="false"></xref> with
<xref href="Grpc.Core.StatusCode.InvalidArgument" data-throw-if-not-resolved="false"></xref>, whose trailers carry <code>grpc-status-details-bin</code>: a
<code>google.rpc.Status</code> holding a <code>google.rpc.ErrorInfo</code>, whose <code>reason</code> is the code mapped by
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.ToUpperSnakeCase(System.String)" data-throw-if-not-resolved="false"></xref>, and whose <code>metadata</code> holds the <code>code</code> and the
<code>valueObject</code>. Neither the status nor the detail holds the value refused. A code that maps to no gRPC reason is
answered <code>INVALID_ARGUMENT</code> with the <code>google.rpc.Status</code> alone, without the <code>ErrorInfo</code>.
</p>
<p>
Everything else is protobuf-net.Grpc's own: the marshallers of <code>ProtoBufMarshallerFactory.Create(model)</code>, then
those of Google.Protobuf messages, as in <xref href="ProtoBuf.Grpc.Configuration.BinderConfiguration.Default" data-throw-if-not-resolved="false"></xref>. A client needs nothing of this
package beyond the model: a reply it refuses surfaces as <xref href="Grpc.Core.StatusCode.Internal" data-throw-if-not-resolved="false"></xref>, whose inner exception
carries the code.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">model</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">domain</code> is empty or white space.

