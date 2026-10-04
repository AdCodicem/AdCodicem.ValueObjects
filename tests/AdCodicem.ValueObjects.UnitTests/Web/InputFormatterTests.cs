using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.Formatters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The System.Text.Json input formatter <c>AddValueObjects()</c> puts in place of MVC's, read from the options an
/// application resolves rather than through a request.
/// </summary>
public class InputFormatterTests
{
    /// <summary>
    /// The formatter takes the place of the framework's, with the serializer settings the application configured in
    /// options of its own, and leaves the application's options as the application left them.
    /// </summary>
    [Fact]
    public void The_framework_formatter_is_replaced_in_place_with_the_application_s_settings()
    {
        using var provider = Build(static mvc => mvc
            .AddJsonOptions(static options => options.JsonSerializerOptions.AllowTrailingCommas = true)
            .AddValueObjects());
        using var withoutPackage = Build(static _ => { });
        var framework = withoutPackage.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;

        var formatters = provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;
        var application = provider.GetRequiredService<IOptions<JsonOptions>>().Value;

        var index = framework.ToList().FindIndex(static formatter => formatter is SystemTextJsonInputFormatter);
        var formatter = formatters[index].Should().BeOfType<ValueObjectJsonInputFormatter>().Subject;
        formatters.OfType<SystemTextJsonInputFormatter>().Should().ContainSingle();
        formatters.Should().HaveSameCount(framework);
        formatter.SerializerOptions.AllowTrailingCommas.Should().BeTrue();
        formatter.SerializerOptions.Should().NotBeSameAs(application.JsonSerializerOptions);
        application.AllowInputFormatterExceptionMessages.Should().BeTrue();
        formatter.SupportedMediaTypes.Should().Equal(framework[index].Should().BeAssignableTo<InputFormatter>().Subject.SupportedMediaTypes);
    }

    [Fact]
    public void Adding_value_objects_twice_replaces_the_formatter_once()
    {
        using var provider = Build(static mvc => mvc.AddValueObjects().AddValueObjects());

        provider.GetServices<IPostConfigureOptions<MvcOptions>>().OfType<ValueObjectInputFormatterSetup>().Should().ContainSingle();
        provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<ValueObjectJsonInputFormatter>().Should().ContainSingle();
    }

    /// <summary>
    /// An application that took the framework formatter out, as Newtonsoft.Json does, gets none back: there is no
    /// System.Text.Json formatter to replace.
    /// </summary>
    [Fact]
    public void An_application_without_the_framework_formatter_gets_none()
    {
        using var provider = Build(static mvc => mvc
            .AddMvcOptions(static options => options.InputFormatters.RemoveType<SystemTextJsonInputFormatter>())
            .AddValueObjects());

        provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<SystemTextJsonInputFormatter>().Should().BeEmpty();
    }

    [Fact]
    public async Task Reading_no_context_is_refused()
    {
        using var provider = Build(static mvc => mvc.AddValueObjects());
        var formatter = provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<ValueObjectJsonInputFormatter>().Single();

        await FluentActions.Awaiting(() => formatter.ReadAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
        FluentActions.Invoking(() => new ValueObjectInputFormatterSetup(
                provider.GetRequiredService<IOptionsFactory<JsonOptions>>(),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>())
            .PostConfigure(null, null!))
            .Should().Throw<ArgumentNullException>();
    }

    private static ServiceProvider Build(Action<IMvcBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services.AddControllers());

        return services.BuildServiceProvider();
    }
}
