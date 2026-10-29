# Namespace AdCodicem.ValueObjects.Blazor {#AdCodicem_ValueObjects_Blazor}

### Classes

 [InputValueObject<TValue\>](AdCodicem.ValueObjects.Blazor.InputValueObject\-1.md)

An input for a value object: reads the text a person types through the value object's rules, binds the normalized
instance, shows the message of the rule that refuses it, and keeps the rule's code on the form's
<xref href="Microsoft.AspNetCore.Components.Forms.EditContext" data-throw-if-not-resolved="false"></xref>.

 [ValueObjectEditContextExtensions](AdCodicem.ValueObjects.Blazor.ValueObjectEditContextExtensions.md)

Reads the rule codes the inputs of value objects keep on their form's <xref href="Microsoft.AspNetCore.Components.Forms.EditContext" data-throw-if-not-resolved="false"></xref>.

 [ValueObjectInputOptions](AdCodicem.ValueObjects.Blazor.ValueObjectInputOptions.md)

The messages the inputs of value objects answer a refusal with: the English message of the rule, or one read from a
resource of the application, under the key of the rule's code.

 [ValueObjectInputServiceCollectionExtensions](AdCodicem.ValueObjects.Blazor.ValueObjectInputServiceCollectionExtensions.md)

Registers the options of the inputs of value objects.

