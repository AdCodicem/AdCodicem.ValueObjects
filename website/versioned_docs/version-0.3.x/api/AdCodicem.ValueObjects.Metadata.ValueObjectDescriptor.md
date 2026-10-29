# Class ValueObjectDescriptor {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Runtime description of a value object type, used by the integrations that only know a <xref href="System.Type" data-throw-if-not-resolved="false"></xref>.

```csharp
public sealed class ValueObjectDescriptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The delegates it exposes operate on boxed values, which is acceptable because every one of them is invoked
during start-up work — building the EF Core model, generating an OpenAPI document, registering a Dapper
type handler — never on a per-request path. Request paths go through the strongly typed generic APIs.
</p>
<p>
Descriptors are produced by <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.For%60%602(AdCodicem.ValueObjects.Metadata.ValueObjectSchema%2cSystem.Func%7bSystem.Text.Json.Serialization.JsonConverter%7b%60%600%7d%7d)" data-throw-if-not-resolved="false"></xref>, which is fully generic and therefore free
of reflection: the source generator emits one call per value object in a module initializer.
</p>
<p>
A caller that needs the type arguments themselves, to close an adapter of its own over the value object, gets them
back through <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>, again without reflection.
</p>

## Properties

### Create {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_Create}

Gets the validating factory. Throws <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> on a rejected value.

```csharp
public Func<object?, object> Create { get; }
```

#### Property Value

 [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<[object](https://learn.microsoft.com/dotnet/api/system.object)?, [object](https://learn.microsoft.com/dotnet/api/system.object)\>

#### Remarks

A <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> is rejected as <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref> whatever the
underlying type: a string value object rejects it through its own rule, and a value-typed underlying value
has no null to accept.

### CreateUnchecked {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_CreateUnchecked}

Gets the trusted-source factory, which performs neither normalization nor validation.

```csharp
public Func<object?, object> CreateUnchecked { get; }
```

#### Property Value

 [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<[object](https://learn.microsoft.com/dotnet/api/system.object)?, [object](https://learn.microsoft.com/dotnet/api/system.object)\>

### Format {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_Format}

Gets the invariant text representation of a boxed value object.

```csharp
public Func<object, string> Format { get; }
```

#### Property Value

 [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<[object](https://learn.microsoft.com/dotnet/api/system.object), [string](https://learn.microsoft.com/dotnet/api/system.string)\>

#### Remarks

It takes a boxed instance of <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.ValueObjectType" data-throw-if-not-resolved="false"></xref>, never <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>: the
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> of a nullable value object throws a <xref href="System.NullReferenceException" data-throw-if-not-resolved="false"></xref> here. A caller
holding a <code>TSelf?</code> handles its <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> first.

### GetValue {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_GetValue}

Gets the accessor returning the boxed underlying value of a boxed value object.

```csharp
public Func<object, object?> GetValue { get; }
```

#### Property Value

 [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<[object](https://learn.microsoft.com/dotnet/api/system.object), [object](https://learn.microsoft.com/dotnet/api/system.object)?\>

#### Remarks

It takes a boxed instance of <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.ValueObjectType" data-throw-if-not-resolved="false"></xref>, never <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>: the
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> of a nullable value object throws a <xref href="System.NullReferenceException" data-throw-if-not-resolved="false"></xref> here. A caller
holding a <code>TSelf?</code> handles its <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> first.

### JsonConverter {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_JsonConverter}

Gets the System.Text.Json converter of the value object, when its registration supplied one.

```csharp
public JsonConverter? JsonConverter { get; }
```

#### Property Value

 [JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter)?

#### Remarks

The generator emits a converter for every value object and registers how to create it here, whatever the
declaring assembly references. <code>ValueObjectJsonConverterFactory</code> hands it to a source-generated serializer
context, whose generator never sees the <code>[JsonConverter]</code> attribute on the type, so that no converter has to
be built by reflection, which trimming and native AOT do not support. It is created the first time it is asked
for, and the same instance is handed out from then on.

### Schema {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_Schema}

Gets the declarative constraints of the value object.

```csharp
public ValueObjectSchema Schema { get; }
```

#### Property Value

 [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

### TryCreate {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_TryCreate}

Gets the non-throwing validating factory.

```csharp
public BoxedTryCreate TryCreate { get; }
```

#### Property Value

 [BoxedTryCreate](AdCodicem.ValueObjects.Metadata.BoxedTryCreate.md)

#### Remarks

A <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> is rejected as <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref> whatever the
underlying type, as it is by <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Create" data-throw-if-not-resolved="false"></xref>.

### TryParse {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_TryParse}

Gets the non-throwing text parser.

```csharp
public BoxedTryParse TryParse { get; }
```

#### Property Value

 [BoxedTryParse](AdCodicem.ValueObjects.Metadata.BoxedTryParse.md)

### ValidateWrite {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_ValidateWrite}

Gets the check a boundary runs on a boxed value object before it writes it: the outcome of the first rule the
value it holds violates, or <xref href="AdCodicem.ValueObjects.ValidationResult.Success" data-throw-if-not-resolved="false"></xref>.

```csharp
public Func<object, ValidationResult> ValidateWrite { get; }
```

#### Property Value

 [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<[object](https://learn.microsoft.com/dotnet/api/system.object), [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)\>

#### Remarks

<p>
A writer refuses what fails it, rather than write a value its type rejects, which a reader would then trust:
the Newtonsoft.Json converter throws, as the generated System.Text.Json converter, the Dapper handler and the
Entity Framework Core converters do for a value object they know by its type.
</p>
<p>
Only an instance equal to <code>default(TSelf)</code> is validated, as its value stands: it is the one an instance that
never went through <code>Create</code> holds. Over a value type, a constructed zero equals the default too, and
validation tells a valid zero from a refused one. Any other instance passes without being validated again.
</p>
<p>
It takes a boxed instance, never <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>, as <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.GetValue" data-throw-if-not-resolved="false"></xref> does.
</p>

### ValueObjectType {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_ValueObjectType}

Gets the value object type.

```csharp
public Type ValueObjectType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)

