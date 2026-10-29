# Class ValueObjectAIExtensions {#AdCodicem_ValueObjects_AI_ValueObjectAIExtensions}

Namespace: [AdCodicem.ValueObjects.AI](AdCodicem.ValueObjects.AI.md)  
Assembly: AdCodicem.ValueObjects.AI.dll  

Describes value objects in the schemas Microsoft.Extensions.AI publishes, and answers a model that sends one a value
its rules refuse with the rule, not with an exception the model never sees.

```csharp
public static class ValueObjectAIExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectAIExtensions](AdCodicem.ValueObjects.AI.ValueObjectAIExtensions.md)

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
<code>AIFunctionFactory</code> describes a tool's parameters from their <code>JsonTypeInfo</code>, and a value object, serialized
by a converter of its own, has none to read: each value-object parameter is published as <code>true</code>, the schema that
accepts anything, and the model has to guess its length, pattern, bounds and allowed values. Binding still validates
each argument, through the generated converter, but <code>FunctionInvokingChatClient</code> answers the exception it throws
with "Error: Function failed.", which tells the model nothing it can correct. The two calls together close both gaps:
</p>

<pre><code class="lang-csharp">AIFunction placeOrder = AIFunctionFactory.Create(
        tools.PlaceOrder,
        new AIFunctionFactoryOptions { JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions().WithValueObjects() })
    .WithValueObjectValidation();</code></pre>

<p>
Where reflection-based serialization is disabled, as native AOT disables it, the factory's default options describe
no value object at all, and refuse to: hand it <code>SerializerOptions</code> whose resolver is a source-generated context
naming <xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonConverterFactory" data-throw-if-not-resolved="false"></xref> and the types of the parameters and of the result.
</p>

## Methods

### WithValueObjectValidation\(AIFunction\) {#AdCodicem_ValueObjects_AI_ValueObjectAIExtensions_WithValueObjectValidation_Microsoft_Extensions_AI_AIFunction_}

Wraps a function so that an argument a value object refuses is answered with its rule, not an exception.

```csharp
public static AIFunction WithValueObjectValidation(this AIFunction function)
```

#### Parameters

`function` AIFunction

The function, built from a method by <xref href="Microsoft.Extensions.AI.AIFunctionFactory" data-throw-if-not-resolved="false"></xref>.

#### Returns

 AIFunction

The wrapping function, or <code class="paramref">function</code> itself when it wraps one already.

#### Remarks

<p>
Before the function binds its arguments, each argument of a value-object parameter is read as the binding reads it,
through the contract the function's serializer options hold for the parameter, which runs the value object's
converter and its rules. The first refused, in the order of the parameters, is returned instead of calling the
function, as a JSON object <code>FunctionInvokingChatClient</code> hands the model as it is:
</p>

<pre><code class="lang-csharp">{"error":"invalid_argument","argument":"quantity","code":"value_object.out_of_range","message":"The value is not a valid Quantity: The value must be less than or equal to 100."}</code></pre>

<p>
The message is the converter's, which names the value object and the rule, never the value sent; where the
converter leaves it to System.Text.Json, for a number its underlying type cannot hold, or where a converter of the
application's own refuses the value with an exception of its own, it is <code>The value is not a valid Quantity.</code>,
naming the value object alone. An argument the value object cannot read at all is <code>value_object.not_parsable</code>, unless a
converter of the application's own carries a code; an absent one the parameter has no default for, and a
<code>null</code> for a value object that cannot be <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>, which the binding would pass on as an
uninitialized instance, are <code>value_object.required</code>. A collection, a dictionary or an object is read the same
way, and answered for a value object it holds, under the parameter's name; any other error is left to the
function, which throws it as it would without the wrapper.
</p>
<p>
An instance of the parameter's type is passed on as it is. Text or a number, which the binding converts through a
JSON round trip of its own rather than refuses, is read as the binding converts it: text that may be JSON as JSON
first, then the value written through the contract of its own type and read back; it is refused only when neither
reading takes it.
</p>
<p>
The parameters are read from <xref href="Microsoft.Extensions.AI.AIFunction.UnderlyingMethod" data-throw-if-not-resolved="false"></xref>, once: a function no method backs, or whose
parameters cannot hold a value object, is called as it is. A parameter a <code>BindParameter</code> callback binds, while
the schema still lists it, is read as the serializer would read it.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">function</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### WithValueObjects\(AIJsonSchemaCreateOptions?\) {#AdCodicem_ValueObjects_AI_ValueObjectAIExtensions_WithValueObjects_Microsoft_Extensions_AI_AIJsonSchemaCreateOptions_}

Returns a copy of the options whose <xref href="Microsoft.Extensions.AI.AIJsonSchemaCreateOptions.TransformSchemaNode" data-throw-if-not-resolved="false"></xref> describes each
value object with the rules declared on it, after any transform the options already carry.

```csharp
public static AIJsonSchemaCreateOptions WithValueObjects(this AIJsonSchemaCreateOptions? options)
```

#### Parameters

`options` AIJsonSchemaCreateOptions?

The options to copy, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for <xref href="Microsoft.Extensions.AI.AIJsonSchemaCreateOptions.Default" data-throw-if-not-resolved="false"></xref>.

#### Returns

 AIJsonSchemaCreateOptions

A copy of the options, every other setting kept; the options given are left as they are.

#### Remarks

<p>
Each value object, alone, nullable, or held by a collection, a dictionary or an object, is described as its
underlying value under <xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonSchemaProfile.LanguageModel" data-throw-if-not-resolved="false"></xref>: its JSON type alone, its lengths,
pattern and numeric bounds, a closed set of known values as <code>enum</code>, a date bound as a sentence of the
description, its description after the one a <code>[Description]</code> gives the parameter, and its example; a format
JSON Schema does not define moves into the description. A transform the options already carry runs first, and
this one completes what it wrote.
</p>
<p>
Under OpenAI's strict mode, Microsoft.Extensions.AI.OpenAI moves the lengths, the pattern, the bounds and the
format into the description, and keeps <code>type</code>, <code>enum</code> and <code>examples</code>: the rules still reach the
model.
</p>

