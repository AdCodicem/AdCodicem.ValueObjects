# Class NullableValueObjectConverter<TSelf, TValue\> {#AdCodicem_ValueObjects_EntityFrameworkCore_NullableValueObjectConverter_2}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

Stores an optional value object over a value type as its bare underlying value, and a value it rejects as
<code>NULL</code>.

```csharp
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "A compiled model calls the conversions on the converter type it was built from.")]
public sealed class NullableValueObjectConverter<TSelf, TValue> : ValueConverter<TSelf?, TValue?> where TSelf : struct, IValueObject<TSelf, TValue> where TValue : struct
```

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueConverter](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter) ← 
[ValueConverter<TSelf?, TValue?\>](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2) ← 
[NullableValueObjectConverter<TSelf, TValue\>](AdCodicem.ValueObjects.EntityFrameworkCore.NullableValueObjectConverter\-2.md)

#### Inherited Members

[ValueConverter<TSelf?, TValue?\>.ConvertToProvider](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.converttoprovider), 
[ValueConverter<TSelf?, TValue?\>.ConvertFromProvider](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.convertfromprovider), 
[ValueConverter<TSelf?, TValue?\>.ConvertToProviderTyped](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.converttoprovidertyped), 
[ValueConverter<TSelf?, TValue?\>.ConvertFromProviderTyped](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.convertfromprovidertyped), 
[ValueConverter<TSelf?, TValue?\>.ConvertToProviderExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.converttoproviderexpression), 
[ValueConverter<TSelf?, TValue?\>.ConvertFromProviderExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.convertfromproviderexpression), 
[ValueConverter<TSelf?, TValue?\>.ModelClrType](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.modelclrtype), 
[ValueConverter<TSelf?, TValue?\>.ProviderClrType](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.providerclrtype), 
[ValueConverter<TSelf?, TValue?\>.ConstructorExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.constructorexpression), 
[ValueConverter.ComposeWith\(ValueConverter?\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter.composewith), 
[ValueConverter.ConvertToProvider](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter.converttoprovider), 
[ValueConverter.ConvertFromProvider](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter.convertfromprovider), 
[ValueConverter.ConvertToProviderExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter.converttoproviderexpression), 
[ValueConverter.ConvertFromProviderExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter.convertfromproviderexpression), 
[ValueConverter.ModelClrType](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter.modelclrtype), 
[ValueConverter.ProviderClrType](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter.providerclrtype), 
[ValueConverter.MappingHints](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter.mappinghints), 
[ValueConverter.ConvertsNulls](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter.convertsnulls), 
[ValueConverter.ConstructorExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter.constructorexpression), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The convention maps a <code>TSelf?</code> property through it. Entity Framework Core answers a <code>null</code> itself, in both
directions; the converter sees a value object, which it stores as <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConverter%602" data-throw-if-not-resolved="false"></xref>
does, but for a value the value object rejects, which only an instance equal to <code>default(TSelf)</code> can hold: the
column takes a <code>NULL</code>, so that is what it stores, rather than a value every later read would trust. Over a
value type, a constructed zero equals the default too, and validation tells a valid zero, which is stored, from a
refused one.
</p>
<p>
Reading uses the trusted factory, as <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConverter%602" data-throw-if-not-resolved="false"></xref> does.
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.StrictNullableValueObjectConverter%602" data-throw-if-not-resolved="false"></xref> validates what it reads.
</p>

## Constructors

### NullableValueObjectConverter\(\) {#AdCodicem_ValueObjects_EntityFrameworkCore_NullableValueObjectConverter_2__ctor}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.NullableValueObjectConverter%602" data-throw-if-not-resolved="false"></xref> class.

```csharp
public NullableValueObjectConverter()
```

