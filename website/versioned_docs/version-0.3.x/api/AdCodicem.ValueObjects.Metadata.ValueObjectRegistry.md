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
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryResolve(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref> additionally falls back to reflection for hand-written value objects, for modules whose
initializer has not run yet, and for the constructions of a generic value object, whose generic definition is all
the initializer can register. It describes each of them from the <xref href="AdCodicem.ValueObjects.IValueObject%602.Schema" data-throw-if-not-resolved="false"></xref>
the type declares. It is annotated as requiring dynamic code, and its result is cached, so a given type pays that
cost at most once.
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

#### Remarks

A generic value object appears through the constructions registered or resolved so far, never through its
definition, which <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.GetRegisteredGenericDefinitions" data-throw-if-not-resolved="false"></xref> lists.

### GetRegisteredGenericDefinitions\(\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_GetRegisteredGenericDefinitions}

Gets the definitions of the generic value objects registered so far.

```csharp
public static IReadOnlyCollection<Type> GetRegisteredGenericDefinitions()
```

#### Returns

 [IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection\-1)<[Type](https://learn.microsoft.com/dotnet/api/system.type)\>

A snapshot of the generic type definitions.

#### Remarks

An integration configuring every value object up front, as the Entity Framework Core convention does, configures
these by their definition and closes each construction where it meets one.

### GetUnderlyingType\(Type\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_GetUnderlyingType_System_Type_}

Gets the underlying value type of a value object.

```csharp
public static Type? GetUnderlyingType(Type type)
```

#### Parameters

`type` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Value object type, possibly nullable.

#### Returns

 [Type](https://learn.microsoft.com/dotnet/api/system.type)?

The <code>TValue</code> of the <xref href="AdCodicem.ValueObjects.IValueObject%602" data-throw-if-not-resolved="false"></xref> the type implements over itself, or
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when the type is not a value object as <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.IsValueObject(System.Type)" data-throw-if-not-resolved="false"></xref> defines one — a
class or a struct implementing only <xref href="AdCodicem.ValueObjects.IValueObject%601" data-throw-if-not-resolved="false"></xref> included.

#### Remarks

Answering registers nothing. A registered type is answered from the registry, without reflection.

### IsValueObject\(Type\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_IsValueObject_System_Type_}

Determines whether a type is a value object: a struct implementing <xref href="AdCodicem.ValueObjects.IValueObject%602" data-throw-if-not-resolved="false"></xref>
over itself, the one shape a descriptor, a converter or a model binder can be built for.

```csharp
public static bool IsValueObject(Type type)
```

#### Parameters

`type` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Type to test, possibly nullable.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when <code class="paramref">type</code>, or the type it makes nullable, is a value object, which is
exactly when <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryResolve(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref> describes it. An interface, a class, or a struct carrying only the
<xref href="AdCodicem.ValueObjects.IValueObject" data-throw-if-not-resolved="false"></xref> marker or <xref href="AdCodicem.ValueObjects.IValueObject%601" data-throw-if-not-resolved="false"></xref> is not one.

#### Remarks

Answering registers nothing. A registered type is answered from the registry, without reflection.

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

#### Remarks

This is the registration native AOT asks of a value object written by hand that a type-driven integration needs,
<code>Register&lt;Link, Uri&gt;(Link.Schema)</code>, since <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryResolve(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref> would describe it by reflection. Its
descriptor carries no System.Text.Json converter: the converter factory of <code>AdCodicem.ValueObjects.Json</code> gives
it a general-purpose one, closed through
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>.

### Register<TSelf, TValue\>\(ValueObjectSchema, JsonConverter<TSelf\>\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_Register__2_AdCodicem_ValueObjects_Metadata_ValueObjectSchema_System_Text_Json_Serialization_JsonConverter___0__}

Registers a value object and its System.Text.Json converter without reflection.

```csharp
public static void Register<TSelf, TValue>(ValueObjectSchema schema, JsonConverter<TSelf> jsonConverter) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`schema` [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

Declarative constraints of the value object.

`jsonConverter` [JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1)<TSelf\>

The converter of the value object.

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Remarks

A source-generated serializer context finds the converter through the descriptor, whatever the assembly declaring
the value object references. A construction of a generic value object registered by hand under native AOT takes
the shorter <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.Register%60%602(System.Func%7bSystem.Text.Json.Serialization.JsonConverter%7b%60%600%7d%7d)" data-throw-if-not-resolved="false"></xref>, which reads its schema off the type.

### Register<TSelf, TValue\>\(ValueObjectSchema, Func<JsonConverter<TSelf\>\>\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_Register__2_AdCodicem_ValueObjects_Metadata_ValueObjectSchema_System_Func_System_Text_Json_Serialization_JsonConverter___0___}

Registers a value object and the factory of its System.Text.Json converter without reflection.

```csharp
public static void Register<TSelf, TValue>(ValueObjectSchema schema, Func<JsonConverter<TSelf>> jsonConverter) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`schema` [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

Declarative constraints of the value object.

`jsonConverter` [Func](https://learn.microsoft.com/dotnet/api/system.func\-1)<[JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1)<TSelf\>\>

Creates the converter of the value object, the first time it is asked for.

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Remarks

The generated registration calls this one. It runs for every value object when the declaring assembly loads,
and an application that never serializes a value object through the descriptor never builds its converter.

### Register<TSelf, TValue\>\(Func<JsonConverter<TSelf\>\>\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_Register__2_System_Func_System_Text_Json_Serialization_JsonConverter___0___}

Registers a value object with the schema it declares and the factory of its System.Text.Json converter, without
reflection.

```csharp
public static void Register<TSelf, TValue>(Func<JsonConverter<TSelf>> jsonConverter) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`jsonConverter` [Func](https://learn.microsoft.com/dotnet/api/system.func\-1)<[JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1)<TSelf\>\>

Creates the converter of the value object, the first time it is asked for.

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Remarks

The schema is <xref href="AdCodicem.ValueObjects.IValueObject%602.Schema" data-throw-if-not-resolved="false"></xref>, the one the generated registration hands over. This
is the registration native AOT asks of each construction of a generic value object that a type-driven integration
needs: <code>Register&lt;Code&lt;Order&gt;, string&gt;(static () =&gt; new Code&lt;Order&gt;.ValueJsonConverter())</code>.
Its descriptor is built in code, where the AOT compiler sees both type arguments, so a visitor
(<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>) runs on it as on any generated
value object.

### RegisterGenericDefinition\(Type\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_RegisterGenericDefinition_System_Type_}

Registers the definition of a generic value object, whose constructions the registry describes on demand.

```csharp
public static void RegisterGenericDefinition(Type definition)
```

#### Parameters

`definition` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The generic type definition, <code>typeof(Code&lt;&gt;)</code>, or that of a value object nested in a generic type,
<code>typeof(Outer&lt;&gt;.Code)</code>.

#### Remarks

<p>
The generated registration calls this for a generic value object, since it knows none of the constructions the
application will use. <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryResolve(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref> then describes each construction it is asked for from the members
the generator wrote on it, its schema and its converter, and caches the descriptor.
</p>
<p>
That takes reflection and dynamic code. Under native AOT, register each construction a type-driven integration
needs instead, which takes neither:
<code>ValueObjectRegistry.Register&lt;Code&lt;Order&gt;, string&gt;(static () =&gt; new Code&lt;Order&gt;.ValueJsonConverter())</code>.
</p>
<p>
This runs in the module initializer of every assembly declaring a generic value object, where a failure would
stop the application before it starts. Its checks hold under native AOT, the <xref href="AdCodicem.ValueObjects.IValueObject" data-throw-if-not-resolved="false"></xref> marker
included, whether or not anything constructs the definition.
</p>

#### Exceptions

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">definition</code> is not the generic type definition of a struct carrying the
<xref href="AdCodicem.ValueObjects.IValueObject" data-throw-if-not-resolved="false"></xref> marker.

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

#### Remarks

Asked for <code>Iban?</code>, it returns the descriptor of <code>Iban</code>: a descriptor describes the value object
itself, never its nullable type. A converter or a provider built from it closes its generic types over
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.ValueObjectType" data-throw-if-not-resolved="false"></xref> rather than over <code class="paramref">type</code>, which a
constraint on the value object refuses for a <xref href="System.Nullable%601" data-throw-if-not-resolved="false"></xref>, and handles a <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>
before the descriptor's delegates see it, leaving it to the host's own nullable wrapper where one exists.

### TryResolve\(Type, out ValueObjectDescriptor?\) {#AdCodicem_ValueObjects_Metadata_ValueObjectRegistry_TryResolve_System_Type_AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor__}

Looks up a descriptor, building it by reflection when the type has not registered itself.

```csharp
[RequiresDynamicCode("Building a descriptor for an unregistered value object instantiates a generic method at run time.")]
[RequiresUnreferencedCode("Building a descriptor for an unregistered value object inspects its interfaces and generated members.")]
public static bool TryResolve(Type type, out ValueObjectDescriptor? descriptor)
```

#### Parameters

`type` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Value object type, possibly nullable.

`descriptor` [ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)?

The descriptor when the type is a value object.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when <code class="paramref">type</code> is a value object, as <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.IsValueObject(System.Type)" data-throw-if-not-resolved="false"></xref> defines
one.

#### Remarks

<p>
Asked for <code>Iban?</code>, it returns the descriptor of <code>Iban</code>, as <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryGet(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref> does, and the caller
handles <xref href="System.Nullable%601" data-throw-if-not-resolved="false"></xref> and <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> as <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryGet(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref> says.
</p>
<p>
A type nothing registered is described with the <xref href="AdCodicem.ValueObjects.IValueObject%602.Schema" data-throw-if-not-resolved="false"></xref> it declares, the
one generic code constrained on it reads, whatever annotation it carries. The descriptor is kept:
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryGet(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref> finds the type from then on, and <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.GetRegistered" data-throw-if-not-resolved="false"></xref> lists it, so an integration that
configures every registered value object, as the Entity Framework Core convention does, configures it too.
</p>

