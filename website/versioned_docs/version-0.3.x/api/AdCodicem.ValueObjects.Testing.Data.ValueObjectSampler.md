# Class ValueObjectSampler {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSampler}

Namespace: [AdCodicem.ValueObjects.Testing.Data](AdCodicem.ValueObjects.Testing.Data.md)  
Assembly: AdCodicem.ValueObjects.Testing.Data.dll  

Draws value objects their own rules accept, from the schema they declare: test data that never restates a rule the
type already holds.

```csharp
public sealed class ValueObjectSampler
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectSampler](AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
A candidate comes from <code>TSelf.Schema</code>. A closed value set gives one of its known values, an open one gives a known
value part of the time (<xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.KnownValueShare" data-throw-if-not-resolved="false"></xref>). A string gets a length between
<code>MinLength</code> and <code>MaxLength</code>; without a maximum, at most <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.MaxExtraLength" data-throw-if-not-resolved="false"></xref>
characters beyond the shortest it can be, its <code>MinLength</code>, the shortest match of its pattern, or one character
when it declares neither. The pattern the type declares fills it: drawn from the pattern when it is inside the subset
the built-in sampler reads, padded at an end no anchor holds when its matches are too short, and otherwise drawn by
rejection sampling, from the pattern read without its lookarounds and with its categories reduced to printable ASCII,
or from ASCII letters and digits, and kept only once the pattern matches. A number, a date, a time or a duration gets a
value between <code>Minimum</code> and <code>Maximum</code>, or across the type's own range on a side left open; a character
keeps to printable ASCII there, unless the bound declared on the other side lies beyond it, where the open side runs
to the type's own extreme. A <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> and a <xref href="System.Boolean" data-throw-if-not-resolved="false"></xref> are drawn as they are. A generator
registered with <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.Use%60%602(System.Func%7bSystem.Random%2c%60%601%7d)" data-throw-if-not-resolved="false"></xref> replaces all of that for its type.
</p>
<p>
Each candidate then goes through <code>TSelf.TryCreate</code>, so the normalizer and the validators apply and nothing the type
refuses comes out. After <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.MaxAttempts" data-throw-if-not-resolved="false"></xref> candidates, the sampler falls back on the
example the type declares, and failing that throws a <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplingException" data-throw-if-not-resolved="false"></xref> that shows the
registration to add, or names the registered generator whose values the type refused. A rule no schema carries, a
checksum for one, makes most candidates fail: the example then comes out most of the time, which a generator registered
for the type avoids.
</p>
<p>
A sampler is as thread-safe as the <xref href="System.Random" data-throw-if-not-resolved="false"></xref> it draws from: <xref href="System.Random.Shared" data-throw-if-not-resolved="false"></xref> is, a
<xref href="System.Random" data-throw-if-not-resolved="false"></xref> created with a seed is not, and replays the same values given the same calls. The typed members
read <code>TSelf.Schema</code> and never the registry.
</p>

## Constructors

### ValueObjectSampler\(Random, ValueObjectSamplerOptions?\) {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSampler__ctor_System_Random_AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler" data-throw-if-not-resolved="false"></xref> class.

```csharp
public ValueObjectSampler(Random random, ValueObjectSamplerOptions? options = null)
```

#### Parameters

`random` [Random](https://learn.microsoft.com/dotnet/api/system.random)

The source of randomness: <xref href="System.Random.Shared" data-throw-if-not-resolved="false"></xref>, or a <xref href="System.Random" data-throw-if-not-resolved="false"></xref> created with a seed to replay a run.

`options` [ValueObjectSamplerOptions](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.md)?

How it draws, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the defaults.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">random</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

## Methods

### Boundaries<TSelf, TValue\>\(\) {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSampler_Boundaries__2}

Gets the accepted values at the edges the schema declares: its <code>Minimum</code> and <code>Maximum</code>, a string of its
<code>MinLength</code> (the empty string without one) and of its <code>MaxLength</code>, and each known value, each once its
rules accept it, normalized and without repeats.

```csharp
public IEnumerable<TValue> Boundaries<TSelf, TValue>() where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Returns

 [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable\-1)<TValue\>

The values, in that order; none for a schema that declares no edge.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

#### Remarks

A property over a wide type lands on its edges by chance once in a blue moon: draw these as well.

### Next<TSelf, TValue\>\(\) {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSampler_Next__2}

Draws a value the type accepts: a candidate built from its schema, kept only once its rules accept it, or the
example it declares once no candidate passed.

