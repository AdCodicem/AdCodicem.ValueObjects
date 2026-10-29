# Class ValueObjectBadRequest {#AdCodicem_ValueObjects_Grpc_ValueObjectBadRequest}

Namespace: [AdCodicem.ValueObjects.Grpc](AdCodicem.ValueObjects.Grpc.md)  
Assembly: AdCodicem.ValueObjects.Grpc.dll  

Creates the value objects a contract-first request maps its fields to, records each value they refuse as a
<code>google.rpc.BadRequest</code> field violation, and answers them all at once as <code>INVALID_ARGUMENT</code>.

```csharp
public sealed class ValueObjectBadRequest
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectBadRequest](AdCodicem.ValueObjects.Grpc.ValueObjectBadRequest.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
One per request: <code>var sku = request.Create&lt;Sku, string&gt;(message.Sku, "sku")</code> for each field, then
<xref href="AdCodicem.ValueObjects.Grpc.ValueObjectBadRequest.ThrowIfAny(System.String)" data-throw-if-not-resolved="false"></xref>, which throws the <xref href="Grpc.Core.RpcException" data-throw-if-not-resolved="false"></xref> <code>Google.Rpc.Status.ToRpcException()</code> builds
when anything was refused. Each violation's <code>field</code> is the path given, an element of a repeated field
<code>skus[1]</code>; its <code>reason</code> the code mapped by <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.ToUpperSnakeCase(System.String)" data-throw-if-not-resolved="false"></xref>, left empty for
a code that maps to no gRPC reason; its <code>description</code> the rule's message, in English, never the value.
</p>
<p>
A value refused leaves the default instance where it was asked for, which no rule checked: read every field before
calling <xref href="AdCodicem.ValueObjects.Grpc.ValueObjectBadRequest.ThrowIfAny(System.String)" data-throw-if-not-resolved="false"></xref>, and use nothing created before it returns.
</p>

## Properties

### Violations {#AdCodicem_ValueObjects_Grpc_ValueObjectBadRequest_Violations}

Gets the field violations recorded so far, in the order the fields were read.

```csharp
public IReadOnlyList<BadRequest.Types.FieldViolation> Violations { get; }
```

#### Property Value

 [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist\-1)<[BadRequest](https://github.com/googleapis/gax\-dotnet/blob/main/Google.Api.CommonProtos/Rpc/ErrorDetails.g.cs).[Types](https://github.com/googleapis/gax\-dotnet/blob/main/Google.Api.CommonProtos/Rpc/ErrorDetails.g.cs).[FieldViolation](https://github.com/googleapis/gax\-dotnet/blob/main/Google.Api.CommonProtos/Rpc/ErrorDetails.g.cs)\>

## Methods

### Create<TSelf, TValue\>\(TValue, string\) {#AdCodicem_ValueObjects_Grpc_ValueObjectBadRequest_Create__2___1_System_String_}

Creates a value object from a field, or records its refusal under the field's path.

```csharp
public TSelf Create<TSelf, TValue>(TValue value, string field) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`value` TValue

The field's value.

`field` [string](https://learn.microsoft.com/dotnet/api/system.string)

The field's path in the request, <code>sku</code> or <code>order.lines[2].sku</code>.

#### Returns

 TSelf

The value object, or the default instance when the value is refused.

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type, the field's.

#### Exceptions

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">field</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>, empty or white space.

### CreateAll<TSelf, TValue\>\(IEnumerable<TValue\>, string\) {#AdCodicem_ValueObjects_Grpc_ValueObjectBadRequest_CreateAll__2_System_Collections_Generic_IEnumerable___1__System_String_}

Creates a value object from each element of a repeated field, recording each refusal under the element's path.

```csharp
public IReadOnlyList<TSelf> CreateAll<TSelf, TValue>(IEnumerable<TValue> values, string field) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`values` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable\-1)<TValue\>

The field's elements.

`field` [string](https://learn.microsoft.com/dotnet/api/system.string)

The field's path in the request; each element's is <code>field[index]</code>.

#### Returns

 [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist\-1)<TSelf\>

The value objects, a default instance in place of each one refused.

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type, the field's elements'.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">values</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">field</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>, empty or white space.

### ThrowIfAny\(string\) {#AdCodicem_ValueObjects_Grpc_ValueObjectBadRequest_ThrowIfAny_System_String_}

Throws, when any value was refused, an <xref href="Grpc.Core.RpcException" data-throw-if-not-resolved="false"></xref> answering them all: <code>INVALID_ARGUMENT</code>, whose
trailers carry a <code>google.rpc.Status</code> holding the <code>google.rpc.BadRequest</code>.

```csharp
public void ThrowIfAny(string message = "The request holds values its value objects refuse.")
```

#### Parameters

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

The status message, in English, for the developer who reads it.

#### Exceptions

 [RpcException](https://github.com/grpc/grpc\-dotnet/blob/main/src/Grpc.Core.Api/RpcException.cs)

A value was refused.

