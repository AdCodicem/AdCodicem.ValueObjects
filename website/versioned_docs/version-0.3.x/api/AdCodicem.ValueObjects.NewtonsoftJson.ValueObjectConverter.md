# Class ValueObjectConverter {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectConverter}

Namespace: [AdCodicem.ValueObjects.NewtonsoftJson](AdCodicem.ValueObjects.NewtonsoftJson.md)  
Assembly: AdCodicem.ValueObjects.NewtonsoftJson.dll  

Reads and writes any value object as its bare underlying value.

```csharp
public sealed class ValueObjectConverter : JsonConverter
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
JsonConverter ← 
[ValueObjectConverter](AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectConverter.md)

#### Inherited Members

JsonConverter.WriteJson\(JsonWriter, object?, JsonSerializer\), 
JsonConverter.ReadJson\(JsonReader, Type, object?, JsonSerializer\), 
JsonConverter.CanConvert\(Type\), 
JsonConverter.CanRead, 
JsonConverter.CanWrite, 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
One converter covers every value object, because the work is delegated to the runtime descriptor rather than
to a type-specific implementation. Newtonsoft.Json resolves converters by reflection anyway, so there is
nothing to gain from a generic converter per type here.
</p>
<p>
It follows the System.Text.Json converter the generator emits: the same rules, and the same values on the wire.
The text is the same too, but for a whole <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, <xref href="System.Double" data-throw-if-not-resolved="false"></xref> or <xref href="System.Single" data-throw-if-not-resolved="false"></xref>,
which Newtonsoft.Json always writes with a fraction: <code>1250.0</code> where System.Text.Json writes <code>1250</code>.
Each serializer reads the other's text as the same value.
</p>
<p>
<xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectJsonSerializerSettingsExtensions.AddValueObjects(Newtonsoft.Json.JsonSerializerSettings%2cSystem.Boolean)" data-throw-if-not-resolved="false"></xref> adds it to
the serializer settings with the two settings it reads value objects best under.
<xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectContractResolver" data-throw-if-not-resolved="false"></xref> puts it on the contract of each value object instead, for code that hands
Newtonsoft.Json a contract resolver alone.
</p>
<p>
A value it refuses is a <xref href="Newtonsoft.Json.JsonSerializationException" data-throw-if-not-resolved="false"></xref>, the exception Newtonsoft.Json's own converters
throw, carrying the code of the rule in <xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref> under
<xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>, where <xref href="AdCodicem.ValueObjects.ValueObjectErrors.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> reads it: the rule's
own code, <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable" data-throw-if-not-resolved="false"></xref> for a token that is not of the underlying type at all, and
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref> for a <code>null</code> read into a value object that cannot be
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>. A value object written by hand over a type the generator does not support has its value
read by Newtonsoft.Json, whose own exception carries no code. A handler of the serializer's
<xref href="Newtonsoft.Json.JsonSerializer.Error" data-throw-if-not-resolved="false"></xref> event that handles it, as MVC's does, leaves that read without a value: the
converter then refuses the token with <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable" data-throw-if-not-resolved="false"></xref>, rather than as missing.
</p>

## Methods

### CanConvert\(Type\) {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectConverter_CanConvert_System_Type_}

Determines whether this instance can convert the specified object type.

```csharp
public override bool CanConvert(Type objectType)
```

#### Parameters

`objectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Type of the object.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<code>true</code> if this instance can convert the specified object type; otherwise, <code>false</code>.

### ReadJson\(JsonReader, Type, object?, JsonSerializer\) {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectConverter_ReadJson_Newtonsoft_Json_JsonReader_System_Type_System_Object_Newtonsoft_Json_JsonSerializer_}

Reads the JSON representation of the object.

```csharp
public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
```

#### Parameters

`reader` JsonReader

The <xref href="Newtonsoft.Json.JsonReader" data-throw-if-not-resolved="false"></xref> to read from.

`objectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Type of the object.

`existingValue` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The existing value of object being read.

`serializer` JsonSerializer

The calling serializer.

#### Returns

 [object](https://learn.microsoft.com/dotnet/api/system.object)?

The object value.

#### Remarks

<p>
The value is read as the System.Text.Json converter the generator emits reads it: from the kind of token it
writes, and through the value object's rules, so a rejection says which rule refused it.
</p>
<p>
A value written as a number is read from a string as well, as System.Text.Json reads one under
<code>JsonNumberHandling.AllowReadingFromString</code>: the whole text, with no white space, no group separator and no
culture. That is how Newtonsoft.Json writes a numeric value object without this converter, through its type
converter, so data stored that way still reads once the converter is added.
</p>
<p>
Newtonsoft.Json reads the token under the serializer's settings before the converter sees it. Under its
default <xref href="Newtonsoft.Json.DateParseHandling" data-throw-if-not-resolved="false"></xref>, a string that looks like a date becomes a <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref>,
converted to local time when the text carries an offset; where that date no longer says what the text said,
the value is refused rather than rebuilt into something else. A <xref href="System.DateTimeOffset" data-throw-if-not-resolved="false"></xref> value object
therefore refuses any text with an explicit offset, its own output included, since it is written with its
offset, <code>+00:00</code> for UTC. <xref href="Newtonsoft.Json.DateParseHandling.None" data-throw-if-not-resolved="false"></xref> keeps the text, and reads it back.
</p>
<p>
Under <xref href="Newtonsoft.Json.FloatParseHandling.Decimal" data-throw-if-not-resolved="false"></xref>, every number with a fraction or an exponent becomes a
<xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, which keeps every digit of a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref> value object. A
<xref href="System.Double" data-throw-if-not-resolved="false"></xref> or <xref href="System.Single" data-throw-if-not-resolved="false"></xref> value object beyond the range of a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref> then makes
the reader throw, and one smaller than its 28 decimal places loses the digits past them, all of them below
about 10^-28, where it reads as zero.
</p>

### WriteJson\(JsonWriter, object?, JsonSerializer\) {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectConverter_WriteJson_Newtonsoft_Json_JsonWriter_System_Object_Newtonsoft_Json_JsonSerializer_}

Writes the JSON representation of the object.

```csharp
public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
```

#### Parameters

`writer` JsonWriter

The <xref href="Newtonsoft.Json.JsonWriter" data-throw-if-not-resolved="false"></xref> to write to.

`value` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The value.

`serializer` JsonSerializer

The calling serializer.

#### Remarks

The value is written as the System.Text.Json converter the generator emits writes it: a number or a boolean
as such, and anything else as a string, <xref href="System.Int128" data-throw-if-not-resolved="false"></xref> and <xref href="System.UInt128" data-throw-if-not-resolved="false"></xref> included, in the same
round-trip form. The serializer's date settings do not apply, so that both serializers write the same text.
A whole <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, <xref href="System.Double" data-throw-if-not-resolved="false"></xref> or <xref href="System.Single" data-throw-if-not-resolved="false"></xref> is the one difference:
Newtonsoft.Json writes it with a fraction, as the same value. An instance equal to the default whose value the
value object rejects is refused with a <xref href="Newtonsoft.Json.JsonSerializationException" data-throw-if-not-resolved="false"></xref> naming the rule, as the
System.Text.Json converter refuses it, rather than written for a reader to refuse.

