# Class ValueObjectFakerExtensions {#AdCodicem_ValueObjects_Bogus_ValueObjectFakerExtensions}

Namespace: [AdCodicem.ValueObjects.Bogus](AdCodicem.ValueObjects.Bogus.md)  
Assembly: AdCodicem.ValueObjects.Bogus.dll  

Draws value objects from Bogus: a rule for every value-object member of a <xref href="Bogus.Faker%601" data-throw-if-not-resolved="false"></xref>, and a value object
drawn from a <xref href="Bogus.Faker" data-throw-if-not-resolved="false"></xref>, each from the rules its type declares.

```csharp
public static class ValueObjectFakerExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectFakerExtensions](AdCodicem.ValueObjects.Bogus.ValueObjectFakerExtensions.md)

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
Without a rule, Bogus leaves a value-object member at its default, the instance no rule of the type accepted; a rule
written by hand restates the bounds the type declares, and drifts from them. Here each value is drawn by a
<xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler" data-throw-if-not-resolved="false"></xref>: a candidate built from the type's schema, kept only once its rules accept it, or the
example it declares.
</p>
<p>
Each value object takes one number from the faker's <xref href="Bogus.Randomizer" data-throw-if-not-resolved="false"></xref>, which seeds the sampler that draws it:
<code>UseSeed</code> replays the same values, and the rules after it see the same numbers whatever the sampler made of its
own draws. A value object is found through <code>ValueObjectRegistry.TryResolve</code>, which describes a value object written
by hand the first time it is asked for, and keeps that description.
</p>

## Methods

### RuleForValueObjects<T\>\(Faker<T\>, ValueObjectSamplerOptions?\) {#AdCodicem_ValueObjects_Bogus_ValueObjectFakerExtensions_RuleForValueObjects__1_Bogus_Faker___0__AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_}

Gives every value-object member of <code class="typeparamref">T</code> a rule, its nullable, array and <xref href="System.Collections.Generic.List%601" data-throw-if-not-resolved="false"></xref>
members included, each value drawn from the rules its type declares, so that <code>StrictMode(true)</code> is met.

```csharp
public static Faker<T> RuleForValueObjects<T>(this Faker<T> faker, ValueObjectSamplerOptions? options = null) where T : class
```

#### Parameters

`faker` Faker<T\>

The faker to add the rules to.

`options` [ValueObjectSamplerOptions](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.md)?

How each value is drawn, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the defaults.

#### Returns

 Faker<T\>

The faker, so that rules chain.

#### Type Parameters

`T` 

The type the faker builds.

#### Remarks

<p>
Call it before the rules of the members it should leave alone: a rule of Bogus replaces the earlier rule of the
same member, so a rule written after this one wins, and one written before is replaced. A nullable member gets a
value, never <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>; an array or a list gets one to three values.
</p>
<p>
The rules are added through Bogus's <code>RuleForType</code>, so they reach exactly the members the faker binds, through a
binder of its own included. A <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplingException" data-throw-if-not-resolved="false"></xref> is thrown by <code>Generate</code>, when a member
is drawn.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">faker</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### ValueObject<TSelf\>\(Faker, ValueObjectSamplerOptions?\) {#AdCodicem_ValueObjects_Bogus_ValueObjectFakerExtensions_ValueObject__1_Bogus_Faker_AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_}

Draws a value of a value object its rules accept, from the schema it declares.

```csharp
public static TSelf ValueObject<TSelf>(this Faker faker, ValueObjectSamplerOptions? options = null) where TSelf : struct, IValueObject
```

#### Parameters

`faker` Faker

The faker whose <xref href="Bogus.Faker.Random" data-throw-if-not-resolved="false"></xref> seeds the draw.

`options` [ValueObjectSamplerOptions](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.md)?

How the value is drawn, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the defaults.

#### Returns

 TSelf

The value object.

#### Type Parameters

`TSelf` 

The value object.

#### Remarks

The underlying type is not named, since C# cannot infer it: the value object is closed through its descriptor. For a
rule no schema carries, a checksum for one, name it and give a generator:
<xref href="AdCodicem.ValueObjects.Bogus.ValueObjectFakerExtensions.ValueObject%60%602(Bogus.Faker%2cSystem.Func%7bBogus.Faker%2c%60%601%7d)" data-throw-if-not-resolved="false"></xref>.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">faker</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="typeparamref">TSelf</code> carries the marker of a value object but implements no <code>IValueObject&lt;TSelf, TValue&gt;</code> over itself.

 [ValueObjectSamplingException](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplingException.md)

No value the type accepts could be drawn.

### ValueObject<TSelf, TValue\>\(Faker, Func<Faker, TValue\>\) {#AdCodicem_ValueObjects_Bogus_ValueObjectFakerExtensions_ValueObject__2_Bogus_Faker_System_Func_Bogus_Faker___1__}

Draws a value of a value object from a generator of its underlying value, a semantic one of Bogus's for instance,
kept only once the type's rules accept it.

```csharp
public static TSelf ValueObject<TSelf, TValue>(this Faker faker, Func<Faker, TValue> generator) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`faker` Faker

The faker handed to <code class="paramref">generator</code>.

`generator` [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<Faker, TValue\>

Draws a candidate underlying value: <code>f =&gt; f.Finance.Iban()</code>.

#### Returns

 TSelf

The value object.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

#### Remarks

The generator is tried as many times as the sampler tries candidates, 100 by default, and the example the type
declares comes out when none passed.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">faker</code> or <code class="paramref">generator</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ValueObjectSamplingException](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplingException.md)

None of the candidates the generator gave passed the type's rules, and it declares no example they accept.

