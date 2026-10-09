using AdCodicem.ValueObjects.Shared;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AdCodicem.ValueObjects.ModelContextProtocol;

/// <summary>
/// Adds to a server's options, once they are configured, the call-tool filter that checks the value-object arguments of
/// the tools <see cref="ValueObjectMcpServerTool"/> created, and answers the first refused with its rule.
/// </summary>
/// <remarks>
/// Placed after every filter the options were configured with, the filter is the last of the list, which makes it the
/// innermost: it runs after the application's own filters and the SDK's authorization, right before the tool binds its
/// arguments, as a wrapper around the tool would have run. A filter added once the options are built, in
/// <c>HttpServerTransportOptions.ConfigureSessionOptions</c> for instance, comes after it, unless
/// <see cref="ValueObjectMcpServerOptionsExtensions.AddValueObjectValidation"/> places it again. It is the SDK's tool
/// that the server lists, so that the SDK still rewrites its output schema for a client on a protocol version before
/// <c>2026-07-28</c>, which it does for its own tools alone.
/// </remarks>
internal sealed class ValueObjectArgumentFilter : IPostConfigureOptions<McpServerOptions>
{
    /// <summary>
    /// Checks the arguments of a call to a tool created with the rules, and passes any other call on.
    /// </summary>
    internal static readonly McpRequestFilter<CallToolRequestParams, CallToolResult> Filter = static next => (request, cancellationToken) =>
        request.MatchedPrimitive is McpServerTool tool
        && ValueObjectMcpServerTool.ArgumentsOf(tool) is { } arguments
        && arguments.FirstRejection(request.Params.Arguments) is { } rejection
            ? new ValueTask<CallToolResult>(Answer(rejection, structured: tool.ProtocolTool.OutputSchema is null))
            : next(request, cancellationToken);

    /// <inheritdoc />
    public void PostConfigure(string? name, McpServerOptions options) => options.AddValueObjectValidation();

    /// <summary>
    /// Answers a refused argument with a tool execution error the model can correct from.
    /// </summary>
    /// <param name="rejection">The refusal.</param>
    /// <param name="structured">Whether the tool declares no output schema, which structured content would have to
    /// conform to.</param>
    /// <returns>The error: the rule in a sentence, and as structured content where the tool allows it.</returns>
    private static CallToolResult Answer(ValueObjectArgumentRejection rejection, bool structured) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = $"Argument '{rejection.Argument}' rejected ({rejection.Code}): {rejection.Message}" }],
        StructuredContent = structured ? rejection.ToJsonElement() : null,
    };
}
