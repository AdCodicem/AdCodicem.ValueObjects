# Struct AnyEntityId {#AdCodicem_ValueObjects_Identifiers_AnyEntityId}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

An identifier of any registered type, resolved from its prefix.

```csharp
[JsonConverter(typeof(AnyEntityIdJsonConverter))]
[TypeConverter(typeof(AnyEntityIdTypeConverter))]
public readonly struct AnyEntityId : IEquatable<AnyEntityId>, ISpanParsable<AnyEntityId>, IParsable<AnyEntityId>, ISpanFormattable, IFormattable
```

#### Implements

[IEquatable<AnyEntityId\>](https://learn.microsoft.com/dotnet/api/system.iequatable\-1), 
[ISpanParsable<AnyEntityId\>](https://learn.microsoft.com/dotnet/api/system.ispanparsable\-1), 
[IParsable<AnyEntityId\>](https://learn.microsoft.com/dotnet/api/system.iparsable\-1), 
[ISpanFormattable](https://learn.microsoft.com/dotnet/api/system.ispanformattable), 
[IFormattable](https://learn.microsoft.com/dotnet/api/system.iformattable)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
This serves the places where the type is not known until the text arrives: a webhook body naming the
resource it concerns, a deep link, an audit trail recording heterogeneous references. Resolution costs a
bounded number of dictionary lookups and no reflection.
</p>
<p>
It deliberately implements neither <xref href="AdCodicem.ValueObjects.IValueObject" data-throw-if-not-resolved="false"></xref> nor <xref href="AdCodicem.ValueObjects.Identifiers.IEntityId" data-throw-if-not-resolved="false"></xref>, and that is what
makes it non-persistable by construction rather than by convention: the Entity Framework Core integration
keys off those interfaces, so this type is invisible to it and no one can map a polymorphic column by
accident. It is a transport and resolution type, nothing more.
</p>

## Properties

### IsDefault {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_IsDefault}

Gets a value indicating whether this instance is the uninitialized <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a>.

```csharp
public bool IsDefault { get; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Prefix {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_Prefix}

Gets the prefix the identifier carries, without its trailing separator.

```csharp
public string Prefix { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### Value {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_Value}

Gets the canonical text of the identifier.

```csharp
public string Value { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### ValueObjectType {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_ValueObjectType}

Gets the identifier type this text belongs to, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the default instance.

```csharp
public Type? ValueObjectType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)?

## Methods

### Equals\(AnyEntityId\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_Equals_AdCodicem_ValueObjects_Identifiers_AnyEntityId_}

Indicates whether the current object is equal to another object of the same type.

```csharp
public bool Equals(AnyEntityId other)
```

#### Parameters

`other` [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

An object to compare with this object.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> if the current object is equal to the <code class="paramref">other</code> parameter; otherwise, <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>.

### Equals\(object?\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_Equals_System_Object_}

Indicates whether this instance and a specified object are equal.

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The object to compare with the current instance.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> if <code class="paramref">obj</code> and this instance are the same type and represent the same value; otherwise, <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>.

### GetHashCode\(\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_GetHashCode}

Returns the hash code for this instance.

```csharp
public override int GetHashCode()
```

#### Returns

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

A 32-bit signed integer that is the hash code for this instance.

### Is<TId\>\(\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_Is__1}

Determines whether this identifier belongs to a given type.

```csharp
public bool Is<TId>() where TId : struct, IEntityId<TId>
```

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the prefix resolved to <code class="typeparamref">TId</code>.

#### Type Parameters

`TId` 

Identifier type to test against.

### Parse\(ReadOnlySpan<char\>, IFormatProvider?\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_Parse_System_ReadOnlySpan_System_Char__System_IFormatProvider_}

Parses text carrying any registered prefix.

```csharp
public static AnyEntityId Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
```

#### Parameters

`s` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Text to parse.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Unused; identifiers are culture-independent.

#### Returns

 [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

The resolved identifier.

#### Exceptions

 [ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

The text is not a valid identifier of any registered type.

### Parse\(string, IFormatProvider?\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_Parse_System_String_System_IFormatProvider_}

Parses text carrying any registered prefix.

```csharp
public static AnyEntityId Parse(string s, IFormatProvider? provider = null)
```

#### Parameters

`s` [string](https://learn.microsoft.com/dotnet/api/system.string)

Text to parse.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Unused; identifiers are culture-independent.

#### Returns

 [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

The resolved identifier.

#### Exceptions

 [ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

The text is not a valid identifier of any registered type.

### ToString\(\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_ToString}

Returns the fully qualified type name of this instance.

```csharp
public override string ToString()
```

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

The fully qualified type name.

### ToString\(string?, IFormatProvider?\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_ToString_System_String_System_IFormatProvider_}

Formats the value of the current instance using the specified format.

```csharp
public string ToString(string? format, IFormatProvider? formatProvider)
```

#### Parameters

`format` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The format to use.

 -or-

 A null reference (<code>Nothing</code> in Visual Basic) to use the default format defined for the type of the <xref href="System.IFormattable" data-throw-if-not-resolved="false"></xref> implementation.

`formatProvider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

The provider to use to format the value.

 -or-

 A null reference (<code>Nothing</code> in Visual Basic) to obtain the numeric format information from the current locale setting of the operating system.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

The value of the current instance in the specified format.

### ToValueObject\(\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_ToValueObject}

Builds the concrete identifier, boxed.

```csharp
public object? ToValueObject()
```

#### Returns

 [object](https://learn.microsoft.com/dotnet/api/system.object)?

The boxed identifier, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the default instance.

#### Remarks

For callers that only hold a <xref href="System.Type" data-throw-if-not-resolved="false"></xref> — a dispatcher looking up a handler, for instance.
Prefer <xref href="AdCodicem.ValueObjects.Identifiers.AnyEntityId.TryConvertTo%60%601(%60%600%40)" data-throw-if-not-resolved="false"></xref> wherever the type is known.

### TryConvertTo<TId\>\(out TId\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_TryConvertTo__1___0__}

Converts to a concrete identifier type.

```csharp
public bool TryConvertTo<TId>(out TId result) where TId : struct, IEntityId<TId>
```

#### Parameters

`result` TId

The typed identifier, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a> when this is not one.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the conversion succeeded.

#### Type Parameters

`TId` 

Identifier type to convert to.

#### Remarks

The type test comes first, so converting to the wrong type costs a reference comparison rather than a
parse that was always going to fail.

### TryFormat\(Span<char\>, out int, ReadOnlySpan<char\>, IFormatProvider?\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_TryFormat_System_Span_System_Char__System_Int32__System_ReadOnlySpan_System_Char__System_IFormatProvider_}

Tries to format the value of the current instance into the provided span of characters.

```csharp
public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
```

#### Parameters

`destination` [Span](https://learn.microsoft.com/dotnet/api/system.span\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

The span in which to write this instance's value formatted as a span of characters.

`charsWritten` [int](https://learn.microsoft.com/dotnet/api/system.int32)

When this method returns, contains the number of characters that were written in <code class="paramref">destination</code>.

`format` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

A span containing the characters that represent a standard or custom format string that defines the acceptable format for <code class="paramref">destination</code>.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

An optional object that supplies culture-specific formatting information for <code class="paramref">destination</code>.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> if the formatting was successful; otherwise, <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>.

### TryParse\(ReadOnlySpan<char\>, IFormatProvider?, out AnyEntityId\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_TryParse_System_ReadOnlySpan_System_Char__System_IFormatProvider_AdCodicem_ValueObjects_Identifiers_AnyEntityId__}

Parses text carrying any registered prefix, without throwing.

```csharp
public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out AnyEntityId result)
```

#### Parameters

`s` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Text to parse.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Unused; identifiers are culture-independent.

`result` [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

The resolved identifier, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a> when the text is rejected.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the text was accepted.

### TryParse\(ReadOnlySpan<char\>, IFormatProvider?, out AnyEntityId, out ValidationResult\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_TryParse_System_ReadOnlySpan_System_Char__System_IFormatProvider_AdCodicem_ValueObjects_Identifiers_AnyEntityId__AdCodicem_ValueObjects_ValidationResult__}

Parses text carrying any registered prefix, reporting why it was rejected.

```csharp
public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out AnyEntityId result, out ValidationResult validation)
```

#### Parameters

`s` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Text to parse.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Unused; identifiers are culture-independent.

`result` [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

The resolved identifier, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a> when the text is rejected.

`validation` [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

The outcome, distinguishing a prefix no type claims from a prefix that resolves but whose body is
corrupt — a caller that cannot tell the two apart cannot write a useful error message.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the text was accepted.

### TryParse\(string?, IFormatProvider?, out AnyEntityId\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_TryParse_System_String_System_IFormatProvider_AdCodicem_ValueObjects_Identifiers_AnyEntityId__}

Tries to parse a string into a value.

```csharp
public static bool TryParse(string? s, IFormatProvider? provider, out AnyEntityId result)
```

#### Parameters

`s` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The string to parse.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

An object that provides culture-specific formatting information about <code class="paramref">s</code>.

`result` [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

When this method returns, contains the result of successfully parsing <code class="paramref">s</code> or an undefined value on failure.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> if <code class="paramref">s</code> was successfully parsed; otherwise, <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>.

## Operators

### operator ==\(AnyEntityId, AnyEntityId\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_op_Equality_AdCodicem_ValueObjects_Identifiers_AnyEntityId_AdCodicem_ValueObjects_Identifiers_AnyEntityId_}

Determines whether two identifiers are equal.

```csharp
public static bool operator ==(AnyEntityId left, AnyEntityId right)
```

#### Parameters

`left` [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

Left operand.

`right` [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

Right operand.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when both are equal.

### operator \!=\(AnyEntityId, AnyEntityId\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityId_op_Inequality_AdCodicem_ValueObjects_Identifiers_AnyEntityId_AdCodicem_ValueObjects_Identifiers_AnyEntityId_}

Determines whether two identifiers differ.

```csharp
public static bool operator !=(AnyEntityId left, AnyEntityId right)
```

#### Parameters

`left` [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

Left operand.

`right` [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

Right operand.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when they differ.

