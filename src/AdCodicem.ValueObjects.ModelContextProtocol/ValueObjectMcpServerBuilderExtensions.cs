using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AdCodicem.ValueObjects.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace AdCodicem.ValueObjects.ModelContextProtocol;

/// <summary>
/// Registers Model Context Protocol tools whose schemas carry the rules of their value objects, and answers a
/// value-object argument a rule refuses with that rule, as a tool execution error a model can correct from.
/// </summary>
/// <remarks>
/// <para>
/// The SDK's <c>WithTools</c> methods hand a tool no schema options, and the SDK describes a value object, serialized by
/// a converter of its own, as <c>true</c>, the schema that accepts anything; it answers the exception binding a refused
/// argument throws with <c>An error occurred invoking 'place_order'.</c>, which loses the rule. Each method here mirrors
/// one of the SDK's, finding the same methods and creating an instance of an instance method's type for each call, and
/// closes both gaps:
/// </para>
/// <code>
/// builder.Services
///     .AddMcpServer()
///     .WithHttpTransport()
///     .WithValueObjectTools&lt;OrderTools&gt;();
/// </code>
/// <para>
/// Each value-object argument is read, before the tool binds it, through the contract the tool's serializer options hold
/// for its parameter, which runs the value object's converter and its rules, never <c>CreateUnchecked</c>. The first
/// refused, in the order of the parameters, is answered instead of calling the tool, and no instance of its type is
/// created:
/// </para>
/// <code>
/// {"isError":true,"content":[{"type":"text","text":"Argument 'quantity' rejected (value_object.out_of_range): The value is not a valid Quantity: The value must be less than or equal to 100."}],
///  "structuredContent":{"error":"invalid_argument","argument":"quantity","code":"value_object.out_of_range","message":"The value is not a valid Quantity: The value must be less than or equal to 100."}}
/// </code>
/// <para>
/// The structured content is left out for a tool that declares an output schema, which it would not conform to. The
/// message names the value object and the rule, never the value sent. An absent argument the parameter has no default
/// for, and a <c>null</c> for a value object that cannot be one, are <c>value_object.required</c>; a collection, a
/// dictionary or an object is answered for a value object it holds, any other error being left to the tool. Parameters
/// the SDK binds itself, the server, the request context, a progress reporter, a cancellation token or a service, are
/// never read.
/// </para>
/// <para>
/// The check is a call-tool filter, added once whatever the number of calls, after every filter the server's options
/// are configured with, so that it runs after the application's own filters and authorization; a filter added later,
/// in <c>HttpServerTransportOptions.ConfigureSessionOptions</c>, runs inside it, unless
/// <see cref="ValueObjectMcpServerOptionsExtensions.AddValueObjectValidation"/> places it again there. The SDK refuses
/// call-tool filters beside an explicit <c>CallToolWithAlternateHandler</c>, and throws when it creates the server: at
/// its start over stdio, for each session over HTTP, for each request when the server is stateless.
/// </para>
/// <para>
/// Where reflection-based serialization is disabled, as native AOT disables it, <c>serializerOptions</c> has to describe
/// every parameter and result: a copy of <see cref="McpJsonUtilities.DefaultOptions"/> whose
/// <see cref="JsonSerializerOptions.TypeInfoResolverChain"/> starts with a source-generated context naming
/// <see cref="ValueObjectJsonConverterFactory"/> and the types of the parameters and results. Without it, the SDK throws
/// a <see cref="NotSupportedException"/> when the server is first resolved.
/// </para>
/// </remarks>
public static class ValueObjectMcpServerBuilderExtensions
{
    /// <summary>The methods a tool type is searched for tools among, as the SDK searches them.</summary>
    private const BindingFlags ToolMethods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>Why the methods that discover tool types by reflection are not trimming-safe.</summary>
    private const string Discovery =
        "Discovers tool methods by reflection, as the SDK's non-generic WithTools and WithToolsFromAssembly do, and might not work in native AOT. Use WithValueObjectTools<TToolType>() instead.";

