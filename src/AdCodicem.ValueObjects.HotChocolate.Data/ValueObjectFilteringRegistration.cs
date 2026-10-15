namespace AdCodicem.ValueObjects.HotChocolate.Data;

/// <summary>
/// Marks a schema whose value objects <c>AddValueObjectFiltering()</c> already filters, with the options its calls
/// configure.
/// </summary>
/// <param name="SchemaName">The name of the schema, as <c>IRequestExecutorBuilder.Name</c> gives it.</param>
/// <param name="Options">The options of the schema's filters.</param>
/// <remarks>
/// Held in the application's services, where a later call on the same builder finds it, so that every call configures
/// one set of options and the schema gets one type interceptor, and one convention extension with the text operations.
/// </remarks>
internal sealed record ValueObjectFilteringRegistration(string SchemaName, ValueObjectFilteringOptions Options);
