using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Controls;

/// <summary>
/// TextEditor that renders git diffs with colored line types.
/// </summary>
public sealed class GitDiffTextEditor : TextEditor
{
    public static readonly StyledProperty<IList<GitDiffLineViewModel>?> DiffLinesProperty =
        AvaloniaProperty.Register<GitDiffTextEditor, IList<GitDiffLineViewModel>?>(nameof(DiffLines));

    private readonly DiffLineColorizer _colorizer;
    private INotifyCollectionChanged? _diffLinesNotifier;

    static GitDiffTextEditor()
    {
        DiffLinesProperty.Changed.AddClassHandler<GitDiffTextEditor>((editor, args) =>
            editor.OnDiffLinesChanged(args));
    }

    public GitDiffTextEditor()
    {
        _colorizer = new DiffLineColorizer(this);
        TextArea.TextView.LineTransformers.Add(_colorizer);
        Document ??= new TextDocument();
    }

    protected override Type StyleKeyOverride => typeof(TextEditor);

    public IList<GitDiffLineViewModel>? DiffLines
    {
        get => GetValue(DiffLinesProperty);
        set => SetValue(DiffLinesProperty, value);
    }

    private void OnDiffLinesChanged(AvaloniaPropertyChangedEventArgs args)
    {
        IList<GitDiffLineViewModel>? lines = args.NewValue as IList<GitDiffLineViewModel>;
        if (_diffLinesNotifier is not null)
        {
            _diffLinesNotifier.CollectionChanged -= OnDiffLinesCollectionChanged;
            _diffLinesNotifier = null;
        }

        if (lines is INotifyCollectionChanged notifier)
        {
            _diffLinesNotifier = notifier;
            _diffLinesNotifier.CollectionChanged += OnDiffLinesCollectionChanged;
        }

        _colorizer.SetLines(lines);
        RebuildText(lines);
    }

    private void OnDiffLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        RebuildText(DiffLines);
    }

    private void RebuildText(IList<GitDiffLineViewModel>? lines)
    {
        StringBuilder builder = new();
        if (lines is not null)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                GitDiffLineViewModel line = lines[i];
                if (!string.IsNullOrWhiteSpace(line.Marker))
                {
                    builder.Append(line.Marker);
                    builder.Append(' ');
                }

                builder.Append(line.Text);
                if (i < lines.Count - 1)
                {
                    builder.AppendLine();
                }
            }
        }

        string text = builder.ToString();
        if (!string.Equals(Text, text, StringComparison.Ordinal))
        {
            Text = text;
        }

        TextArea.TextView.InvalidateLayer(KnownLayer.Text);
        TextArea.TextView.InvalidateLayer(KnownLayer.Background);
    }

    private sealed class DiffLineColorizer : DocumentColorizingTransformer
    {
        private readonly GitDiffTextEditor _owner;
        private IList<GitDiffLineViewModel>? _lines;

        public DiffLineColorizer(GitDiffTextEditor owner)
        {
            _owner = owner;
        }

        public void SetLines(IList<GitDiffLineViewModel>? lines)
        {
            _lines = lines;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (_lines is null)
            {
                return;
            }

            int index = line.LineNumber - 1;
            if (index < 0 || index >= _lines.Count)
            {
                return;
            }

            GitDiffLineKind kind = _lines[index].Kind;
            IBrush? foreground = GetForeground(kind);
            IBrush? background = GetBackground(kind);

            if (foreground is null && background is null)
            {
                return;
            }

            ChangeLinePart(line.Offset, line.EndOffset, element =>
            {
                if (foreground is not null)
                {
                    element.TextRunProperties.SetForegroundBrush(foreground);
                }

                if (background is not null)
                {
                    element.TextRunProperties.SetBackgroundBrush(background);
                }
            });
        }

        private static IBrush? GetForeground(GitDiffLineKind kind)
        {
            return kind switch
            {
                GitDiffLineKind.Added => Brushes.ForestGreen,
                GitDiffLineKind.Removed => Brushes.IndianRed,
                GitDiffLineKind.HunkHeader => Brushes.DodgerBlue,
                GitDiffLineKind.FileHeader => Brushes.SlateGray,
                GitDiffLineKind.NoNewline => Brushes.Orange,
                _ => null
            };
        }

        private static IBrush? GetBackground(GitDiffLineKind kind)
        {
            return kind switch
            {
                GitDiffLineKind.Added => new SolidColorBrush(Color.Parse("#1A1F8A4C")),
                GitDiffLineKind.Removed => new SolidColorBrush(Color.Parse("#1AE5534B")),
                GitDiffLineKind.HunkHeader => new SolidColorBrush(Color.Parse("#1A1E3A8A")),
                _ => null
            };
        }
    }
}
