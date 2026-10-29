# Class ValueObjectMessages {#AdCodicem_ValueObjects_Metadata_ValueObjectMessages}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

The keys and the arguments an application localises the message of a refused value object with: one key per rule
code, and the arguments the rules the type declares supply.

```csharp
public static class ValueObjectMessages
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectMessages](AdCodicem.ValueObjects.Metadata.ValueObjectMessages.md)

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
The message a rule refuses a value with is English, and stays so: <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> and the logs are
read by developers. An application that answers its users in their language looks a message up by the code of the
rule instead, under the key <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.KeyFor(System.String)" data-throw-if-not-resolved="false"></xref> gives it, <code>ValueObject.value_object.too_short</code> for
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.TooShort" data-throw-if-not-resolved="false"></xref> and <code>ValueObject.iban.check_digits</code> for a code of its own. A key
with no entry keeps the English message, so an application translates only what it wants to.
</p>
<p>
The arguments a message quotes come from the <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema" data-throw-if-not-resolved="false"></xref> of the type that refused the value, the
rules it declares once: a length, the bounds, the known values. Each has a name, which FluentValidation quotes as a
placeholder, <code>{MinLength}</code>, and a position, from <code>{1}</code>, which an <code>IStringLocalizer</code> formats with
<xref href="System.String.Format(System.IFormatProvider%2cSystem.String%2cSystem.Object%5b%5d)" data-throw-if-not-resolved="false"></xref>; <code>{0}</code> is the member the target reports the
refusal under, which the target adds. The value refused is never an argument: a message shown to someone else must
not quote what was sent.
</p>
<p>
The integrations look the messages up through this class: <code>AddValueObjects(messages =&gt; …)</code> on MVC,
<code>WithValueObjectProblemDetails(messages =&gt; …)</code> on a minimal API,
<code>UseValueObjectProblemDetails(messages =&gt; …)</code> on an Azure Functions HTTP trigger,
<code>AddValueObjects(options =&gt; options.UseResource&lt;T&gt;())</code> on a HotChocolate schema,
<code>AddValueObjectInputs(options =&gt; options.UseResource&lt;T&gt;())</code> for the inputs of a Blazor form,
FluentValidation's <code>LanguageManager</code> under <code>MustParseAs</code>, <code>MustSatisfy</code> and
<code>NotDefault</code>, and DataAnnotations' <xref href="AdCodicem.ValueObjects.Annotations.ParsesAsAttribute" data-throw-if-not-resolved="false"></xref> and
<xref href="AdCodicem.ValueObjects.Annotations.NotDefaultAttribute" data-throw-if-not-resolved="false"></xref> under the resource their <code>ErrorMessageResourceType</code> names alone.
It reads no annotation and closes no generic type at run time, so it runs in a native binary.
</p>

## Fields

### KeyPrefix {#AdCodicem_ValueObjects_Metadata_ValueObjectMessages_KeyPrefix}

The prefix of every key, <code>ValueObject.</code>, which keeps the keys of value objects together in a resource.

```csharp
public const string KeyPrefix = "ValueObject."
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### ArgumentsFor\(ValueObjectDescriptor, string, IFormatProvider?\) {#AdCodicem_ValueObjects_Metadata_ValueObjectMessages_ArgumentsFor_AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_System_String_System_IFormatProvider_}

Gets the arguments a message for this code can quote, read from the schema of a descriptor and formatted for the
culture.

```csharp
public static IReadOnlyList<KeyValuePair<string, object?>> ArgumentsFor(ValueObjectDescriptor descriptor, string errorCode, IFormatProvider? provider)
```

#### Parameters

`descriptor` [ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)

The descriptor of the type that refused the value.

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)

The code of the rule.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Formats the numbers and the dates; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for <xref href="System.Globalization.CultureInfo.CurrentCulture" data-throw-if-not-resolved="false"></xref>.

