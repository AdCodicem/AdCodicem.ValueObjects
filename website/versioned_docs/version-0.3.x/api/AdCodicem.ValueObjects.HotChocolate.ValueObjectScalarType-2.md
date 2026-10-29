# Class ValueObjectScalarType<TSelf, TValue\> {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarType_2}

Namespace: [AdCodicem.ValueObjects.HotChocolate](AdCodicem.ValueObjects.HotChocolate.md)  
Assembly: AdCodicem.ValueObjects.HotChocolate.dll  

A value object as a GraphQL scalar of its own, whose literal is its underlying value's.

```csharp
public sealed class ValueObjectScalarType<TSelf, TValue> : ScalarType, IScalarTypeDefinition, ISyntaxNodeProvider<ScalarTypeDefinitionNode>, ILeafType, IInputTypeDefinition, IInputType, IOutputTypeDefinition, ITypeDefinition, INameProvider, IDescriptionProvider, IDirectivesProvider, IFeatureProvider, ISyntaxNodeProvider, ISchemaCoordinateProvider, IRuntimeTypeProvider, IOutputType, IType, ITypeSystemMember, IEquatable<IType> where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

The value object type.

`TValue` 

Its underlying type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
TypeSystemObject ← 
TypeSystemObject<ScalarTypeConfiguration\> ← 
ScalarType ← 
[ValueObjectScalarType<TSelf, TValue\>](AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarType\-2.md)

#### Implements

IScalarTypeDefinition, 
ISyntaxNodeProvider<ScalarTypeDefinitionNode\>, 
ILeafType, 
IInputTypeDefinition, 
IInputType, 
IOutputTypeDefinition, 
ITypeDefinition, 
INameProvider, 
IDescriptionProvider, 
IDirectivesProvider, 
IFeatureProvider, 
ISyntaxNodeProvider, 
ISchemaCoordinateProvider, 
IRuntimeTypeProvider, 
IOutputType, 
IType, 
ITypeSystemMember, 
[IEquatable<IType\>](https://learn.microsoft.com/dotnet/api/system.iequatable\-1)

#### Inherited Members

ScalarType.IsAssignableFrom\(ITypeDefinition\), 
ScalarType.Equals\(IType?\), 
ScalarType.IsValueCompatible\(IValueNode\), 
ScalarType.IsValueCompatible\(JsonElement\), 
ScalarType.CoerceInputLiteral\(IValueNode\), 
ScalarType.CoerceInputValue\(JsonElement, IFeatureProvider\), 
ScalarType.CoerceOutputValue\(object, ResultElement\), 
ScalarType.ValueToLiteral\(object\), 
ScalarType.InputValueToLiteral\(JsonElement, IFeatureProvider\), 
ScalarType.ToString\(\), 
ScalarType.ToSyntaxNode\(\), 
ScalarType.Kind, 
ScalarType.Bind, 
ScalarType.RuntimeType, 
ScalarType.Coordinate, 
ScalarType.SpecifiedBy, 
ScalarType.SerializationType, 
ScalarType.Pattern, 
ScalarType.Directives, 
TypeSystemObject<ScalarTypeConfiguration\>.Features, 
TypeSystemObject.Scope, 
TypeSystemObject.Name, 
TypeSystemObject.Description, 
TypeSystemObject.Features, 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
A value object over a <xref href="System.Boolean" data-throw-if-not-resolved="false"></xref> is serialized as a Boolean, one over an integer of 64 bits at most as an Int,
whatever its width, and one over a real as a Float, read from an Int literal too. Every other one, over text, a
character, a <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>, a 128-bit integer, a date or a time, or a value object written by hand over another
type, is serialized as a String, read through <code>TSelf.TryParse</code> in the invariant culture, its normalizer included.
A number is read exactly as the underlying type, then goes through <code>TSelf.TryCreate</code>.
</p>
<p>
A value the type refuses is answered with a <xref href="HotChocolate.Types.LeafCoercionException" data-throw-if-not-resolved="false"></xref> whose GraphQL error carries the
message of the violated rule and, as its <code>extensions.code</code>, the rule's code; the exception carries the code in
its <xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref> under <xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>, and the error's
<code>Exception</code> is a <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> with the type and the code, never the value. A literal or a
JSON value of another kind, or a number the underlying type cannot hold, is refused with
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable" data-throw-if-not-resolved="false"></xref>. GraphQL input always comes from a client, so a scalar always
validates.
</p>
<p>
A value is written as its underlying value, never through a formatting hook of the value object. An uninitialized
instance, equal to the default, whose value the type refuses is never written, nor is a real that is not finite.
</p>
<p>
The description is the type's, from its schema. The pattern it declares, or the one the wire form of a duration, a
time of day or a <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref> follows, is the scalar's <xref href="HotChocolate.Types.ScalarType.Pattern" data-throw-if-not-resolved="false"></xref>, which HotChocolate
publishes as <code>@serializeAs</code> under <code>ApplySerializeAsToScalars</code>. GraphQL has no place for a length, a bound
or a known value.
</p>

## Properties

### RuntimeType {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarType_2_RuntimeType}

The .NET type representation of this scalar.

```csharp
public override Type RuntimeType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)

