# Class ValueObjectEditContextExtensions {#AdCodicem_ValueObjects_Blazor_ValueObjectEditContextExtensions}

Namespace: [AdCodicem.ValueObjects.Blazor](AdCodicem.ValueObjects.Blazor.md)  
Assembly: AdCodicem.ValueObjects.Blazor.dll  

Reads the rule codes the inputs of value objects keep on their form's <xref href="Microsoft.AspNetCore.Components.Forms.EditContext" data-throw-if-not-resolved="false"></xref>.

```csharp
public static class ValueObjectEditContextExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectEditContextExtensions](AdCodicem.ValueObjects.Blazor.ValueObjectEditContextExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

An <xref href="Microsoft.AspNetCore.Components.Forms.EditContext" data-throw-if-not-resolved="false"></xref> holds a field's refusal as a message alone. An <xref href="AdCodicem.ValueObjects.Blazor.InputValueObject%601" data-throw-if-not-resolved="false"></xref> keeps
the code of the rule beside it, in <xref href="Microsoft.AspNetCore.Components.Forms.EditContext.Properties" data-throw-if-not-resolved="false"></xref>, from the refusal until the next text it reads,
or until it is disposed: the field's equivalent of <xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>, for a test, a
telemetry hook or a component that styles the field by rule rather than by its English message.

## Methods

### TryGetValueObjectErrorCode\(EditContext, in FieldIdentifier, out string?\) {#AdCodicem_ValueObjects_Blazor_ValueObjectEditContextExtensions_TryGetValueObjectErrorCode_Microsoft_AspNetCore_Components_Forms_EditContext_Microsoft_AspNetCore_Components_Forms_FieldIdentifier__System_String__}

Gets the code of the rule that refused the text of a field, while its input shows the refusal.

```csharp
public static bool TryGetValueObjectErrorCode(this EditContext editContext, in FieldIdentifier field, out string? code)
```

#### Parameters

`editContext` [EditContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.editcontext)

The form's edit context.

`field` [FieldIdentifier](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.components.forms.fieldidentifier)

The field.

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The code: one of <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes" data-throw-if-not-resolved="false"></xref>, or a code of the application's own rule.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the field's input shows text its value object refused.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">editContext</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