```csharp
public TSelf Next<TSelf, TValue>() where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Returns

 TSelf

The value object.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

#### Exceptions

 [ValueObjectSamplingException](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplingException.md)

No candidate, drawn from the schema or from the generator registered for the type, passed its rules, and it declares
no example they accept.

### Next\(ValueObjectDescriptor\) {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSampler_Next_AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_}

Draws a value of a value object known only by its descriptor, as <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler.Next%60%602" data-throw-if-not-resolved="false"></xref> draws it, closed
over the descriptor's type arguments through its visitor.

```csharp
public object Next(ValueObjectDescriptor descriptor)
```

#### Parameters

`descriptor` [ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)

The descriptor, as <code>ValueObjectRegistry</code> holds it.

#### Returns

 [object](https://learn.microsoft.com/dotnet/api/system.object)

The value object, boxed.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">descriptor</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ValueObjectSamplingException](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplingException.md)

No candidate, drawn from the schema or from the generator registered for the type, passed its rules, and it declares
no example they accept.

### RejectedValues<TSelf, TValue\>\(\) {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSampler_RejectedValues__2}

Gets values the schema rules out, each with the rule it breaks: a string one character shorter than its
<code>MinLength</code> and the empty string, one character longer than its <code>MaxLength</code>, a value one step below its
<code>Minimum</code> and one above its <code>Maximum</code>, and a value outside its closed value set.

```csharp
public IEnumerable<SchemaViolation<TValue>> RejectedValues<TSelf, TValue>() where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Returns

 [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable\-1)<[SchemaViolation](AdCodicem.ValueObjects.Testing.Data.SchemaViolation\-1.md)<TValue\>\>

The values; none for a schema that rules nothing out.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

#### Remarks

<p>
A string of the wrong length is otherwise drawn as the pattern asks, so that its length alone breaks a rule, and
drawn again while the type's normalizer brings it back to a right length, a trimmed space for one. The empty string
is derived only beside a <code>MinLength</code> of 2 or more, since the schema does not say whether a type without one
accepts it. A step is one unit of the bound's last written digit for a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, one unit
in the last place for a binary floating-point number, one tick for a date, a time or a duration, and one day for a
<xref href="System.DateOnly" data-throw-if-not-resolved="false"></xref>; no step is taken past the type's own extremes. A value outside a closed set is drawn from the
rest of the schema, and compared with the known values once normalized, ignoring case for a string.
</p>
<p>
A type whose normalizer clamps a value into its rules, rather than its validator refusing it, accepts some of these:
a number past a bound, or a string its normalizer cuts or pads to its lengths whatever the draw.
</p>

### Shrink<TSelf, TValue\>\(TSelf, Func<TValue, IEnumerable<TValue\>\>?\) {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSampler_Shrink__2___0_System_Func___1_System_Collections_Generic_IEnumerable___1___}

Proposes simpler values the type accepts than one it holds, for a property-based testing library to shrink a
counterexample to: the values its schema declares, its <code>Minimum</code>, its first known value and its example, then
the shrinks of the underlying value the type accepts as they are.

```csharp
public static IEnumerable<TSelf> Shrink<TSelf, TValue>(TSelf value, Func<TValue, IEnumerable<TValue>>? shrinkUnderlying = null) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`value` TSelf

The value to shrink.

`shrinkUnderlying` [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<TValue, [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable\-1)<TValue\>\>?

The shrinker of the underlying type, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to propose the declared values alone.

#### Returns

 [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable\-1)<TSelf\>

The proposals, none equal to the value or to one another.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

#### Remarks

<p>
A shrink of the underlying value is proposed only when the type accepts it and its normalizer leaves it unchanged: a
normalizer maps a shrink to a value that may be no simpler, as upper-casing maps the <code>'b'</code> a character shrinks to
back to <code>'B'</code>, and two values would then shrink to each other until the library gives up.
</p>
<p>
The declared values are ranked in that order, and from one of them only those ranked before it are proposed, and no
shrink of its underlying value: every chain of shrinks ends wherever the chains of the underlying shrinker end.
</p>

### TryNext<TSelf, TValue\>\(out TSelf, out ValidationResult\) {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSampler_TryNext__2___0__AdCodicem_ValueObjects_ValidationResult__}

Draws a value the type accepts, or reports why none could be drawn: the refusal of the last candidate, or no refusal
when the schema gave no candidate at all.

```csharp
public bool TryNext<TSelf, TValue>(out TSelf value, out ValidationResult refusal) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`value` TSelf

The value object, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a> when none could be drawn.

`refusal` [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

<xref href="AdCodicem.ValueObjects.ValidationResult.Success" data-throw-if-not-resolved="false"></xref> when a value was drawn; otherwise the refusal of the last candidate, or
<xref href="AdCodicem.ValueObjects.ValidationResult.Success" data-throw-if-not-resolved="false"></xref> again when the schema gave none.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when a value was drawn.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

