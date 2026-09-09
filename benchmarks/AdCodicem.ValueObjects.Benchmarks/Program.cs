using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace AdCodicem.ValueObjects.Benchmarks;

/// <summary>
/// Entry point. Run all suites with <c>dotnet run -c Release -- --filter *</c>, or one with
/// <c>--filter *WrapperCost*</c>.
/// </summary>
internal static class Program
{
    private static void Main(string[] args)
        => BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(args, DefaultConfig.Instance.WithOptions(ConfigOptions.DisableOptimizationsValidator));
}
