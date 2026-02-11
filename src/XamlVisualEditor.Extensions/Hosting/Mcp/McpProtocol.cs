using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Protocol constants and shared contracts for MCP.</summary>
public static class McpProtocol
{
    /// <summary>Current MCP protocol version.</summary>
    public const string ProtocolVersion = "2024-11-05";

    public const string InitializeMethod = "initialize";
    public const string InitializedNotification = "initialized";

    public const string ToolsListMethod = "tools/list";
    public const string ToolsCallMethod = "tools/call";

    public const string ResourcesListMethod = "resources/list";
    public const string ResourcesReadMethod = "resources/read";

    public const string PromptsListMethod = "prompts/list";
    public const string PromptsGetMethod = "prompts/get";
}

/// <summary>Initialize request parameters.</summary>
public sealed record McpInitializeParams(
    string? SessionToken,
    string? WorkspaceId,
    McpClientInfo? ClientInfo,
    McpClientCapabilities? Capabilities);

/// <summary>Client metadata.</summary>
public sealed record McpClientInfo(
    string? Name,
    string? Version);

/// <summary>Client capabilities (reserved for future use).</summary>
public sealed record McpClientCapabilities(
    object? Experimental);

/// <summary>Initialize response payload.</summary>
public sealed record McpInitializeResult(
    string ProtocolVersion,
    McpServerInfo ServerInfo,
    McpServerCapabilities Capabilities,
    string WorkspaceId,
    string SessionToken);

/// <summary>Initialize outcome with permission metadata.</summary>
public sealed record McpInitializeOutcome(
    McpInitializeResult Result,
    McpAccessLevel AccessLevel);

/// <summary>Server metadata.</summary>
public sealed record McpServerInfo(
    string Name,
    string Version);

/// <summary>Server capability descriptor.</summary>
public sealed record McpServerCapabilities(
    McpToolsCapability Tools,
    McpResourcesCapability Resources,
    McpPromptsCapability Prompts,
    McpLoggingCapability Logging,
    object? Experimental = null);

/// <summary>Tools capability.</summary>
public sealed record McpToolsCapability(bool ListChanged = false);

/// <summary>Resources capability.</summary>
public sealed record McpResourcesCapability(bool Subscribe = false, bool ListChanged = false);

/// <summary>Prompts capability.</summary>
public sealed record McpPromptsCapability(bool ListChanged = false);

/// <summary>Logging capability.</summary>
public sealed record McpLoggingCapability(bool SupportsLevel = true);

/// <summary>Tool descriptor.</summary>
public sealed record McpToolDefinition(
    string Name,
    string Description,
    object InputSchema);

/// <summary>Tool list response.</summary>
public sealed record McpToolsListResult(
    IReadOnlyList<McpToolDefinition> Tools);

/// <summary>Tool call request parameters.</summary>
public sealed record McpToolCallParams(
    string Name,
    JsonElement? Arguments);

/// <summary>Tool response content.</summary>
public sealed record McpContent(
    string Type,
    string? Text = null,
    object? Json = null,
    string? MimeType = null,
    string? Uri = null,
    string? Blob = null);

/// <summary>Tool call response payload.</summary>
public sealed record McpToolCallResult(
    IReadOnlyList<McpContent> Content,
    bool IsError = false);

/// <summary>Resource descriptor.</summary>
public sealed record McpResource(
    string Uri,
    string Name,
    string? Description = null,
    string? MimeType = null);

/// <summary>Resource list response.</summary>
public sealed record McpResourcesListResult(
    IReadOnlyList<McpResource> Resources);

/// <summary>Resource read request parameters.</summary>
public sealed record McpResourceReadParams(
    string Uri);

/// <summary>Resource content response.</summary>
public sealed record McpResourceContent(
    string Uri,
    string? MimeType,
    string? Text,
    string? Blob = null);

/// <summary>Resource read response payload.</summary>
public sealed record McpResourcesReadResult(
    IReadOnlyList<McpResourceContent> Contents);

/// <summary>Prompt descriptor.</summary>
public sealed record McpPrompt(
    string Name,
    string Description,
    IReadOnlyList<McpPromptArgument>? Arguments = null);

/// <summary>Prompt argument descriptor.</summary>
public sealed record McpPromptArgument(
    string Name,
    string Description,
    bool Required = false,
    string? DefaultValue = null);

/// <summary>Prompt list response.</summary>
public sealed record McpPromptsListResult(
    IReadOnlyList<McpPrompt> Prompts);

/// <summary>Prompt get request parameters.</summary>
public sealed record McpPromptGetParams(
    string Name,
    JsonElement? Arguments);

/// <summary>Prompt message.</summary>
public sealed record McpPromptMessage(
    string Role,
    IReadOnlyList<McpContent> Content);

/// <summary>Prompt response payload.</summary>
public sealed record McpPromptsGetResult(
    IReadOnlyList<McpPromptMessage> Messages);

/// <summary>Permission level for MCP sessions.</summary>
public enum McpAccessLevel
{
    ReadOnly,
    Full
}

/// <summary>Session info for MCP clients.</summary>
public sealed record McpSessionInfo(
    string WorkspaceId,
    McpAccessLevel AccessLevel,
    string SessionToken);

/// <summary>Settings stored for MCP server.</summary>
public sealed record McpSettings(
    bool Enabled = false,
    string? Transport = "both",
    int HttpPort = 4712,
    string? HttpPath = "/mcp/");
