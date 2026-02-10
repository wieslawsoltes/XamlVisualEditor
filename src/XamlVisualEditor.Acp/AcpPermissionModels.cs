using System;
using System.Collections.Generic;
using System.Text.Json;

namespace XamlVisualEditor.Acp;

public sealed record AcpPermissionOption(string OptionId, string Name, string Kind);

public sealed record AcpPermissionOutcome(bool IsCancelled, string? SelectedOptionId)
{
    public static AcpPermissionOutcome Cancelled()
    {
        return new AcpPermissionOutcome(true, null);
    }

    public static AcpPermissionOutcome Selected(string optionId)
    {
        return new AcpPermissionOutcome(false, optionId);
    }
}

public sealed class AcpPermissionRequest
{
    public AcpPermissionRequest(
        string sessionId,
        IReadOnlyList<AcpPermissionOption> options,
        string? toolCallId,
        string? toolTitle,
        string? toolKind,
        JsonElement? rawToolCall)
    {
        SessionId = sessionId;
        Options = options;
        ToolCallId = toolCallId;
        ToolTitle = toolTitle;
        ToolKind = toolKind;
        RawToolCall = rawToolCall;
    }

    public string SessionId { get; }

    public IReadOnlyList<AcpPermissionOption> Options { get; }

    public string? ToolCallId { get; }

    public string? ToolTitle { get; }

    public string? ToolKind { get; }

    public JsonElement? RawToolCall { get; }

    public static AcpPermissionRequest Parse(JsonElement? parameters)
    {
        if (parameters is null || parameters.Value.ValueKind != JsonValueKind.Object)
        {
            throw new JsonRpcException(-32602, "Missing parameters.");
        }

        string sessionId = RequireString(parameters.Value, "sessionId");

        List<AcpPermissionOption> options = new();
        if (parameters.Value.TryGetProperty("options", out JsonElement optionsElement)
            && optionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement option in optionsElement.EnumerateArray())
            {
                if (option.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string optionId = RequireString(option, "optionId");
                string name = RequireString(option, "name");
                string kind = RequireString(option, "kind");
                options.Add(new AcpPermissionOption(optionId, name, kind));
            }
        }

        if (options.Count == 0)
        {
            throw new JsonRpcException(-32602, "Permission options are missing.");
        }

        string? toolCallId = null;
        string? toolTitle = null;
        string? toolKind = null;
        JsonElement? rawToolCall = null;

        if (parameters.Value.TryGetProperty("toolCall", out JsonElement toolCallElement)
            && toolCallElement.ValueKind == JsonValueKind.Object)
        {
            rawToolCall = toolCallElement.Clone();
            toolCallId = TryGetString(toolCallElement, "toolCallId");
            toolTitle = TryGetString(toolCallElement, "title");
            toolKind = TryGetString(toolCallElement, "kind");
        }

        return new AcpPermissionRequest(sessionId, options, toolCallId, toolTitle, toolKind, rawToolCall);
    }

    private static string RequireString(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            string? result = value.GetString();
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result;
            }
        }

        throw new JsonRpcException(-32602, "Missing parameter '" + name + "'.");
    }

    private static string? TryGetString(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}
