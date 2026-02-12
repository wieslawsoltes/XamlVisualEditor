using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.Workspace;

public sealed class DotNetTemplateService : IDotNetTemplateService
{
    private readonly IDotNetCli _cli;
    private readonly ILogger<DotNetTemplateService> _logger;

    public DotNetTemplateService(IDotNetCli cli, ILogger<DotNetTemplateService>? logger = null)
    {
        _cli = cli;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DotNetTemplateService>.Instance;
    }

    public async Task<IReadOnlyList<DotNetTemplateInfo>> ListTemplatesAsync(CancellationToken ct = default)
    {
        DotNetCliResult jsonResult = await _cli.RunAsync(new[] { "new", "list", "--format", "json" }, null, ct);
        if (jsonResult.Success && !string.IsNullOrWhiteSpace(jsonResult.StandardOutput))
        {
            IReadOnlyList<DotNetTemplateInfo> templates = ParseTemplatesFromJson(jsonResult.StandardOutput);
            if (templates.Count > 0)
            {
                return templates;
            }
        }

        DotNetCliResult textResult = await _cli.RunAsync(new[] { "new", "--list" }, null, ct);
        if (!textResult.Success)
        {
            string error = string.IsNullOrWhiteSpace(textResult.StandardError)
                ? "dotnet new --list failed"
                : textResult.StandardError.Trim();
            throw new InvalidOperationException(error);
        }

        IReadOnlyList<DotNetTemplateInfo> parsed = ParseTemplatesFromText(textResult.StandardOutput);
        if (parsed.Count == 0)
        {
            throw new InvalidOperationException("No templates were found.");
        }

        return parsed;
    }

    public async Task<DotNetTemplateInstallResult> InstallTemplateAsync(string packageOrPath, CancellationToken ct = default)
    {
        DotNetCliResult result = await _cli.RunAsync(new[] { "new", "install", packageOrPath }, null, ct);
        return new DotNetTemplateInstallResult
        {
            Success = result.Success,
            ErrorMessage = result.Success ? null : result.StandardError,
            StandardOutput = result.StandardOutput
        };
    }

