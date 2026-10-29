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
<xref href="AdCodicem.ValueObjects.IValueObjectPatternValidator" data-throw-if-not-resolved="false"></xref>, <xref href="AdCodicem.ValueObjects.IValueObjectMinimum%601" data-throw-if-not-resolved="false"></xref>,
<xref href="AdCodicem.ValueObjects.IValueObjectMaximum%601" data-throw-if-not-resolved="false"></xref>, <xref href="AdCodicem.ValueObjects.IValueObjectValidator%601" data-throw-if-not-resolved="false"></xref>,
<xref href="AdCodicem.ValueObjects.IValueObjectFormatter%601" data-throw-if-not-resolved="false"></xref>, <xref href="AdCodicem.ValueObjects.IValueObjectStringFormatter%601" data-throw-if-not-resolved="false"></xref> and
<xref href="AdCodicem.ValueObjects.IValueObjectExample%601" data-throw-if-not-resolved="false"></xref>. All are optional, and a rule written without its interface is reported as
<code>VO0011</code> rather than silently ignored. Its known values are static members it declares, marked
<xref href="AdCodicem.ValueObjects.Annotations.KnownValueAttribute" data-throw-if-not-resolved="false"></xref>.
</p>
<p>
Declarative constraints set on this attribute (<xref href="AdCodicem.ValueObjects.Annotations.ValueObjectAttribute%601.MinLength" data-throw-if-not-resolved="false"></xref>, <xref href="AdCodicem.ValueObjects.Annotations.ValueObjectAttribute%601.MaxLength" data-throw-if-not-resolved="false"></xref>), like the pattern
of <xref href="AdCodicem.ValueObjects.IValueObjectPatternValidator" data-throw-if-not-resolved="false"></xref> and the bounds of <xref href="AdCodicem.ValueObjects.IValueObjectMinimum%601" data-throw-if-not-resolved="false"></xref> and
<xref href="AdCodicem.ValueObjects.IValueObjectMaximum%601" data-throw-if-not-resolved="false"></xref>, are checked before <xref href="AdCodicem.ValueObjects.IValueObjectValidator%601" data-throw-if-not-resolved="false"></xref> runs, and
also feed the generated OpenAPI schema, so a rule is stated once and enforced everywhere. <xref href="AdCodicem.ValueObjects.Annotations.ValueObjectAttribute%601.Unicode" data-throw-if-not-resolved="false"></xref>,
which states how the text is stored rather than what the type accepts, is read by the Entity Framework Core
conventions and the Dapper handler alone.
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
for a value object whose default state is meaningful, such as a sequence number starting at zero. They are
reported where <code>default</code> or <code>new</code> is written: a parameter defaulting to <code>default</code> on its
declaration, not at each call that leaves the argument out. The same holds in the code another source
generator writes, such as a Riok.Mapperly mapping, where they are <code>VO0032</code> rather than <code>VO0010</code>.

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

Was an example value surfaced in the OpenAPI schema, written as text. Removed: implement
<xref href="AdCodicem.ValueObjects.IValueObjectExample%601" data-throw-if-not-resolved="false"></xref> instead.

