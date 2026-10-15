using HotChocolate;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Filters.Expressions;
using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.HotChocolate.Data;

/// <summary>
/// Filters value objects in HotChocolate.Data through the operations of their scalars.
/// </summary>
public static class ValueObjectFilteringRequestExecutorBuilderExtensions
{
    /// <summary>
    /// Filters every value object a filter input of the schema meets through the operations of its scalar; call it after
    /// <c>AddFiltering()</c>.
    /// </summary>
    /// <param name="builder">The schema's builder, from <c>AddGraphQL()</c> or <c>AddGraphQLServer()</c>.</param>
    /// <param name="configure">The operations to add, or <see langword="null"/> for the comparable ones alone.</param>
    /// <returns><paramref name="builder"/>, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// A type interceptor replaces the filter HotChocolate.Data infers for a value-object member, an input of the struct's
    /// public properties, with <see cref="ValueObjectOperationFilterInputType{TSelf}"/>, closed over the value object
    /// through the descriptor's visitor: a member of the value object's type or of its nullable type, and the elements of
    /// a list or an array of either, whose list operations are HotChocolate's over it, in every filter input of every
    /// convention. A value object the registry holds is closed at compile time; one it does not hold yet, a construction
    /// of a generic value object or a value object written by hand, is described by <c>ValueObjectRegistry.TryResolve</c>
    /// where the runtime supports dynamic code, and refused with a <see cref="NotSupportedException"/> naming
    /// <c>ValueObjectRegistry.Register</c>, and <c>AddValueObjectScalar</c> with
    /// <see cref="ValueObjectFilterConventionDescriptorExtensions.BindValueObject{TSelf, TValue}"/>, where it does not, as
    /// under native AOT. A binding of the application's own, HotChocolate's <c>[ID]</c> and a field typed explicitly are
    /// left alone.
    /// </para>
    /// <para>
    /// The operands are the value object's scalar, so the first call also calls <c>AddValueObjects()</c>, and selects the
    /// operations of every filter with <see cref="ValueObjectScalarOptions.QuoteScalarNameIn"/>: a localized refusal of an
    /// operand quotes the scalar's name, never <c>eq</c> or <c>in</c>. Every call on one schema configures the same
    /// <see cref="ValueObjectFilteringOptions"/>, in the order of the calls, read when the schema is built.
    /// </para>
    /// <para>
    /// The comparable operations work wherever the call stands; the text operations
    /// (<see cref="ValueObjectFilteringOptions.IncludeTextOperations"/>) extend the default filter convention, so the call
    /// must follow <c>AddFiltering()</c>, or the schema fails at start, naming it.
    /// </para>
    /// </remarks>
    public static IRequestExecutorBuilder AddValueObjectFiltering(
        this IRequestExecutorBuilder builder,
        Action<ValueObjectFilteringOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = OptionsOf(builder);
        configure?.Invoke(options);
        return builder;
    }

    /// <summary>
    /// Gets the options of the schema, registering the scalars, the type interceptor, the options and the convention
    /// extension of the text operations on the first call.
    /// </summary>
    /// <param name="builder">The schema's builder.</param>
    /// <returns>The options every call on the schema configures.</returns>
    private static ValueObjectFilteringOptions OptionsOf(IRequestExecutorBuilder builder)
    {
        foreach (var service in builder.Services)
        {
            // Keyed first: a keyed descriptor throws on ImplementationInstance.
            if (service is { IsKeyedService: false, ImplementationInstance: ValueObjectFilteringRegistration registration }
                && registration.SchemaName == builder.Name)
            {
                return registration.Options;
            }
        }

        var options = new ValueObjectFilteringOptions();
        builder.Services.AddSingleton(new ValueObjectFilteringRegistration(builder.Name, options));
        builder
            // The scalars, and the operations of every filter, HotChocolate.Data's own, the application's and the
            // package's, in every convention, quoted by the scalar's name in a localized refusal.
            .AddValueObjects(static scalars => scalars.QuoteScalarNameIn(static inputValue => inputValue is IFilterOperationField))
            .ConfigureSchema(schema =>
            {
                if (options.TextOperations)
                {
                    // An extension of the default convention, which AddFiltering() registers, so that its provider holds
                    // the handlers; a handler set on a field alone would be skipped by the visitor of another provider.
                    schema.AddConvention<IFilterConvention>(new FilterConventionExtension(static convention =>
                        convention.AddProviderExtension(new QueryableFilterProviderExtension(static provider =>
                        {
                            foreach (var operation in ValueObjectTextOperationHandler.Operations)
                            {
                                provider.AddFieldHandler(context => new ValueObjectTextOperationHandler(context.InputParser, operation));
                            }
                        }))));
                }
            })
            // A factory, not the type: HotChocolate would create the type by reflection, whose constructor the AOT compiler
            // trims. Each schema build gets an interceptor of its own, over the options every call configures.
            .TryAddTypeInterceptor(_ => new ValueObjectFilterTypeInterceptor(options));
        return options;
    }
}
