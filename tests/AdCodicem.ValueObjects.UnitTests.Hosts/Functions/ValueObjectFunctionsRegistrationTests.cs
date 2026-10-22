using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AzureFunctions;
using AdCodicem.ValueObjects.AzureFunctions.AspNetCore;
using AdCodicem.ValueObjects.UnitTests.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.UnitTests.Functions;

/// <summary>
/// The two registrations: the converter first among the worker's input converters, with or without the ASP.NET Core
/// integration, in both builder styles the templates use, or right behind the integration's own converter when called
/// before it; and the middleware, added once, with the messages of the last call naming some.
/// </summary>
public sealed class ValueObjectFunctionsRegistrationTests
{
    /// <summary>
    /// The worker's own converters, in the order it registers them, which the value object converter goes in front of.
    /// </summary>
    private static readonly string[] WorkerConverters =
    [
        "FunctionContextConverter", "TypeConverter", "GuidConverter", "DateTimeConverter", "MemoryConverter",
        "StringToByteConverter", "JsonPocoConverter", "ArrayConverter", "CancellationTokenConverter",
    ];

    [Fact]
    public void The_converter_goes_first_among_the_workers_converters_once()
    {
        using var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults(static worker => worker.UseValueObjects().UseValueObjects())
            .Build();

        ConverterNames(host.Services).Should().Equal(["ValueObjectInputConverter", .. WorkerConverters]);
    }

    /// <summary>
    /// The ASP.NET Core integration registers its own converter at the front, from a configuration that runs before the
    /// application's: the value object converter goes before it, and neither reads what the other does.
    /// </summary>
    [Fact]
    public void The_converter_goes_before_the_converter_of_the_ASP_NET_Core_integration()
    {
        using var host = new HostBuilder()
            .ConfigureFunctionsWebApplication(static worker => worker.UseValueObjects())
            .Build();

        ConverterNames(host.Services).Should().Equal(["ValueObjectInputConverter", "HttpContextConverter", .. WorkerConverters]);
    }

    /// <summary>The builder <c>FunctionsApplication.CreateBuilder(args)</c> returns takes the same calls.</summary>
    [Fact]
    public void The_application_builder_of_the_current_template_takes_both_calls()
    {
        var builder = FunctionsApplication.CreateBuilder([]);
        builder.ConfigureFunctionsWebApplication();
        builder.UseValueObjects().UseValueObjectProblemDetails();
        using var host = builder.Build();

        ConverterNames(host.Services).Should().StartWith(["ValueObjectInputConverter", "HttpContextConverter"]);
        host.Services.GetRequiredService<ValueObjectProblemDetailsMiddleware>().Should().NotBeNull();
    }

    /// <summary>
    /// Called before <c>ConfigureFunctionsWebApplication()</c>, the converter goes to the front first, and the
    /// integration's configuration, which runs later, puts its own converter in front of it: the value object converter
    /// comes right behind one that binds an <c>HttpRequest</c> and an <c>HttpRequestData</c> alone.
    /// </summary>
    [Fact]
    public void Called_before_the_integration_the_converter_comes_right_behind_the_integrations()
    {
        var builder = FunctionsApplication.CreateBuilder([]);
        builder.UseValueObjects();
        builder.ConfigureFunctionsWebApplication();
        using var host = builder.Build();

        ConverterNames(host.Services).Should().Equal(["HttpContextConverter", "ValueObjectInputConverter", .. WorkerConverters]);
    }

    [Fact]
    public void A_missing_builder_or_configuration_is_refused()
    {
        var builder = Substitute.For<IFunctionsWorkerApplicationBuilder>();

        FluentActions.Invoking(() => ValueObjectFunctionsWorkerApplicationBuilderExtensions.UseValueObjects(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(() => ValueObjectProblemDetailsWorkerApplicationBuilderExtensions.UseValueObjectProblemDetails(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(() => ValueObjectProblemDetailsWorkerApplicationBuilderExtensions.UseValueObjectProblemDetails(null!, static _ => { }))
            .Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(() => builder.UseValueObjectProblemDetails(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    /// <summary>
    /// Called more than once, the middleware is added once, the messages of the last call naming some are kept, and a call
    /// naming none keeps them.
    /// </summary>
    [Fact]
    public void The_middleware_is_added_once_with_the_messages_of_the_last_call_naming_some()
    {
        IServiceCollection? services = null;
        using var host = new HostBuilder()
            .ConfigureFunctionsWebApplication(worker =>
            {
                worker.UseValueObjectProblemDetails()
                    .UseValueObjectProblemDetails(static messages => messages.UseResource<SparseMessages>())
                    .UseValueObjectProblemDetails(static messages => messages.UseResource<ValidationMessages>())
                    .UseValueObjectProblemDetails();
                services = worker.Services;
            })
            .Build();

        services!.Count(static service => service.ServiceType == typeof(ValueObjectProblemDetailsMiddleware)).Should().Be(1);
        services!.Count(static service => service.ServiceType == typeof(ValueObjectProblemDetailsSettings)).Should().Be(1);
        host.Services.GetRequiredService<ValueObjectProblemDetailsSettings>().Messages!.ResourceType.Should().Be<ValidationMessages>();
        host.Services.GetRequiredService<ValueObjectProblemDetailsMiddleware>().Should().BeSameAs(host.Services.GetRequiredService<ValueObjectProblemDetailsMiddleware>());
    }

    [Fact]
    public void Without_messages_the_English_ones_are_kept()
    {
        using var host = new HostBuilder()
            .ConfigureFunctionsWebApplication(static worker => worker.UseValueObjectProblemDetails())
            .Build();

        host.Services.GetRequiredService<ValueObjectProblemDetailsSettings>().Messages.Should().BeNull();
    }

    private static IEnumerable<string> ConverterNames(IServiceProvider services)
        => services.GetRequiredService<IOptions<WorkerOptions>>().Value.InputConverters.Select(static converter => converter.Name);
}
