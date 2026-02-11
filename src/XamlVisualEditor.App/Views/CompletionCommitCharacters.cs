using System.Collections.Generic;

namespace XamlVisualEditor.App.Views;

internal interface ICommitCharactersProvider
{
    IReadOnlyList<char> CommitCharacters { get; }
}
