using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.Wave;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using WinRT.Interop;

namespace FreewriteWindows;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _focusTimer;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _recordingTimer;
    private readonly DispatcherTimer _dictationPlaybackTimer;
    private readonly Random _random = new();
    private readonly double[] _fontSizes = [16, 18, 20, 22, 24, 26];
    private readonly string[] _placeholderOptions =
    [
        "Begin writing",
        "Pick a thought and go",
        "Start typing",
        "What's on your mind",
        "Just start",
        "Type your first thought",
        "Start with one sentence",
        "Just say it"
    ];

    private FreewriteStore _store;
    private List<HumanEntry> _entries = [];
    private HumanEntry? _selectedEntry;
    private bool _isLoadingEntry;
    private bool _timerIsRunning;
    private bool _backspaceDisabled;
    private bool _isDarkMode;
    private bool _isFullscreen;
    private bool _isVideoEntryVisible;
    private bool _isVideoPaused;
    private HumanEntry? _pendingDeleteEntry;
    private Guid? _hoveredHistoryEntryId;
    private readonly LocalTranscriptionService _transcription = new();
    private AudioDictationService? _dictation;
    private TaskCompletionSource<bool>? _recordingStopCompletion;
    private DateTime _recordingStartedAt;
    private bool _isTalkRecording;
    private bool _isDictationPlaying;
    private bool _dictationSeekDragging;
    private TimeSpan? _dictationNaturalDuration;
    private List<string> _dictationClips = [];
    private int _selectedDictationClipIndex;
    private string _talkButtonDefaultContent = "Talk";
    private Brush? _talkButtonDefaultForeground;
    private int _timeRemaining = 900;
    private string _selectedFont = "Lato";
    private double _currentFontSize = 18;
    private string _currentRandomFont = string.Empty;
    private WindowState _previousWindowState;
    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode;

    private const string ChatGptPrompt = """
        below is my journal entry. wyt? talk through it with me like a friend. don't therpaize me and give me a whole breakdown, don't repeat my thoughts with headings. really take all of this, and tell me back stuff truly as if you're an old homie.
        
        Keep it casual, dont say yo, help me make new connections i don't see, comfort, validate, challenge, all of it. dont be afraid to say a lot. format with markdown headings if needed.

        do not just go through every single thing i say, and say it back to me. you need to proccess everythikng is say, make connections i don't see it, and deliver it all back to me as a story that makes me feel what you think i wanna feel. thats what the best therapists do.

        ideally, you're style/tone should sound like the user themselves. it's as if the user is hearing their own tone but it should still feel different, because you have different things to say and don't just repeat back they say.

        else, start by saying, "hey, thanks for showing me this. my thoughts:"
            
        my entry:
        """;

    private const string ClaudePrompt = """
        Take a look at my journal entry below. I'd like you to analyze it and respond with deep insight that feels personal, not clinical.
        Imagine you're not just a friend, but a mentor who truly gets both my tech background and my psychological patterns. I want you to uncover the deeper meaning and emotional undercurrents behind my scattered thoughts.
        Keep it casual, dont say yo, help me make new connections i don't see, comfort, validate, challenge, all of it. dont be afraid to say a lot. format with markdown headings if needed.
        Use vivid metaphors and powerful imagery to help me see what I'm really building. Organize your thoughts with meaningful headings that create a narrative journey through my ideas.
        Don't just validate my thoughts - reframe them in a way that shows me what I'm really seeking beneath the surface. Go beyond the product concepts to the emotional core of what I'm trying to solve.
        Be willing to be profound and philosophical without sounding like you're giving therapy. I want someone who can see the patterns I can't see myself and articulate them in a way that feels like an epiphany.
        Start with 'hey, thanks for showing me this. my thoughts:' and then use markdown headings to structure your response.

        Here's my journal entry:
        """;

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        _store = new FreewriteStore(_settings.StorageFolder);
        _isDarkMode = _settings.ColorScheme == "dark";
        _focusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _focusTimer.Tick += FocusTimer_Tick;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveCurrentEntry(Sidebar.Visibility == Visibility.Visible);
        };
        _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _recordingTimer.Tick += RecordingTimer_Tick;
        _dictationPlaybackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _dictationPlaybackTimer.Tick += DictationPlaybackTimer_Tick;
        Loaded += MainWindow_Loaded;
        SourceInitialized += (_, _) => BorderlessChrome.Apply(this);
        Closing += (_, _) =>
        {
            SaveEntryTypography();
            SaveCurrentEntry(renderHistory: false);
        };
        Closed += (_, _) =>
        {
            StopDictationPlayback();
            _settings.Save();
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme();
        ApplyFont();
        NormalizeEditorChrome();
        _talkButtonDefaultContent = TalkButton.Content?.ToString() ?? "Talk";
        _talkButtonDefaultForeground = TalkButton.Foreground;
        UpdateTimerText();
        UpdateFolderPath();
        EditorTextBox.IsEnabled = false;

        try
        {
            var entries = await Task.Run(() => _store.LoadEntries().ToList());
            _entries = entries;
            ApplyStartupEntrySelection();
            RenderHistory();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not load entries.\n\n{ex.Message}", "Freewrite");
            _entries = [];
            CreateNewEntry();
        }
        finally
        {
            EditorTextBox.IsEnabled = true;
            EditorTextBox.Focus();
        }
    }

    private void ApplyStartupEntrySelection()
    {
        var hasEntryToday = _entries.Any(IsEntryFromToday);
        var hasEmptyTextEntryToday = _entries.Any(entry =>
            IsEntryFromToday(entry)
            && entry.EntryType == EntryType.Text
            && string.IsNullOrWhiteSpace(entry.PreviewText));
        var hasOnlyWelcomeEntry = _entries.Count == 1
            && _store.ReadEntry(_entries[0]).Contains("Welcome to Freewrite", StringComparison.OrdinalIgnoreCase);

        if (_entries.FirstOrDefault()?.EntryType == EntryType.Video)
        {
            CreateNewEntry();
        }
        else if (_entries.Count == 0)
        {
            CreateNewEntry();
        }
        else if (!hasEntryToday && !hasOnlyWelcomeEntry)
        {
            CreateNewEntry();
        }
        else if (hasEmptyTextEntryToday)
        {
            SelectEntry(_entries.First(entry =>
                IsEntryFromToday(entry)
                && entry.EntryType == EntryType.Text
                && string.IsNullOrWhiteSpace(entry.PreviewText)));
        }
        else
        {
            SelectEntry(_entries[0]);
        }
    }

    private void NormalizeEditorChrome()
    {
        EditorTextBox.BorderThickness = new Thickness(0);
        var scrollViewer = FindVisualChildren<ScrollViewer>(EditorTextBox).FirstOrDefault();
        if (scrollViewer is not null)
        {
            scrollViewer.Padding = new Thickness(0, 0, 14, 0);
            scrollViewer.Margin = new Thickness(0);
        }
    }

    private static bool IsEntryFromToday(HumanEntry entry)
    {
        return entry.Timestamp.Date == DateTime.Now.Date;
    }

    private void SelectEntry(HumanEntry entry)
    {
        _hoveredHistoryEntryId = null;
        StopDictationPlayback();
        SaveEntryTypography();
        SaveCurrentEntry();
        _selectedEntry = entry;
        _isLoadingEntry = true;
        try
        {
            if (entry.EntryType == EntryType.Video && entry.VideoFilename is not null)
            {
                var videoPath = _store.ResolveVideoPath(entry.VideoFilename);
                ShowVideo(videoPath);
                EditorTextBox.Text = string.Empty;
            }
            else
            {
                HideVideo();
                EditorTextBox.Text = _store.ReadEntry(entry);
            }
        }
        finally
        {
            _isLoadingEntry = false;
        }

        LoadEntryTypography(entry);
        UpdateVideoAwareControls();
        UpdateDictationAudioBar();
        UpdatePlaceholder();
        RenderHistory();
    }

    private void CreateNewEntry()
    {
        SaveEntryTypography();
        SaveCurrentEntry();
        var entry = HumanEntry.CreateNew();
        _entries.Insert(0, entry);
        _selectedEntry = entry;
        HideVideo();
        _isLoadingEntry = true;
        try
        {
            if (_entries.Count == 1)
            {
                EditorTextBox.Text = LoadDefaultGuide();
            }
            else
            {
                EditorTextBox.Text = NewEntryLeadingSpacing;
            }

            PlaceholderText.Text = _placeholderOptions[_random.Next(_placeholderOptions.Length)];
            _store.SaveNewEntry(entry, EditorTextBox.Text);
        }
        finally
        {
            _isLoadingEntry = false;
        }

        LoadEntryTypography(entry);
        UpdateVideoAwareControls();
        UpdateDictationAudioBar();
        UpdatePlaceholder();
        RenderHistory();
        EditorTextBox.CaretIndex = EditorTextBox.Text.Length;
        EditorTextBox.Focus();
    }

    private void UpdateDictationAudioBar(bool selectLatest = false)
    {
        var previousPath = GetSelectedDictationPath();
        _dictationClips = _selectedEntry is not null && _selectedEntry.EntryType == EntryType.Text
            ? _store.ListDictationAudioPaths(_selectedEntry).ToList()
            : [];

        var hasAudio = _dictationClips.Count > 0;
        DictationAudioBar.Visibility = hasAudio ? Visibility.Visible : Visibility.Collapsed;
        RetryTranscribeButton.Visibility = hasAudio ? Visibility.Visible : Visibility.Collapsed;

        if (!hasAudio)
        {
            _selectedDictationClipIndex = 0;
            StopDictationPlayback();
            RenderDictationClipSelector();
            return;
        }

        if (selectLatest)
        {
            _selectedDictationClipIndex = _dictationClips.Count - 1;
        }
        else if (previousPath is not null)
        {
            var preservedIndex = _dictationClips.FindIndex(path =>
                path.Equals(previousPath, StringComparison.OrdinalIgnoreCase));
            _selectedDictationClipIndex = preservedIndex >= 0 ? preservedIndex : _dictationClips.Count - 1;
        }
        else
        {
            _selectedDictationClipIndex = _dictationClips.Count - 1;
        }

        ReleaseDictationMedia();
        RenderDictationClipSelector();
        ResetDictationPlaybackUi();
    }

    private string? GetSelectedDictationPath()
    {
        if (_dictationClips.Count == 0 || _selectedDictationClipIndex < 0 || _selectedDictationClipIndex >= _dictationClips.Count)
        {
            return null;
        }

        return _dictationClips[_selectedDictationClipIndex];
    }

    private void RenderDictationClipSelector()
    {
        DictationClipSelector.Children.Clear();
        if (_dictationClips.Count <= 1)
        {
            DictationClipSelector.Visibility = Visibility.Collapsed;
            return;
        }

        DictationClipSelector.Visibility = Visibility.Visible;
        var plainButtonStyle = (Style)FindResource("PlainButton");
        var softBrush = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(128, 128, 128))
            : new SolidColorBrush(Color.FromRgb(136, 136, 136));
        var activeBrush = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(220, 220, 220))
            : new SolidColorBrush(Color.FromRgb(68, 68, 68));

        for (var i = 0; i < _dictationClips.Count; i++)
        {
            if (i > 0)
            {
                DictationClipSelector.Children.Add(new TextBlock
                {
                    Text = "·",
                    FontSize = 11,
                    Margin = new Thickness(2, 0, 2, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = softBrush
                });
            }

            var clipIndex = i;
            var clipButton = new Button
            {
                Content = $"{i + 1}",
                Tag = clipIndex,
                Style = plainButtonStyle,
                FontSize = 11,
                Padding = new Thickness(4, 0, 4, 0),
                MinWidth = 0,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = i == _selectedDictationClipIndex ? activeBrush : softBrush,
                FontWeight = i == _selectedDictationClipIndex ? FontWeights.SemiBold : FontWeights.Normal,
                Cursor = Cursors.Hand,
                ToolTip = $"Recording {i + 1}"
            };
            clipButton.Click += (_, _) => SelectDictationClip(clipIndex);
            DictationClipSelector.Children.Add(clipButton);
        }
    }

    private void SelectDictationClip(int index)
    {
        if (index < 0 || index >= _dictationClips.Count || index == _selectedDictationClipIndex)
        {
            return;
        }

        ReleaseDictationMedia();
        _selectedDictationClipIndex = index;
        RenderDictationClipSelector();
        ResetDictationPlaybackUi();
        ApplyDictationDurationToSlider(GetSelectedDictationPath());
    }

    private void LoadEntryTypography(HumanEntry entry)
    {
        var preferences = _store.LoadEntryPreferences(entry);
        _selectedFont = preferences.FontName;
        _currentFontSize = _fontSizes.FirstOrDefault(size => Math.Abs(size - preferences.FontSize) < 0.01);
        if (_currentFontSize <= 0)
        {
            _currentFontSize = 18;
        }

        _currentRandomFont = preferences.RandomFontName ?? string.Empty;
        RandomFontButton.Content = string.IsNullOrEmpty(_currentRandomFont)
            ? "Random"
            : $"Random [{_currentRandomFont}]";
        ApplyFont();
    }

    private void SaveEntryTypography()
    {
        if (_selectedEntry is null)
        {
            return;
        }

        _store.SaveEntryPreferences(
            _selectedEntry,
            new EntryPreferences
            {
                FontName = _selectedFont,
                FontSize = _currentFontSize,
                RandomFontName = string.IsNullOrEmpty(_currentRandomFont) ? null : _currentRandomFont
            });
    }

    private static string LoadDefaultGuide()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/default.md"));
        if (resource is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(resource.Stream);
        return reader.ReadToEnd();
    }

    private void SaveCurrentEntry(bool renderHistory = true)
    {
        _saveTimer.Stop();
        if (_isLoadingEntry || _selectedEntry is null || _selectedEntry.EntryType != EntryType.Text)
        {
            return;
        }

        _store.SaveEntry(_selectedEntry, EditorTextBox.Text);
        if (renderHistory)
        {
            RenderHistory();
        }
    }

    private void QueueSaveCurrentEntry()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void RenderHistory()
    {
        if (HistoryItems is null)
        {
            return;
        }

        HistoryItems.Children.Clear();
        var includeThumbnails = Sidebar.Visibility == Visibility.Visible;
        foreach (var entry in _entries)
        {
            HistoryItems.Children.Add(CreateHistoryRow(entry, includeThumbnails));
        }
    }

    private UIElement CreateHistoryRow(HumanEntry entry, bool includeThumbnails)
    {
        var isSelected = _selectedEntry?.Id == entry.Id;
        var isHovered = _hoveredHistoryEntryId == entry.Id;
        var border = new Border
        {
            Background = GetHistoryRowBackground(isSelected, isHovered),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(6, 2, 6, 2),
            Padding = new Thickness(10, 8, 8, 8),
            Cursor = Cursors.Hand
        };
        border.MouseRightButtonUp += (_, e) =>
        {
            if (border.ContextMenu is null)
            {
                AttachHistoryContextMenu(border, entry);
            }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textPanel = new StackPanel { Orientation = Orientation.Vertical };
        if (includeThumbnails && entry.EntryType == EntryType.Video && entry.VideoFilename is not null)
        {
            var thumb = _store.LoadThumbnail(entry.VideoFilename);
            if (thumb is not null)
            {
                textPanel.Children.Add(new Image
                {
                    Source = thumb,
                    Width = 64,
                    Height = 36,
                    Stretch = Stretch.UniformToFill,
                    Margin = new Thickness(0, 0, 0, 6)
                });
            }
        }

        textPanel.Children.Add(new TextBlock
        {
            Text = entry.Date,
            Foreground = new SolidColorBrush(_isDarkMode ? Colors.White : Color.FromRgb(70, 70, 70)),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(entry.PreviewText) ? "Empty entry" : entry.PreviewText,
            Foreground = new SolidColorBrush(_isDarkMode ? Color.FromRgb(170, 170, 170) : Color.FromRgb(110, 110, 110)),
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        grid.Children.Add(textPanel);

        var deleteButton = new Button
        {
            Content = "✕",
            Style = (Style)FindResource("PlainButton"),
            FontSize = 12,
            Padding = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Delete entry",
            Visibility = isSelected || isHovered ? Visibility.Visible : Visibility.Collapsed
        };
        deleteButton.Click += (_, e) =>
        {
            e.Handled = true;
            DeleteEntry(entry);
        };
        Grid.SetColumn(deleteButton, 1);
        grid.Children.Add(deleteButton);

        border.Child = grid;
        border.MouseLeftButtonUp += (_, _) => SelectEntry(entry);
        border.MouseEnter += (_, _) =>
        {
            _hoveredHistoryEntryId = entry.Id;
            border.Background = GetHistoryRowBackground(_selectedEntry?.Id == entry.Id, true);
            deleteButton.Visibility = Visibility.Visible;
        };
        border.MouseLeave += (_, _) =>
        {
            if (_hoveredHistoryEntryId == entry.Id)
            {
                _hoveredHistoryEntryId = null;
            }

            var stillSelected = _selectedEntry?.Id == entry.Id;
            border.Background = GetHistoryRowBackground(stillSelected, false);
            deleteButton.Visibility = stillSelected ? Visibility.Visible : Visibility.Collapsed;
        };
        return border;
    }

    private Brush GetHistoryRowBackground(bool isSelected, bool isHovered)
    {
        if (isSelected)
        {
            return new SolidColorBrush(_isDarkMode ? Color.FromRgb(62, 62, 62) : Color.FromRgb(245, 245, 245));
        }

        if (isHovered)
        {
            return new SolidColorBrush(_isDarkMode ? Color.FromRgb(50, 50, 50) : Color.FromRgb(248, 248, 248));
        }

        return Brushes.Transparent;
    }

    private void ConfirmDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingDeleteEntry is null)
        {
            HideDeleteConfirmation();
            return;
        }

        var entry = _pendingDeleteEntry;
        HideDeleteConfirmation();
        DeleteEntryImmediately(entry);
    }

    private void CancelDelete_Click(object sender, RoutedEventArgs e)
    {
        HideDeleteConfirmation();
    }

    private void HideDeleteConfirmation()
    {
        _pendingDeleteEntry = null;
        DeleteOverlay.Visibility = Visibility.Collapsed;
    }

    private void ExportEntry(HumanEntry entry)
    {
        if (entry.EntryType == EntryType.Video)
        {
            MessageBox.Show(this, "PDF export is for text entries. Video transcripts can be copied from the video view.", "Freewrite");
            return;
        }

        if (_selectedEntry?.Id == entry.Id)
        {
            SaveCurrentEntry();
        }

        PdfExporter.Export(new WindowOwner(this), entry, _store.ReadEntry(entry));
    }

    private void DeleteEntry(HumanEntry entry)
    {
        _pendingDeleteEntry = entry;
        DeleteOverlay.Visibility = Visibility.Visible;
    }

    private void DeleteEntryImmediately(HumanEntry entry)
    {
        _store.DeleteEntry(entry);
        _entries.RemoveAll(item => item.Id == entry.Id);
        if (_selectedEntry?.Id == entry.Id)
        {
            if (_entries.Count == 0)
            {
                CreateNewEntry();
            }
            else
            {
                SelectEntry(_entries[0]);
            }
        }

        RenderHistory();
    }

    private void ShowVideo(string path)
    {
        _isVideoEntryVisible = true;
        DictationAudioBar.Visibility = Visibility.Collapsed;
        EditorShell.Visibility = Visibility.Collapsed;
        VideoPlayer.Visibility = Visibility.Visible;
        VideoPlayer.Source = new Uri(path);
        VideoPlayer.Volume = _settings.VideoIsMuted ? 0 : _settings.VideoVolume;
        VideoPlayer.Play();
        _isVideoPaused = false;
        UpdateVideoPlaybackControls();
    }

    private void HideVideo()
    {
        _isVideoEntryVisible = false;
        VideoPlayer.Stop();
        VideoPlayer.Source = null;
        VideoPlayer.Visibility = Visibility.Collapsed;
        EditorShell.Visibility = Visibility.Visible;
        UpdateDictationAudioBar();
        _isVideoPaused = false;
        UpdateVideoPlaybackControls();
    }

    private void UpdateVideoAwareControls()
    {
        var hasTranscript = _selectedEntry?.VideoFilename is not null
            && _store.LoadTranscript(_selectedEntry.VideoFilename) is not null;
        CopyTranscriptButton.Visibility = _isVideoEntryVisible && hasTranscript ? Visibility.Visible : Visibility.Collapsed;
        VideoControlsSeparatorA.Visibility = _isVideoEntryVisible && hasTranscript ? Visibility.Visible : Visibility.Collapsed;
        VideoPlayButton.Visibility = _isVideoEntryVisible ? Visibility.Visible : Visibility.Collapsed;
        VideoControlsSeparatorB.Visibility = _isVideoEntryVisible ? Visibility.Visible : Visibility.Collapsed;
        VideoMuteButton.Visibility = _isVideoEntryVisible ? Visibility.Visible : Visibility.Collapsed;
        VideoControlsSeparatorC.Visibility = _isVideoEntryVisible ? Visibility.Visible : Visibility.Collapsed;
        var fontVisibility = _isVideoEntryVisible ? Visibility.Collapsed : Visibility.Visible;
        FontSizeButton.Visibility = fontVisibility;
        FontSep1.Visibility = fontVisibility;
        LatoButton.Visibility = fontVisibility;
        FontSep2.Visibility = fontVisibility;
        ArialButton.Visibility = fontVisibility;
        FontSep3.Visibility = fontVisibility;
        SystemFontButton.Visibility = fontVisibility;
        FontSep4.Visibility = fontVisibility;
        SerifButton.Visibility = fontVisibility;
        FontSep5.Visibility = fontVisibility;
        RandomFontButton.Visibility = fontVisibility;
        TalkButton.Visibility = fontVisibility;
        BackspaceSeparator.Visibility = _isVideoEntryVisible ? Visibility.Collapsed : Visibility.Visible;
        BackspaceButton.Visibility = _isVideoEntryVisible ? Visibility.Collapsed : Visibility.Visible;
        UpdateVideoPlaybackControls();
    }

    private void ApplyFont()
    {
        var fontFamily = _selectedFont == "Lato"
            ? new FontFamily(new Uri("pack://application:,,,/"), "./Resources/fonts/#Lato")
            : new FontFamily(_selectedFont);

        EditorTextBox.FontFamily = fontFamily;
        PlaceholderText.FontFamily = fontFamily;
        EditorTextBox.FontSize = _currentFontSize;
        PlaceholderText.FontSize = _currentFontSize;
        FontSizeButton.Content = $"{(int)_currentFontSize}px";
    }

    private void ApplyTheme()
    {
        var bg = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(35, 35, 35))
            : Brushes.White;
        var fg = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(230, 230, 230))
            : new SolidColorBrush(Color.FromRgb(51, 51, 51));
        var soft = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(140, 140, 140))
            : new SolidColorBrush(Color.FromRgb(136, 136, 136));

        Background = Brushes.Transparent;
        WindowShell.Background = bg;
        WindowShell.BorderBrush = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(52, 52, 52))
            : new SolidColorBrush(Color.FromRgb(230, 230, 230));
        RootGrid.Background = bg;
        MainSurface.Background = bg;
        Sidebar.Background = bg;
        Sidebar.BorderBrush = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(48, 48, 48))
            : new SolidColorBrush(Color.FromRgb(238, 238, 238));
        EditorTextBox.Background = Brushes.Transparent;
        EditorTextBox.Foreground = fg;
        EditorTextBox.CaretBrush = _isDarkMode ? Brushes.White : new SolidColorBrush(Color.FromRgb(51, 51, 51));
        EditorTextBox.Cursor = _isDarkMode ? FreewriteCursors.LightIBeam : Cursors.IBeam;
        PlaceholderText.Foreground = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(100, 100, 100))
            : new SolidColorBrush(Color.FromRgb(187, 187, 187));
        ThemeButton.Content = _isDarkMode ? "Light Mode" : "Dark Mode";
        SidebarTitleText.Foreground = fg;
        FolderPathText.Foreground = soft;
        DeleteDialog.Background = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(42, 42, 42))
            : Brushes.White;
        DeleteDialogTitle.Foreground = fg;
        DeleteDialogBody.Foreground = soft;
        RecordDialog.Background = DeleteDialog.Background;
        RecordingStatusText.Foreground = new SolidColorBrush(Color.FromRgb(232, 75, 75));
        RecordingPulseDot.Fill = RecordingStatusText.Foreground;
        VideoRecordElapsedText.Foreground = soft;
        TalkRecordingBar.Background = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(42, 42, 42))
            : new SolidColorBrush(Color.FromRgb(245, 245, 245));
        TalkRecordingBar.BorderBrush = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(52, 52, 52))
            : new SolidColorBrush(Color.FromRgb(220, 220, 220));
        TalkElapsedText.Foreground = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(200, 200, 200))
            : new SolidColorBrush(Color.FromRgb(100, 100, 100));
        TalkWaveform.BarBrush = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(170, 170, 170))
            : new SolidColorBrush(Color.FromRgb(120, 120, 120));
        DictationAudioBar.Background = TalkRecordingBar.Background;
        DictationAudioBar.BorderBrush = TalkRecordingBar.BorderBrush;
        DictationTimeText.Foreground = soft;
        DictationSeekSlider.Foreground = soft;
        ApplyDictationSliderTrackBrush();
        PlayPauseIcon.Fill = soft;
        CancelDeleteButton.Background = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(48, 48, 48))
            : new SolidColorBrush(Color.FromRgb(238, 238, 238));
        StopRecordingButton.Background = CancelDeleteButton.Background;
        ConfirmDeleteButton.Background = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(72, 34, 34))
            : new SolidColorBrush(Color.FromRgb(248, 226, 226));

        foreach (var button in FindVisualChildren<Button>(RootGrid))
        {
            button.Foreground = soft;
        }
        ConfirmDeleteButton.Foreground = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(255, 132, 132))
            : new SolidColorBrush(Color.FromRgb(150, 40, 40));

        _settings.ColorScheme = _isDarkMode ? "dark" : "light";
        _settings.Save();
        RenderHistory();
    }

    private void ApplyDictationSliderTrackBrush()
    {
        try
        {
            DictationSeekSlider.ApplyTemplate();
            if (DictationSeekSlider.Template?.FindName("SliderTrack", DictationSeekSlider) is Border track)
            {
                track.Background = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(64, 64, 64))
                    : new SolidColorBrush(Color.FromRgb(212, 212, 212));
            }
        }
        catch
        {
            // Slider template not ready yet; safe to skip.
        }
    }

    private void EditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePlaceholder();
        if (!_isLoadingEntry && _selectedEntry?.EntryType == EntryType.Text)
        {
            _selectedEntry.PreviewText = FreewriteStore.PreviewTextFromContent(EditorTextBox.Text, 30);
            QueueSaveCurrentEntry();
            if (Sidebar.Visibility == Visibility.Visible)
            {
                RenderHistory();
            }
        }
    }

    private void UpdatePlaceholder()
    {
        PlaceholderText.Visibility = !_isVideoEntryVisible && string.IsNullOrEmpty(EditorTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void FocusTimer_Tick(object? sender, EventArgs e)
    {
        if (!_timerIsRunning)
        {
            return;
        }

        if (_timeRemaining > 0)
        {
            _timeRemaining--;
            UpdateTimerText();
            return;
        }

        _timerIsRunning = false;
        FadeBottomNav(1);
    }

    private void UpdateTimerText()
    {
        var minutes = _timeRemaining / 60;
        var seconds = _timeRemaining % 60;
        TimerButton.Content = $"{minutes}:{seconds:00}";
        TimerButton.Foreground = _timerIsRunning ? Brushes.Red : (_isDarkMode ? Brushes.Gray : Brushes.Gray);
    }

    private void FadeBottomNav(double opacity)
    {
        var animation = new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(opacity > 0 ? 200 : 1000));
        BottomNav.BeginAnimation(OpacityProperty, animation);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_backspaceDisabled && (e.Key == Key.Back || e.Key == Key.Delete))
        {
            e.Handled = true;
        }
    }

    private void FontSize_Click(object sender, RoutedEventArgs e)
    {
        var currentIndex = Array.FindIndex(_fontSizes, size => Math.Abs(size - _currentFontSize) < 0.01);
        if (currentIndex < 0)
        {
            currentIndex = 1;
        }

        var next = _fontSizes[(currentIndex + 1) % _fontSizes.Length];
        _currentFontSize = next;
        ApplyFont();
        SaveEntryTypography();
    }

    private void Lato_Click(object sender, RoutedEventArgs e) => SetFont("Lato");
    private void Arial_Click(object sender, RoutedEventArgs e) => SetFont("Arial");
    private void SystemFont_Click(object sender, RoutedEventArgs e) => SetFont("Segoe UI");
    private void Serif_Click(object sender, RoutedEventArgs e) => SetFont("Times New Roman");

    private void RandomFont_Click(object sender, RoutedEventArgs e)
    {
        var standards = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Lato", "Arial", "Segoe UI", "Times New Roman" };
        var font = Fonts.SystemFontFamilies
            .Select(family => family.Source)
            .Where(source => !standards.Contains(source))
            .OrderBy(_ => _random.Next())
            .FirstOrDefault() ?? "Segoe UI";
        _currentRandomFont = font;
        RandomFontButton.Content = $"Random [{font}]";
        _selectedFont = font;
        ApplyFont();
        SaveEntryTypography();
    }

    private void SetFont(string font)
    {
        _selectedFont = font;
        if (font != _currentRandomFont)
        {
            _currentRandomFont = string.Empty;
            RandomFontButton.Content = "Random";
        }

        ApplyFont();
        SaveEntryTypography();
    }

    private void Timer_Click(object sender, RoutedEventArgs e)
    {
        _timerIsRunning = !_timerIsRunning;
        if (_timerIsRunning)
        {
            _focusTimer.Start();
            FadeBottomNav(0);
        }
        else
        {
            FadeBottomNav(1);
        }
        UpdateTimerText();
    }

    private void Timer_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        _timerIsRunning = false;
        _timeRemaining = 900;
        UpdateTimerText();
        FadeBottomNav(1);
    }

    private void Timer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var minutes = Math.Max(5, _timeRemaining / 60);
        minutes += e.Delta > 0 ? 5 : -5;
        minutes = Math.Clamp(minutes, 5, 180);
        _timeRemaining = minutes * 60;
        UpdateTimerText();
        e.Handled = true;
    }

    private async void Talk_Click(object sender, RoutedEventArgs e)
    {
        if (_isTalkRecording)
        {
            return;
        }

        if (_selectedEntry is null || _selectedEntry.EntryType != EntryType.Text)
        {
            MessageBox.Show(this, "Talk works on text entries. Open or create a writing entry first.", "Freewrite");
            return;
        }

        try
        {
            await StartTalkRecordingAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not start talk recording.\n\n{ex.Message}", "Freewrite");
        }
    }

    private async Task StartTalkRecordingAsync()
    {
        ReleaseDictationMedia();
        var audioPath = _store.CreateDictationAudioPath(_selectedEntry!);

        _dictation = new AudioDictationService();
        _dictation.LevelChanged += OnTalkLevelChanged;
        await _dictation.StartAsync(audioPath);
        _isTalkRecording = true;
        _recordingStartedAt = DateTime.Now;
        TalkElapsedText.Text = "0:00";
        TalkWaveform.Reset();
        TalkRecordingBar.Visibility = Visibility.Visible;
        CancelTalkButton.IsEnabled = true;
        FinishTalkButton.IsEnabled = true;
        TalkButton.IsEnabled = false;
        _recordingTimer.Start();
    }

    private void OnTalkLevelChanged(float level)
    {
        if (Dispatcher.CheckAccess())
        {
            TalkWaveform.PushLevel(level);
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Render, () => TalkWaveform.PushLevel(level));
    }

    private async Task RecordVideoWithWindowsCameraAsync()
    {
        using var transcriptCapture = new WindowsTranscriptCapture();
        var transcriptCaptureStarted = await transcriptCapture.TryStartAsync();

        try
        {
            var captureUi = new CameraCaptureUI();
            var windowHandle = new WindowInteropHelper(this).Handle;
            InitializeWithWindow.Initialize(captureUi, windowHandle);
            captureUi.VideoSettings.Format = CameraCaptureUIVideoFormat.Mp4;
            captureUi.VideoSettings.AllowTrimming = true;
            var capturedFile = await captureUi.CaptureFileAsync(CameraCaptureUIMode.Video);
            if (capturedFile is null)
            {
                return;
            }

            var tempFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetTempPath());
            var tempFile = await capturedFile.CopyAsync(
                tempFolder,
                $"{Guid.NewGuid()}{Path.GetExtension(capturedFile.Name)}",
                NameCollisionOption.ReplaceExisting);
            var transcript = transcriptCaptureStarted ? await transcriptCapture.StopAsync() : null;
            transcriptCaptureStarted = false;
            ImportVideoFromPath(tempFile.Path, transcript);
        }
        catch (Exception cameraException)
        {
            try
            {
                var fallbackPath = await RecordVideoInAppAsync();
                if (fallbackPath is null)
                {
                    return;
                }

                var transcript = transcriptCaptureStarted ? await transcriptCapture.StopAsync() : null;
                transcriptCaptureStarted = false;
                ImportVideoFromPath(fallbackPath, transcript);
            }
            catch (Exception fallbackException)
            {
                MessageBox.Show(
                    this,
                    "Windows camera capture failed, and the fallback recorder could not start. Use Choose Video instead.\n\n"
                        + $"Camera: {cameraException.Message}\n\nFallback: {fallbackException.Message}",
                    "Freewrite");
            }
        }
        finally
        {
            if (transcriptCaptureStarted)
            {
                await transcriptCapture.StopAsync();
            }
        }
    }

    private async Task<string?> RecordVideoInAppAsync()
    {
        using var mediaCapture = new MediaCapture();
        await mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
        {
            StreamingCaptureMode = StreamingCaptureMode.AudioAndVideo
        });

        var tempFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetTempPath());
        var outputFile = await tempFolder.CreateFileAsync($"{Guid.NewGuid()}.mp4", CreationCollisionOption.ReplaceExisting);
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);

        _recordingStopCompletion = new TaskCompletionSource<bool>();
        ShowRecordingOverlay();
        try
        {
            await mediaCapture.StartRecordToStorageFileAsync(profile, outputFile);
            await _recordingStopCompletion.Task;
            await mediaCapture.StopRecordAsync();
            return outputFile.Path;
        }
        finally
        {
            HideRecordingOverlay();
            _recordingStopCompletion = null;
        }
    }

    private void HideTalkOverlay()
    {
        _recordingTimer.Stop();
        TalkRecordingBar.Visibility = Visibility.Collapsed;
        TalkButton.IsEnabled = true;
        TalkButton.Content = _talkButtonDefaultContent;
        TalkButton.Foreground = _talkButtonDefaultForeground ?? Brushes.Gray;
        _isTalkRecording = false;
        if (_dictation is not null)
        {
            _dictation.LevelChanged -= OnTalkLevelChanged;
        }
    }

    private void ShowRecordingOverlay()
    {
        _recordingStartedAt = DateTime.Now;
        RecordingStatusText.Text = "Recording";
        VideoRecordElapsedText.Text = "0:00";
        RecordOverlay.Visibility = Visibility.Visible;
        StartRecordingPulse();
        _recordingTimer.Start();
    }

    private void HideRecordingOverlay()
    {
        _recordingTimer.Stop();
        RecordOverlay.Visibility = Visibility.Collapsed;
        StopRecordingPulse();
    }

    private void StartRecordingPulse()
    {
        var animation = new DoubleAnimation(1, 0.35, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        RecordingPulseDot.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private void StopRecordingPulse()
    {
        RecordingPulseDot.BeginAnimation(UIElement.OpacityProperty, null);
        RecordingPulseDot.Opacity = 1;
    }

    private void StopRecording_Click(object sender, RoutedEventArgs e)
    {
        _recordingStopCompletion?.TrySetResult(true);
    }

    private async void CancelTalk_Click(object sender, RoutedEventArgs e)
    {
        if (!_isTalkRecording || _dictation is null)
        {
            return;
        }

        try
        {
            await _dictation.StopAsync();
            var audioPath = _dictation.OutputPath ?? (_selectedEntry is not null
                ? _store.DictationAudioPath(_selectedEntry)
                : null);
            if (audioPath is not null && File.Exists(audioPath))
            {
                File.Delete(audioPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not discard recording.\n\n{ex.Message}", "Freewrite");
        }
        finally
        {
            _dictation?.Dispose();
            _dictation = null;
            HideTalkOverlay();
        }
    }

    private async void FinishTalk_Click(object sender, RoutedEventArgs e)
    {
        await FinishTalkRecordingAsync();
    }

    private async Task FinishTalkRecordingAsync()
    {
        if (_selectedEntry is null || _dictation is null || !_isTalkRecording)
        {
            HideTalkOverlay();
            return;
        }

        _isTalkRecording = false;
        _recordingTimer.Stop();
        CancelTalkButton.IsEnabled = false;
        FinishTalkButton.IsEnabled = false;
        TalkElapsedText.Text = "Transcribing…";

        try
        {
            await _dictation.StopAsync();
            var audioPath = _dictation.OutputPath ?? GetSelectedDictationPath() ?? _store.LatestDictationAudioPath(_selectedEntry);
            if (!File.Exists(audioPath))
            {
                MessageBox.Show(this, "No audio was captured. Check microphone permissions and try again.", "Freewrite");
                HideTalkOverlay();
                return;
            }

            var transcript = await _transcription.TranscribeWavAsync(
                audioPath,
                new Progress<string>(status => Dispatcher.Invoke(() => TalkElapsedText.Text = status)));

            if (string.IsNullOrWhiteSpace(transcript))
            {
                MessageBox.Show(
                    this,
                    "Audio saved, but transcription came back empty. Use \"Transcribe again\" after the local model finishes downloading.",
                    "Freewrite");
            }
            else
            {
                AppendTranscriptToEditor(transcript);
                QueueSaveCurrentEntry();
            }

            UpdateDictationAudioBar(selectLatest: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Transcription failed.\n\n{ex.Message}\n\nUse \"Transcribe again\" once the local model is ready.",
                "Freewrite");
            UpdateDictationAudioBar(selectLatest: true);
        }
        finally
        {
            _dictation?.Dispose();
            _dictation = null;
            ReleaseDictationMedia();
            HideTalkOverlay();
        }
    }

    private const string TranscriptTopSpacing = "\n\n";
    private const string NewEntryLeadingSpacing = "\n\n\n";

    private void AppendTranscriptToEditor(string transcript)
    {
        var trimmed = transcript.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditorTextBox.Text))
        {
            EditorTextBox.Text = TranscriptTopSpacing + trimmed;
        }
        else
        {
            EditorTextBox.Text = EditorTextBox.Text.TrimEnd() + "\n\n" + trimmed;
        }

        EditorTextBox.CaretIndex = EditorTextBox.Text.Length;
    }

    private void PlayDictation_Click(object sender, RoutedEventArgs e)
    {
        var path = GetSelectedDictationPath();
        if (_selectedEntry is null || path is null)
        {
            return;
        }

        if (_isDictationPlaying)
        {
            DictationPlayer.Pause();
            _isDictationPlaying = false;
            SetPlayPauseIcon(isPlaying: false);
            _dictationPlaybackTimer.Stop();
            UpdateDictationPlaybackUi();
            return;
        }

        if (DictationPlayer.Source is null || DictationPlayer.Source.LocalPath != path)
        {
            DictationPlayer.Source = new Uri(path);
            _dictationNaturalDuration = null;
            DictationSeekSlider.Value = 0;
            ApplyDictationDurationToSlider(path);
        }
        else
        {
            ApplyDictationDurationToSlider();
        }

        DictationPlayer.Play();
        _isDictationPlaying = true;
        SetPlayPauseIcon(isPlaying: true);
        _dictationPlaybackTimer.Start();
    }

    private void ReleaseDictationMedia()
    {
        _dictationPlaybackTimer.Stop();
        _isDictationPlaying = false;
        _dictationSeekDragging = false;
        SetPlayPauseIcon(isPlaying: false);

        if (DictationPlayer.Source is null)
        {
            return;
        }

        DictationPlayer.Stop();
        DictationPlayer.Source = null;
        _dictationNaturalDuration = null;
    }

    private void StopDictationPlayback()
    {
        ReleaseDictationMedia();
        ResetDictationPlaybackUi();
    }

    private void ResetDictationPlaybackUi()
    {
        _dictationSeekDragging = false;
        SetPlayPauseIcon(isPlaying: false);
        DictationSeekSlider.Value = 0;
        DictationTimeText.Text = FormatPlaybackTime(TimeSpan.Zero, GetDictationDuration());
    }

    private void SetPlayPauseIcon(bool isPlaying)
    {
        PlayPauseIcon.Data = isPlaying
            ? Geometry.Parse("M 0,3 L 3.5,3 L 3.5,13 L 0,13 Z M 6.5,3 L 10,3 L 10,13 L 6.5,13 Z")
            : Geometry.Parse("M 0,3 L 11,8 L 0,13 Z");
        PlayDictationButton.ToolTip = isPlaying ? "Pause" : "Play";
    }

    private void DictationPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        ApplyDictationDurationToSlider();
        UpdateDictationPlaybackUi();
    }

    private void DictationPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        _dictationPlaybackTimer.Stop();
        _isDictationPlaying = false;
        SetPlayPauseIcon(isPlaying: false);
        var duration = GetDictationDuration();
        if (duration > TimeSpan.Zero)
        {
            DictationSeekSlider.Value = DictationSeekSlider.Maximum;
            DictationTimeText.Text = FormatPlaybackTime(duration, duration);
        }
    }

    private void DictationPlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (_dictationSeekDragging || !_isDictationPlaying)
        {
            return;
        }

        UpdateDictationPlaybackUi();
    }

    private void UpdateDictationPlaybackUi()
    {
        var position = DictationPlayer.Position;
        var duration = GetDictationDuration();
        if (duration > TimeSpan.Zero)
        {
            DictationSeekSlider.Maximum = Math.Max(1, duration.TotalSeconds);
            DictationSeekSlider.Value = Math.Clamp(position.TotalSeconds, 0, DictationSeekSlider.Maximum);
        }

        DictationTimeText.Text = FormatPlaybackTime(position, duration);
    }

    private static string FormatPlaybackTime(TimeSpan position, TimeSpan duration)
    {
        return $"{FormatClock(position)} / {FormatClock(duration)}";
    }

    private static string FormatClock(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }

    private void DictationSeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dictationSeekDragging = true;
    }

    private void DictationSeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _dictationSeekDragging = false;
        SeekDictationToSlider();
    }

    private void DictationSeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_dictationSeekDragging)
        {
            return;
        }

        var duration = GetDictationDuration();
        DictationTimeText.Text = FormatPlaybackTime(TimeSpan.FromSeconds(e.NewValue), duration);
    }

    private void SeekDictationToSlider()
    {
        if (DictationPlayer.Source is null)
        {
            return;
        }

        var duration = GetDictationDuration();
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var target = TimeSpan.FromSeconds(DictationSeekSlider.Value);
        DictationPlayer.Position = target;
        UpdateDictationPlaybackUi();
    }

    private void ApplyDictationDurationToSlider(string? wavPath = null)
    {
        if (!TryResolveDictationDuration(wavPath, out var duration))
        {
            return;
        }

        DictationSeekSlider.Maximum = Math.Max(1, duration.TotalSeconds);
    }

    private TimeSpan GetDictationDuration()
    {
        return TryResolveDictationDuration(null, out var duration) ? duration : TimeSpan.Zero;
    }

    private bool TryResolveDictationDuration(string? wavPath, out TimeSpan duration)
    {
        if (_dictationNaturalDuration is { } cached && cached > TimeSpan.Zero)
        {
            duration = cached;
            return true;
        }

        if (DictationPlayer.NaturalDuration.HasTimeSpan)
        {
            duration = DictationPlayer.NaturalDuration.TimeSpan;
            _dictationNaturalDuration = duration;
            return duration > TimeSpan.Zero;
        }

        wavPath ??= DictationPlayer.Source?.LocalPath ?? GetSelectedDictationPath();

        if (wavPath is null || !File.Exists(wavPath))
        {
            duration = TimeSpan.Zero;
            return false;
        }

        try
        {
            using var reader = new WaveFileReader(wavPath);
            duration = reader.TotalTime;
            _dictationNaturalDuration = duration;
            return duration > TimeSpan.Zero;
        }
        catch
        {
            duration = TimeSpan.Zero;
            return false;
        }
    }

    private async void RetryTranscribe_Click(object sender, RoutedEventArgs e)
    {
        var audioPath = GetSelectedDictationPath();
        if (_selectedEntry is null || audioPath is null)
        {
            return;
        }

        try
        {
            RetryTranscribeButton.IsEnabled = false;
            var transcript = await _transcription.TranscribeWavAsync(audioPath);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                MessageBox.Show(this, "Transcription returned empty. Try again after the model download completes.", "Freewrite");
                return;
            }

            AppendTranscriptToEditor(transcript);
            QueueSaveCurrentEntry();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Transcription failed.\n\n{ex.Message}", "Freewrite");
        }
        finally
        {
            RetryTranscribeButton.IsEnabled = true;
        }
    }

    private void RecordingTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _recordingStartedAt;
        var minutes = (int)elapsed.TotalMinutes;
        var text = $"{minutes}:{elapsed.Seconds:00}";
        if (_isTalkRecording)
        {
            TalkElapsedText.Text = text;
        }
        else if (RecordOverlay.Visibility == Visibility.Visible)
        {
            VideoRecordElapsedText.Text = text;
        }
    }

    private void ChooseVideoFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a video entry",
            Filter = "Video files (*.mov;*.mp4;*.m4v;*.wmv)|*.mov;*.mp4;*.m4v;*.wmv|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ImportVideoFromPath(dialog.FileName);
    }

    private void ImportVideoFromPath(string videoPath, string? capturedTranscript = null)
    {
        var transcriptCandidate = Path.ChangeExtension(videoPath, ".md");
        var transcript = capturedTranscript ?? (File.Exists(transcriptCandidate) ? File.ReadAllText(transcriptCandidate) : null);
        var replacement = _selectedEntry?.EntryType == EntryType.Text && string.IsNullOrWhiteSpace(EditorTextBox.Text)
            ? _selectedEntry
            : null;
        var entry = _store.SaveImportedVideo(videoPath, transcript, replacement);
        if (replacement is not null)
        {
            var index = _entries.FindIndex(item => item.Id == replacement.Id);
            if (index >= 0)
            {
                _entries[index] = entry;
            }
        }
        else
        {
            _entries.Insert(0, entry);
        }

        _entries = _entries.OrderByDescending(item => item.Timestamp).ToList();
        SelectEntry(entry);
    }

    private void Chat_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            OpenChatMenu();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Chat menu failed.\n\n{ex.Message}", "Freewrite");
        }
    }

    private void OpenChatMenu()
    {
        var sourceText = CurrentChatSourceText();
        var encodedGpt = Uri.EscapeDataString(ChatGptPrompt + "\n\n" + sourceText);
        var encodedClaude = Uri.EscapeDataString(ClaudePrompt + "\n\n" + sourceText);
        var gptUrlLength = "https://chat.openai.com/?prompt=".Length + encodedGpt.Length;
        var claudeUrlLength = "https://claude.ai/new?q=".Length + encodedClaude.Length;
        var isUrlTooLong = gptUrlLength > 6000 || claudeUrlLength > 6000;

        var menu = FreewriteMenu.Create(ChatButton, _isDarkMode, minWidth: 120, maxWidth: 320);
        if (isUrlTooLong)
        {
            menu.Items.Add(FreewriteMenu.CreateItem(
                "Hey, your entry is quite long. You'll need to manually copy the prompt by clicking 'Copy Prompt' below and then paste it into AI of your choice (ex. ChatGPT). The prompt includes your entry as well.",
                action: null,
                _isDarkMode,
                enabled: false));
            menu.Items.Add(FreewriteMenu.CreateDivider(_isDarkMode));
            menu.Items.Add(FreewriteMenu.CreateItem(
                "Copy Prompt",
                () => CopyPrompt(ChatGptPrompt, sourceText),
                _isDarkMode));
        }
        else if (!_isVideoEntryVisible && EditorTextBox.Text.TrimStart().StartsWith("Hi. Welcome to Freewrite", StringComparison.OrdinalIgnoreCase))
        {
            menu.Items.Add(FreewriteMenu.CreateItem(
                "Yo. Sorry, you can't chat with the guide lol. Please write your own entry.",
                action: null,
                _isDarkMode,
                enabled: false));
        }
        else if (!_isVideoEntryVisible && sourceText.Length < 350)
        {
            menu.Items.Add(FreewriteMenu.CreateItem(
                "Please free write for at minimum 5 minutes first. Then click this. Trust.",
                action: null,
                _isDarkMode,
                enabled: false));
        }
        else
        {
            menu.Items.Add(FreewriteMenu.CreateItem(
                "ChatGPT",
                () => OpenChat("https://chat.openai.com/?prompt=", ChatGptPrompt, sourceText),
                _isDarkMode));
            menu.Items.Add(FreewriteMenu.CreateDivider(_isDarkMode));
            menu.Items.Add(FreewriteMenu.CreateItem(
                "Claude",
                () => OpenChat("https://claude.ai/new?q=", ClaudePrompt, sourceText),
                _isDarkMode));
            menu.Items.Add(FreewriteMenu.CreateDivider(_isDarkMode));
            menu.Items.Add(FreewriteMenu.CreateItem(
                "Copy Prompt",
                () => CopyPrompt(ChatGptPrompt, sourceText),
                _isDarkMode));
        }

        menu.IsOpen = true;
    }

    private void OpenChat(string baseUrl, string prompt, string sourceText)
    {
        var fullText = prompt + "\n\n" + sourceText;
        var encoded = Uri.EscapeDataString(fullText);
        if ((baseUrl + encoded).Length > 6000)
        {
            CopyPrompt(prompt, sourceText);
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = baseUrl + encoded, UseShellExecute = true });
    }

    private static void CopyPrompt(string prompt, string sourceText)
    {
        Clipboard.SetText(prompt + "\n\n" + sourceText);
    }

    private string CurrentChatSourceText()
    {
        if (_selectedEntry?.VideoFilename is not null)
        {
            return _store.LoadTranscript(_selectedEntry.VideoFilename)?.Trim() ?? string.Empty;
        }

        return EditorTextBox.Text.Trim();
    }

    private void CopyTranscript_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry?.VideoFilename is null)
        {
            return;
        }

        var transcript = _store.LoadTranscript(_selectedEntry.VideoFilename);
        if (!string.IsNullOrWhiteSpace(transcript))
        {
            Clipboard.SetText(transcript);
        }
    }

    private void VideoPlay_Click(object sender, RoutedEventArgs e)
    {
        if (!_isVideoEntryVisible)
        {
            return;
        }

        if (_isVideoPaused)
        {
            VideoPlayer.Play();
            _isVideoPaused = false;
        }
        else
        {
            VideoPlayer.Pause();
            _isVideoPaused = true;
        }

        UpdateVideoPlaybackControls();
    }

    private void VideoMute_Click(object sender, RoutedEventArgs e)
    {
        _settings.VideoIsMuted = !_settings.VideoIsMuted;
        if (!_settings.VideoIsMuted && _settings.VideoVolume <= 0)
        {
            _settings.VideoVolume = 1.0;
        }

        VideoPlayer.Volume = _settings.VideoIsMuted ? 0 : _settings.VideoVolume;
        _settings.Save();
        UpdateVideoPlaybackControls();
    }

    private void UpdateVideoPlaybackControls()
    {
        if (VideoPlayButton is null || VideoMuteButton is null)
        {
            return;
        }

        VideoPlayButton.Content = _isVideoPaused ? "Play" : "Pause";
        VideoMuteButton.Content = _settings.VideoIsMuted ? "Unmute" : "Mute";
    }

    private void Backspace_Click(object sender, RoutedEventArgs e)
    {
        _backspaceDisabled = !_backspaceDisabled;
        BackspaceButton.Content = _backspaceDisabled ? "Backspace is Off" : "Backspace is On";
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (!_isFullscreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            _previousResizeMode = ResizeMode;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            _isFullscreen = true;
            FullscreenButton.Content = "Minimize";
        }
        else
        {
            WindowStyle = _previousWindowStyle;
            ResizeMode = _previousResizeMode;
            WindowState = _previousWindowState;
            _isFullscreen = false;
            FullscreenButton.Content = "Fullscreen";
        }
    }

    private void NewEntry_Click(object sender, RoutedEventArgs e) => CreateNewEntry();

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        _isDarkMode = !_isDarkMode;
        ApplyTheme();
    }

    private void AttachHistoryContextMenu(Border border, HumanEntry entry)
    {
        var menu = FreewriteMenu.Create(border, _isDarkMode, minWidth: 140, maxWidth: 180);
        menu.Items.Add(FreewriteMenu.CreateItem("Export", () => ExportEntry(entry), _isDarkMode));
        menu.Items.Add(FreewriteMenu.CreateDivider(_isDarkMode));
        menu.Items.Add(FreewriteMenu.CreateItem("Delete", () => DeleteEntry(entry), _isDarkMode));
        border.ContextMenu = menu;
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        var show = Sidebar.Visibility != Visibility.Visible;
        Sidebar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        SidebarColumn.Width = show ? new GridLength(248) : new GridLength(0);
        RenderHistory();
    }

    private void CloseSidebar_Click(object sender, RoutedEventArgs e)
    {
        Sidebar.Visibility = Visibility.Collapsed;
        SidebarColumn.Width = new GridLength(0);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var menu = FreewriteMenu.Create(SidebarFolderButton, _isDarkMode, minWidth: 140, maxWidth: 200);
        menu.Items.Add(FreewriteMenu.CreateItem(
            "Open Folder",
            () => Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{_store.RootDirectory}\"", UseShellExecute = true }),
            _isDarkMode));
        menu.Items.Add(FreewriteMenu.CreateDivider(_isDarkMode));
        menu.Items.Add(FreewriteMenu.CreateItem("Choose Folder", ChooseFolder, _isDarkMode));
        menu.IsOpen = true;
    }

    private void ChooseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose Freewrite storage folder",
            InitialDirectory = Directory.Exists(_store.RootDirectory)
                ? _store.RootDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        SaveCurrentEntry();
        _store.ChangeRoot(dialog.FolderName);
        _settings.StorageFolder = dialog.FolderName;
        _settings.Save();
        UpdateFolderPath();
        ReloadEntriesAsync();
    }

    private async void ReloadEntriesAsync()
    {
        EditorTextBox.IsEnabled = false;
        try
        {
            _entries = await Task.Run(() => _store.LoadEntries().ToList());
            ApplyStartupEntrySelection();
            RenderHistory();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not load entries.\n\n{ex.Message}", "Freewrite");
        }
        finally
        {
            EditorTextBox.IsEnabled = true;
        }
    }

    private void UpdateFolderPath()
    {
        FolderPathText.Text = _store.RootDirectory;
        FolderPathText.ToolTip = _store.RootDirectory;
        SidebarFolderButton.ToolTip = _store.RootDirectory;
    }

    private void BottomNav_MouseEnter(object sender, MouseEventArgs e) => FadeBottomNav(1);

    private void BottomNav_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_timerIsRunning)
        {
            FadeBottomNav(0);
        }
    }

    private void MainSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
        {
            Fullscreen_Click(sender, e);
        }
        else if (e.ChangedButton == MouseButton.Left && e.GetPosition(this).Y < 32)
        {
            DragMove();
        }
    }

    private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (_isVideoEntryVisible)
        {
            VideoPlayer.Position = TimeSpan.Zero;
            VideoPlayer.Play();
            return;
        }

        HideVideo();
    }

    private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        MessageBox.Show(this, "Windows could not play this video. Install the needed codec or import a Windows-supported video file.", "Freewrite");
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
