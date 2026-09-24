# Class ValueObjectRegistry {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Process-wide directory of the value object types known to the application.

```csharp
public static class ValueObjectRegistry
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectRegistry](AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.md)

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
The source generator emits a module initializer per assembly that registers every generated value object,
so the common case is a lock-free dictionary hit with no reflection and no dynamic code — the registry is
therefore usable under native AOT.
</p>
<p>
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryResolve(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref> additionally falls back to reflection for hand-written value objects and for
modules whose initializer has not run yet. It is annotated as requiring dynamic code, and its result is
cached, so a given type pays that cost at most once.
</p>

## Methods

### EnsureAssemblyRegistered\(Assembly\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_EnsureAssemblyRegistered_System_Reflection_Assembly_}

Runs the generated registration of an assembly, if it has one.

```csharp
[RequiresUnreferencedCode("Locating the generated registration type of an assembly requires its metadata.")]
public static void EnsureAssemblyRegistered(Assembly assembly)
```

#### Parameters

`assembly` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)

Assembly declaring value objects.

#### Remarks

Module initializers are triggered by the first access to a member of the module, which model-building
code that only reflects over types may never perform. Integrations call this before enumerating types.

### GetRegistered\(\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_GetRegistered}

Gets the descriptors registered so far.

```csharp
public static IReadOnlyCollection<ValueObjectDescriptor> GetRegistered()
```

#### Returns

 [IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection\-1)<[ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)\>

A snapshot of the registered descriptors.

### GetUnderlyingType\(Type\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_GetUnderlyingType_System_Type_}

Gets the underlying value type of a value object.

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2070:UnrecognizedReflectionPattern", Justification = "The interface list of a value object is preserved: the type is referenced by the caller and its IValueObject implementation is part of its public contract.")]
public static Type? GetUnderlyingType(Type type)
```

#### Parameters

`type` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Value object type, possibly nullable.

#### Returns

 [Type](https://learn.microsoft.com/dotnet/api/system.type)?

The underlying value type, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when the type is not a value object.

### IsValueObject\(Type\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_IsValueObject_System_Type_}

Determines whether a type is a value object.

```csharp
public static bool IsValueObject(Type type)
```

#### Parameters

`type` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Type to test, possibly nullable.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the type implements <xref href="AdCodicem.ValueObjects.IValueObject" data-throw-if-not-resolved="false"></xref>.

### Register\(ValueObjectDescriptor\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_Register_AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_}

Registers a descriptor, replacing any previous registration for the same type.

```csharp
public static void Register(ValueObjectDescriptor descriptor)
```

#### Parameters

`descriptor` [ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)

Descriptor to register.

### Register<TSelf, TValue\>\(ValueObjectSchema\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_Register__2_AdCodicem_ValueObjects_Metadata_ValueObjectSchema_}

Registers a value object without reflection.

```csharp
public static void Register<TSelf, TValue>(ValueObjectSchema schema) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`schema` [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

Declarative constraints of the value object.

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

### TryGet\(Type, out ValueObjectDescriptor?\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_TryGet_System_Type_AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor__}

Looks up an already registered descriptor.

```csharp
public static bool TryGet(Type type, out ValueObjectDescriptor? descriptor)
```

#### Parameters

`type` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Value object type, possibly nullable.

`descriptor` [ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)?

The descriptor when found.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when a descriptor is registered for <code class="paramref">type</code>.

### TryResolve\(Type, out ValueObjectDescriptor?\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_TryResolve_System_Type_AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor__}

Looks up a descriptor, building it by reflection when the type has not registered itself.

```csharp
[RequiresDynamicCode("Building a descriptor for an unregistered value object instantiates a generic method at run time.")]
[RequiresUnreferencedCode("Building a descriptor for an unregistered value object inspects its interfaces and attributes.")]
public static bool TryResolve(Type type, out ValueObjectDescriptor? descriptor)
```

#### Parameters

`type` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Value object type, possibly nullable.

`descriptor` [ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)?

The descriptor when the type is a value object.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when <code class="paramref">type</code> is a value object.

