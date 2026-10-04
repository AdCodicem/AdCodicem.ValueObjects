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
    /// which would otherwise try to bind a value object as an object with a <c>Value</c> property.
    /// </para>
    /// <para>
    /// This adds the binder alone. <see cref="AddValueObjects(IMvcBuilder)"/> also configures the JSON options, and
    /// records the code of a value refused inside a request body.
    /// </para>
    /// </remarks>
    public static MvcOptions AddValueObjects(this MvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ModelBinderProviders.Insert(0, new ValueObjectModelBinderProvider());

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
    /// says, and a change made to the JSON options later on reaches it as it reaches the framework's. A System.Text.Json
    /// formatter the application built with options of its own, or derived, is left as it is, and records no code.
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
    /// and a body records no code.
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
}
