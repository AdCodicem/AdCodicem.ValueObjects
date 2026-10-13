using System.Globalization;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AzureFunctions;
using AdCodicem.ValueObjects.AzureFunctions.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdCodicem.ValueObjects.Fixtures.Functions;

/// <summary>
/// Starts the worker in the configuration <c>FIXTURE_ORDER</c> names: <c>after</c>, the current template's builder with
/// the problem details registered after the ASP.NET Core integration and <c>UseRequestLocalization()</c> in its pipeline;
/// <c>hostbuilder</c>, the <see cref="HostBuilder"/> style, which registers them inside the integration's call, the
/// culture chosen by the request localization options alone; and <c>before</c>, the problem details registered before
/// the integration, which keeps the worker's 500.
/// </summary>
internal static class Program
{
    private static readonly CultureInfo[] Cultures = [CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("fr-FR")];

    private static void Main(string[] args)
    {
        switch (Environment.GetEnvironmentVariable("FIXTURE_ORDER"))
        {
            case "before":
                {
                    var builder = FunctionsApplication.CreateBuilder(args);
                    builder.UseValueObjects().UseValueObjectProblemDetails();
                    builder.ConfigureFunctionsWebApplication();
                    builder.Services.AddMvc().AddValueObjects();
                    builder.Build().Run();
                    break;
                }

            case "hostbuilder":
                new HostBuilder()
                    .ConfigureFunctionsWebApplication(static worker => Configure(worker, startupFilter: false))
                    .Build()
                    .Run();
                break;

            default:
                {
                    var builder = FunctionsApplication.CreateBuilder(args);
                    builder.ConfigureFunctionsWebApplication();
                    Configure(builder, startupFilter: true);
                    builder.Build().Run();
                    break;
                }
        }
    }

    /// <summary>
    /// Binds value objects, answers a refused one with problem details in the request's language, and reads a
    /// <c>[FromBody]</c> one through the MVC package.
    /// </summary>
    private static void Configure(IFunctionsWorkerApplicationBuilder worker, bool startupFilter)
    {
        worker.UseValueObjects();
        worker.UseValueObjectProblemDetails(static messages => messages.UseResource<ProblemMessages>());
        worker.Services.AddMvc().AddValueObjects();
        worker.Services.AddLocalization();
        worker.Services.AddRequestLocalization(static options =>
        {
            options.DefaultRequestCulture = new RequestCulture(Cultures[0]);
            options.SupportedCultures = Cultures;
            options.SupportedUICultures = Cultures;
        });
        if (startupFilter)
        {
            worker.Services.AddTransient<IStartupFilter, LocalizationStartupFilter>();
        }
    }

    /// <summary>Puts <c>UseRequestLocalization()</c> in front of the ASP.NET Core integration's pipeline.</summary>
    private sealed class LocalizationStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRequestLocalization();
            next(app);
        };
    }
}