```csharp
[Obsolete("Implement IValueObjectExample<TSelf> with a static Example property of the value object's type instead. Example is no longer read, and any minor version may remove it before 1.0.0.", true, DiagnosticId = "VO0035")]
public string? Example { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

The text was parsed at compile time and again at run time. Setting it is <code>VO0035</code>, a compile error, and
nothing reads it: <xref href="AdCodicem.ValueObjects.IValueObjectExample%601" data-throw-if-not-resolved="false"></xref> declares the example as an instance of the value
object. Any minor version may remove it before 1.0.0.

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

Was the inclusive upper bound, written as text. Removed: implement <xref href="AdCodicem.ValueObjects.IValueObjectMaximum%601" data-throw-if-not-resolved="false"></xref>
instead.

```csharp
[Obsolete("Implement IValueObjectMaximum<T> with a static Maximum property of the underlying type instead. Maximum is no longer read, and any minor version may remove it before 1.0.0.", true, DiagnosticId = "VO0028")]
public string? Maximum { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

Setting it is <code>VO0028</code>, a compile error, and nothing reads it, as for <xref href="AdCodicem.ValueObjects.Annotations.ValueObjectAttribute%601.Minimum" data-throw-if-not-resolved="false"></xref>. Any minor
version may remove it before 1.0.0.

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

Was the inclusive lower bound, written as text. Removed: implement <xref href="AdCodicem.ValueObjects.IValueObjectMinimum%601" data-throw-if-not-resolved="false"></xref>
instead.

```csharp
[Obsolete("Implement IValueObjectMinimum<T> with a static Minimum property of the underlying type instead. Minimum is no longer read, and any minor version may remove it before 1.0.0.", true, DiagnosticId = "VO0028")]
public string? Minimum { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

The text was read under a grammar of one form per underlying type, which the compiler could not check. Setting
it is <code>VO0028</code>, a compile error, and nothing reads it: <xref href="AdCodicem.ValueObjects.IValueObjectMinimum%601" data-throw-if-not-resolved="false"></xref> declares
the bound as a value of the underlying type. Any minor version may remove it before 1.0.0.

### Pattern {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_Pattern}

Was a regular expression the normalized value had to match. Removed: implement
<xref href="AdCodicem.ValueObjects.IValueObjectPatternValidator" data-throw-if-not-resolved="false"></xref> instead.

```csharp
[Obsolete("Implement IValueObjectPatternValidator with a [GeneratedRegex] partial property instead. Pattern is no longer read, and any minor version may remove it before 1.0.0.", true, DiagnosticId = "VO0021")]
[StringSyntax("Regex")]
public string? Pattern { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

The option compiled its regular expression at run time, which native AOT interprets. Setting it is
<code>VO0021</code>, a compile error, and nothing reads it: <xref href="AdCodicem.ValueObjects.IValueObjectPatternValidator" data-throw-if-not-resolved="false"></xref> takes a
<code>[GeneratedRegex]</code> the author writes, which the regex generator compiles. Any minor version may remove it
before 1.0.0.

### SchemaFormat {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_SchemaFormat}

Gets or sets the value of the OpenAPI <code>format</code> keyword for the generated schema.

```csharp
public string? SchemaFormat { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

<p>
Defaults to the natural format of the underlying type, such as <code>uuid</code>, <code>date</code>, <code>date-time</code> for a
<xref href="System.DateTimeOffset" data-throw-if-not-resolved="false"></xref>, or <code>int64</code>. Three types have none, and are documented with the pattern of the
form they are written in instead. A <xref href="System.TimeSpan" data-throw-if-not-resolved="false"></xref> is written in the invariant constant form
<code>[-][d.]hh:mm:ss[.fffffff]</code>, not the ISO 8601 form the <code>duration</code> format names. A
<xref href="System.TimeOnly" data-throw-if-not-resolved="false"></xref> and a <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref> are written without the offset RFC 3339 requires of the
<code>time</code> and <code>date-time</code> formats: a time of day never has one, and a <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref> of
<xref href="System.DateTimeKind.Unspecified" data-throw-if-not-resolved="false"></xref> is written without one.
</p>
<p>
Set it to <code>date-time</code> on a value object over <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref> whose normalizer guarantees a kind, such
as <xref href="System.DateTimeKind.Utc" data-throw-if-not-resolved="false"></xref>, which is written with its offset.
</p>

### Unicode {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_Unicode}

Gets or sets whether the text is stored as Unicode text; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a> stores and sends it as non-Unicode
text, <code>varchar</code> on SQL Server.

```csharp
public bool Unicode { get; set; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Remarks

Only meaningful for <xref href="System.String" data-throw-if-not-resolved="false"></xref> (<code>VO0008</code> otherwise). Defaults to <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a>. Read by the
Entity Framework Core conventions, which map the value object to non-Unicode text, and by the Dapper handler, which
declares its parameters non-Unicode; nothing else stores text that way. It states how the text is stored, not what
the type accepts: SQL Server replaces a character the column's code page lacks with a look-alike or <code>?</code>,
silently, so declare it for ASCII text and hold the rules to ASCII, as <code>VO0047</code> asks. Declared on a type
already mapped, it alters its columns at the next migration.

### ValueSet {#AdCodicem_ValueObjects_Annotations_ValueObjectAttribute_1_ValueSet}

Gets or sets whether the type accepts any valid value or only the members it marks <xref href="AdCodicem.ValueObjects.Annotations.KnownValueAttribute" data-throw-if-not-resolved="false"></xref>.

```csharp
public ValueSetKind ValueSet { get; set; }
```

#### Property Value

 [ValueSetKind](AdCodicem.ValueObjects.Annotations.ValueSetKind.md)

