# Class ValueObjectScalarOptions {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarOptions}

Namespace: [AdCodicem.ValueObjects.HotChocolate](AdCodicem.ValueObjects.HotChocolate.md)  
Assembly: AdCodicem.ValueObjects.HotChocolate.dll  

Names, specifications and messages of the scalars <code>AddValueObjects()</code> gives value objects in one schema.

```csharp
public sealed class ValueObjectScalarOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectScalarOptions](AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
A scalar is named by the schema's naming conventions, as HotChocolate names the type it would otherwise infer:
<code>Iban</code>, <code>ReferenceOfOrder</code> for <code>Reference&lt;Order&gt;</code>, or the name a <code>[GraphQLName]</code> on the
struct gives. Two value objects of one name fail the schema at start, naming the second: <xref href="AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.Name%60%601(System.String)" data-throw-if-not-resolved="false"></xref>
names one from the composition root, and the name passed to <code>AddValueObjectScalar</code> wins over both.
</p>
<p>
Every call of <code>AddValueObjects()</code> on one schema configures these same options, in the order of the calls.
</p>

## Properties

### ResourceType {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarOptions_ResourceType}

Gets the type whose resources hold the messages of refused values, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to keep the English
messages.

```csharp
public Type? ResourceType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)?

## Methods

### Name<TSelf\>\(string\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarOptions_Name__1_System_String_}

Names the scalar of a value object, where two value objects share a name.

```csharp
public ValueObjectScalarOptions Name<TSelf>(string name) where TSelf : struct, IValueObject
```

#### Parameters

`name` [string](https://learn.microsoft.com/dotnet/api/system.string)

The name of its scalar, a GraphQL name.

#### Returns

 [ValueObjectScalarOptions](AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.md)

These options, so calls can be chained.

#### Type Parameters

`TSelf` 

The value object, or a construction of a generic one.

#### Remarks

HotChocolate refuses a name that is not a GraphQL name when it builds the schema.

#### Exceptions

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">name</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>, empty or white space.

### QuoteScalarNameIn\(Func<IInputValueDefinition, bool\>\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarOptions_QuoteScalarNameIn_System_Func_HotChocolate_Types_IInputValueDefinition_System_Boolean__}

Quotes the scalar's name as <code>{0}</code>, rather than the argument or the input field, in the localized message of a
value refused in one a predicate selects: an input named after what it does with the value, as the <code>eq</code> or the
<code>in</code> of a HotChocolate.Data filter, rather than after what the value is.

```csharp
public ValueObjectScalarOptions QuoteScalarNameIn(Func<IInputValueDefinition, bool> inputValues)
```

#### Parameters

`inputValues` [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<IInputValueDefinition, [bool](https://learn.microsoft.com/dotnet/api/system.boolean)\>

Selects, by the member the error's <code>coordinate</code> names, the arguments and input fields whose refused values are
quoted by the scalar's name. Called in the request, on a refusal alone.

#### Returns

 [ValueObjectScalarOptions](AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.md)

These options, so calls can be chained.

#### Remarks

<p>
Each call adds a predicate, and a value any of them selects is quoted by the scalar's name. HotChocolate sets the
coordinate of an error to the innermost argument or input field it read the refused value under, whatever
variable or list holds it: <code>IbanOperationFilterInput.eq</code> for the <code>eq</code> of a filter on an <code>Iban</code>
member. A variable HotChocolate reads before any field, as <code>$v</code> in <code>eq: $v</code>, has no coordinate and keeps
its name, and a variable's default value keeps the scalar's.
</p>
<p>
Only the message of a localized refusal changes, which <xref href="AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.UseResource%60%601" data-throw-if-not-resolved="false"></xref> asks for: its code, its
path, its <code>inputPath</code> and its <code>coordinate</code> stay HotChocolate's. A predicate that throws fails the error
filter, as any error filter that throws does.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">inputValues</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### SpecifiedBy<TSelf\>\(string\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarOptions_SpecifiedBy__1_System_String_}

Publishes the URL of the specification of a value object's format, as <code>@specifiedBy</code>.

```csharp
public ValueObjectScalarOptions SpecifiedBy<TSelf>(string url) where TSelf : struct, IValueObject
```

#### Parameters

`url` [string](https://learn.microsoft.com/dotnet/api/system.string)

The absolute URL of a specification readers of the schema can follow.

#### Returns

 [ValueObjectScalarOptions](AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.md)

These options, so calls can be chained.

#### Type Parameters

`TSelf` 

The value object, or a construction of a generic one.

#### Remarks

None is published by default: HotChocolate's own scalars point to specifications of their wire forms, and no
built-in one describes a value object's.

#### Exceptions

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">url</code> is not an absolute URL.

### UseResource<TResource\>\(\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarOptions_UseResource__1}

Reads the messages of refused values from the resources of <code class="typeparamref">TResource</code>, by the key of the
rule's code.

```csharp
public ValueObjectScalarOptions UseResource<TResource>()
```

#### Returns

 [ValueObjectScalarOptions](AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.md)

These options, so calls can be chained.

#### Type Parameters

`TResource` 

The type the resources are named after, as <code>IStringLocalizer&lt;T&gt;</code> names them: an empty class of the
application, beside a <code>.resx</code> file of the same name for each culture.

#### Remarks

<p>
An error filter of the schema looks each refusal up under the keys of <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.KeysFor(AdCodicem.ValueObjects.Metadata.ValueObjectSchema%2cSystem.String)" data-throw-if-not-resolved="false"></xref>,
in the current UI culture, and formats it in the current culture with the argument, the input field or the
variable the refusal concerns as <code>{0}</code>, or the scalar's name for a variable's default value, which
HotChocolate's error names no variable for, and for a value refused in an argument or an input field
<xref href="AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.QuoteScalarNameIn(System.Func%7bHotChocolate.Types.IInputValueDefinition%2cSystem.Boolean%7d)" data-throw-if-not-resolved="false"></xref> selects, followed by the arguments
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.ArgumentsFor%60%602(System.String%2cSystem.IFormatProvider)" data-throw-if-not-resolved="false"></xref> reads from the type's rules. A key with no entry keeps
the English message, and the code stays the rule's.
</p>
<p>
The localizer comes from the application's services, which <code>services.AddLocalization()</code> registers; without
it, the request executor fails to start, naming the call.
</p>

### UseResource\(Type\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarOptions_UseResource_System_Type_}

Reads the messages of refused values from the resources of a type.

```csharp
public ValueObjectScalarOptions UseResource(Type resourceType)
```

#### Parameters

`resourceType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The type the resources are named after.

#### Returns

 [ValueObjectScalarOptions](AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.md)

These options, so calls can be chained.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">resourceType</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

