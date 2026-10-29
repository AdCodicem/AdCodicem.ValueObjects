# Class ValueObjectInputOptions {#AdCodicem_ValueObjects_Blazor_ValueObjectInputOptions}

Namespace: [AdCodicem.ValueObjects.Blazor](AdCodicem.ValueObjects.Blazor.md)  
Assembly: AdCodicem.ValueObjects.Blazor.dll  

The messages the inputs of value objects answer a refusal with: the English message of the rule, or one read from a
resource of the application, under the key of the rule's code.

```csharp
public sealed class ValueObjectInputOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectInputOptions](AdCodicem.ValueObjects.Blazor.ValueObjectInputOptions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The options of <code>AddValueObjectInputs(options =&gt; …)</code>. Without a resource, a refusal keeps the English message of
the rule. With one, the message is looked up under the keys of <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.KeysFor(AdCodicem.ValueObjects.Metadata.ValueObjectSchema%2cSystem.String)" data-throw-if-not-resolved="false"></xref>: the key of
the code, <code>ValueObject.value_object.too_short</code>, after the same key followed by <code>.minimum</code> or <code>.maximum</code>
for a range of one bound. It is formatted with the input's <code>DisplayName</code>, or else the name of its field, as
<code>{0}</code>, followed by the arguments <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.ArgumentsFor%60%602(System.String%2cSystem.IFormatProvider)" data-throw-if-not-resolved="false"></xref> reads from the
type's rules, from <code>{1}</code>. A key with no entry keeps the English message.
</p>
<p>
The current UI culture chooses the resource, and the current culture formats the arguments: the culture a Blazor
Server circuit or a Blazor WebAssembly application runs in. The localizer comes from the application's services,
which <code>AddValueObjectInputs</code> registers with <code>AddLocalization()</code>, keeping a registration the application made
first. A template quoting an argument its code does not supply is the application's error, and its
<xref href="System.FormatException" data-throw-if-not-resolved="false"></xref> is not caught.
</p>

## Properties

### ResourceType {#AdCodicem_ValueObjects_Blazor_ValueObjectInputOptions_ResourceType}

Gets the type whose resources hold the messages, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to keep the English messages.

```csharp
public Type? ResourceType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)?

## Methods

### UseResource<TResource\>\(\) {#AdCodicem_ValueObjects_Blazor_ValueObjectInputOptions_UseResource__1}

Reads the messages from the resources of <code class="typeparamref">TResource</code>, under the key of the rule's code.

```csharp
public ValueObjectInputOptions UseResource<TResource>()
```

#### Returns

 [ValueObjectInputOptions](AdCodicem.ValueObjects.Blazor.ValueObjectInputOptions.md)

These options, so calls can be chained.

#### Type Parameters

`TResource` 

The type the resources are named after, as <code>IStringLocalizer&lt;T&gt;</code> names them: an empty class of the
application, beside a <code>.resx</code> file of the same name for each culture.

### UseResource\(Type\) {#AdCodicem_ValueObjects_Blazor_ValueObjectInputOptions_UseResource_System_Type_}

Reads the messages from the resources of a type.

```csharp
public ValueObjectInputOptions UseResource(Type resourceType)
```

#### Parameters

`resourceType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The type the resources are named after.

#### Returns

 [ValueObjectInputOptions](AdCodicem.ValueObjects.Blazor.ValueObjectInputOptions.md)

These options, so calls can be chained.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">resourceType</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

