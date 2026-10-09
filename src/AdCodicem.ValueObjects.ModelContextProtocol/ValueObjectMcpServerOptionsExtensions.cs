using System.Reflection;
using ModelContextProtocol.Server;

namespace AdCodicem.ValueObjects.ModelContextProtocol;

/// <summary>
/// Adds the check of value-object arguments to server options the application builds or changes itself.
/// </summary>
public static class ValueObjectMcpServerOptionsExtensions
{
    /// <summary>
    /// Places the check of the value-object arguments of the tools
    /// <see cref="ValueObjectMcpServerTool.Create(MethodInfo, object, McpServerToolCreateOptions)"/> created last among
    /// the call-tool filters of the options, once, so that it runs inside every filter already there.
    /// </summary>
    /// <param name="options">The options of the server, before the server is created from them.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The <c>WithValueObjectTools</c> methods of <see cref="ValueObjectMcpServerBuilderExtensions"/> add the check
    /// themselves, once the options are configured. Call this method where nothing does, on the options of a server
    /// created without dependency injection:
    /// </para>
    /// <code>
    /// var options = new McpServerOptions { ToolCollection = [ValueObjectMcpServerTool.Create(method)] };
    /// await using var server = McpServer.Create(transport, options.AddValueObjectValidation());
    /// </code>
    /// <para>
    /// or after adding a call-tool filter of the application's own where the options are changed later, in
    /// <c>HttpServerTransportOptions.ConfigureSessionOptions</c> for instance, which runs for each session after the
    /// options are configured: the check, already there, moves after that filter. The server builds its pipeline when it
    /// is created, from the filters the options hold then. The SDK refuses call-tool filters beside an explicit
    /// <c>CallToolWithAlternateHandler</c>, and throws when the server is created.
    /// </para>
    /// </remarks>
    public static McpServerOptions AddValueObjectValidation(this McpServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var filters = options.Filters.Request.CallToolFilters;
        filters.Remove(ValueObjectArgumentFilter.Filter);
        filters.Add(ValueObjectArgumentFilter.Filter);

        return options;
    }
}
