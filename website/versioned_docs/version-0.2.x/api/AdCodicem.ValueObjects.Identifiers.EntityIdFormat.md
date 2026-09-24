# Class EntityIdFormat {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

The layout of an entity identifier: <code>prefix _ bucket random check</code>.

```csharp
public static class EntityIdFormat
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[EntityIdFormat](AdCodicem.ValueObjects.Identifiers.EntityIdFormat.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
Generated identifier types call into this rather than carrying their own copy of the layout, so a change to
the format is a change in one place. Every member is public because generated code lives in the consumer's
assembly and cannot reach anything internal here.
</p>
<p>
Validation is a span scan, not a regular expression. At fixed length over a fixed alphabet a scan is both
faster and simpler, and it spares an entity identifier the compiled <code>Regex</code> that a
<code>Pattern</code>-constrained value object has to pay for at start-up — source generators cannot feed
<code>[GeneratedRegex]</code>, so that cost is unavoidable there and avoidable here.
</p>

## Fields

### ChecksumLength {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_ChecksumLength}

The number of trailing check characters.

```csharp
public const int ChecksumLength = 1
```

#### Field Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

### MaxTotalLength {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_MaxTotalLength}

The greatest total length any profile can produce, which bounds a stack buffer.

```csharp
public const int MaxTotalLength = 40
```

#### Field Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

### RandomLength {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_RandomLength}

The number of characters carrying randomness, at every granularity.

```csharp
public const int RandomLength = 16
```

#### Field Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

#### Remarks

<p>
16 symbols of five bits each: 80 bits. The birthday bound is roughly 1.1 × 10¹² identifiers within one
time bucket, against the 10⁴–10⁵ a bucket is sized to hold, so a collision is not a thing that happens.
</p>
<p>
Sized against the bucket, not against the table. Randomness is redrawn on every bucket, so what has to
stay out of reach is the number of identifiers minted within one bucket width — orders of magnitude
below the row count of the table, and the reason this is 80 bits rather than the 128 an unbucketed
identifier would need.
</p>

## Properties

### EntropyByteCount {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_EntropyByteCount}

Gets the number of entropy bytes <xref href="AdCodicem.ValueObjects.Identifiers.EntityIdFormat.Create(System.String%2cAdCodicem.ValueObjects.Identifiers.IdGranularity%2cSystem.TimeProvider%2cAdCodicem.ValueObjects.Identifiers.IdEntropySource)" data-throw-if-not-resolved="false"></xref> consumes.

```csharp
public static int EntropyByteCount { get; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

#### Remarks

One byte per random character. Taking the low five bits of a uniform byte is itself uniform, which
reducing a smaller buffer modulo 32 would not be.

### Epoch {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_Epoch}

Gets the instant the time bucket counts from.

```csharp
public static DateTimeOffset Epoch { get; }
```

#### Property Value

 [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

#### Remarks

2020, not 1970. Half a century of elapsed time spent before the first identifier is issued is half the
bucket space burned for nothing, and moving the epoch forward buys that back as horizon.

## Methods

### BodyLength\(IdGranularity\) {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_BodyLength_AdCodicem_ValueObjects_Identifiers_IdGranularity_}

Gets the number of characters after the prefix separator.

```csharp
public static int BodyLength(IdGranularity granularity)
```

#### Parameters

`granularity` [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

Bucket width.

#### Returns

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

The body length.

### CarriesPrefix\(ReadOnlySpan<char\>, string\) {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_CarriesPrefix_System_ReadOnlySpan_System_Char__System_String_}

Determines whether a text opens with a prefix and its separator, ignoring case.

```csharp
public static bool CarriesPrefix(ReadOnlySpan<char> text, string prefix)
```

#### Parameters

`text` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Text to test.

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

Declared prefix, without its trailing separator.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the prefix and its separator are present.

### Create\(string, IdGranularity, TimeProvider, IdEntropySource\) {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_Create_System_String_AdCodicem_ValueObjects_Identifiers_IdGranularity_System_TimeProvider_AdCodicem_ValueObjects_Identifiers_IdEntropySource_}

Builds a new identifier.

```csharp
public static string Create(string prefix, IdGranularity granularity, TimeProvider timeProvider, IdEntropySource entropy)
```

#### Parameters

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

Declared prefix, without its trailing separator.

`granularity` [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

Bucket width.

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

Clock supplying the bucket.

`entropy` [IdEntropySource](AdCodicem.ValueObjects.Identifiers.IdEntropySource.md)

Source of the random part.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

The identifier, canonical and valid by construction.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

An argument is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">prefix</code> breaks the prefix rules.

### Example\(string, IdGranularity\) {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_Example_System_String_AdCodicem_ValueObjects_Identifiers_IdGranularity_}

Builds a representative identifier, for publication in an OpenAPI schema.

```csharp
public static string Example(string prefix, IdGranularity granularity)
```

#### Parameters

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

Declared prefix, without its trailing separator.

`granularity` [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

Bucket width.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

A valid identifier of the right shape.

#### Remarks

Derived from a fixed instant and a fixed byte pattern rather than minted, so that regenerating the
document twice produces the same bytes. An example drawn from the real entropy source would be valid and
would make a committed specification churn on every build.

### Normalize\(ReadOnlySpan<char\>, string\) {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_Normalize_System_ReadOnlySpan_System_Char__System_String_}

Rewrites a candidate into its canonical spelling.

```csharp
public static string Normalize(ReadOnlySpan<char> text, string prefix)
```

#### Parameters

`text` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Candidate text.

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

Declared prefix, without its trailing separator.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

The canonical identifier, or the trimmed input when it does not carry the declared prefix. Normalization
never rejects: a text this could not make sense of is handed to <xref href="AdCodicem.ValueObjects.Identifiers.EntityIdFormat.Validate(System.ReadOnlySpan%7bSystem.Char%7d%2cSystem.String%2cAdCodicem.ValueObjects.Identifiers.IdGranularity)" data-throw-if-not-resolved="false"></xref> to refuse.

#### Remarks

Trims surrounding whitespace, folds the body to its canonical symbols — lower case, with Crockford's
aliases mapped — and restores the declared casing of the prefix. Idempotent, as the contract of a
normalizer requires.

<p>
Length is preserved: nothing is dropped. Crockford allows a hyphen anywhere in an encoded value for
readability, and this format deliberately does not, because these identifiers are never transcribed by
hand. Repairing a hyphenated candidate would buy a spelling nobody produces at the price of a second
text that maps onto the same identifier.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">prefix</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### SchemaPattern\(string, IdGranularity\) {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_SchemaPattern_System_String_AdCodicem_ValueObjects_Identifiers_IdGranularity_}

Builds the regular expression describing a profile, for publication in an OpenAPI schema.

```csharp
public static string SchemaPattern(string prefix, IdGranularity granularity)
```

#### Parameters

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

Declared prefix, without its trailing separator.

`granularity` [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

Bucket width.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

An anchored pattern.

#### Remarks

Published as schema text and never compiled: the running validation is <xref href="AdCodicem.ValueObjects.Identifiers.EntityIdFormat.Validate(System.ReadOnlySpan%7bSystem.Char%7d%2cSystem.String%2cAdCodicem.ValueObjects.Identifiers.IdGranularity)" data-throw-if-not-resolved="false"></xref>, a span
scan. The pattern exists so that a client generated from the document rejects the same texts.

### TimestampLength\(IdGranularity\) {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_TimestampLength_AdCodicem_ValueObjects_Identifiers_IdGranularity_}

Gets the number of characters the time bucket occupies at a granularity.

```csharp
public static int TimestampLength(IdGranularity granularity)
```

#### Parameters

`granularity` [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

Bucket width.

#### Returns

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

The number of characters.

#### Exceptions

 [ArgumentOutOfRangeException](https://learn.microsoft.com/dotnet/api/system.argumentoutofrangeexception)

<code class="paramref">granularity</code> is not a declared value.

### TotalLength\(string, IdGranularity\) {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_TotalLength_System_String_AdCodicem_ValueObjects_Identifiers_IdGranularity_}

Gets the exact length of an identifier, which is also the width of its database column.

```csharp
public static int TotalLength(string prefix, IdGranularity granularity)
```

#### Parameters

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

Declared prefix, without its trailing separator.

`granularity` [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

Bucket width.

#### Returns

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

The total length.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">prefix</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### Validate\(ReadOnlySpan<char\>, string, IdGranularity\) {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_Validate_System_ReadOnlySpan_System_Char__System_String_AdCodicem_ValueObjects_Identifiers_IdGranularity_}

Validates an already normalized candidate.

```csharp
public static ValidationResult Validate(ReadOnlySpan<char> value, string prefix, IdGranularity granularity)
```

#### Parameters

`value` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Normalized candidate.

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

Declared prefix, without its trailing separator.

`granularity` [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

Bucket width.

#### Returns

 [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

The first rule the candidate breaks, or success.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">prefix</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### Write\(string, IdGranularity, DateTimeOffset, ReadOnlySpan<byte\>, Span<char\>\) {#AdCodicem_ValueObjects_Identifiers_EntityIdFormat_Write_System_String_AdCodicem_ValueObjects_Identifiers_IdGranularity_System_DateTimeOffset_System_ReadOnlySpan_System_Byte__System_Span_System_Char__}

Writes an identifier into a destination buffer.

```csharp
public static int Write(string prefix, IdGranularity granularity, DateTimeOffset timestamp, ReadOnlySpan<byte> entropy, Span<char> destination)
```

#### Parameters

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

Declared prefix, without its trailing separator.

`granularity` [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

Bucket width.

`timestamp` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

Instant the bucket encodes.

`entropy` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[byte](https://learn.microsoft.com/dotnet/api/system.byte)\>

At least <xref href="AdCodicem.ValueObjects.Identifiers.EntityIdFormat.EntropyByteCount" data-throw-if-not-resolved="false"></xref> bytes of randomness.

`destination` [Span](https://learn.microsoft.com/dotnet/api/system.span\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Buffer receiving the identifier.

#### Returns

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

The number of characters written.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">prefix</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">entropy</code> or <code class="paramref">destination</code> is too short.

