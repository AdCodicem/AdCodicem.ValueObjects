# Class ValueObjectBson {#AdCodicem_ValueObjects_MongoDB_ValueObjectBson}

Namespace: [AdCodicem.ValueObjects.MongoDB](AdCodicem.ValueObjects.MongoDB.md)  
Assembly: AdCodicem.ValueObjects.MongoDB.dll  

Registers the MongoDB.Driver serializers of value objects.

```csharp
public static class ValueObjectBson
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectBson](AdCodicem.ValueObjects.MongoDB.ValueObjectBson.md)

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
MongoDB.Driver keeps its serializers in a process-wide registry that never forgets one, and maps a type it has no
serializer for through a class map. A value object has no member a class map can set, so without these serializers it
is written as an empty sub-document, <code>{}</code>, read back as its default instance, and a query over it compares
<code>{}</code>, matching every document or none. Call <xref href="AdCodicem.ValueObjects.MongoDB.ValueObjectBson.Register(System.Reflection.Assembly%5b%5d)" data-throw-if-not-resolved="false"></xref> once at start-up, after
registering any serializer of the application's own, the <xref href="MongoDB.Bson.Serialization.Serializers.GuidSerializer" data-throw-if-not-resolved="false"></xref> first, and before anything is
serialized or any class map is built.
</p>

## Methods

### Register\(params Assembly\[\]\) {#AdCodicem_ValueObjects_MongoDB_ValueObjectBson_Register_System_Reflection_Assembly___}

Registers, once, the provider that hands the driver a strict serializer for every value object.

```csharp
[RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
public static void Register(params Assembly[] assemblies)
```

#### Parameters

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring value objects, whose generated registration runs first, so that their value objects are
checked.

#### Remarks

Strict, as <xref href="AdCodicem.ValueObjects.MongoDB.ValueObjectBson.Register(System.Boolean%2cSystem.Reflection.Assembly%5b%5d)" data-throw-if-not-resolved="false"></xref> with <code>trusted: false</code> is: every value read goes through
the value object's rules, since a collection has no schema and is often written by other services, scripts or
tools.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">assemblies</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

The provider is already registered with <code>trusted: true</code>; a value object over <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> is
registered and the serializer of <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> has no representation; or the driver has already mapped a
registered value object through a class map.

### Register\(bool, params Assembly\[\]\) {#AdCodicem_ValueObjects_MongoDB_ValueObjectBson_Register_System_Boolean_System_Reflection_Assembly___}

Registers, once, the provider that hands the driver a serializer for every value object, trusted or strict.

```csharp
[RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
public static void Register(bool trusted, params Assembly[] assemblies)
```

#### Parameters

`trusted` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

Whether reads skip validation, through <code>CreateUnchecked</code>, for a collection the application alone writes;
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a> reads through <code>TryCreate</code>, and refuses a value the value object rejects.

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring value objects, whose generated registration runs first, so that their value objects are
checked.

#### Remarks

<p>
The provider answers every value object the driver meets, whichever assembly declares it, with a
<xref href="AdCodicem.ValueObjects.MongoDB.ValueObjectBsonSerializer%602" data-throw-if-not-resolved="false"></xref> over the serializer the registry holds for its underlying
type, closed at compile time through the type arguments the descriptor hands back to a visitor
(<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>). It is registered once: a
second call with the same trust registers the assemblies given and repeats the checks below, one with the other
trust throws. A value object that needs the other trust, or a serializer of the application's own, is given it per
type with <xref href="AdCodicem.ValueObjects.MongoDB.ValueObjectBson.Register%60%602(System.Boolean)" data-throw-if-not-resolved="false"></xref> or <xref href="MongoDB.Bson.Serialization.BsonSerializer.RegisterSerializer(System.Type%2cMongoDB.Bson.Serialization.IBsonSerializer)" data-throw-if-not-resolved="false"></xref>,
or per member, on its class map, before the driver first asks for it.
</p>
<p>
Two mistakes are refused here rather than on the first document. A value object over <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>, with the
driver's default <xref href="MongoDB.Bson.Serialization.Serializers.GuidSerializer" data-throw-if-not-resolved="false"></xref>, whose representation is unspecified, would throw on the first write:
register <code>new GuidSerializer(GuidRepresentation.Standard)</code> before calling this method. And a value object the
driver has already serialized, before this call, keeps the class map it was given, which writes <code>{}</code>, for the
rest of the process: the exception names the value objects concerned, and the registration has to move before
whatever serialized them, a construction of a generic value object and a value object written by hand included.
The <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> check covers the value objects the registry holds when the method runs; the provider repeats
it for one it meets later, when it writes one and when it reads a binary value.
</p>
<p>
A value object over <xref href="System.Int128" data-throw-if-not-resolved="false"></xref> or <xref href="System.UInt128" data-throw-if-not-resolved="false"></xref>, for which MongoDB.Bson has no serializer, is
refused with a <xref href="MongoDB.Bson.BsonSerializationException" data-throw-if-not-resolved="false"></xref> when it is written or read, unless the application
registered a serializer of its own for the underlying type, which it then goes through.
</p>
<p>
A construction of a generic value object, a value object written by hand, or one in an assembly whose registration
has not run, that nothing registered, is described by reflection the first time the driver asks for it
(<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryResolve(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref>), which only the JIT can run; <xref href="AdCodicem.ValueObjects.MongoDB.ValueObjectBson.Register%60%602(System.Boolean)" data-throw-if-not-resolved="false"></xref>
registers one without reflection. Locating the generated registration of an assembly given by name reads its
metadata, which trimming may remove. MongoDB.Driver itself maps documents through class maps it builds by
reflection and is not compatible with trimming or native AOT.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">assemblies</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

The provider is already registered with the other trust; a value object over <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> is registered
and the serializer of <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> has no representation; or the driver has already mapped a registered value
object through a class map.

### Register<TSelf, TValue\>\(bool\) {#AdCodicem_ValueObjects_MongoDB_ValueObjectBson_Register__2_System_Boolean_}

Registers the serializer of one value object, such as a construction of a generic value object.

```csharp
public static void Register<TSelf, TValue>(bool trusted = false) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`trusted` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

Whether reads skip validation, for a collection the application alone writes; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>, the
default, reads through the value object's rules.

#### Type Parameters

`TSelf` 

Value object type, <code>Code&lt;Order&gt;</code> for instance.

`TValue` 

Underlying value type.

#### Remarks

The serializer is the one <xref href="AdCodicem.ValueObjects.MongoDB.ValueObjectBson.Register(System.Boolean%2cSystem.Reflection.Assembly%5b%5d)" data-throw-if-not-resolved="false"></xref> hands the driver, registered with
<xref href="MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(System.Type%2cMongoDB.Bson.Serialization.IBsonSerializer)" data-throw-if-not-resolved="false"></xref>, so a second call with the same trust,
or a call for a value object the provider already served with that trust, does nothing. Closed at compile time,
it needs no reflection and no dynamic code to be built.

#### Exceptions

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

<code class="typeparamref">TValue</code> is <xref href="System.Int128" data-throw-if-not-resolved="false"></xref> or <xref href="System.UInt128" data-throw-if-not-resolved="false"></xref>, MongoDB.Bson has no serializer for
it, and the application registered none of its own.

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

<code class="typeparamref">TValue</code> is <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> and the serializer of <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> has no
representation.

 BsonSerializationException

The driver already holds a different serializer for <code class="typeparamref">TSelf</code>: one of the other trust, one of
the application's own, or the class map it gave a value object it serialized before any registration.

