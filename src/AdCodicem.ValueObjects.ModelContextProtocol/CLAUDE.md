# AdCodicem.ValueObjects.ModelContextProtocol

User-facing behaviour: `website/docs/how-to/language-models.md`. Tests: `LanguageModels/Mcp*` of the unit suite, a server
in process over a pair of pipes against the SDK's own client (`McpHarness`). The argument check is shared with the AI
package (`src/Shared/ValueObjectArguments.cs`).

- A refused argument is a tool execution error (`isError: true`) whose text carries the code, and whose structured
  content is `{"error":"invalid_argument",…}` when the tool declares no output schema.
- The SDK rewrites the output schema of its own tools only: for a client on a protocol version before `2026-07-28`, which
  is every client in use today, `McpServerImpl` wraps a non-object `outputSchema` in
  `{"type":"object","properties":{"result":…}}` only when the tool is its internal `AIFunctionMcpServerTool`, while the
  tool still wraps its structured content, so a tool wrapping another would publish a schema its content does not match.
  The package therefore registers the SDK's own tools (`McpServerTool.Create`, schema options from `WithValueObjects()`)
  and keeps the parameters to check of each in a `ConditionalWeakTable` keyed by the tool.
- The SDK's client asks for `2026-07-28`, which hides the rewrite: a test of the older shape pins
  `McpClientOptions.ProtocolVersion`.
- The check is a call-tool filter, which must be the innermost, the last of the list: an
  `IPostConfigureOptions<McpServerOptions>` places it after every filter `Configure` added, once per service collection
  (`TryAddEnumerable`). Options built or changed after that (a server created without dependency injection,
  ModelContextProtocol.AspNetCore's `ConfigureSessionOptions`) take it through the public `AddValueObjectValidation()`,
  which moves it last, once.
- The SDK builds the pipeline when it creates a server (at start over stdio, per session over HTTP, per request when
  stateless), and refuses call-tool filters beside an explicit `CallToolWithAlternateHandler` (`MCPEXP002`).
- `McpServerToolCreateOptions.Clone()` is internal, so `ValueObjectMcpServerTool.Describe` copies the properties one by
  one; a test fails when the SDK adds one the copy drops.
- Under reflection-free serialization the server's options are a copy of `McpJsonUtilities.DefaultOptions` with the
  application's context first in the resolver chain and `ValueObjectJsonConverterFactory` in `Converters`: a context
  resolving for options other than its own does not apply the converters its attribute names.
- A tool's `outputSchema` can refuse a result the server writes (the language-model profile keeps the case-insensitive
  `enum` and the UTF-16 `minLength`): a limit the guide states.
- The namespace hides the root `ModelContextProtocol` namespace (root `CLAUDE.md`).
