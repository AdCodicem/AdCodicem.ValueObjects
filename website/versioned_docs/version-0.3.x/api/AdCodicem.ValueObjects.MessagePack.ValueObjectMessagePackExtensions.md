# Class ValueObjectMessagePackExtensions {#AdCodicem_ValueObjects_MessagePack_ValueObjectMessagePackExtensions}

Namespace: [AdCodicem.ValueObjects.MessagePack](AdCodicem.ValueObjects.MessagePack.md)  
Assembly: AdCodicem.ValueObjects.MessagePack.dll  

Wires value objects into MessagePack serializer options.

```csharp
public static class ValueObjectMessagePackExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectMessagePackExtensions](AdCodicem.ValueObjects.MessagePack.ValueObjectMessagePackExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### WithValueObjects\(MessagePackSerializerOptions, bool\) {#AdCodicem_ValueObjects_MessagePack_ValueObjectMessagePackExtensions_WithValueObjects_MessagePack_MessagePackSerializerOptions_System_Boolean_}

Returns options whose resolver answers value objects first, then the options' own resolver.

```csharp
public static MessagePackSerializerOptions WithValueObjects(this MessagePackSerializerOptions options, bool trusted = false)
```

#### Parameters

`options` MessagePackSerializerOptions

The options, <code>MessagePackSerializerOptions.Standard</code> or the application's own.

`trusted` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

Whether value objects are read without validation, for bytes the application alone wrote, a cache among them;
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>, the default, reads them through their rules.

#### Returns

 MessagePackSerializerOptions

A copy of <code class="paramref">options</code> with the resolver in front of theirs.

#### Remarks

<p>
The resolver of the copy is a composite of <xref href="AdCodicem.ValueObjects.MessagePack.ValueObjectResolver.Instance" data-throw-if-not-resolved="false"></xref>, or
<xref href="AdCodicem.ValueObjects.MessagePack.ValueObjectResolver.Trusted" data-throw-if-not-resolved="false"></xref>, and the options' own resolver, which answers every other type: nothing
else changes, <xref href="MessagePack.MessagePackSerializerOptions.Security" data-throw-if-not-resolved="false"></xref> and
<xref href="MessagePack.MessagePackSerializerOptions.Compression" data-throw-if-not-resolved="false"></xref> included. A value object is then written as the bare value
the options' resolver writes for its underlying type, so that a <code>Native*</code> resolver of the chain is honoured.
</p>
<p>
Called twice, the last call answers value objects first: its trust is the one that applies.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">options</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

