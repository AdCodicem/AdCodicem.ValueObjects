# Class ValueObjectInputConverter {#AdCodicem_ValueObjects_AzureFunctions_ValueObjectInputConverter}

Namespace: [AdCodicem.ValueObjects.AzureFunctions](AdCodicem.ValueObjects.AzureFunctions.md)  
Assembly: AdCodicem.ValueObjects.AzureFunctions.dll  

Binds a value object from the text of a route value, a query value or other binding data, through its
<code>TryParse</code>.

```csharp
public sealed class ValueObjectInputConverter : IInputConverter
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectInputConverter](AdCodicem.ValueObjects.AzureFunctions.ValueObjectInputConverter.md)

#### Implements

[IInputConverter](https://learn.microsoft.com/dotnet/api/microsoft.azure.functions.worker.converters.iinputconverter)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The isolated worker reads no <code>[TypeConverter]</code>: without this converter, the text of a value object falls through
to the worker's JSON conversion, which refuses a valid <code>Guid</code>, <code>string</code> or <code>DateOnly</code> and fails every
such invocation. <xref href="AdCodicem.ValueObjects.AzureFunctions.ValueObjectFunctionsWorkerApplicationBuilderExtensions.UseValueObjects(Microsoft.Azure.Functions.Worker.IFunctionsWorkerApplicationBuilder)" data-throw-if-not-resolved="false"></xref> puts this
converter first among the worker's registered converters, or right behind the ASP.NET Core integration's, which binds
an <code>HttpRequest</code> and an <code>HttpRequestData</code> alone.
</p>
<p>
It reads a parameter whose type, <xref href="System.Nullable%601" data-throw-if-not-resolved="false"></xref> unwrapped, is a value object the registry describes, and
whose source is text: the text is parsed as it stands, then, when that fails and it is a JSON string literal, as the
string it encodes, since the host sends binding data of another type than <xref href="System.String" data-throw-if-not-resolved="false"></xref> as JSON, and, for a
value object over <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, when it is a JSON number, as the number the worker's JSON conversion reads,
an exponent included, which a decimal's <code>TryParse</code> refuses. Empty text is parsed too, and refused by the type's
own rule. A parameter with no binding data is left to the worker, which binds <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to a nullable
value object and its default value to an optional one, and fails a required one itself. A parameter carrying an input
or trigger binding attribute is left to that binding and to the worker's serializer, which honours the generated JSON
converter: naming this converter on the parameter with <code>[InputConverter(typeof(ValueObjectInputConverter))]</code>
reads a raw-text payload instead, and one that names another converter is left to it.
</p>
<p>
A refusal is <xref href="Microsoft.Azure.Functions.Worker.Converters.ConversionResult.Failed(System.Exception)" data-throw-if-not-resolved="false"></xref> with a <xref href="System.FormatException" data-throw-if-not-resolved="false"></xref> whose message names
the type, the rule and its code, and whose <xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref> holds the code under
<xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>; neither holds the refused text. The worker keeps only the exception's
text, in the <code>FunctionInputConverterException</code> it throws, which is why the message carries the code.
</p>
<p>
A generated value object registers itself; a value object written by hand and a construction of a generic one are
described by reflection where the runtime supports dynamic code, and otherwise once registered by hand. Only the types
described are kept, so that one registered after a first miss is read from then on.
</p>

## Methods

### ConvertAsync\(ConverterContext\) {#AdCodicem_ValueObjects_AzureFunctions_ValueObjectInputConverter_ConvertAsync_Microsoft_Azure_Functions_Worker_Converters_ConverterContext_}

Parses the text the host bound to the parameter, or leaves the parameter to the next converter.

```csharp
public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
```

#### Parameters

`context` [ConverterContext](https://learn.microsoft.com/dotnet/api/microsoft.azure.functions.worker.converters.convertercontext)

The parameter's conversion context.

#### Returns

 [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask\-1)<[ConversionResult](https://learn.microsoft.com/dotnet/api/microsoft.azure.functions.worker.converters.conversionresult)\>

The value object, a failure carrying the rule's code, or <xref href="Microsoft.Azure.Functions.Worker.Converters.ConversionResult.Unhandled" data-throw-if-not-resolved="false"></xref> for a parameter
that is not this converter's.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">context</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

