# Class InputValueObject<TValue\> {#AdCodicem_ValueObjects_Blazor_InputValueObject_1}

Namespace: [AdCodicem.ValueObjects.Blazor](AdCodicem.ValueObjects.Blazor.md)  
Assembly: AdCodicem.ValueObjects.Blazor.dll  

An input for a value object: reads the text a person types through the value object's rules, binds the normalized
instance, shows the message of the rule that refuses it, and keeps the rule's code on the form's
<xref href="Microsoft.AspNetCore.Components.Forms.EditContext" data-throw-if-not-resolved="false"></xref>.

```csharp
public class InputValueObject<TValue> : InputBase<TValue>, IComponent, IHandleEvent, IHandleAfterRender, IDisposable
```

#### Type Parameters

`TValue` 

The value object, or its nullable type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ComponentBase](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase) ← 
[InputBase<TValue\>](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1) ← 
[InputValueObject<TValue\>](AdCodicem.ValueObjects.Blazor.InputValueObject\-1.md)

#### Implements

[IComponent](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.icomponent), 
[IHandleEvent](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.ihandleevent), 
[IHandleAfterRender](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.ihandleafterrender), 
[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

#### Inherited Members

[InputBase<TValue\>.FormatValueAsString\(TValue?\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.formatvalueasstring), 
[InputBase<TValue\>.TryParseValueFromString\(string?, out TValue, out string?\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.tryparsevaluefromstring), 
[InputBase<TValue\>.SetParametersAsync\(ParameterView\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.setparametersasync), 
[InputBase<TValue\>.Dispose\(bool\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.dispose), 
[InputBase<TValue\>.AdditionalAttributes](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.additionalattributes), 
[InputBase<TValue\>.Value](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.value), 
[InputBase<TValue\>.ValueChanged](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.valuechanged), 
[InputBase<TValue\>.ValueExpression](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.valueexpression), 
[InputBase<TValue\>.DisplayName](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.displayname), 
[InputBase<TValue\>.EditContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.editcontext), 
[InputBase<TValue\>.FieldIdentifier](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.fieldidentifier), 
[InputBase<TValue\>.CurrentValue](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.currentvalue), 
[InputBase<TValue\>.CurrentValueAsString](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.currentvalueasstring), 
[InputBase<TValue\>.CssClass](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.cssclass), 
[InputBase<TValue\>.NameAttributeValue](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.inputbase\-1.nameattributevalue), 
[ComponentBase.BuildRenderTree\(RenderTreeBuilder\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.buildrendertree), 
[ComponentBase.OnInitialized\(\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.oninitialized), 
[ComponentBase.OnInitializedAsync\(\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.oninitializedasync), 
[ComponentBase.OnParametersSet\(\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.onparametersset), 
[ComponentBase.OnParametersSetAsync\(\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.onparameterssetasync), 
[ComponentBase.StateHasChanged\(\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.statehaschanged), 
[ComponentBase.ShouldRender\(\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.shouldrender), 
[ComponentBase.OnAfterRender\(bool\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.onafterrender), 
[ComponentBase.OnAfterRenderAsync\(bool\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.onafterrenderasync), 
[ComponentBase.InvokeAsync\(Action\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.invokeasync\#microsoft\-aspnetcore\-components\-componentbase\-invokeasync\(system\-action\)), 
[ComponentBase.InvokeAsync\(Func<Task\>\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.invokeasync\#microsoft\-aspnetcore\-components\-componentbase\-invokeasync\(system\-func\(\(system\-threading\-tasks\-task\)\)\)), 
[ComponentBase.DispatchExceptionAsync\(Exception\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.dispatchexceptionasync), 
[ComponentBase.SetParametersAsync\(ParameterView\)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.setparametersasync), 
[ComponentBase.RendererInfo](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.rendererinfo), 
[ComponentBase.Assets](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.assets), 
[ComponentBase.AssignedRenderMode](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.componentbase.assignedrendermode), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
It renders a text <code>&lt;input&gt;</code>, as <xref href="Microsoft.AspNetCore.Components.Forms.InputText" data-throw-if-not-resolved="false"></xref> does, and reads its text through
<xref href="AdCodicem.ValueObjects.IValueObject%602.TryParse(System.ReadOnlySpan%7bSystem.Char%7d%2cSystem.IFormatProvider%2c%600%40%2cAdCodicem.ValueObjects.ValidationResult%40)" data-throw-if-not-resolved="false"></xref>, the overload that reports the rule. Accepted text binds the
instance the type creates, which the input then shows normalized. Refused text leaves the model as it was, stays in the
field, marked invalid, and adds the rule's message to the form's validation messages, so that
<xref href="Microsoft.AspNetCore.Components.Forms.ValidationMessage%601" data-throw-if-not-resolved="false"></xref> and <xref href="Microsoft.AspNetCore.Components.Forms.ValidationSummary" data-throw-if-not-resolved="false"></xref> show it and
<xref href="Microsoft.AspNetCore.Components.Forms.EditContext.Validate" data-throw-if-not-resolved="false"></xref> returns <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>; the code of the rule is read with
<xref href="AdCodicem.ValueObjects.Blazor.ValueObjectEditContextExtensions.TryGetValueObjectErrorCode(Microsoft.AspNetCore.Components.Forms.EditContext%2cMicrosoft.AspNetCore.Components.Forms.FieldIdentifier%40%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref>. A message never quotes the text typed.
</p>
<p>
Empty text binds <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to a nullable value object, which <xref href="Microsoft.AspNetCore.Components.Forms.InputBase%601" data-throw-if-not-resolved="false"></xref> does before
any parsing. On a value object that cannot be <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>, it is parsed as any other text: a type that
accepts it, such as <code>[ValueObject&lt;string&gt;(AllowEmpty = true)]</code>, binds the instance it creates, and a type
that refuses it is answered with <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref> and the message
<code>[Required]</code> gives, "The {0} field is required.", <code>{0}</code> being <xref href="Microsoft.AspNetCore.Components.Forms.InputBase%601.DisplayName" data-throw-if-not-resolved="false"></xref>,
or else the name of the field.
</p>
<p>
The text an instance shows is the text its type reads back: the value object's own text, written in the round-trip
form of its underlying type (<code>2026-10-14</code> for a date), and the bare underlying value, in that same form, for a
type whose formatting hook writes something else. An instance equal to the default that its rules refuse, a field
nobody filled in, shows as empty text; one they accept, a zero they allow, shows as its value.
</p>
<p>
The text is read and written in <xref href="AdCodicem.ValueObjects.Blazor.InputValueObject%601.Culture" data-throw-if-not-resolved="false"></xref>, or else in the current culture where the component is
interactive, or prerenders for interactivity, and in the invariant culture under static rendering, which is the culture
the framework reads a posted form in. A date, of a value object over <xref href="System.DateOnly" data-throw-if-not-resolved="false"></xref>, <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref> or
<xref href="System.DateTimeOffset" data-throw-if-not-resolved="false"></xref>, is read in the Gregorian calendar its text is written in, whatever the culture's own
calendar: in <code>th-TH</code> or <code>fa-IR</code>, the culture's patterns and names stay, and a year typed is a Gregorian one.
A statically rendered form is mapped by the framework, through <xref href="System.IParsable%601" data-throw-if-not-resolved="false"></xref>, before any component
runs: a refused value is then reported with the framework's own message, which quotes the text and carries no code.
</p>
<p>
The value object is found in <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry" data-throw-if-not-resolved="false"></xref>, where a generated one is registered, or else, where
the runtime supports dynamic code, described by reflection. Under native AOT, a value object written by hand and a
construction of a generic one are registered by hand before the input renders; one the registry does not describe
fails the first render with an <xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref> naming the registration.
</p>

## Properties

### Culture {#AdCodicem_ValueObjects_Blazor_InputValueObject_1_Culture}

Gets or sets the culture the text is read and written in; when not set, the current culture where the component is
interactive or prerenders for interactivity, the invariant one under static rendering.

```csharp
[Parameter]
public CultureInfo? Culture { get; set; }
```

#### Property Value

 [CultureInfo](https://learn.microsoft.com/dotnet/api/system.globalization.cultureinfo)?

### Element {#AdCodicem_ValueObjects_Blazor_InputValueObject_1_Element}

Gets or sets the associated <xref href="Microsoft.AspNetCore.Components.ElementReference" data-throw-if-not-resolved="false"></xref>.

```csharp
[DisallowNull]
public ElementReference? Element { get; protected set; }
```

#### Property Value

 [ElementReference](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.elementreference)?

### SchemaAttributes {#AdCodicem_ValueObjects_Blazor_InputValueObject_1_SchemaAttributes}

Gets or sets a value indicating whether the input carries the <code>maxlength</code>, <code>minlength</code> and
<code>pattern</code> attributes the value object's rules give; off by default, since a browser checks the text as it is
typed, before the value object's normalizer runs.

```csharp
[Parameter]
public bool SchemaAttributes { get; set; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Remarks

The lengths are counted in UTF-16 code units, by the browser as by .NET. The pattern is written only when a browser,
which compiles it with the <code>v</code> flag and matches it against the whole text, accepts every text the .NET pattern
accepts: a pattern each of whose alternatives is anchored at both ends, with no <code>.</code>, negated class or escape,
word boundary, class subtraction, surrogate, named block or category <code>C</code> or <code>Cs</code>, and nothing the shared
reader of .NET patterns refuses, such as a lookaround; <code>\d</code>, <code>\w</code>, <code>\s</code> and <code>\p{…}</code> are
written as the characters .NET matches them with. The attributes are written the first time an input of the type
asks for them. An attribute the input is given explicitly wins over the one the rules give.

## Methods

### BuildRenderTree\(RenderTreeBuilder\) {#AdCodicem_ValueObjects_Blazor_InputValueObject_1_BuildRenderTree_Microsoft_AspNetCore_Components_Rendering_RenderTreeBuilder_}

Renders the component to the supplied <xref href="Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder" data-throw-if-not-resolved="false"></xref>.

```csharp
protected override void BuildRenderTree(RenderTreeBuilder builder)
```

#### Parameters

`builder` [RenderTreeBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.rendering.rendertreebuilder)

A <xref href="Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder" data-throw-if-not-resolved="false"></xref> that will receive the render output.

### Dispose\(bool\) {#AdCodicem_ValueObjects_Blazor_InputValueObject_1_Dispose_System_Boolean_}

```csharp
protected override void Dispose(bool disposing)
```

#### Parameters

`disposing` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### FormatValueAsString\(TValue?\) {#AdCodicem_ValueObjects_Blazor_InputValueObject_1_FormatValueAsString__0_}

Formats the value as a string. Derived classes can override this to determine the formatting used for <xref href="Microsoft.AspNetCore.Components.Forms.InputBase%601.CurrentValueAsString" data-throw-if-not-resolved="false"></xref>.

```csharp
protected override string? FormatValueAsString(TValue? value)
```

#### Parameters

`value` TValue?

The value to format.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

A string representation of the value.

#### Remarks

<xref href="Microsoft.AspNetCore.Components.Forms.InputBase%601" data-throw-if-not-resolved="false"></xref> formats the model's value only while no refused text shows, so the code of an
earlier refusal is cleared here too, whichever way the text was set. A subclass that sets
<xref href="Microsoft.AspNetCore.Components.Forms.InputBase%601.CurrentValueAsString" data-throw-if-not-resolved="false"></xref> to empty text on a nullable value object reaches no member of
the input before the form notifies the change, which still reads the earlier code; the change event clears it first.

### TryParseValueFromString\(string?, out TValue, out string?\) {#AdCodicem_ValueObjects_Blazor_InputValueObject_1_TryParseValueFromString_System_String__0__System_String__}

Parses a string to create an instance of <code class="typeparamref">TValue</code>. Derived classes can override this to change how
<xref href="Microsoft.AspNetCore.Components.Forms.InputBase%601.CurrentValueAsString" data-throw-if-not-resolved="false"></xref> interprets incoming values.

```csharp
protected override bool TryParseValueFromString(string? value, out TValue result, out string? validationErrorMessage)
```

#### Parameters

`value` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The string value to be parsed.

`result` TValue

An instance of <code class="typeparamref">TValue</code>.

`validationErrorMessage` [string](https://learn.microsoft.com/dotnet/api/system.string)?

If the value could not be parsed, provides a validation error message.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

True if the value could be parsed; otherwise false.

