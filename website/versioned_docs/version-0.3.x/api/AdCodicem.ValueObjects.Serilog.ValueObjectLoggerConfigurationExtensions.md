# Class ValueObjectLoggerConfigurationExtensions {#AdCodicem_ValueObjects_Serilog_ValueObjectLoggerConfigurationExtensions}

Namespace: [AdCodicem.ValueObjects.Serilog](AdCodicem.ValueObjects.Serilog.md)  
Assembly: AdCodicem.ValueObjects.Serilog.dll  

Wires value objects into Serilog's capture of log event properties.

```csharp
public static class ValueObjectLoggerConfigurationExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectLoggerConfigurationExtensions](AdCodicem.ValueObjects.Serilog.ValueObjectLoggerConfigurationExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### ValueObjects\(LoggerDestructuringConfiguration\) {#AdCodicem_ValueObjects_Serilog_ValueObjectLoggerConfigurationExtensions_ValueObjects_Serilog_Configuration_LoggerDestructuringConfiguration_}

Destructures value objects as their underlying value.

```csharp
public static LoggerConfiguration ValueObjects(this LoggerDestructuringConfiguration destructure)
```

#### Parameters

`destructure` LoggerDestructuringConfiguration

Serilog's destructuring configuration, <code>LoggerConfiguration.Destructure</code>.

#### Returns

 LoggerConfiguration

The logger configuration, so that calls can be chained.

#### Remarks

<p>
A value object logged with <code>@</code>, or met inside an object, a collection or a dictionary logged with <code>@</code>, is
captured as the scalar its underlying value is, <code>"Qty": 42</code> rather than <code>"Qty": {"Value": 42}</code>, and
Serilog writes it as it writes that type. Every type implementing <xref href="AdCodicem.ValueObjects.IValueObject" data-throw-if-not-resolved="false"></xref> is captured so,
through <xref href="AdCodicem.ValueObjects.IValueObject.GetBoxedValue" data-throw-if-not-resolved="false"></xref>: a value object written by hand and a construction of a generic
value object included, with nothing registered.
</p>
<p>
Without <code>@</code>, Serilog captures a type it does not know through <code>ToString()</code>, and consults no
destructuring policy: <code>{Qty}</code> stays the text <code>"42"</code>.
<xref href="AdCodicem.ValueObjects.Serilog.ValueObjectLoggingOptions.CaptureAsUnderlyingValue" data-throw-if-not-resolved="false"></xref> captures it as its underlying value too.
</p>
<p>
Serilog runs destructuring policies in the order they were declared: an application's own destructuring of a
value object type, a mask written with <code>Destructure.ByTransforming&lt;T&gt;()</code> included, applies only when
it is declared before this call.
</p>

### ValueObjects\(LoggerDestructuringConfiguration, Action<ValueObjectLoggingOptions\>\) {#AdCodicem_ValueObjects_Serilog_ValueObjectLoggerConfigurationExtensions_ValueObjects_Serilog_Configuration_LoggerDestructuringConfiguration_System_Action_AdCodicem_ValueObjects_Serilog_ValueObjectLoggingOptions__}

Destructures value objects as their underlying value, with the options given.

```csharp
public static LoggerConfiguration ValueObjects(this LoggerDestructuringConfiguration destructure, Action<ValueObjectLoggingOptions> configure)
```

#### Parameters

`destructure` LoggerDestructuringConfiguration

Serilog's destructuring configuration, <code>LoggerConfiguration.Destructure</code>.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action\-1)<[ValueObjectLoggingOptions](AdCodicem.ValueObjects.Serilog.ValueObjectLoggingOptions.md)\>

Sets the options.

#### Returns

 LoggerConfiguration

The logger configuration, so that calls can be chained.

#### Remarks

<p>
The assemblies of <xref href="AdCodicem.ValueObjects.Serilog.ValueObjectLoggingOptions.Assemblies" data-throw-if-not-resolved="false"></xref> are registered first. With
<xref href="AdCodicem.ValueObjects.Serilog.ValueObjectLoggingOptions.CaptureAsUnderlyingValue" data-throw-if-not-resolved="false"></xref>, Serilog then treats every value object the
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry" data-throw-if-not-resolved="false"></xref> holds as a scalar, which it captures as it is rather than through
<code>ToString()</code>, and an enricher replaces each one an event holds, in its properties and in the structures,
sequences and dictionaries they hold, with its underlying value.
</p>
<p>
The registry is read when this method runs. A construction of a generic value object is in it once it was
registered by hand, as native AOT asks, or resolved by another integration before the call, and is then made a
scalar as any other; one that was not, and a value object written by hand that nothing registered, is captured as
its text until the application makes it a scalar, with <code>Destructure.AsScalar&lt;T&gt;()</code>, and the enricher
then replaces it too. The enricher runs where this call places it among the enrichers: an enricher added after
it, <code>Enrich.FromLogContext()</code> included, adds a value object the enricher no longer sees, written as its text.
</p>
<p>
Serilog consults its scalar types before any destructuring policy. Without the option, the application should
therefore make no value object a scalar, or it is written as its text with <code>@</code> too. With it, no destructuring
of the application's runs for a value object the registry holds, whatever the order of the calls and wherever it
is met: a mask written with <code>Destructure.ByTransforming&lt;T&gt;()</code> is lost, and the masked text is what to
log. Without it, an application's own destructuring of a value object type applies when it is declared before
this call, Serilog running destructuring policies in the order they were declared.
</p>

