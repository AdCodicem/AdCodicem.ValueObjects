# Class StrictValueObjectConverter<TSelf, TValue\> {#AdCodicem_ValueObjects_EntityFrameworkCore_StrictValueObjectConverter_2}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

Stores a value object as its bare underlying value, and re-validates whatever comes back from the database.

```csharp
public sealed class StrictValueObjectConverter<TSelf, TValue> : ValueConverter<TSelf, TValue> where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueConverter](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter) ← 
[ValueConverter<TSelf, TValue\>](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2) ← 
[StrictValueObjectConverter<TSelf, TValue\>](AdCodicem.ValueObjects.EntityFrameworkCore.StrictValueObjectConverter\-2.md)

#### Inherited Members

[ValueConverter<TSelf, TValue\>.ConvertToProvider](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.converttoprovider), 
[ValueConverter<TSelf, TValue\>.ConvertFromProvider](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.convertfromprovider), 
[ValueConverter<TSelf, TValue\>.ConvertToProviderTyped](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.converttoprovidertyped), 
[ValueConverter<TSelf, TValue\>.ConvertFromProviderTyped](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.convertfromprovidertyped), 
[ValueConverter<TSelf, TValue\>.ConvertToProviderExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.converttoproviderexpression), 
[ValueConverter<TSelf, TValue\>.ConvertFromProviderExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.convertfromproviderexpression), 
[ValueConverter<TSelf, TValue\>.ModelClrType](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.modelclrtype), 
[ValueConverter<TSelf, TValue\>.ProviderClrType](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.providerclrtype), 
[ValueConverter<TSelf, TValue\>.ConstructorExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter\-2.constructorexpression), 
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

Pays normalization and validation on every materialized row. Worth it when the table is shared with another
writer — a legacy application, an ETL job, a migration script — and a row may therefore hold a value the
domain would refuse.

## Constructors

### StrictValueObjectConverter\(\) {#AdCodicem_ValueObjects_EntityFrameworkCore_StrictValueObjectConverter_2__ctor}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.StrictValueObjectConverter%602" data-throw-if-not-resolved="false"></xref> class.

```csharp
public StrictValueObjectConverter()
```

