# Class EntityIdRegistry {#AdCodicem_ValueObjects_Identifiers_EntityIdRegistry}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

Process-wide index from a prefix to the identifier type that claims it.

```csharp
public static class EntityIdRegistry
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[EntityIdRegistry](AdCodicem.ValueObjects.Identifiers.EntityIdRegistry.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The generator emits a registration per assembly alongside the value object one, so the common case is a
lock-free dictionary hit with no reflection and no dynamic code — usable under native AOT.
</p>
<p>
Two types claiming the same prefix is refused rather than tolerated. Within one compilation the generator
catches it as <code>VO0016</code>; across assemblies only the second registration can notice, and letting it win
silently would make <xref href="AdCodicem.ValueObjects.Identifiers.AnyEntityId" data-throw-if-not-resolved="false"></xref> resolve identifiers to the wrong type — a failure that
surfaces as data corruption long after the deployment that caused it.
</p>

## Methods

### GetRegistered\(\) {#AdCodicem_ValueObjects_Identifiers_EntityIdRegistry_GetRegistered}

Gets the identifier types registered so far.

```csharp
public static IReadOnlyCollection<EntityIdDescriptor> GetRegistered()
```

#### Returns

 [IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection\-1)<[EntityIdDescriptor](AdCodicem.ValueObjects.Identifiers.EntityIdDescriptor.md)\>

A snapshot of the registered descriptors.

### Register<TSelf\>\(\) {#AdCodicem_ValueObjects_Identifiers_EntityIdRegistry_Register__1}

Registers an identifier type without reflection.

```csharp
public static void Register<TSelf>() where TSelf : struct, IEntityId<TSelf>
```

#### Type Parameters

`TSelf` 

Identifier type.

#### Exceptions

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

Another type already claims the same prefix.

### Register\(EntityIdDescriptor\) {#AdCodicem_ValueObjects_Identifiers_EntityIdRegistry_Register_AdCodicem_ValueObjects_Identifiers_EntityIdDescriptor_}

Registers a descriptor.

```csharp
public static void Register(EntityIdDescriptor descriptor)
```

#### Parameters

`descriptor` [EntityIdDescriptor](AdCodicem.ValueObjects.Identifiers.EntityIdDescriptor.md)

Descriptor to register.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">descriptor</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

Another type already claims the same prefix.

### TryGetByPrefix\(string, out EntityIdDescriptor?\) {#AdCodicem_ValueObjects_Identifiers_EntityIdRegistry_TryGetByPrefix_System_String_AdCodicem_ValueObjects_Identifiers_EntityIdDescriptor__}

Looks up the type claiming a prefix.

```csharp
public static bool TryGetByPrefix(string prefix, out EntityIdDescriptor? descriptor)
```

#### Parameters

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

Prefix, without its trailing separator.

`descriptor` [EntityIdDescriptor](AdCodicem.ValueObjects.Identifiers.EntityIdDescriptor.md)?

The descriptor when found.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when a type claims <code class="paramref">prefix</code>.

### TryResolve\(ReadOnlySpan<char\>, out EntityIdDescriptor?\) {#AdCodicem_ValueObjects_Identifiers_EntityIdRegistry_TryResolve_System_ReadOnlySpan_System_Char__AdCodicem_ValueObjects_Identifiers_EntityIdDescriptor__}

Resolves the identifier type a text belongs to, from its prefix.

```csharp
public static bool TryResolve(ReadOnlySpan<char> text, out EntityIdDescriptor? descriptor)
```

#### Parameters

`text` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Candidate identifier.

`descriptor` [EntityIdDescriptor](AdCodicem.ValueObjects.Identifiers.EntityIdDescriptor.md)?

The descriptor when the prefix is claimed.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the text carries a registered prefix.

#### Remarks

A prefix may itself hold separators, so the split cannot be taken at the first one. The body length is
fixed per granularity instead, which makes the prefix boundary computable and bounds resolution to one
lookup per granularity rather than a scan of the registry.

