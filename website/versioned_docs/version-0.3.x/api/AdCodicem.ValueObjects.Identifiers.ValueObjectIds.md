# Class ValueObjectIds {#AdCodicem_ValueObjects_Identifiers_ValueObjectIds}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

The clock and the entropy source that <code>New()</code> reads.

```csharp
public static class ValueObjectIds
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectIds](AdCodicem.ValueObjects.Identifiers.ValueObjectIds.md)

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
Two layers. <xref href="AdCodicem.ValueObjects.Identifiers.ValueObjectIds.Configure(System.TimeProvider%2cAdCodicem.ValueObjects.Identifiers.IdEntropySource)" data-throw-if-not-resolved="false"></xref> sets a process-wide default, which is what an application does once at
start-up. <xref href="AdCodicem.ValueObjects.Identifiers.ValueObjectIds.Use(System.TimeProvider%2cAdCodicem.ValueObjects.Identifiers.IdEntropySource)" data-throw-if-not-resolved="false"></xref> opens a scope bound to the current execution flow, which wins over the default
for as long as it lives.
</p>
<p>
The scope is not a convenience: the test suites run in parallel, and a settable static alone would let two
tests racing to substitute the clock corrupt one another. The failure that produces is intermittent and
lands in whichever test happens to observe it, which is the most expensive kind to diagnose. An
<xref href="System.Threading.AsyncLocal%601" data-throw-if-not-resolved="false"></xref> scope is bounded by the execution flow, so parallel tests do not interfere and
no test collection has to be serialized to stay correct.
</p>

## Properties

### Entropy {#AdCodicem_ValueObjects_Identifiers_ValueObjectIds_Entropy}

Gets the entropy source in effect, preferring the innermost open scope.

```csharp
public static IdEntropySource Entropy { get; }
```

#### Property Value

 [IdEntropySource](AdCodicem.ValueObjects.Identifiers.IdEntropySource.md)

### TimeProvider {#AdCodicem_ValueObjects_Identifiers_ValueObjectIds_TimeProvider}

Gets the clock in effect, preferring the innermost open scope.

```csharp
public static TimeProvider TimeProvider { get; }
```

#### Property Value

 [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

## Methods

### Configure\(TimeProvider?, IdEntropySource?\) {#AdCodicem_ValueObjects_Identifiers_ValueObjectIds_Configure_System_TimeProvider_AdCodicem_ValueObjects_Identifiers_IdEntropySource_}

Replaces the process-wide default.

```csharp
public static void Configure(TimeProvider? timeProvider = null, IdEntropySource? entropy = null)
```

#### Parameters

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)?

Clock to use, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to keep the current one.

`entropy` [IdEntropySource](AdCodicem.ValueObjects.Identifiers.IdEntropySource.md)?

Entropy source to use, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to keep the current one.

#### Remarks

Intended to be called once, during start-up, before anything generates an identifier. It is not
synchronized against concurrent generation: substituting the clock while requests are in flight is a
test concern, and <xref href="AdCodicem.ValueObjects.Identifiers.ValueObjectIds.Use(System.TimeProvider%2cAdCodicem.ValueObjects.Identifiers.IdEntropySource)" data-throw-if-not-resolved="false"></xref> is the member for that.

### Use\(TimeProvider?, IdEntropySource?\) {#AdCodicem_ValueObjects_Identifiers_ValueObjectIds_Use_System_TimeProvider_AdCodicem_ValueObjects_Identifiers_IdEntropySource_}

Opens a scope that overrides the default for the current execution flow.

```csharp
public static IDisposable Use(TimeProvider? timeProvider = null, IdEntropySource? entropy = null)
```

#### Parameters

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)?

Clock to use, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to inherit the enclosing one.

`entropy` [IdEntropySource](AdCodicem.ValueObjects.Identifiers.IdEntropySource.md)?

Entropy source to use, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to inherit the enclosing one.

#### Returns

 [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

A handle that restores the enclosing state when disposed.

#### Examples

<pre><code class="lang-csharp">using (ValueObjectIds.Use(fakeClock, deterministicBytes))
{
    var id = AccountId.New();
}</code></pre>

