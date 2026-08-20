using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using XamlVisualEditor.Acp;

namespace XamlVisualEditor.AcpExtension;

public sealed partial class AcpProfileViewModel : ReactiveObject
{
    public string Id { get; }

    [Reactive]
    public partial string Name { get; set; }

    [Reactive]
    public partial string? Description { get; set; }

    [Reactive]
    public partial string Command { get; set; }

    [Reactive]
    public partial string ArgumentsText { get; set; }

    [Reactive]
    public partial string? WorkingDirectory { get; set; }

    [Reactive]
    public partial string? Model { get; set; }

    [Reactive]
    public partial string? ModelEnvVar { get; set; }

    [Reactive]
    public partial string? ApiKeyEnvVar { get; set; }

    [Reactive]
    public partial string? OAuthClientId { get; set; }

    [Reactive]
    public partial string? OAuthScopes { get; set; }

    [Reactive]
    public partial string? OAuthDeviceCodeUrl { get; set; }

    [Reactive]
    public partial string? OAuthTokenUrl { get; set; }

    [Reactive]
    public partial bool UseKeychain { get; set; }

    public bool IsBuiltIn { get; }

    public Dictionary<string, string> Environment { get; }

    public AcpProfileViewModel(AcpProfile profile)
    {
        Id = profile.Id;
        Name = profile.Name;
        Description = profile.Description;
        Command = profile.Command;
        ArgumentsText = string.Join(' ', profile.Arguments.Select(EscapeArgument));
        WorkingDirectory = profile.WorkingDirectory;
        Model = profile.Model;
        ModelEnvVar = profile.ModelEnvVar;
        ApiKeyEnvVar = profile.ApiKeyEnvVar;
        OAuthClientId = profile.OAuthClientId;
        OAuthScopes = profile.OAuthScopes;
        OAuthDeviceCodeUrl = profile.OAuthDeviceCodeUrl;
        OAuthTokenUrl = profile.OAuthTokenUrl;
        UseKeychain = profile.UseKeychain;
        IsBuiltIn = profile.IsBuiltIn;
        Environment = new Dictionary<string, string>(profile.Environment, StringComparer.Ordinal);
    }

    public AcpProfile ToProfile()
    {
        return new AcpProfile
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Command = Command,
            Arguments = SplitArguments(ArgumentsText),
            WorkingDirectory = WorkingDirectory,
            Environment = new Dictionary<string, string>(Environment, StringComparer.Ordinal),
            Model = Model,
            ModelEnvVar = ModelEnvVar,
            ApiKeyEnvVar = ApiKeyEnvVar,
            OAuthClientId = OAuthClientId,
            OAuthScopes = OAuthScopes,
            OAuthDeviceCodeUrl = OAuthDeviceCodeUrl,
            OAuthTokenUrl = OAuthTokenUrl,
            UseKeychain = UseKeychain,
            IsBuiltIn = IsBuiltIn
        };
    }

    private static List<string> SplitArguments(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        List<string> args = new();
        char quote = '\0';
        int start = 0;
        bool inToken = false;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quote == '\0' && char.IsWhiteSpace(ch))
            {
                if (inToken)
                {
                    args.Add(text.Substring(start, i - start));
                    inToken = false;
                }
                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                if (quote == '\0')
                {
                    quote = ch;
                    if (!inToken)
                    {
                        inToken = true;
                        start = i + 1;
                    }
                    else
                    {
                        start = start == i ? i + 1 : start;
                    }
                }
                else if (quote == ch)
                {
                    args.Add(text.Substring(start, i - start));
                    quote = '\0';
                    inToken = false;
                }
                continue;
            }

            if (!inToken)
            {
                inToken = true;
                start = i;
            }
        }

        if (inToken)
        {
            args.Add(text.Substring(start));
        }

        return args;
    }

    private static string EscapeArgument(string arg)
    {
        if (arg.IndexOf(' ') < 0 && arg.IndexOf('"') < 0)
        {
            return arg;
        }

        string escaped = arg.Replace("\"", "\\\"");
        return "\"" + escaped + "\"";
    }
}