### ValueType {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_ValueType}

Gets the underlying value type.

```csharp
public Type ValueType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)

## Methods

### Accept<TResult\>\(IValueObjectVisitor<TResult\>\) {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_Accept__1_AdCodicem_ValueObjects_Metadata_IValueObjectVisitor___0__}

Hands the type arguments the descriptor was built with to a visitor, which closes its own generic code over them.

```csharp
public TResult Accept<TResult>(IValueObjectVisitor<TResult> visitor)
```

#### Parameters

`visitor` [IValueObjectVisitor](AdCodicem.ValueObjects.Metadata.IValueObjectVisitor\-1.md)<TResult\>

The visitor, whose <xref href="AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%601.Visit%60%602" data-throw-if-not-resolved="false"></xref> is called
     with <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.ValueObjectType" data-throw-if-not-resolved="false"></xref> and <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.ValueType" data-throw-if-not-resolved="false"></xref>.

#### Returns

 TResult

What the visitor returned.

#### Type Parameters

`TResult` 

What the visitor hands back.

#### Remarks

<p>
This is the typed way out of a descriptor. An integration that finds value objects by <xref href="System.Type" data-throw-if-not-resolved="false"></xref> creates its
adapter in the visitor, closed at compile time, where <xref href="System.Type.MakeGenericType(System.Type%5b%5d)" data-throw-if-not-resolved="false"></xref> would close it at
run time, which native AOT cannot do for a struct:
</p>

<pre><code class="lang-csharp">sealed class FormatterFor : IValueObjectVisitor&lt;IFormatter&gt;
{
    public IFormatter Visit&lt;TSelf, TValue&gt;() where TSelf : struct, IValueObject&lt;TSelf, TValue&gt;
        =&gt; new ValueObjectFormatter&lt;TSelf, TValue&gt;();
}

var formatter = descriptor.Accept(new FormatterFor());</code></pre>

<p>
Every descriptor carries its type arguments, whoever built it: the generated registration, a registration by hand,
or <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryResolve(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref> describing a type by reflection, whose descriptor only the JIT can
visit. A descriptor built for a value object over <xref href="System.Int128" data-throw-if-not-resolved="false"></xref> is visited like any other: a visitor
skips what its host cannot carry.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">visitor</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### For<TSelf, TValue\>\(ValueObjectSchema\) {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_For__2_AdCodicem_ValueObjects_Metadata_ValueObjectSchema_}

Builds a descriptor for a value object, resolving every operation statically.

```csharp
public static ValueObjectDescriptor For<TSelf, TValue>(ValueObjectSchema schema) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`schema` [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

Declarative constraints of the value object.

#### Returns

 [ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)

The descriptor.

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

### For<TSelf, TValue\>\(ValueObjectSchema, Func<JsonConverter<TSelf\>\>?\) {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_For__2_AdCodicem_ValueObjects_Metadata_ValueObjectSchema_System_Func_System_Text_Json_Serialization_JsonConverter___0___}

Builds a descriptor for a value object carrying its System.Text.Json converter, resolving every operation
statically.

```csharp
public static ValueObjectDescriptor For<TSelf, TValue>(ValueObjectSchema schema, Func<JsonConverter<TSelf>>? jsonConverter) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`schema` [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

Declarative constraints of the value object.

`jsonConverter` [Func](https://learn.microsoft.com/dotnet/api/system.func\-1)<[JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1)<TSelf\>\>?

Creates the converter of the value object, the first time <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.JsonConverter" data-throw-if-not-resolved="false"></xref> is read, or
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when its registration supplies none.

#### Returns

 [ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)

The descriptor.

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