### SerializationType {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarType_2_SerializationType}

Gets the possible types that this scalar can serialize to.

```csharp
public override ScalarSerializationType SerializationType { get; }
```

#### Property Value

 ScalarSerializationType

## Methods

### CoerceInputLiteral\(IValueNode\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarType_2_CoerceInputLiteral_HotChocolate_Language_IValueNode_}

Coerces a GraphQL literal (AST value node) into a runtime value.

```csharp
public override object CoerceInputLiteral(IValueNode valueLiteral)
```

#### Parameters

`valueLiteral` IValueNode

The GraphQL literal to coerce.

#### Returns

 [object](https://learn.microsoft.com/dotnet/api/system.object)

Returns the runtime value representation.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">valueLiteral</code> is <code>null</code>.

 LeafCoercionException

Unable to coerce the given <code class="paramref">valueLiteral</code> into a runtime value.

### CoerceInputValue\(JsonElement, IFeatureProvider\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarType_2_CoerceInputValue_System_Text_Json_JsonElement_HotChocolate_Features_IFeatureProvider_}

Coerces an external input value (deserialized JSON) into a runtime value.

```csharp
public override object CoerceInputValue(JsonElement inputValue, IFeatureProvider context)
```

#### Parameters

`inputValue` [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

The deserialized JSON input value to coerce.

`context` IFeatureProvider

Provides access to the coercion context.

#### Returns

 [object](https://learn.microsoft.com/dotnet/api/system.object)

Returns the runtime value representation.

#### Exceptions

 LeafCoercionException

Unable to coerce the given <code class="paramref">inputValue</code> into a runtime value.

### CoerceOutputValue\(object, ResultElement\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarType_2_CoerceOutputValue_System_Object_HotChocolate_Text_Json_ResultElement_}

Coerces a runtime value into an external output representation
and writes it to the result.

```csharp
public override void CoerceOutputValue(object runtimeValue, ResultElement resultValue)
```

#### Parameters

`runtimeValue` [object](https://learn.microsoft.com/dotnet/api/system.object)

The runtime value to coerce. Must not be <code>null</code>; null handling
is the responsibility of the caller.

`resultValue` ResultElement

The result element to write the output value to.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">runtimeValue</code> is <code>null</code>.

 LeafCoercionException

Unable to coerce the given <code class="paramref">runtimeValue</code> into an output value.

### CreateConfiguration\(ITypeDiscoveryContext\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarType_2_CreateConfiguration_HotChocolate_Configuration_ITypeDiscoveryContext_}

```csharp
protected override ScalarTypeConfiguration CreateConfiguration(ITypeDiscoveryContext context)
```

#### Parameters

`context` ITypeDiscoveryContext

#### Returns

 ScalarTypeConfiguration

### ValueToLiteral\(object\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarType_2_ValueToLiteral_System_Object_}

Converts a runtime value into a GraphQL literal (AST value node).
Used for default value representation in SDL and introspection.

```csharp
public override IValueNode ValueToLiteral(object runtimeValue)
```

#### Parameters

`runtimeValue` [object](https://learn.microsoft.com/dotnet/api/system.object)

The runtime value to convert. Must not be <code>null</code>; null handling
is the responsibility of the caller.

#### Returns

 IValueNode

Returns a GraphQL literal representation of the runtime value.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">runtimeValue</code> is <code>null</code>.

 LeafCoercionException

Unable to convert the given <code class="paramref">runtimeValue</code> into a literal.

