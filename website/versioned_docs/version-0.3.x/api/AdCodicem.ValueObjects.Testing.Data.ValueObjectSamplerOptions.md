# Class ValueObjectSamplerOptions {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions}

Namespace: [AdCodicem.ValueObjects.Testing.Data](AdCodicem.ValueObjects.Testing.Data.md)  
Assembly: AdCodicem.ValueObjects.Testing.Data.dll  

How a <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler" data-throw-if-not-resolved="false"></xref> draws: how many candidates it tries, how often it takes a known value of an open
set, how long a string without a maximum length gets, and the generators of the rules no schema carries.

```csharp
public sealed class ValueObjectSamplerOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectSamplerOptions](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Options are read, never written, while a sampler draws: set them up before the first draw, and share them between
samplers as you like.

## Properties

### KnownValueShare {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_KnownValueShare}

Gets or sets the share of draws taken from the known values of an open value set, from 0 to 1, the others drawn
from the rest of its schema; one half by default.

```csharp
public double KnownValueShare { get; set; }
```

#### Property Value

 [double](https://learn.microsoft.com/dotnet/api/system.double)

#### Remarks

A closed value set always draws one of its known values.

#### Exceptions

 [ArgumentOutOfRangeException](https://learn.microsoft.com/dotnet/api/system.argumentoutofrangeexception)

The value is not between 0 and 1.

### MaxAttempts {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_MaxAttempts}

Gets or sets how many candidates a draw tries before it falls back on the example the type declares; 100 by
default.

```csharp
public int MaxAttempts { get; set; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

#### Exceptions

 [ArgumentOutOfRangeException](https://learn.microsoft.com/dotnet/api/system.argumentoutofrangeexception)

The value is less than 1.

### MaxExtraLength {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_MaxExtraLength}

Gets or sets the most characters a string without a maximum length gets beyond the shortest it can be (its minimum
length, the shortest match of its pattern, or one character when it declares neither), and the most repetitions an
open quantifier of a pattern (<code>*</code>, <code>+</code>, <code>{n,}</code>) adds to its minimum unless the lengths the type
declares ask for more; 32 by default.

```csharp
public int MaxExtraLength { get; set; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

#### Exceptions

 [ArgumentOutOfRangeException](https://learn.microsoft.com/dotnet/api/system.argumentoutofrangeexception)

The value is negative.

### PatternSampler {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_PatternSampler}

Gets or sets a sampler tried before the built-in one on the pattern of a string value object: it receives the
pattern and the source of randomness, and returns a string the pattern matches, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for a
pattern it cannot sample, which leaves it to the built-in sampler.

```csharp
public Func<string, Random, string?>? PatternSampler { get; set; }
```

#### Property Value

 [Func](https://learn.microsoft.com/dotnet/api/system.func\-3)<[string](https://learn.microsoft.com/dotnet/api/system.string), [Random](https://learn.microsoft.com/dotnet/api/system.random), [string](https://learn.microsoft.com/dotnet/api/system.string)?\>?

#### Remarks

Every string it returns still goes through the rules of the type. It serves <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler.Next%60%602" data-throw-if-not-resolved="false"></xref>;
<xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler.Boundaries%60%602" data-throw-if-not-resolved="false"></xref> and <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler.RejectedValues%60%602" data-throw-if-not-resolved="false"></xref>,
which need a string of an exact length, use the built-in sampler.

## Methods

### Use<TSelf, TValue\>\(Func<Random, TValue\>\) {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_Use__2_System_Func_System_Random___1__}

Registers the generator of the underlying value of a value object, for a rule its schema cannot carry, such as a
checksum: the sampler draws its candidates from it rather than from the schema, and keeps only those the type's
rules accept.

```csharp
public ValueObjectSamplerOptions Use<TSelf, TValue>(Func<Random, TValue> generator) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`generator` [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<[Random](https://learn.microsoft.com/dotnet/api/system.random), TValue\>

Draws a candidate underlying value from the sampler's source of randomness.

#### Returns

 [ValueObjectSamplerOptions](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.md)

These options, so that registrations chain.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

#### Remarks

A later registration for the same value object replaces the earlier one.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">generator</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

