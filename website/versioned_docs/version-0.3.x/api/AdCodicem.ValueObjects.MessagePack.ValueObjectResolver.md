# Class ValueObjectResolver {#AdCodicem_ValueObjects_MessagePack_ValueObjectResolver}

Namespace: [AdCodicem.ValueObjects.MessagePack](AdCodicem.ValueObjects.MessagePack.md)  
Assembly: AdCodicem.ValueObjects.MessagePack.dll  

Answers the formatter of any value object, closed over it at compile time through the descriptor's visitor, and
nothing else.

```csharp
public sealed class ValueObjectResolver : IFormatterResolver
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectResolver](AdCodicem.ValueObjects.MessagePack.ValueObjectResolver.md)

#### Implements

IFormatterResolver

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
It answers <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for any other type, and for a <xref href="System.Nullable%601" data-throw-if-not-resolved="false"></xref> of a value object, so that
the next resolver of a composite answers: MessagePack's own <code>NullableFormatter</code> then writes and reads a
<code>nil</code> for a <code>TSelf?</code> holding nothing and asks this resolver for <code>TSelf</code> otherwise. Placed first, before
<code>StandardResolver</code>, <code>ContractlessStandardResolver</code> or SignalR's own, it is the one MessagePack asks for a
value object, which no other resolver writes as its value: <code>StandardResolver</code> knows no formatter for one, and a
contractless resolver writes it as an empty map, <code>{}</code>, read back as the default instance.
</p>
<p>
A value object the registry holds is closed through <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>. One it does
not hold yet, a construction of a generic value object nothing registered, a value object written by hand, a value
object of a module whose registration has not run, is described by <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryResolve(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref>, by
reflection, which only the JIT can run. The formatter of each type is built once per resolver and kept.
</p>

## Properties

### Instance {#AdCodicem_ValueObjects_MessagePack_ValueObjectResolver_Instance}

Gets the resolver whose formatters read through the value objects' rules.

```csharp
public static ValueObjectResolver Instance { get; }
```

#### Property Value

 [ValueObjectResolver](AdCodicem.ValueObjects.MessagePack.ValueObjectResolver.md)

### Trusted {#AdCodicem_ValueObjects_MessagePack_ValueObjectResolver_Trusted}

Gets the resolver whose formatters read without validating, for a cache or a store the application alone writes.

```csharp
public static ValueObjectResolver Trusted { get; }
```

#### Property Value

 [ValueObjectResolver](AdCodicem.ValueObjects.MessagePack.ValueObjectResolver.md)

## Methods

### GetFormatter<T\>\(\) {#AdCodicem_ValueObjects_MessagePack_ValueObjectResolver_GetFormatter__1}

Gets an <xref href="MessagePack.Formatters.IMessagePackFormatter%601" data-throw-if-not-resolved="false"></xref> instance that can serialize or deserialize some type <code class="typeparamref">T</code>.

```csharp
public IMessagePackFormatter<T>? GetFormatter<T>()
```

#### Returns

 IMessagePackFormatter<T\>?

A formatter, if this resolver supplies one for type <code class="typeparamref">T</code>; otherwise <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

#### Type Parameters

`T` 

The type of value to be serialized or deserialized.

