# Class ValueObjectDapper {#AdCodicem_ValueObjects_Dapper_ValueObjectDapper}

Namespace: [AdCodicem.ValueObjects.Dapper](AdCodicem.ValueObjects.Dapper.md)  
Assembly: AdCodicem.ValueObjects.Dapper.dll  

Registers Dapper type handlers for value objects.

```csharp
public static class ValueObjectDapper
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectDapper](AdCodicem.ValueObjects.Dapper.ValueObjectDapper.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Dapper keeps its handlers in a process-wide table, so this is called once at start-up. Without it, every
query touching a value object would need an explicit projection.

## Methods

### AddValueObjectHandler<TSelf, TValue\>\(\) {#AdCodicem_ValueObjects_Dapper_ValueObjectDapper_AddValueObjectHandler__2}

Registers a handler for one value object, such as a construction of a generic value object.

```csharp
public static void AddValueObjectHandler<TSelf, TValue>() where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

Value object type, <code>Code&lt;Order&gt;</code> for instance.

`TValue` 

Underlying value type.

#### Remarks

The handler is the one <xref href="AdCodicem.ValueObjects.Dapper.ValueObjectDapper.AddValueObjectHandlers(System.Reflection.Assembly%5b%5d)" data-throw-if-not-resolved="false"></xref> registers, for the value object and its nullable
form, and a value object Dapper already has a handler for keeps it. Closed at compile time, it needs no dynamic
code to be built, and it reads the column the value object declares off
<xref href="AdCodicem.ValueObjects.IValueObject%602.Schema" data-throw-if-not-resolved="false"></xref>, without asking the registry, which would describe a construction
by reflection; Dapper reflects over the handlers it is given.

#### Exceptions

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

<code class="typeparamref">TValue</code> is <xref href="System.Int128" data-throw-if-not-resolved="false"></xref> or <xref href="System.UInt128" data-throw-if-not-resolved="false"></xref>, which no ADO.NET provider carries.

### AddValueObjectHandlers\(params Assembly\[\]\) {#AdCodicem_ValueObjects_Dapper_ValueObjectDapper_AddValueObjectHandlers_System_Reflection_Assembly___}

Registers a handler for every value object declared in the given assemblies.

```csharp
[RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
public static void AddValueObjectHandlers(params Assembly[] assemblies)
```

#### Parameters

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring the value objects. When none is given, everything already registered is handled.

#### Remarks

A value object Dapper already has a handler for keeps it, whoever registered it, so calling this again
changes nothing and a handler of the application's own is not replaced. Which types are handled is read
from Dapper's table rather than remembered here, so a call after <xref href="Dapper.SqlMapper.ResetTypeHandlers" data-throw-if-not-resolved="false"></xref>
registers everything again.

<p>
A value object over <xref href="System.Int128" data-throw-if-not-resolved="false"></xref> or <xref href="System.UInt128" data-throw-if-not-resolved="false"></xref> gets no handler. No ADO.NET provider takes
either as a parameter or returns one, so the column type, and the conversion to it, are the application's
to choose, in a handler of its own.
</p>
<p>
A generic value object has no handler until its constructions are known, and Dapper looks a handler up by the
exact type, ahead of any query: register each construction with
<xref href="AdCodicem.ValueObjects.Dapper.ValueObjectDapper.AddValueObjectHandler%60%602" data-throw-if-not-resolved="false"></xref>.
</p>
<p>
Each handler is closed over its value object at compile time, through the type arguments the descriptor hands
back to a visitor (<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>), never at
run time with <xref href="System.Type.MakeGenericType(System.Type%5b%5d)" data-throw-if-not-resolved="false"></xref>, so building them takes no dynamic code. Locating the
generated registration of an assembly given by name reads its metadata, which trimming may remove. Dapper itself
is not compatible with native AOT: it files each handler in a cache it closes over the type at run time, and reads
rows through code it emits.
</p>

