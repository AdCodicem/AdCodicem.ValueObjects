using AdCodicem.ValueObjects.AspNetCore.Formatters;
using AdCodicem.ValueObjects.AspNetCore.ModelBinding;
using AdCodicem.ValueObjects.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.AspNetCore;

/// <summary>
/// Wires value object support into an ASP.NET Core application.
/// </summary>
public static class ValueObjectMvcExtensions
{
    /// <summary>
    /// Binds value objects from the underlying value everywhere MVC reads text.
    /// </summary>
    /// <param name="options">MVC options.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// The provider is inserted first so that it wins over the built-in simple-type and complex-type binders,
    /// which would otherwise try to bind a value object as an object with a <c>Value</c> property. It declines a
    /// greedy binding source, which MVC's own providers bind: a header, through this binder again, the body, through
    /// the input formatter, the services, and a binder named by <c>[ModelBinder(typeof(…))]</c>.
    /// </para>
    /// <para>
    /// This adds the binder alone. <see cref="AddValueObjects(IMvcBuilder)"/> also configures the JSON options, and
    /// records the code of a value refused inside a request body.
    /// </para>
    /// <para>
    /// The options hold one provider: calling this again, or
    /// <see cref="AddValueObjects(MvcOptions, Action{ValueObjectMessageOptions})"/>, keeps the provider where the first
    /// call put it, and the last configuration of its messages wins.
    /// </para>
    /// </remarks>
    public static MvcOptions AddValueObjects(this MvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _ = Provider(options);

        return options;
    }

    /// <summary>
    /// Binds value objects from the underlying value everywhere MVC reads text, giving a refusal the message the
    /// application's resource holds for its code.
    /// </summary>
    /// <param name="options">MVC options.</param>
    /// <param name="configure">Names the resource the messages are read from, through <see cref="ValueObjectMessageOptions.UseResource{TResource}"/>.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The binder is added as <see cref="AddValueObjects(MvcOptions)"/> adds it, and a value it refuses in a route,
    /// query, header or form value is reported with the message the resource holds under the key of the rule's code,
    /// <c>ValueObject.value_object.too_short</c>, looked up after <c>ValueObject.value_object.out_of_range.minimum</c> or
    /// <c>.maximum</c> for a type declaring one bound alone. It is formatted with the display name of the member as
    /// <c>{0}</c>, and the arguments the type's rules supply from <c>{1}</c>: a length, both bounds, the known values.
    /// The code under <c>errorCodes</c> is the same in every language, and a key the resource lacks keeps the English
    /// message. Blank text keeps the framework's message, which MVC's own message provider localises.
    /// </para>
    /// <para>
    /// The resource is read through the request's <c>IStringLocalizerFactory</c>: register
    /// <c>builder.Services.AddLocalization()</c>, without which a refusal throws an <see cref="InvalidOperationException"/>
    /// naming it, and <c>app.UseRequestLocalization()</c>, so that the request chooses the culture. The current UI culture
    /// chooses the template and the current culture formats the arguments. A template quoting an argument its code does
    /// not supply throws its <see cref="FormatException"/>, which is not caught.
    /// </para>
    /// <para>
    /// A request body is read by the input formatter, which <see cref="AddValueObjects(IMvcBuilder, Action{ValueObjectMessageOptions})"/>
    /// puts in place: on the options alone, a body keeps its English message.
    /// </para>
    /// </remarks>
    public static MvcOptions AddValueObjects(this MvcOptions options, Action<ValueObjectMessageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        configure(Provider(options).Messages);

        return options;
    }