#### Returns

 [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist\-1)<[KeyValuePair](https://learn.microsoft.com/dotnet/api/system.collections.generic.keyvaluepair\-2)<[string](https://learn.microsoft.com/dotnet/api/system.string), [object](https://learn.microsoft.com/dotnet/api/system.object)?\>\>

The name and the text of each argument, in the order of their positions from <code>{1}</code>; empty for a code other
than <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.TooShort" data-throw-if-not-resolved="false"></xref>, <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.TooLong" data-throw-if-not-resolved="false"></xref>,
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.OutOfRange" data-throw-if-not-resolved="false"></xref> and <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotAKnownValue" data-throw-if-not-resolved="false"></xref>.

#### Remarks

<p>
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.TooShort" data-throw-if-not-resolved="false"></xref> has <code>MinLength</code>, <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.TooLong" data-throw-if-not-resolved="false"></xref>
<code>MaxLength</code>, <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.OutOfRange" data-throw-if-not-resolved="false"></xref> <code>Minimum</code> then <code>Maximum</code>, and
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotAKnownValue" data-throw-if-not-resolved="false"></xref> <code>KnownValues</code>. Every value is text, never
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>: the empty text stands for a rule the schema does not declare, the other bound of a range
that has one. Any other code, an application's own among them, has none.
</p>
<p>
A bound is held as invariant text, which is read back as the underlying type and written for the culture:
<code>0.01</code> becomes <code>0,01</code> in French. Text the type cannot read, which a schema written by hand may hold, is
kept as it is. The known values are written each for the culture and joined with the culture's list separator
followed by a space, <code>, </code> in English and <code>; </code> in French, where the comma separates the decimals.
</p>
<p>
The descriptor's schema may differ from the type's, when the application registered one by hand: its rules are
those the integrations that read the registry enforce and publish.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">descriptor</code> or <code class="paramref">errorCode</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">errorCode</code> is empty.

### ArgumentsFor<TSelf, TValue\>\(string, IFormatProvider?\) {#AdCodicem_ValueObjects_Metadata_ValueObjectMessages_ArgumentsFor__2_System_String_System_IFormatProvider_}

Gets the arguments a message for this code can quote, read from
<xref href="AdCodicem.ValueObjects.IValueObject%602.Schema" data-throw-if-not-resolved="false"></xref> and formatted for the culture.

```csharp
public static IReadOnlyList<KeyValuePair<string, object?>> ArgumentsFor<TSelf, TValue>(string errorCode, IFormatProvider? provider) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)

The code of the rule.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Formats the numbers and the dates; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for <xref href="System.Globalization.CultureInfo.CurrentCulture" data-throw-if-not-resolved="false"></xref>.

#### Returns

 [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist\-1)<[KeyValuePair](https://learn.microsoft.com/dotnet/api/system.collections.generic.keyvaluepair\-2)<[string](https://learn.microsoft.com/dotnet/api/system.string), [object](https://learn.microsoft.com/dotnet/api/system.object)?\>\>

The name and the text of each argument, in the order of their positions from <code>{1}</code>; empty for a code other
than <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.TooShort" data-throw-if-not-resolved="false"></xref>, <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.TooLong" data-throw-if-not-resolved="false"></xref>,
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.OutOfRange" data-throw-if-not-resolved="false"></xref> and <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotAKnownValue" data-throw-if-not-resolved="false"></xref>.

#### Type Parameters

`TSelf` 

The value object type that refused the value.

`TValue` 

Its underlying type.

#### Remarks

The arguments are those
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.ArgumentsFor(AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%2cSystem.String%2cSystem.IFormatProvider)" data-throw-if-not-resolved="false"></xref> describes, read from the type's own
schema, as a typed integration reads it, without asking the registry.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">errorCode</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">errorCode</code> is empty.

### KeyFor\(string\) {#AdCodicem_ValueObjects_Metadata_ValueObjectMessages_KeyFor_System_String_}

Gets the key of a rule code: <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.KeyPrefix" data-throw-if-not-resolved="false"></xref> followed by the code.

```csharp
public static string KeyFor(string errorCode)
```

#### Parameters

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)

The code, one of <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes" data-throw-if-not-resolved="false"></xref> or one of the application's own.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

The key, <code>ValueObject.value_object.too_short</code> for <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.TooShort" data-throw-if-not-resolved="false"></xref>.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">errorCode</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">errorCode</code> is empty.

### KeysFor\(ValueObjectSchema, string\) {#AdCodicem_ValueObjects_Metadata_ValueObjectMessages_KeysFor_AdCodicem_ValueObjects_Metadata_ValueObjectSchema_System_String_}

Gets the keys a message for a rule code is looked up under, the most specific first.

```csharp
public static IReadOnlyList<string> KeysFor(ValueObjectSchema schema, string errorCode)
```

#### Parameters

`schema` [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

The rules of the type that refused the value.

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)

The code of the rule.

#### Returns

 [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist\-1)<[string](https://learn.microsoft.com/dotnet/api/system.string)\>

The keys, the first with an entry giving the message.

#### Remarks

<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.OutOfRange" data-throw-if-not-resolved="false"></xref> is the code of both bounds, and a message saying "between {1} and
{2}" reads badly when the type declares one. For a schema declaring a minimum alone, the key of the code followed
by <code>.minimum</code> comes first, <code>ValueObject.value_object.out_of_range.minimum</code>; for one declaring a maximum
alone, followed by <code>.maximum</code>; then the key of the code. The key is chosen by the bounds the type declares,
not by the side the value crossed, which a refusal does not tell. Any other case has the key of the code alone.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">schema</code> or <code class="paramref">errorCode</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">errorCode</code> is empty.

