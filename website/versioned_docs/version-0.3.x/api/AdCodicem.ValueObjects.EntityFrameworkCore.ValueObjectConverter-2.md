# Class ValueObjectConverter<TSelf, TValue\> {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectConverter_2}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

Stores a value object as its bare underlying value.

```csharp
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "A compiled model calls the conversions on the converter type it was built from.")]
public sealed class ValueObjectConverter<TSelf, TValue> : ValueConverter<TSelf, TValue> where TSelf : struct, IValueObject<TSelf, TValue>
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
[ValueObjectConverter<TSelf, TValue\>](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConverter\-2.md)

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

<p>
Reading uses the trusted factory: no normalization, no validation, no allocation beyond the value itself.
Materializing an entity is the hottest path in most applications, and the rows being read were written by
this same application through the validating factory. Use <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.StrictValueObjectConverter%602" data-throw-if-not-resolved="false"></xref>
when the table is also written to by something else.
</p>
<p>
Writing refuses a value the value object rejects, with a <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref>, rather than store one
that every later read would trust. Only an instance equal to <code>default(TSelf)</code> can hold one, since any other
went through <code>Create</code>, so only that one is validated: over a value type, a constructed zero equals the default
too, and validation tells a valid zero, which is stored, from a refused one. A property holding a <code>TSelf?</code> is
mapped through <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.NullableValueObjectConverter%602" data-throw-if-not-resolved="false"></xref> or
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.NullableValueObjectConverter%601" data-throw-if-not-resolved="false"></xref> instead, which stores such a value as <code>NULL</code>.
</p>
<p>
Both directions go through static helpers rather than inline lambdas, because an expression tree cannot
contain a call to a static abstract interface member. The helpers are public, out of sight of IntelliSense: the
code of a compiled model, which <code>dotnet ef dbcontext optimize</code> writes into the application's assembly, calls
them there, as it calls whatever a converter calls when it is generated for native AOT.
</p>

## Constructors

### ValueObjectConverter\(\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectConverter_2__ctor}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConverter%602" data-throw-if-not-resolved="false"></xref> class.

```csharp
public ValueObjectConverter()
```

