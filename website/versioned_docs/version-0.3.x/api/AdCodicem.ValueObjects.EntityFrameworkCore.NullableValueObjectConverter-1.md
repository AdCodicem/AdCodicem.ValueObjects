# Class NullableValueObjectConverter<TSelf\> {#AdCodicem_ValueObjects_EntityFrameworkCore_NullableValueObjectConverter_1}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

Stores an optional value object over <xref href="System.String" data-throw-if-not-resolved="false"></xref> as its bare text, and a value it rejects as <code>NULL</code>.

```csharp
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "A compiled model calls the conversions on the converter type it was built from.")]
public sealed class NullableValueObjectConverter<TSelf> : ValueConverter<TSelf?, string?> where TSelf : struct, IValueObject<TSelf, string>
```

#### Type Parameters

`TSelf` 

Value object type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueConverter](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter) ← 
[ValueConverter<TSelf?, string?\>](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2) ← 
[NullableValueObjectConverter<TSelf\>](AdCodicem.ValueObjects.EntityFrameworkCore.NullableValueObjectConverter\-1.md)

#### Inherited Members

[ValueConverter<TSelf?, string?\>.ConvertToProvider](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.converttoprovider), 
[ValueConverter<TSelf?, string?\>.ConvertFromProvider](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.convertfromprovider), 
[ValueConverter<TSelf?, string?\>.ConvertToProviderTyped](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.converttoprovidertyped), 
[ValueConverter<TSelf?, string?\>.ConvertFromProviderTyped](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.convertfromprovidertyped), 
[ValueConverter<TSelf?, string?\>.ConvertToProviderExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.converttoproviderexpression), 
[ValueConverter<TSelf?, string?\>.ConvertFromProviderExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.convertfromproviderexpression), 
[ValueConverter<TSelf?, string?\>.ModelClrType](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.modelclrtype), 
[ValueConverter<TSelf?, string?\>.ProviderClrType](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.providerclrtype), 
[ValueConverter<TSelf?, string?\>.ConstructorExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.constructorexpression), 
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
The convention maps a <code>TSelf?</code> property through it, as it maps one over a value type through
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.NullableValueObjectConverter%602" data-throw-if-not-resolved="false"></xref>, which it writes and reads as. The value object it
rejects is an instance equal to <code>default(TSelf)</code>, whose text is empty.
</p>
<p>
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.StrictNullableValueObjectConverter%601" data-throw-if-not-resolved="false"></xref> validates what it reads.
</p>

## Constructors

### NullableValueObjectConverter\(\) {#AdCodicem_ValueObjects_EntityFrameworkCore_NullableValueObjectConverter_1__ctor}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.NullableValueObjectConverter%601" data-throw-if-not-resolved="false"></xref> class.

```csharp
public NullableValueObjectConverter()
```

