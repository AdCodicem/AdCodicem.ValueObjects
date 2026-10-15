using HotChocolate;
using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.HotChocolate;

/// <summary>
/// Maps value objects to GraphQL scalars of their own in a HotChocolate schema, read through their rules.
/// </summary>
public static class ValueObjectRequestExecutorBuilderExtensions
{
    /// <summary>
    /// Maps every value object the schema meets to a scalar of its own, which reads a value through the type's rules.
    /// </summary>
    /// <param name="builder">The schema's builder, from <c>AddGraphQL()</c> or <c>AddGraphQLServer()</c>.</param>
    /// <param name="configure">Names, specifications and messages of the scalars, or <see langword="null"/> for none.</param>
    /// <returns><paramref name="builder"/>, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// A type discovery handler, asked before HotChocolate's own, answers each value object the schema references, as an
    /// argument, an input field, a field or the element of a list, with a <see cref="ValueObjectScalarType{TSelf, TValue}"/>
    /// closed over it through the descriptor's visitor. A <c>TSelf?</c> is the nullable scalar. A value object the registry
    /// holds is closed at compile time; one it does not hold yet, a construction of a generic value object or a value
    /// object written by hand, is described by <c>ValueObjectRegistry.TryResolve</c> where the runtime supports dynamic
    /// code, and refused with a <see cref="NotSupportedException"/> naming <c>ValueObjectRegistry.Register</c> and
    /// <see cref="AddValueObjectScalar{TSelf, TValue}"/> where it does not, as under native AOT.
    /// </para>
    /// <para>
    /// Every call on one schema configures the same <see cref="ValueObjectScalarOptions"/>, in the order of the calls, and
    /// the schema gets one handler. A scalar <see cref="AddValueObjectScalar{TSelf, TValue}"/> adds wins for its type.
    /// </para>
    /// <para>
    /// The first call also adds an error filter, which localizes a refusal under
    /// <see cref="ValueObjectScalarOptions.UseResource{TResource}"/>, quoting the scalar's name for a value refused in an
    /// argument or an input field <see cref="ValueObjectScalarOptions.QuoteScalarNameIn"/> selects.
    /// </para>
    /// </remarks>
    public static IRequestExecutorBuilder AddValueObjects(
        this IRequestExecutorBuilder builder,
        Action<ValueObjectScalarOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = OptionsOf(builder);
        configure?.Invoke(options);
        return builder;
    }

    /// <summary>
    /// Maps one value object to its scalar, closed at compile time: a construction of a generic value object, or one
    /// written by hand, under native AOT.
    /// </summary>
    /// <typeparam name="TSelf">The value object type.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="builder">The schema's builder.</param>
    /// <param name="name">
    /// The name of the scalar, a GraphQL name, or <see langword="null"/> to take the one
    /// <see cref="ValueObjectScalarOptions.Name{TSelf}"/> gives, or else the schema's naming conventions.
    /// </param>
    /// <returns><paramref name="builder"/>, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    /// <remarks>
    /// The scalar binds implicitly to <typeparamref name="TSelf"/>, so that the schema uses it wherever the type appears,
    /// beside <see cref="AddValueObjects"/> or without it. It needs no registration of the value object: its rules are
    /// read through <typeparamref name="TSelf"/>, never through the registry.
    /// </remarks>
    public static IRequestExecutorBuilder AddValueObjectScalar<TSelf, TValue>(this IRequestExecutorBuilder builder, string? name = null)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (name is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
        }

        // A factory, not an instance: a type system object belongs to the schema that completes it, and the schema is
        // built again when the executor is evicted.
        return builder.AddType(() => new ValueObjectScalarType<TSelf, TValue>(name));
    }

    /// <summary>
    /// Gets the options of the schema, registering the handler, the options and the error filter on the first call.
    /// </summary>
    /// <param name="builder">The schema's builder.</param>
    /// <returns>The options every call on the schema configures.</returns>
    private static ValueObjectScalarOptions OptionsOf(IRequestExecutorBuilder builder)
    {
        foreach (var service in builder.Services)
        {
            // Keyed first: a keyed descriptor throws on ImplementationInstance.
            if (service is { IsKeyedService: false, ImplementationInstance: ValueObjectScalarRegistration registration }
                && registration.SchemaName == builder.Name)
            {
                return registration.Options;
            }
        }

        var options = new ValueObjectScalarOptions();
        builder.Services.AddSingleton(new ValueObjectScalarRegistration(builder.Name, options));
        builder
            .ConfigureSchema(schema => schema.Features.Set(options))
            .AddTypeDiscoveryHandler(static _ => new ValueObjectTypeDiscoveryHandler())
            .AddErrorFilter(services => new ValueObjectErrorFilter(
                options,
                services.GetRequiredService<ISchemaDefinition>(),
                services.GetRootServiceProvider()));
        return options;
    }
}
