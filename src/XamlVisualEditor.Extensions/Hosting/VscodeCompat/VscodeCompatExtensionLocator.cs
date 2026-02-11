namespace XamlVisualEditor.Extensions.Hosting.VscodeCompat;

/// <summary>Resolves VS Code extension paths.</summary>
public sealed class VscodeCompatExtensionLocator
{
    /// <summary>Finds the latest installed extension directories.</summary>
    public IReadOnlyList<string> ResolveExtensions(string extensionsRoot, IReadOnlyList<string> extensionIds)
    {
        if (string.IsNullOrWhiteSpace(extensionsRoot) || extensionIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (!Directory.Exists(extensionsRoot))
        {
            return Array.Empty<string>();
        }

        List<string> results = new();
        foreach (string extensionId in extensionIds)
        {
            ExtensionCandidate? candidate = FindLatest(extensionsRoot, extensionId);
            if (candidate is not null)
            {
                results.Add(candidate.Value.Path);
            }
        }

        return results.Count == 0 ? Array.Empty<string>() : results;
    }

    private static ExtensionCandidate? FindLatest(string extensionsRoot, string extensionId)
    {
        if (string.IsNullOrWhiteSpace(extensionId))
        {
            return null;
        }

        string pattern = extensionId + "-*";
        ExtensionCandidate? latest = null;
        foreach (string directory in Directory.EnumerateDirectories(extensionsRoot, pattern))
        {
            string name = Path.GetFileName(directory);
            if (!name.StartsWith(extensionId + "-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string suffix = name[(extensionId.Length + 1)..];
            ExtensionCandidate candidate = new(directory, suffix, ParseVersion(suffix));
            if (latest is null || Compare(candidate, latest.Value) > 0)
            {
                latest = candidate;
            }
        }

        return latest;
    }

    private static Version? ParseVersion(string suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return null;
        }

        string core = suffix.Split('-', 2)[0];
        return Version.TryParse(core, out Version? version) ? version : null;
    }

    private static int Compare(ExtensionCandidate left, ExtensionCandidate right)
    {
        if (left.Version is not null && right.Version is not null)
        {
            int compare = left.Version.CompareTo(right.Version);
            if (compare != 0)
            {
                return compare;
            }
        }
        else if (left.Version is not null)
        {
            return 1;
        }
        else if (right.Version is not null)
        {
            return -1;
        }

        return string.Compare(left.Suffix, right.Suffix, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct ExtensionCandidate(string Path, string Suffix, Version? Version);
}
