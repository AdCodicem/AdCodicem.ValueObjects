using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Runs a test of this assembly again, alone, in a child process whose runtime supports no dynamic code, as native AOT's
/// does: a branch guarded by <see cref="RuntimeFeature.IsDynamicCodeSupported"/>, which a process reads once, then takes
/// under the JIT the side a native binary takes.
/// </summary>
/// <remarks>
/// The child is this assembly, started by the <c>dotnet</c> host of the running runtime over a copy of its runtime
/// configuration that sets <c>System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported</c> to
/// <see langword="false"/>, as the <c>DynamicCodeSupport</c> property does, and running the one test through xUnit's own
/// runner, its default reporter alone, never one a CI environment enables. Without dynamic code, a test there creates no
/// NSubstitute proxy and no Entity Framework Core context, and its class runs nothing of the kind in its constructor.
/// </remarks>
internal static class WithoutDynamicCode
{
    private const string Switch = "System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported";

    /// <summary>Marks the child, which would otherwise start one of its own if the switch were not read.</summary>
    private const string Child = "ADCODICEM_WITHOUT_DYNAMIC_CODE";

    /// <summary>
    /// Runs a test in a child process without dynamic code, unless this process is that child.
    /// </summary>
    /// <param name="test">The class of the test.</param>
    /// <param name="method">The test method.</param>
    /// <returns>
    /// <see langword="true"/> once the test passed in the child, where the caller returns; <see langword="false"/> in the
    /// child, where the caller runs its assertions.
    /// </returns>
    public static async Task<bool> RanInAChildAsync(Type test, string method)
    {
        if (Environment.GetEnvironmentVariable(Child) is not null || !RuntimeFeature.IsDynamicCodeSupported)
        {
            RuntimeFeature.IsDynamicCodeSupported.Should().BeFalse("the child's runtime configuration turns dynamic code off");
            return false;
        }

        var cancellation = TestContext.Current.CancellationToken;
        var assembly = typeof(WithoutDynamicCode).Assembly.Location;
        var directory = Directory.CreateTempSubdirectory("adcodicem-without-dynamic-code-");
        try
        {
            var configuration = JsonNode.Parse(await File.ReadAllTextAsync(Path.ChangeExtension(assembly, ".runtimeconfig.json"), cancellation))!;
            (configuration["runtimeOptions"]!["configProperties"] ??= new JsonObject())[Switch] = false;
            var runtimeConfiguration = Path.Combine(directory.FullName, "runtimeconfig.json");
            await File.WriteAllTextAsync(runtimeConfiguration, configuration.ToJsonString(), cancellation);

            var start = new ProcessStartInfo(Host())
            {
                ArgumentList =
                {
                    "exec", "--runtimeconfig", runtimeConfiguration, "--depsfile", Path.ChangeExtension(assembly, ".deps.json"),
                    assembly, "-method", $"{test.FullName}.{method}", "-noLogo", "-noColor", "-noAutoReporters",
                },
                WorkingDirectory = directory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment = { [Child] = "1" },
            };

            using var child = Process.Start(start)!;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            var output = child.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = child.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await child.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                child.Kill(entireProcessTree: true);
                throw;
            }

            var report = await output + await error;
            child.ExitCode.Should().Be(0, "the test passes without dynamic code:{0}{1}", Environment.NewLine, report);
            report.Should().Contain("Total: 1, Errors: 0, Failed: 0, Skipped: 0", "the child ran the one test");
            return true;
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Gets the <c>dotnet</c> host of the running runtime, three folders above it, as a framework-dependent installation
    /// lays them out.
    /// </summary>
    internal static string Host()
        => Path.GetFullPath(Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(),
            "..",
            "..",
            "..",
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
}