    /// <summary>
    /// Registers the <see cref="McpServerToolAttribute"/> methods of a type as
    /// <see cref="McpServerBuilderExtensions.WithTools{TToolType}(IMcpServerBuilder, JsonSerializerOptions)"/> does, with
    /// the rules of each value object in their schemas, and answers a refused value-object argument with its rule.
    /// </summary>
    /// <typeparam name="TToolType">The tool type, whose static and instance methods, public and not, are searched; an
    /// instance of it is created for each call of an instance method, its constructor's parameters resolved from the
    /// services of the call.</typeparam>
    /// <param name="builder">The builder of the server.</param>
    /// <param name="serializerOptions">The options the arguments and results are serialized with, or
    /// <see langword="null"/> for <see cref="McpJsonUtilities.DefaultOptions"/>.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IMcpServerBuilder WithValueObjectTools<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.NonPublicMethods |
        DynamicallyAccessedMemberTypes.PublicConstructors)] TToolType>(
        this IMcpServerBuilder builder,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var method in typeof(TToolType).GetMethods(ToolMethods))
        {
            if (method.GetCustomAttribute<McpServerToolAttribute>() is null)
            {
                continue;
            }

            builder.Services.AddSingleton(services => ValueObjectMcpServerTool.Create(
                method,
                method.IsStatic ? null : static request => ToolTargets.Create(request.Services, typeof(TToolType)),
                services,
                serializerOptions));
        }

        return builder.WithValueObjectArguments();
    }

    /// <summary>
    /// Registers the <see cref="McpServerToolAttribute"/> methods of each type as
    /// <see cref="McpServerBuilderExtensions.WithTools(IMcpServerBuilder, IEnumerable{Type}, JsonSerializerOptions)"/> does,
    /// with the rules of each value object in their schemas, and answers a refused value-object argument with its rule.
    /// </summary>
    /// <param name="builder">The builder of the server.</param>
    /// <param name="toolTypes">The tool types; a <see langword="null"/> among them is skipped.</param>
    /// <param name="serializerOptions">The options the arguments and results are serialized with, or
    /// <see langword="null"/> for <see cref="McpJsonUtilities.DefaultOptions"/>.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="toolTypes"/> is
    /// <see langword="null"/>.</exception>
    /// <remarks>A static class, which cannot be a type argument, is registered here.</remarks>
    [RequiresUnreferencedCode(Discovery)]
    public static IMcpServerBuilder WithValueObjectTools(
        this IMcpServerBuilder builder,
        IEnumerable<Type> toolTypes,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(toolTypes);

        foreach (var toolType in toolTypes)
        {
            if (toolType is null)
            {
                continue;
            }

            foreach (var method in toolType.GetMethods(ToolMethods))
            {
                if (method.GetCustomAttribute<McpServerToolAttribute>() is null)
                {
                    continue;
                }

                builder.Services.AddSingleton(services => ValueObjectMcpServerTool.Create(
                    method,
                    method.IsStatic ? null : request => ToolTargets.Create(request.Services, toolType),
                    services,
                    serializerOptions));
            }
        }

        return builder.WithValueObjectArguments();
    }

    /// <summary>
    /// Registers the <see cref="McpServerToolAttribute"/> methods of every <see cref="McpServerToolTypeAttribute"/> type of
    /// an assembly as
    /// <see cref="McpServerBuilderExtensions.WithToolsFromAssembly(IMcpServerBuilder, Assembly, JsonSerializerOptions)"/>
    /// does, with the rules of each value object in their schemas, and answers a refused value-object argument with its
    /// rule.
    /// </summary>
    /// <param name="builder">The builder of the server.</param>
    /// <param name="toolAssembly">The assembly, or <see langword="null"/> for the one that calls this method.</param>
    /// <param name="serializerOptions">The options the arguments and results are serialized with, or
    /// <see langword="null"/> for <see cref="McpJsonUtilities.DefaultOptions"/>.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode(Discovery)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IMcpServerBuilder WithValueObjectToolsFromAssembly(
        this IMcpServerBuilder builder,
        Assembly? toolAssembly = null,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Not inlined, so that the calling assembly is the caller's, not this one's.
        toolAssembly ??= Assembly.GetCallingAssembly();

        return builder.WithValueObjectTools(
            toolAssembly.GetTypes().Where(static type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null),
            serializerOptions);
    }

    /// <summary>
    /// Registers tools as <see cref="McpServerBuilderExtensions.WithTools(IMcpServerBuilder, IEnumerable{McpServerTool})"/>
    /// does, and answers a refused value-object argument of those <see cref="ValueObjectMcpServerTool"/> created with its
    /// rule.
    /// </summary>
    /// <param name="builder">The builder of the server.</param>
    /// <param name="tools">The tools; a <see langword="null"/> among them is skipped.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="tools"/> is
    /// <see langword="null"/>.</exception>
    /// <remarks>
    /// A tool <see cref="ValueObjectMcpServerTool.Create(MethodInfo, object, McpServerToolCreateOptions)"/> created
    /// carries the rules in its schemas wherever it is registered; registered through the SDK's <c>WithTools</c> alone,
    /// on a server none of these methods configured, its refused arguments get the SDK's bare error, unless the
    /// server's options are handed to <see cref="ValueObjectMcpServerOptionsExtensions.AddValueObjectValidation"/>.
    /// </remarks>
    public static IMcpServerBuilder WithValueObjectTools(this IMcpServerBuilder builder, IEnumerable<McpServerTool> tools)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(tools);

        return builder.WithTools(tools).WithValueObjectArguments();
    }

    /// <summary>
    /// Adds, once per service collection, the filter that checks the value-object arguments of every tool created with
    /// the rules.
    /// </summary>
    private static IMcpServerBuilder WithValueObjectArguments(this IMcpServerBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<McpServerOptions>, ValueObjectArgumentFilter>());
        return builder;
    }
}
