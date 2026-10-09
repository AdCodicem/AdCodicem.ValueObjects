using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AdCodicem.ValueObjects.AI;
using AdCodicem.ValueObjects.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AdCodicem.ValueObjects.ModelContextProtocol;

/// <summary>
/// Creates Model Context Protocol tools whose schemas carry the rules of their value objects.
/// </summary>
/// <remarks>
/// <para>
/// A tool created here is the SDK's own, built by <see cref="McpServerTool.Create(MethodInfo, object, McpServerToolCreateOptions)"/>
/// with schema options that describe each value object, alone, nullable, or held by a collection, a dictionary or an
/// object, with the rules declared on it, in its <c>inputSchema</c>, and in its <c>outputSchema</c> when it returns
/// structured content. The SDK rewrites the output schema of its own tools for a client on a protocol version before
/// <c>2026-07-28</c>, so the tool is never wrapped.
/// </para>
/// <para>
/// Its value-object arguments are checked on a server that one of the <c>WithValueObjectTools</c> methods of
/// <see cref="ValueObjectMcpServerBuilderExtensions"/> configured: register it with
/// <see cref="ValueObjectMcpServerBuilderExtensions.WithValueObjectTools(IMcpServerBuilder, IEnumerable{McpServerTool})"/>,
/// or, on a server created without dependency injection, hand its options to
/// <see cref="ValueObjectMcpServerOptionsExtensions.AddValueObjectValidation"/>. The SDK's <c>WithTools</c> alone
/// publishes its schemas and leaves a refused argument to the SDK's bare error.
/// </para>
/// </remarks>
public static class ValueObjectMcpServerTool
{
    /// <summary>
    /// The schema options of a tool the registrations build, which the SDK hands none.
    /// </summary>
    private static readonly AIJsonSchemaCreateOptions Rules = ((AIJsonSchemaCreateOptions?)null).WithValueObjects();

    /// <summary>
    /// The parameters to check of each tool created here, kept beside the tool, and as long as it lives.
    /// </summary>
    private static readonly ConditionalWeakTable<McpServerTool, ValueObjectArguments> Checked = [];

    /// <summary>
    /// Creates a tool for a method as <see cref="McpServerTool.Create(MethodInfo, object, McpServerToolCreateOptions)"/>
    /// does, with the rules of each value object in its schemas.
    /// </summary>
    /// <param name="method">The method the tool calls.</param>
    /// <param name="target">The instance the method runs on, or <see langword="null"/> for a static method.</param>
    /// <param name="options">The options of the tool, copied and never changed: a transform its
    /// <see cref="McpServerToolCreateOptions.SchemaCreateOptions"/> carries runs first, and the value objects are
    /// described after it.</param>
    /// <returns>The SDK's tool, whose value-object arguments a server one of the <c>WithValueObjectTools</c> methods
    /// configured checks, or one whose options
    /// <see cref="ValueObjectMcpServerOptionsExtensions.AddValueObjectValidation"/> was given.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="method"/> is an instance method and <paramref name="target"/>
    /// is <see langword="null"/>, which the SDK refuses.</exception>
    public static McpServerTool Create(MethodInfo method, object? target = null, McpServerToolCreateOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(method);

        var described = Describe(options);
        return Track(McpServerTool.Create(method, target, described), method, described.SerializerOptions);
    }

    /// <summary>
    /// Creates a tool for a method of a tool type as the SDK's registrations do: a static method alone, an instance method
    /// on an instance the factory creates for each call.
    /// </summary>
    /// <param name="method">The method the tool calls.</param>
    /// <param name="createTarget">Creates the instance of each call, or <see langword="null"/> for a static method.</param>
    /// <param name="services">The services of the server, which the SDK binds the parameters of the method from.</param>
    /// <param name="serializerOptions">The options the arguments are bound with, or <see langword="null"/> for the SDK's.</param>
    /// <returns>The SDK's tool.</returns>
    internal static McpServerTool Create(
        MethodInfo method,
        Func<RequestContext<CallToolRequestParams>, object>? createTarget,
        IServiceProvider services,
        JsonSerializerOptions? serializerOptions)
    {
        var options = new McpServerToolCreateOptions { Services = services, SerializerOptions = serializerOptions, SchemaCreateOptions = Rules };
        var tool = createTarget is null
            ? McpServerTool.Create(method, target: null, options)
            : McpServerTool.Create(method, createTarget, options);

        return Track(tool, method, serializerOptions);
    }

    /// <summary>
    /// Finds the parameters to check of a tool created here.
    /// </summary>
    /// <param name="tool">The tool a call matched.</param>
    /// <returns>The parameters, or <see langword="null"/> for a tool created elsewhere, or with none that may hold a
    /// value object.</returns>
    internal static ValueObjectArguments? ArgumentsOf(McpServerTool tool)
        => Checked.TryGetValue(tool, out var arguments) ? arguments : null;

    /// <summary>
    /// Copies the options of a tool, every property but the schema options as it is, and those with the value objects
    /// described after any transform they carry.
    /// </summary>
    /// <param name="options">The options, or <see langword="null"/>.</param>
    /// <returns>The copy.</returns>
    /// <remarks>The SDK's own copy is internal: a property it adds is a property to add here.</remarks>
    internal static McpServerToolCreateOptions Describe(McpServerToolCreateOptions? options)
        => options is null
            ? new McpServerToolCreateOptions { SchemaCreateOptions = Rules }
            : new McpServerToolCreateOptions
            {
                Services = options.Services,
                Name = options.Name,
                Description = options.Description,
                Title = options.Title,
                Destructive = options.Destructive,
                Idempotent = options.Idempotent,
                OpenWorld = options.OpenWorld,
                ReadOnly = options.ReadOnly,
                UseStructuredContent = options.UseStructuredContent,
                OutputSchema = options.OutputSchema,
                SerializerOptions = options.SerializerOptions,
                SchemaCreateOptions = options.SchemaCreateOptions.WithValueObjects(),
                Metadata = options.Metadata,
                Icons = options.Icons,
                Meta = options.Meta,
            };

    /// <summary>
    /// Keeps, beside a tool, the parameters of its method that may hold a value object, read as the SDK binds them.
    /// </summary>
    private static McpServerTool Track(McpServerTool tool, MethodInfo method, JsonSerializerOptions? serializerOptions)
    {
        // The SDK binds the arguments with McpJsonUtilities.DefaultOptions when it is handed no options.
        if (ValueObjectArguments.For(method, tool.ProtocolTool.InputSchema, serializerOptions ?? McpJsonUtilities.DefaultOptions) is { } arguments)
        {
            Checked.AddOrUpdate(tool, arguments);
        }

        return tool;
    }
}
