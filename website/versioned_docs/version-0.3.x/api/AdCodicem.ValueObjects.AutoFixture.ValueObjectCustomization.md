# Class ValueObjectCustomization {#AdCodicem_ValueObjects_AutoFixture_ValueObjectCustomization}

Namespace: [AdCodicem.ValueObjects.AutoFixture](AdCodicem.ValueObjects.AutoFixture.md)  
Assembly: AdCodicem.ValueObjects.AutoFixture.dll  

Customizes an AutoFixture fixture so that every value object it creates is drawn from the rules its type declares:
asked for directly, as its nullable form, or as a member of an object the fixture builds.

```csharp
public sealed class ValueObjectCustomization : ICustomization
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectCustomization](AdCodicem.ValueObjects.AutoFixture.ValueObjectCustomization.md)

#### Implements

ICustomization

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
Without it, AutoFixture calls the value object's <code>Create</code> with values of its own, longer than a declared
<code>MaxLength</code>, outside declared bounds, always the first known value of a closed set, and the creation fails or
passes by chance. With it, each value object is drawn by a <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler" data-throw-if-not-resolved="false"></xref>: a candidate built from its
schema, kept only once its rules accept it, or the example it declares.
</p>
<p>
A registration of the fixture wins over the customization, whether it comes before or after it:
<code>fixture.Register(() =&gt; EvenCode.Create(...))</code> is how a rule no schema carries, a checksum for one, is met in
AutoFixture's terms, as <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.Use%60%602(System.Func%7bSystem.Random%2c%60%601%7d)" data-throw-if-not-resolved="false"></xref> is in the sampler's. A specimen builder
the fixture's customizations held before the customization wins too, its builder being added after them.
</p>
<p>
A value object is found through <code>ValueObjectRegistry.TryResolve</code>, which describes a value object written by hand
the first time a fixture asks for it, and keeps that description. The customization is as thread-safe as the
<xref href="System.Random" data-throw-if-not-resolved="false"></xref> it draws from: <xref href="System.Random.Shared" data-throw-if-not-resolved="false"></xref> by default is, a <xref href="System.Random" data-throw-if-not-resolved="false"></xref> created with a seed
is not, and gives the same values for the same requests only when a single thread makes them.
</p>

## Constructors

### ValueObjectCustomization\(\) {#AdCodicem_ValueObjects_AutoFixture_ValueObjectCustomization__ctor}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.AutoFixture.ValueObjectCustomization" data-throw-if-not-resolved="false"></xref> class, drawing from
<xref href="System.Random.Shared" data-throw-if-not-resolved="false"></xref> with the default sampler options.

```csharp
public ValueObjectCustomization()
```

### ValueObjectCustomization\(ValueObjectSamplerOptions\) {#AdCodicem_ValueObjects_AutoFixture_ValueObjectCustomization__ctor_AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.AutoFixture.ValueObjectCustomization" data-throw-if-not-resolved="false"></xref> class, drawing from
<xref href="System.Random.Shared" data-throw-if-not-resolved="false"></xref> with the given sampler options.

```csharp
public ValueObjectCustomization(ValueObjectSamplerOptions options)
```

#### Parameters

`options` [ValueObjectSamplerOptions](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.md)

How each value object is drawn, the generators of the rules no schema carries included.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">options</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### ValueObjectCustomization\(ValueObjectSamplerOptions, Random\) {#AdCodicem_ValueObjects_AutoFixture_ValueObjectCustomization__ctor_AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_System_Random_}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.AutoFixture.ValueObjectCustomization" data-throw-if-not-resolved="false"></xref> class, drawing from the given source of
randomness with the given sampler options.

```csharp
public ValueObjectCustomization(ValueObjectSamplerOptions options, Random random)
```

#### Parameters

`options` [ValueObjectSamplerOptions](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.md)

How each value object is drawn, the generators of the rules no schema carries included.

`random` [Random](https://learn.microsoft.com/dotnet/api/system.random)

The source of randomness: a <xref href="System.Random" data-throw-if-not-resolved="false"></xref> created with a seed replays the same values.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">options</code> or <code class="paramref">random</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

## Methods

### Customize\(IFixture\) {#AdCodicem_ValueObjects_AutoFixture_ValueObjectCustomization_Customize_AutoFixture_IFixture_}

Adds to the fixture's customizations a specimen builder that answers the request for any value object type, or
for its nullable type, with a value its rules accept.

```csharp
public void Customize(IFixture fixture)
```

#### Parameters

`fixture` IFixture

The fixture to customize.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">fixture</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

