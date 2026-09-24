# Class ValueObjectAttribute<TValue\> {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1}

Namespace: [AdCodicem.ValueObjects.Annotations](AdCodicem.ValueObjects.Annotations.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Marks a <code>readonly partial struct</code> as a single-value value object and drives code generation for it.

```csharp
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ValueObjectAttribute<TValue> : Attribute
```

#### Type Parameters

`TValue` 

Underlying value type. Supported types are <xref href="System.String" data-throw-if-not-resolved="false"></xref>, <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>, <xref href="System.Boolean" data-throw-if-not-resolved="false"></xref>,
<xref href="System.Char" data-throw-if-not-resolved="false"></xref>, every built-in integer type, <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, <xref href="System.Double" data-throw-if-not-resolved="false"></xref>,
<xref href="System.Single" data-throw-if-not-resolved="false"></xref>, <xref href="System.DateOnly" data-throw-if-not-resolved="false"></xref>, <xref href="System.TimeOnly" data-throw-if-not-resolved="false"></xref>, <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref>,
<xref href="System.DateTimeOffset" data-throw-if-not-resolved="false"></xref> and <xref href="System.TimeSpan" data-throw-if-not-resolved="false"></xref>.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[Attribute](https://learn.microsoft.com/dotnet/api/system.attribute) ← 
[ValueObjectAttribute<TValue\>](AdCodicem.ValueObjects.Annotations.ValueObjectAttribute\-1.md)

#### Inherited Members

[Attribute.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.attribute.equals), 
[Attribute.GetCustomAttribute\(Assembly, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-assembly\-system\-type\)), 
[Attribute.GetCustomAttribute\(Assembly, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-assembly\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttribute\(MemberInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-memberinfo\-system\-type\)), 
[Attribute.GetCustomAttribute\(MemberInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-memberinfo\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttribute\(Module, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-module\-system\-type\)), 
[Attribute.GetCustomAttribute\(Module, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-module\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttribute\(ParameterInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-parameterinfo\-system\-type\)), 
[Attribute.GetCustomAttribute\(ParameterInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-parameterinfo\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(Assembly\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-assembly\)), 
[Attribute.GetCustomAttributes\(Assembly, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-assembly\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(Assembly, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-assembly\-system\-type\)), 
[Attribute.GetCustomAttributes\(Assembly, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-assembly\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(MemberInfo\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-memberinfo\)), 
[Attribute.GetCustomAttributes\(MemberInfo, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-memberinfo\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(MemberInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-memberinfo\-system\-type\)), 
[Attribute.GetCustomAttributes\(MemberInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-memberinfo\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(Module\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-module\)), 
[Attribute.GetCustomAttributes\(Module, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-module\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(Module, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-module\-system\-type\)), 
[Attribute.GetCustomAttributes\(Module, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-module\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(ParameterInfo\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-parameterinfo\)), 
[Attribute.GetCustomAttributes\(ParameterInfo, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-parameterinfo\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(ParameterInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-parameterinfo\-system\-type\)), 
[Attribute.GetCustomAttributes\(ParameterInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-parameterinfo\-system\-type\-system\-boolean\)), 
[Attribute.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.attribute.gethashcode), 
[Attribute.IsDefaultAttribute\(\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefaultattribute), 
[Attribute.IsDefined\(Assembly, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-assembly\-system\-type\)), 
[Attribute.IsDefined\(Assembly, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-assembly\-system\-type\-system\-boolean\)), 
[Attribute.IsDefined\(MemberInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-memberinfo\-system\-type\)), 
[Attribute.IsDefined\(MemberInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-memberinfo\-system\-type\-system\-boolean\)), 
[Attribute.IsDefined\(Module, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-module\-system\-type\)), 
[Attribute.IsDefined\(Module, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-module\-system\-type\-system\-boolean\)), 
[Attribute.IsDefined\(ParameterInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-parameterinfo\-system\-type\)), 
[Attribute.IsDefined\(ParameterInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-parameterinfo\-system\-type\-system\-boolean\)), 
[Attribute.Match\(object?\)](https://learn.microsoft.com/dotnet/api/system.attribute.match), 
[Attribute.TypeId](https://learn.microsoft.com/dotnet/api/system.attribute.typeid), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The declaring type opts into a rule by implementing the interface that declares it, so the compiler checks
its signature: <xref href="AdCodicem.ValueObjects.IValueObjectNormalizer%601" data-throw-if-not-resolved="false"></xref>, <xref href="AdCodicem.ValueObjects.IValueObjectSpanNormalizer" data-throw-if-not-resolved="false"></xref>,
<xref href="AdCodicem.ValueObjects.IValueObjectValidator%601" data-throw-if-not-resolved="false"></xref>, <xref href="AdCodicem.ValueObjects.IValueObjectFormatter%601" data-throw-if-not-resolved="false"></xref> and
<xref href="AdCodicem.ValueObjects.IValueObjectStringFormatter%601" data-throw-if-not-resolved="false"></xref>. All are optional, and a rule written without its
interface is reported as <code>VO0011</code> rather than silently ignored.
</p>
<p>
Declarative constraints set on this attribute (<xref href="AdCodicem.ValueObjects.Annotations.ValueObjectAttribute%601.Pattern" data-throw-if-not-resolved="false"></xref>, <xref href="AdCodicem.ValueObjects.Annotations.ValueObjectAttribute%601.MinLength" data-throw-if-not-resolved="false"></xref>,
<xref href="AdCodicem.ValueObjects.Annotations.ValueObjectAttribute%601.Minimum" data-throw-if-not-resolved="false"></xref>) are checked before any of those rules run, and also feed the generated OpenAPI
schema, so a rule is stated once and enforced everywhere.
</p>

## Properties

### AllowDefault {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_AllowDefault}

Gets or sets a value indicating whether the analyzer tolerates <code>default</code> and parameterless construction.

```csharp
public bool AllowDefault { get; set; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Remarks

Those expressions produce an instance that never went through validation. They are reported as errors by
the analyzers shipped with <code>AdCodicem.ValueObjects</code> unless this is set, which is occasionally needed
for a value object whose default state is meaningful, such as a sequence number starting at zero.

### AllowEmpty {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_AllowEmpty}

Gets or sets a value indicating whether an empty string is accepted.

```csharp
public bool AllowEmpty { get; set; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Remarks

Only meaningful for <xref href="System.String" data-throw-if-not-resolved="false"></xref>. A <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> value is always rejected: a value
object that may be absent is expressed as a nullable value object, never as one wrapping <code>null</code>.

### Arithmetic {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_Arithmetic}

Gets or sets a value indicating whether arithmetic operators and <xref href="AdCodicem.ValueObjects.INumericValueObject%602" data-throw-if-not-resolved="false"></xref> are generated.

```csharp
public bool Arithmetic { get; set; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Remarks

Requires a numeric underlying type. Every result is re-validated.

### Comparison {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_Comparison}

Gets or sets how two values are compared for equality, ordering and hashing.

```csharp
public StringComparison Comparison { get; set; }
```

#### Property Value

 [StringComparison](https://learn.microsoft.com/dotnet/api/system.stringcomparison)

#### Remarks

Only meaningful when the underlying type is <xref href="System.String" data-throw-if-not-resolved="false"></xref>. Defaults to
<xref href="System.StringComparison.Ordinal" data-throw-if-not-resolved="false"></xref>: culture-independent, the fastest option, and the only one an
EF Core provider can translate faithfully. Choose <xref href="System.StringComparison.OrdinalIgnoreCase" data-throw-if-not-resolved="false"></xref> when
the value is not case-normalized, and make sure the database collation agrees.

### Description {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_Description}

Gets or sets the description surfaced in the OpenAPI schema.

```csharp
public string? Description { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

Defaults to the XML documentation summary of the declaring type when it is available.

### Example {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_Example}

Gets or sets an example value surfaced in the OpenAPI schema.

```csharp
public string? Example { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### ExplicitConversionFromValue {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_ExplicitConversionFromValue}

Gets or sets a value indicating whether an explicit conversion from the underlying value is generated.

```csharp
public bool ExplicitConversionFromValue { get; set; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Remarks

The conversion validates, and throws <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> on a rejected value.

### ImplicitConversionToValue {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_ImplicitConversionToValue}

Gets or sets a value indicating whether an implicit conversion to the underlying value is generated.

```csharp
public bool ImplicitConversionToValue { get; set; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Remarks

Reading stays terse (<code>string s = iban;</code>) while construction remains explicit and validated.

### MaxLength {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_MaxLength}

Gets or sets the maximum accepted length. A negative value means unconstrained.

```csharp
public int MaxLength { get; set; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

#### Remarks

Only meaningful for <xref href="System.String" data-throw-if-not-resolved="false"></xref>. Emitted as the <code>maxLength</code> OpenAPI keyword and used by the
EF Core integration to size the column, so the value object maps to a bounded column rather than an
unbounded one.

### Maximum {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_Maximum}

Gets or sets the inclusive upper bound, written in invariant culture.

```csharp
public string? Maximum { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

Expressed as text so that <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, <xref href="System.DateOnly" data-throw-if-not-resolved="false"></xref> or <xref href="System.TimeSpan" data-throw-if-not-resolved="false"></xref> bounds
keep full precision; attribute arguments cannot carry those types. Parsed at compile time and reported
as a diagnostic when malformed. Also emitted as the <code>minimum</code> OpenAPI keyword.

### MinLength {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_MinLength}

Gets or sets the minimum accepted length. A negative value means unconstrained.

```csharp
public int MinLength { get; set; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

#### Remarks

Only meaningful for <xref href="System.String" data-throw-if-not-resolved="false"></xref>. Also emitted as the <code>minLength</code> OpenAPI keyword.

### Minimum {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_Minimum}

Gets or sets the inclusive lower bound, written in invariant culture.

```csharp
public string? Minimum { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

Expressed as text so that <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, <xref href="System.DateOnly" data-throw-if-not-resolved="false"></xref> or <xref href="System.TimeSpan" data-throw-if-not-resolved="false"></xref> bounds
keep full precision; attribute arguments cannot carry those types. Parsed at compile time and reported
as a diagnostic when malformed. Also emitted as the <code>minimum</code> OpenAPI keyword.

### Pattern {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_Pattern}

Gets or sets a regular expression the normalized value must match.

```csharp
[StringSyntax("Regex")]
public string? Pattern { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

Compiled once into a static <code>Regex</code> with <code>RegexOptions.Compiled</code>: one source generator cannot
see another's output, so <code>[GeneratedRegex]</code> is not reachable from emitted code. Also emitted as the
<code>pattern</code> keyword of the OpenAPI schema.

### SchemaFormat {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_SchemaFormat}

Gets or sets the value of the OpenAPI <code>format</code> keyword for the generated schema.

```csharp
public string? SchemaFormat { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

Defaults to the natural format of the underlying type, such as <code>uuid</code>, <code>date</code> or <code>int64</code>.

### ValueSet {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_ValueSet}

Gets or sets whether the type accepts any valid value or only the declared <xref href="AdCodicem.ValueObjects.Annotations.KnownValueAttribute" data-throw-if-not-resolved="false"></xref> ones.

```csharp
public ValueSetKind ValueSet { get; set; }
```

#### Property Value

 [ValueSetKind](AdCodicem.ValueObjects.Annotations.ValueSetKind.md)

