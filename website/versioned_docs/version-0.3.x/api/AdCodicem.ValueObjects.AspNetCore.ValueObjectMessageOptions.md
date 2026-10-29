# Class ValueObjectMessageOptions {#AdCodicem_ValueObjects_AspNetCore_ValueObjectMessageOptions}

Namespace: [AdCodicem.ValueObjects.AspNetCore](AdCodicem.ValueObjects.AspNetCore.md)  
Assembly: AdCodicem.ValueObjects.AspNetCore.Http.dll  

Where a refused value object finds a message in the application's language: a resource read through
<code>IStringLocalizerFactory</code>, under the key of the rule's code.

```csharp
public sealed class ValueObjectMessageOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectMessageOptions](AdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The options of <code>WithValueObjectProblemDetails(messages =&gt; …)</code> on a minimal API, of
<code>AddValueObjects(messages =&gt; …)</code> on MVC, and of <code>UseValueObjectProblemDetails(messages =&gt; …)</code> on an
Azure Functions HTTP trigger. Without a
resource, a refusal keeps the English message of the rule. With one, the message is looked up under the keys of
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.KeysFor(AdCodicem.ValueObjects.Metadata.ValueObjectSchema%2cSystem.String)" data-throw-if-not-resolved="false"></xref>: the key of the code,
<code>ValueObject.value_object.too_short</code>, after the same key followed by <code>.minimum</code> or <code>.maximum</code> for a
range of one bound. It is formatted with the member the refusal is listed under as <code>{0}</code>, followed by the arguments
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.ArgumentsFor(AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%2cSystem.String%2cSystem.IFormatProvider)" data-throw-if-not-resolved="false"></xref> reads from the type's
rules, from <code>{1}</code>. A key with no entry keeps the English message.
</p>
<p>
The current UI culture chooses the resource, and the current culture formats the arguments: with
<code>UseRequestLocalization()</code>, both follow the request. An Azure Functions invocation runs on the host's call, not
on the request's, so the Functions middleware applies the request's cultures itself: those
<code>UseRequestLocalization()</code> chose, or else those the application's <code>RequestLocalizationOptions</code> choose from
the request. The localizer comes from the request's services, which <code>builder.Services.AddLocalization()</code>
registers; without it, a refusal throws an <xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref> naming the call. A template quoting
an argument its code does not supply is the application's error, and its <xref href="System.FormatException" data-throw-if-not-resolved="false"></xref> is not caught.
</p>
<p>
The type lives in <code>AdCodicem.ValueObjects.AspNetCore.Http</code>, beside <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectProblemDetails" data-throw-if-not-resolved="false"></xref> and
under the same namespace, so that the minimal API package, the MVC one and the Azure Functions ASP.NET Core one,
which reference it, share it.
</p>

## Properties

### ResourceType {#AdCodicem_ValueObjects_AspNetCore_ValueObjectMessageOptions_ResourceType}

Gets the type whose resources hold the messages, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to keep the English messages.

```csharp
public Type? ResourceType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)?

## Methods

### UseResource<TResource\>\(\) {#AdCodicem_ValueObjects_AspNetCore_ValueObjectMessageOptions_UseResource__1}

Reads the messages from the resources of <code class="typeparamref">TResource</code>.

```csharp
public ValueObjectMessageOptions UseResource<TResource>()
```

#### Returns

 [ValueObjectMessageOptions](AdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions.md)

These options, so calls can be chained.

#### Type Parameters

`TResource` 

The type the resources are named after, as <code>IStringLocalizer&lt;T&gt;</code> names them: an empty class of the
application, beside a <code>.resx</code> file of the same name for each culture.

### UseResource\(Type\) {#AdCodicem_ValueObjects_AspNetCore_ValueObjectMessageOptions_UseResource_System_Type_}

Reads the messages from the resources of a type.

```csharp
public ValueObjectMessageOptions UseResource(Type resourceType)
```

#### Parameters

`resourceType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The type the resources are named after.

#### Returns

 [ValueObjectMessageOptions](AdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions.md)

These options, so calls can be chained.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">resourceType</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

