# Class CrockfordBase32 {#AdCodicem_ValueObjects_Identifiers_CrockfordBase32}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

The Crockford Base32 alphabet, and the decoding that makes an entity identifier canonical.

```csharp
public static class CrockfordBase32
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[CrockfordBase32](AdCodicem.ValueObjects.Identifiers.CrockfordBase32.md)

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
The alphabet is <code>0123456789abcdefghjkmnpqrstvwxyz</code>: the digits, then the letters with <code>i</code>,
<code>l</code>, <code>o</code> and <code>u</code> removed. Two properties are load bearing here.
</p>
<p>
It is <b>strictly increasing in ASCII</b>, so an ordinal comparison of two encoded values reproduces the
comparison of the numbers they encode. That is what lets the time bucket at the head of an identifier order
the database index chronologically without any decoding, under the ordinal comparison this library already
uses by default.
</p>
<p>
It is <b>case insensitive on input</b> and folds the confusable characters, so normalization produces a
single canonical spelling and a case-insensitive column collation can no longer collapse two distinct
identifiers into one.
</p>

## Fields

### Alphabet {#AdCodicem_ValueObjects_Identifiers_CrockfordBase32_Alphabet}

The encoding alphabet, indexed by the value it encodes.

```csharp
public const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Remarks

Lower case is the canonical spelling, so an identifier is one unbroken lowercase token wherever it
travels — a URL, a JSON body, a log line. Upper case still decodes; normalization folds it down.

### BitsPerSymbol {#AdCodicem_ValueObjects_Identifiers_CrockfordBase32_BitsPerSymbol}

The number of bits one symbol carries.

```csharp
public const int BitsPerSymbol = 5
```

#### Field Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

### Canonicalize\(char\) {#AdCodicem_ValueObjects_Identifiers_CrockfordBase32_Canonicalize_System_Char_}

Rewrites a character into its canonical spelling.

```csharp
public static char Canonicalize(char symbol)
```

#### Parameters

`symbol` [char](https://learn.microsoft.com/dotnet/api/system.char)

Character to canonicalize.

#### Returns

 [char](https://learn.microsoft.com/dotnet/api/system.char)

The canonical symbol, or <code class="paramref">symbol</code> itself when it decodes to nothing — normalization
must never reject, so an unusable character is carried through for validation to refuse.

### Decode\(char\) {#AdCodicem_ValueObjects_Identifiers_CrockfordBase32_Decode_System_Char_}

Decodes a symbol, accepting either case and the Crockford aliases.

```csharp
public static int Decode(char symbol)
```

#### Parameters

`symbol` [char](https://learn.microsoft.com/dotnet/api/system.char)

Character to decode.

#### Returns

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

The value from 0 to 31, or -1 when <code class="paramref">symbol</code> is not a symbol.

### Encode\(int\) {#AdCodicem_ValueObjects_Identifiers_CrockfordBase32_Encode_System_Int32_}

Encodes a five-bit value.

```csharp
public static char Encode(int value)
```

#### Parameters

`value` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Value to encode, from 0 to 31.

#### Returns

 [char](https://learn.microsoft.com/dotnet/api/system.char)

The symbol carrying <code class="paramref">value</code>.

#### Exceptions

 [ArgumentOutOfRangeException](https://learn.microsoft.com/dotnet/api/system.argumentoutofrangeexception)

<code class="paramref">value</code> does not fit in five bits.

### IsSymbol\(char\) {#AdCodicem_ValueObjects_Identifiers_CrockfordBase32_IsSymbol_System_Char_}

Determines whether a character decodes to a value.

```csharp
public static bool IsSymbol(char symbol)
```

#### Parameters

`symbol` [char](https://learn.microsoft.com/dotnet/api/system.char)

Character to test.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when <code class="paramref">symbol</code> is a symbol or one of its aliases.

