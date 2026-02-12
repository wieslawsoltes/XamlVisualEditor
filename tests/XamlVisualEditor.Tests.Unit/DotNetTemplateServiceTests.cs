using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Workspace;

namespace XamlVisualEditor.Tests.Unit;

public sealed class DotNetTemplateServiceTests
{
    [Fact]
    public async Task ListTemplatesAsync_ParsesJsonTemplates()
    {
        string json = "{" +
            "\"templates\":[" +
            "{" +
            "\"name\":\"Console App\"," +
            "\"shortName\":\"console\"," +
            "\"language\":\"C#\"," +
            "\"description\":\"A console app\"," +
            "\"tags\":{\"type\":\"project\"}" +
            "}" +
            "]}";

        FakeDotNetCli cli = new(new DotNetCliResult(0, json, string.Empty));
        DotNetTemplateService service = new(cli);

        IReadOnlyList<DotNetTemplateInfo> templates = await service.ListTemplatesAsync(CancellationToken.None);

        Assert.Single(templates);
        DotNetTemplateInfo template = templates[0];
        Assert.Equal("Console App", template.Name);
        Assert.Equal("console", template.ShortName);
        Assert.Equal("C#", template.Language);
    }

    private sealed class FakeDotNetCli : IDotNetCli
    {
        private readonly Queue<DotNetCliResult> _results;

        public FakeDotNetCli(params DotNetCliResult[] results)
        {
            _results = new Queue<DotNetCliResult>(results);
        }

        public Task<DotNetCliResult> RunAsync(IReadOnlyList<string> args, string? workingDirectory, CancellationToken ct = default)
        {
            if (_results.Count == 0)
            {
                return Task.FromResult(new DotNetCliResult(1, string.Empty, "No results"));
            }

            return Task.FromResult(_results.Dequeue());
        }
    }
}