    public async Task<DotNetNewResult> CreateProjectAsync(DotNetNewProjectRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateShortName))
        {
            return new DotNetNewResult { Success = false, ErrorMessage = "Template short name is required." };
        }

        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            return new DotNetNewResult { Success = false, ErrorMessage = "Project name is required." };
        }

        if (string.IsNullOrWhiteSpace(request.Location))
        {
            return new DotNetNewResult { Success = false, ErrorMessage = "Location is required." };
        }

        string outputDir = request.CreateProjectDirectory
            ? Path.Combine(request.Location, request.ProjectName)
            : request.Location;

        Directory.CreateDirectory(outputDir);

        List<string> args = BuildNewArgs(request.TemplateShortName, request.ProjectName, outputDir, request.Parameters);
        DotNetCliResult result = await _cli.RunAsync(args, outputDir, ct);
        if (!result.Success)
        {
            return new DotNetNewResult
            {
                Success = false,
                ErrorMessage = string.IsNullOrWhiteSpace(result.StandardError)
                    ? "dotnet new failed"
                    : result.StandardError.Trim(),
                StandardOutput = result.StandardOutput
            };
        }

        string? projectPath = FindProjectFile(outputDir);
        return new DotNetNewResult
        {
            Success = true,
            ProjectPath = projectPath ?? outputDir,
            StandardOutput = result.StandardOutput
        };
    }

    public async Task<DotNetNewResult> CreateSolutionAsync(DotNetNewSolutionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionName))
        {
            return new DotNetNewResult { Success = false, ErrorMessage = "Solution name is required." };
        }

        if (string.IsNullOrWhiteSpace(request.Location))
        {
            return new DotNetNewResult { Success = false, ErrorMessage = "Location is required." };
        }

        string solutionRoot = request.CreateSolutionDirectory
            ? Path.Combine(request.Location, request.SolutionName)
            : request.Location;

        Directory.CreateDirectory(solutionRoot);

        DotNetCliResult slnResult = await _cli.RunAsync(
            new[] { "new", "sln", "-n", request.SolutionName, "-o", solutionRoot },
            solutionRoot,
            ct);
        if (!slnResult.Success)
        {
            return new DotNetNewResult
            {
                Success = false,
                ErrorMessage = string.IsNullOrWhiteSpace(slnResult.StandardError)
                    ? "dotnet new sln failed"
                    : slnResult.StandardError.Trim(),
                StandardOutput = slnResult.StandardOutput
            };
        }

        string solutionPath = Path.Combine(solutionRoot, request.SolutionName + ".sln");
        string? firstProjectPath = null;

        foreach (DotNetNewProjectRequest projectRequest in request.Projects)
        {
            DotNetNewResult projectResult = await CreateProjectAsync(new DotNetNewProjectRequest
            {
                TemplateShortName = projectRequest.TemplateShortName,
                ProjectName = projectRequest.ProjectName,
                Location = solutionRoot,
                CreateProjectDirectory = projectRequest.CreateProjectDirectory,
                Parameters = projectRequest.Parameters
            }, ct);

            if (!projectResult.Success)
            {
                return projectResult;
            }

            if (string.IsNullOrWhiteSpace(firstProjectPath))
            {
                firstProjectPath = projectResult.ProjectPath;
            }

            if (request.AddProjectsToSolution && !string.IsNullOrWhiteSpace(projectResult.ProjectPath))
            {
                string? projectFile = projectResult.ProjectPath;
                if (Directory.Exists(projectFile))
                {
                    projectFile = FindProjectFile(projectFile);
                }

                if (!string.IsNullOrWhiteSpace(projectFile))
                {
                    DotNetCliResult addResult = await _cli.RunAsync(
                        new[] { "sln", solutionPath, "add", projectFile },
                        solutionRoot,
                        ct);

                    if (!addResult.Success)
                    {
                        return new DotNetNewResult
                        {
                            Success = false,
                            ErrorMessage = string.IsNullOrWhiteSpace(addResult.StandardError)
                                ? "dotnet sln add failed"
                                : addResult.StandardError.Trim(),
                            StandardOutput = addResult.StandardOutput
                        };
                    }
                }
            }
        }

        return new DotNetNewResult
        {
            Success = true,
            SolutionPath = solutionPath,
            ProjectPath = firstProjectPath,
            StandardOutput = slnResult.StandardOutput
        };
    }

    private static List<string> BuildNewArgs(
        string templateShortName,
        string projectName,
        string outputDir,
        IReadOnlyDictionary<string, string> parameters)
    {
        List<string> args = new() { "new", templateShortName, "-n", projectName, "-o", outputDir };
        foreach (KeyValuePair<string, string> parameter in parameters)
        {
            string key = parameter.Key.StartsWith("--", StringComparison.Ordinal)
                ? parameter.Key
                : "--" + parameter.Key;

            if (string.IsNullOrWhiteSpace(parameter.Value))
            {
                args.Add(key);
            }
            else
            {
                args.Add(key);
                args.Add(parameter.Value);
            }
        }

        return args;
    }

    private static string? FindProjectFile(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return null;
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.csproj", SearchOption.TopDirectoryOnly))
            {
                return file;
            }
        }
        catch
        {
        }

        return null;
    }

    private IReadOnlyList<DotNetTemplateInfo> ParseTemplatesFromJson(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            JsonElement templates = root;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("templates", out JsonElement templatesProp))
                {
                    templates = templatesProp;
                }
                else if (root.TryGetProperty("Templates", out JsonElement templatesUpper))
                {
                    templates = templatesUpper;
                }
            }

            if (templates.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<DotNetTemplateInfo>();
            }

            List<DotNetTemplateInfo> result = new();
            foreach (JsonElement item in templates.EnumerateArray())
            {
                string? name = GetString(item, "name") ?? GetString(item, "Name");
                string? shortName = GetString(item, "shortName") ??
                    GetString(item, "shortNameList") ??
                    GetFirstString(item, "shortNameList") ??
                    GetString(item, "ShortName");

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(shortName))
                {
                    continue;
                }

                string? language = GetString(item, "language") ?? GetString(item, "Language");
                string? type = GetString(item, "type") ?? GetString(item, "Type");
                string? author = GetString(item, "author") ?? GetString(item, "Author");
                string? description = GetString(item, "description") ?? GetString(item, "Description");
                Dictionary<string, string> tags = GetTags(item);

                if (string.IsNullOrWhiteSpace(type) && tags.TryGetValue("type", out string? tagType))
                {
                    type = tagType;
                }

                if (string.IsNullOrWhiteSpace(language) && tags.TryGetValue("language", out string? tagLanguage))
                {
                    language = tagLanguage;
                }

                result.Add(new DotNetTemplateInfo
                {
                    Name = name,
                    ShortName = shortName,
                    Language = language,
                    Type = type,
                    Author = author,
                    Description = description,
                    Tags = tags
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Template JSON parse failed: {Message}", ex.Message);
            return Array.Empty<DotNetTemplateInfo>();
        }
    }

    private static IReadOnlyList<DotNetTemplateInfo> ParseTemplatesFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<DotNetTemplateInfo>();
        }

        string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int headerIndex = Array.FindIndex(lines, line =>
            line.Contains("Template Name", StringComparison.OrdinalIgnoreCase)
            && line.Contains("Short Name", StringComparison.OrdinalIgnoreCase));
        if (headerIndex < 0 || headerIndex + 1 >= lines.Length)
        {
            return Array.Empty<DotNetTemplateInfo>();
        }

        string header = lines[headerIndex];
        int shortNameIndex = header.IndexOf("Short Name", StringComparison.OrdinalIgnoreCase);
        int languageIndex = header.IndexOf("Language", StringComparison.OrdinalIgnoreCase);
        int tagsIndex = header.IndexOf("Tags", StringComparison.OrdinalIgnoreCase);

        if (shortNameIndex < 0)
        {
            return Array.Empty<DotNetTemplateInfo>();
        }

        List<DotNetTemplateInfo> results = new();
        for (int i = headerIndex + 2; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.StartsWith("-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string name = SliceColumn(line, 0, shortNameIndex).Trim();
            string shortName = SliceColumn(line, shortNameIndex, languageIndex > 0 ? languageIndex : line.Length).Trim();
            string language = languageIndex > 0
                ? SliceColumn(line, languageIndex, tagsIndex > 0 ? tagsIndex : line.Length).Trim()
                : string.Empty;
            string tags = tagsIndex > 0
                ? SliceColumn(line, tagsIndex, line.Length).Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(shortName))
            {
                continue;
            }

            results.Add(new DotNetTemplateInfo
            {
                Name = name,
                ShortName = shortName,
                Language = language,
                Type = null,
                Description = null,
                Tags = new Dictionary<string, string> { { "tags", tags } }
            });
        }

        return results;
    }

    private static string SliceColumn(string line, int start, int end)
    {
        if (start < 0)
        {
            start = 0;
        }

        if (end > line.Length)
        {
            end = line.Length;
        }

        if (start >= end || start >= line.Length)
        {
            return string.Empty;
        }

        return line.Substring(start, end - start);
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static string? GetFirstString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                return item.GetString();
            }
        }

        return null;
    }

    private static Dictionary<string, string> GetTags(JsonElement element)
    {
        Dictionary<string, string> tags = new(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return tags;
        }

        if (!element.TryGetProperty("tags", out JsonElement tagElement))
        {
            return tags;
        }

        if (tagElement.ValueKind != JsonValueKind.Object)
        {
            return tags;
        }

        foreach (JsonProperty property in tagElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                tags[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return tags;
    }
}
