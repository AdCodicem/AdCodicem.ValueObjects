namespace AdCodicem.ValueObjects.HotChocolate;

/// <summary>
/// Marks a schema whose value objects <c>AddValueObjects()</c> already maps, with the options its calls configure.
/// </summary>
/// <param name="SchemaName">The name of the schema, as <c>IRequestExecutorBuilder.Name</c> gives it.</param>
/// <param name="Options">The options of the schema's scalars.</param>
/// <remarks>
/// Held in the application's services, where a later call on the same builder finds it, so that every call configures
/// one set of options and the schema gets one handler and one error filter.
/// </remarks>
internal sealed record ValueObjectScalarRegistration(string SchemaName, ValueObjectScalarOptions Options);
