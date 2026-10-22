using System.Reflection;
using System.Runtime.Loader;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Serilog;
using Serilog;
using Serilog.Configuration;
using static AdCodicem.ValueObjects.UnitTests.Logging.SerilogCapture;

namespace AdCodicem.ValueObjects.UnitTests.Logging;

/// <summary>
/// The two overloads of <c>Destructure.ValueObjects</c>, <see cref="ValueObjectLoggingOptions"/> and the assemblies it
/// registers.
/// </summary>
public sealed class SerilogOptionsTests
{
    private const string Rank = "AdCodicem.ValueObjects.Fixtures.Untouched.Rank";

    /// <summary>The option is off and no assembly is named until the application says otherwise.</summary>
    [Fact]
    public void The_options_capture_nothing_without_at_and_name_no_assembly_by_default()
    {
        var options = new ValueObjectLoggingOptions();

        options.CaptureAsUnderlyingValue.Should().BeFalse();
        options.Assemblies.Should().BeEmpty();
        options.Assemblies.Add(typeof(SerilogOptionsTests).Assembly);
        options.Assemblies.Should().ContainSingle();
    }

    /// <summary>Both overloads return the configuration they were called on, so that calls chain.</summary>
    [Fact]
    public void Both_overloads_return_the_configuration_they_were_called_on()
    {
        var configuration = new LoggerConfiguration();

        configuration.Destructure.ValueObjects().Should().BeSameAs(configuration);
        configuration.Destructure.ValueObjects(static _ => { }).Should().BeSameAs(configuration);
        configuration.Destructure.ValueObjects(static options => options.CaptureAsUnderlyingValue = true).Should().BeSameAs(configuration);
    }

    /// <summary>A missing configuration or a missing callback is refused, naming it.</summary>
    [Fact]
    public void A_missing_configuration_or_callback_is_refused()
    {
        LoggerDestructuringConfiguration destructure = null!;

        FluentActions.Invoking(() => destructure.ValueObjects())
            .Should().Throw<ArgumentNullException>().WithParameterName("destructure");
        FluentActions.Invoking(() => destructure.ValueObjects(static _ => { }))
            .Should().Throw<ArgumentNullException>().WithParameterName("destructure");
        FluentActions.Invoking(() => new LoggerConfiguration().Destructure.ValueObjects(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("configure");
        FluentActions.Invoking(() => new LoggerConfiguration().Destructure.ValueObjects(static options => options.Assemblies.Add(null!)))
            .Should().Throw<ArgumentNullException>().WithParameterName("assembly");
    }

    /// <summary>The options overload with the option left off behaves as the overload without options.</summary>
    [Fact]
    public void The_options_overload_without_the_option_is_the_policy_alone()
    {
        var configuration = new LoggerConfiguration().Destructure.ValueObjects(static _ => { });

        Compact(Capture(configuration, "{X} {@D}", PageNumber.Create(42), PageNumber.Create(42)))
            .Should().Be("""{"@mt":"{X} {@D}","X":"42","D":42}""");
    }

    /// <summary>
    /// A logger configured before anything used the module declaring a value object finds it unregistered, and captures
    /// it as text; naming the assembly registers it first, so that the option captures it as its underlying value. Each
    /// assembly named is, two copies of one module loaded apart being two assemblies.
    /// </summary>
    [Fact]
    public void Every_assembly_named_is_registered_before_the_value_objects_registered_are_read()
    {
        Assembly[] copies = [FreshCopy("First named for the Serilog option"), FreshCopy("Second named for the Serilog option")];
        var ranks = copies.Select(static copy => copy.GetType(Rank, throwOnError: true)!).ToArray();
        ranks[0].Should().NotBe(ranks[1]);
        foreach (var rank in ranks)
        {
            ValueObjectRegistry.TryGet(rank, out _).Should().BeFalse("nothing has used this copy of the assembly yet");
        }

        var configuration = new LoggerConfiguration().Destructure.ValueObjects(options =>
        {
            options.CaptureAsUnderlyingValue = true;
            foreach (var copy in copies)
            {
                options.Assemblies.Add(copy);
            }
        });

        ranks.Should().AllSatisfy(static rank => ValueObjectRegistry.TryGet(rank, out _).Should().BeTrue());
        var logged = Capture(configuration, "{First} {Second}", Create(ranks[0], 5), Create(ranks[1], 6));
        Scalar(logged, "First").Should().Be(5);
        Scalar(logged, "Second").Should().Be(6);
    }

    /// <summary>Without the assembly named, the value object its module registers later is captured as its text.</summary>
    [Fact]
    public void An_assembly_left_unnamed_is_registered_too_late_for_the_option()
    {
        var copy = FreshCopy("Left unnamed for the Serilog option");
        var rank = copy.GetType(Rank, throwOnError: true)!;

        var configuration = new LoggerConfiguration().Destructure.ValueObjects(static options => options.CaptureAsUnderlyingValue = true);

        Scalar(Capture(configuration, "{X}", Create(rank, 5)), "X").Should().Be("5");
    }

    /// <summary>An assembly named is registered whether or not the option is on.</summary>
    [Fact]
    public void An_assembly_named_is_registered_without_the_option_too()
    {
        var copy = FreshCopy("Named without the Serilog option");
        var rank = copy.GetType(Rank, throwOnError: true)!;

        _ = new LoggerConfiguration().Destructure.ValueObjects(options => options.Assemblies.Add(copy));

        ValueObjectRegistry.TryGet(rank, out _).Should().BeTrue();
    }

    /// <summary>Loads a copy of the untouched fixture nothing has used, as a module the application has not reached yet.</summary>
    private static Assembly FreshCopy(string name)
        => new AssemblyLoadContext(name).LoadFromAssemblyPath(
            Path.Combine(AppContext.BaseDirectory, "AdCodicem.ValueObjects.Fixtures.Untouched.dll"));

    private static object Create(Type type, int value) => type.GetMethod("Create", [typeof(int)])!.Invoke(null, [value])!;
}