    /// <summary>
    /// Registers value object model binding and JSON serialization on an MVC builder.
    /// </summary>
    /// <param name="builder">MVC builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// It also puts a formatter of its own in place of MVC's System.Text.Json input formatter, at its index: one
    /// deriving from it, which reads with the very serializer options the framework's formatter read with, and records
    /// the code of a value object a request body refuses, so that <see cref="AddValueObjectProblemDetails"/> puts it in
    /// the response under the JSON path, as it puts the code of a route or query value under the parameter. The model
    /// state keeps the messages it keeps without it, whatever <see cref="JsonOptions.AllowInputFormatterExceptionMessages"/>
    /// says, unless messages are configured, through
    /// <see cref="AddValueObjects(IMvcBuilder, Action{ValueObjectMessageOptions})"/> or on the options, which replace the
    /// message of the exception with the resource's under that option, its default. A change made to the JSON options
    /// later on reaches it as it reaches the framework's. A System.Text.Json formatter the application built with
    /// options of its own, or derived, is left as it is, and records no code.
    /// </para>
    /// <para>
    /// The swap is a post-configuration of <see cref="MvcOptions"/>. Removing the framework's formatter with
    /// <c>InputFormatters.RemoveType&lt;SystemTextJsonInputFormatter&gt;()</c> still works in <c>AddMvcOptions</c>, in
    /// <c>Configure&lt;MvcOptions&gt;</c> or in a post-configuration registered before this call; in one registered
    /// after it, that finds this formatter in its place, which is not of that exact type: remove there every formatter
    /// that is a <see cref="Microsoft.AspNetCore.Mvc.Formatters.SystemTextJsonInputFormatter"/>, derived included.
    /// </para>
    /// <para>
    /// Once Newtonsoft.Json reads the body, through <c>AddNewtonsoftJson()</c>, there is no such formatter to replace,
    /// and a body records no code; <c>AddValueObjectsNewtonsoftJson()</c>, from
    /// <c>AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson</c>, replaces Newtonsoft.Json's formatter instead.
    /// </para>
    /// </remarks>
    public static IMvcBuilder AddValueObjects(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddMvcOptions(static options => options.AddValueObjects());
        builder.AddJsonOptions(static options => options.JsonSerializerOptions.AddValueObjects());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Transient<IPostConfigureOptions<MvcOptions>, ValueObjectInputFormatterSetup>());

        return builder;
    }

    /// <summary>
    /// Registers value object model binding and JSON serialization on an MVC builder, giving a refusal, in a route,
    /// query, header or form value or in a JSON body, the message the application's resource holds for its code.
    /// </summary>
    /// <param name="builder">MVC builder.</param>
    /// <param name="configure">Names the resource the messages are read from, through <see cref="ValueObjectMessageOptions.UseResource{TResource}"/>.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Everything <see cref="AddValueObjects(IMvcBuilder)"/> registers is registered, and the messages are configured as
    /// <see cref="AddValueObjects(MvcOptions, Action{ValueObjectMessageOptions})"/> configures them, which says how they are
    /// looked up. The input formatter reads them too: a value object a JSON body refuses is reported under its JSON path
    /// with the message the resource holds for the code, quoting the name of the value object as <c>{0}</c>, since the
    /// path may hold a refused dictionary key, the refused value itself. A type the registry cannot describe, such as
    /// <c>AnyEntityId</c>, has the key of the code alone and no argument.
    /// </para>
    /// <para>
    /// A body's message is translated only where MVC answers with it, under
    /// <see cref="JsonOptions.AllowInputFormatterExceptionMessages"/>, its default; turned off, every error of a body is
    /// answered "The input was not valid.", which MVC's message provider holds. A body read by Newtonsoft.Json, through
    /// <c>AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson</c>, keeps its English message.
    /// </para>
    /// </remarks>
    public static IMvcBuilder AddValueObjects(this IMvcBuilder builder, Action<ValueObjectMessageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.AddValueObjects();
        builder.AddMvcOptions(options => options.AddValueObjects(configure));

        return builder;
    }

    /// <summary>
    /// Adds the stable error codes of the rejected value objects to the automatic 400 response.
    /// </summary>
    /// <param name="options">API behaviour options.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <remarks>
    /// The framework's own <c>ValidationProblemDetails</c> response is kept as is, with an <c>errorCodes</c>
    /// extension added next to <c>errors</c>: the human-readable messages stay where clients expect them, and a
    /// client that wants to branch on a rule now has something stable to branch on.
    /// </remarks>
    public static ApiBehaviorOptions AddValueObjectProblemDetails(this ApiBehaviorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var inner = options.InvalidModelStateResponseFactory;

        options.InvalidModelStateResponseFactory = context =>
        {
            var result = inner(context);

            var codes = ValueObjectProblemDetails.GetErrorCodes(context.HttpContext);
            if (codes.Count > 0 && result is ObjectResult { Value: ProblemDetails problem })
            {
                problem.Extensions[ValueObjectProblemDetails.ExtensionName] = codes;
            }

            return result;
        };

        return options;
    }

    /// <summary>
    /// Gets the value object binder provider of the options, inserting one first when there is none, so that it wins over
    /// the built-in simple-type and complex-type binders.
    /// </summary>
    private static ValueObjectModelBinderProvider Provider(MvcOptions options)
    {
        if (options.ModelBinderProviders.OfType<ValueObjectModelBinderProvider>().FirstOrDefault() is { } provider)
        {
            return provider;
        }

        provider = new ValueObjectModelBinderProvider();
        options.ModelBinderProviders.Insert(0, provider);

        return provider;
    }
}
