using System.Text.Json;
using XamlVisualEditor.Acp;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class AcpPermissionModelsTests
{
    [Fact]
    public void Parse_ValidRequest_ReturnsValues()
    {
        const string json = """
        {
          "sessionId": "s-1",
          "options": [
            { "optionId": "allow_once", "name": "Allow once", "kind": "allow_once" },
            { "optionId": "reject_once", "name": "Reject once", "kind": "reject_once" }
          ],
          "toolCall": { "toolCallId": "t1", "title": "Write file", "kind": "edit" }
        }
        """;

        using JsonDocument doc = JsonDocument.Parse(json);
        AcpPermissionRequest request = AcpPermissionRequest.Parse(doc.RootElement);

        Assert.Equal("s-1", request.SessionId);
        Assert.Equal(2, request.Options.Count);
        Assert.Equal("t1", request.ToolCallId);
        Assert.Equal("Write file", request.ToolTitle);
        Assert.Equal("edit", request.ToolKind);
    }

    [Fact]
    public void Parse_MissingOptions_Throws()
    {
        const string json = """
        {
          "sessionId": "s-1",
          "options": []
        }
        """;

        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Throws<JsonRpcException>(() => AcpPermissionRequest.Parse(doc.RootElement));
    }

    [Fact]
    public void Parse_MissingSessionId_Throws()
    {
        const string json = """
        {
          "options": [
            { "optionId": "allow_once", "name": "Allow once", "kind": "allow_once" }
          ]
        }
        """;

        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Throws<JsonRpcException>(() => AcpPermissionRequest.Parse(doc.RootElement));
    }
}
