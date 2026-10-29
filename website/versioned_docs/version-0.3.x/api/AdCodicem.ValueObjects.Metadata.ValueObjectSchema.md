# Class ValueObjectSchema {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

The declarative constraints of a value object, captured once at compile time.

```csharp
public sealed record ValueObjectSchema : IEquatable<ValueObjectSchema>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

#### Implements

[IEquatable<ValueObjectSchema\>](https://learn.microsoft.com/dotnet/api/system.iequatable\-1)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The generator fills this from the <code>ValueObjectAttribute</code> so that the rules enforced by
<code>Validate</code> and the rules published in the OpenAPI schema or applied to an EF Core column can never
drift apart: they all read the same instance.
</p>
<p>
That instance is the value object's <xref href="AdCodicem.ValueObjects.IValueObject%602.Schema" data-throw-if-not-resolved="false"></xref>, which generic code reads through
its type parameter and the registration hands the descriptor. A value object written by hand builds its own, stating
the rules its <code>Validate</code> enforces.
</p>

## Properties

### Comparison {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Comparison}

Gets how the value object compares two values of its text, if the schema states it.

```csharp
public StringComparison? Comparison { get; init; }
```

#### Property Value

 [StringComparison](https://learn.microsoft.com/dotnet/api/system.stringcomparison)?

#### Remarks

<p>
The generator states here the <code>Comparison</code> of the <code>ValueObjectAttribute</code> on every value object over
text, <xref href="System.StringComparison.Ordinal" data-throw-if-not-resolved="false"></xref> where the attribute declares none, and leaves it
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> on a value object over any other type. Equality, ordering and hashing compare this way, and
so does the lookup of a closed set's <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema.KnownValues" data-throw-if-not-resolved="false"></xref>.
</p>
<p>
A check written from the known values alone and compared exactly, the <code>enum</code> of the MongoDB collection
validator or the <code>IN</code> of an Entity Framework Core CHECK constraint, reads it: a closed set compared
<xref href="System.StringComparison.Ordinal" data-throw-if-not-resolved="false"></xref> stores each value as its known value is spelled and gets one; a set compared
as a culture compares may store another spelling of a known value, longer, shorter or of the same length, and gets
none; a set compared <xref href="System.StringComparison.OrdinalIgnoreCase" data-throw-if-not-resolved="false"></xref> gets one when its normalizer spells every
other case back.
</p>
<p>
A schema built by hand states the comparison its <code>Validate</code> looks the known values up with. Left
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>, those checks try the spellings a comparison ignoring case, or a culture's ignoring a soft
hyphen, would take for a known value, and write one when the type stores none of them; a culture's comparison
takes other spellings still, so a schema over such a set states it.
</p>

### Description {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Description}

Gets the human-readable description of the value object, if any.

```csharp
public string? Description { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Example {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Example}

Gets an example value, boxed, if any.

```csharp
public object? Example { get; init; }
```

#### Property Value

 [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Remarks

The generator holds here the underlying value of the example the value object declares through
<xref href="AdCodicem.ValueObjects.IValueObjectExample%601" data-throw-if-not-resolved="false"></xref>, as <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema.KnownValues" data-throw-if-not-resolved="false"></xref> holds its known values, and the schemas
write it as the type writes it in JSON. A schema built by hand may hold the text of the value instead, which they
read in the invariant culture as the type parses it.

### Format {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Format}

Gets the OpenAPI <code>format</code> keyword for the value, if any.

```csharp
public string? Format { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### IsClosedValueSet {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_IsClosedValueSet}

Gets a value indicating whether only <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema.KnownValues" data-throw-if-not-resolved="false"></xref> are accepted.

```csharp
public bool IsClosedValueSet { get; init; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### IsUnicode {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_IsUnicode}

Gets whether the text is stored as Unicode text, if the schema states it: <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a> for a value
object declaring <code>Unicode = false</code>.

```csharp
public bool? IsUnicode { get; init; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)?

#### Remarks

The generator states <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a> where the declaration does, and leaves it <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>
otherwise, an entity identifier's included. The Entity Framework Core conventions map such a value object to
non-Unicode text, and the Dapper handler declares its parameters <code>DbType.AnsiString</code>; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a>
and <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> leave both to the provider. It is no rule: nothing validates the text against it. A
schema built by hand states it as the option would.

### KnownValueDetails {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_KnownValueDetails}

Gets the declared known values with the name and the description each was declared with, in declaration order.

```csharp
public ImmutableArray<KnownValueInfo> KnownValueDetails { get; init; }
```

#### Property Value

 [ImmutableArray](https://learn.microsoft.com/dotnet/api/system.collections.immutable.immutablearray\-1)<[KnownValueInfo](AdCodicem.ValueObjects.Metadata.KnownValueInfo.md)\>

#### Remarks

<p>
The generator lists here the values of <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema.KnownValues" data-throw-if-not-resolved="false"></xref>, in the same order, each the same value, with
the name of the static property it creates for it and the <code>Description</code> its attribute gives, if any. The
OpenAPI integration publishes the names of a closed value set beside its <code>enum</code>, as
<code>x-enum-varnames</code>, <code>x-enumNames</code> and <code>x-ms-enum</code>, which client generators name the members of
their enumeration after.
</p>
<p>
A schema built by hand fills it as well, listing the values of <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema.KnownValues" data-throw-if-not-resolved="false"></xref> in the same order, each
equal to its counterpart. The OpenAPI integration publishes no name otherwise, rather than one beside the wrong
value, and the contract kit of <code>AdCodicem.ValueObjects.Testing</code> fails a schema that leaves it empty beside
known values, or out of step with them.
</p>

### KnownValues {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_KnownValues}

Gets the declared known underlying values, boxed, in declaration order.

```csharp
public ImmutableArray<object> KnownValues { get; init; }
```

#### Property Value

 [ImmutableArray](https://learn.microsoft.com/dotnet/api/system.collections.immutable.immutablearray\-1)<[object](https://learn.microsoft.com/dotnet/api/system.object)\>

#### Remarks

Surfaced as the <code>enum</code> keyword of the OpenAPI schema. <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema.KnownValueDetails" data-throw-if-not-resolved="false"></xref> lists the same values
with their names and descriptions.

### MaxLength {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_MaxLength}

Gets the maximum accepted length, if any.

```csharp
public int? MaxLength { get; init; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)?

#### Remarks

Used by the EF Core integration to size the mapped column.

### Maximum {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Maximum}

Gets the inclusive upper bound in invariant culture, if any.

```csharp
public string? Maximum { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### MinLength {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_MinLength}

Gets the minimum accepted length, if any.

```csharp
public int? MinLength { get; init; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)?

### Minimum {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Minimum}

Gets the inclusive lower bound in invariant culture, if any.

```csharp
public string? Minimum { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Pattern {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Pattern}

Gets the regular expression the value must match, if any.

```csharp
public string? Pattern { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Unconstrained {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Unconstrained}

Gets the schema of a value object that declares no constraint.

```csharp
public static ValueObjectSchema Unconstrained { get; }
```

#### Property Value

 [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

