using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Media.Capture;
using Windows.Storage;
using WinRT.Interop;

namespace FreewriteWindows;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _focusTimer;
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
    private int _timeRemaining = 900;
    private string _selectedFont = "Lato";
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
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _settings.Save();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme();
        ApplyFont();
        LoadExistingEntries();
        UpdateTimerText();
        UpdateFolderPath();
    }

    private void LoadExistingEntries()
    {
        _entries = _store.LoadEntries().ToList();
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

        RenderHistory();
    }

    private static bool IsEntryFromToday(HumanEntry entry)
    {
        return entry.Timestamp.Date == DateTime.Now.Date;
    }

    private void SelectEntry(HumanEntry entry)
    {
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

        UpdateVideoAwareControls();
        UpdatePlaceholder();
        RenderHistory();
    }

    private void CreateNewEntry()
    {
        SaveCurrentEntry();
        var entry = HumanEntry.CreateNew();
        _entries.Insert(0, entry);
        _selectedEntry = entry;
        HideVideo();
        _isLoadingEntry = true;
        try
        {
            EditorTextBox.Text = _entries.Count == 1 ? LoadDefaultGuide() : string.Empty;
            PlaceholderText.Text = _placeholderOptions[_random.Next(_placeholderOptions.Length)];
            _store.SaveNewEntry(entry, EditorTextBox.Text);
        }
        finally
        {
            _isLoadingEntry = false;
        }

        UpdateVideoAwareControls();
        UpdatePlaceholder();
        RenderHistory();
        EditorTextBox.Focus();
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

    private void SaveCurrentEntry()
    {
        if (_isLoadingEntry || _selectedEntry is null || _selectedEntry.EntryType != EntryType.Text)
        {
            return;
        }

        _store.SaveEntry(_selectedEntry, EditorTextBox.Text);
        RenderHistory();
    }

    private void RenderHistory()
    {
        if (HistoryItems is null)
        {
            return;
        }

        HistoryItems.Children.Clear();
        foreach (var entry in _entries)
        {
            HistoryItems.Children.Add(CreateHistoryRow(entry));
        }
    }

    private UIElement CreateHistoryRow(HumanEntry entry)
    {
        var isSelected = _selectedEntry?.Id == entry.Id;
        var border = new Border
        {
            Background = isSelected
                ? new SolidColorBrush(_isDarkMode ? Color.FromRgb(60, 60, 60) : Color.FromRgb(245, 245, 245))
                : Brushes.Transparent,
            BorderBrush = new SolidColorBrush(_isDarkMode ? Color.FromRgb(38, 38, 38) : Color.FromRgb(238, 238, 238)),
            BorderThickness = _isDarkMode ? new Thickness(0) : new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 10, 12, 10),
            Cursor = Cursors.Hand
        };

        var textPanel = new StackPanel { Orientation = Orientation.Vertical };
        if (entry.EntryType == EntryType.Video && entry.VideoFilename is not null)
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
            Text = string.IsNullOrWhiteSpace(entry.PreviewText) ? "Untitled" : entry.PreviewText,
            Foreground = new SolidColorBrush(_isDarkMode ? Color.FromRgb(218, 218, 218) : Color.FromRgb(65, 65, 65)),
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var menu = new ContextMenu();
        menu.Items.Add(ChatMenuItem("Export", () => ExportEntry(entry)));
        menu.Items.Add(ChatMenuItem("Delete", () => DeleteEntry(entry)));
        border.ContextMenu = menu;
        border.Child = textPanel;
        border.MouseLeftButtonUp += (_, _) => SelectEntry(entry);
        return border;
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
        FontSep6.Visibility = fontVisibility;
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
        EditorTextBox.FontSize = _fontSizes.First(size => Math.Abs(size - EditorTextBox.FontSize) < 0.01);
        PlaceholderText.FontSize = EditorTextBox.FontSize;
        FontSizeButton.Content = $"{(int)EditorTextBox.FontSize}px";
    }

    private void ApplyTheme()
    {
        var bg = _isDarkMode ? Brushes.Black : Brushes.White;
        var fg = _isDarkMode ? new SolidColorBrush(Color.FromRgb(230, 230, 230)) : new SolidColorBrush(Color.FromRgb(51, 51, 51));
        var soft = _isDarkMode ? new SolidColorBrush(Color.FromRgb(128, 128, 128)) : new SolidColorBrush(Color.FromRgb(136, 136, 136));

        Background = bg;
        RootGrid.Background = bg;
        MainSurface.Background = bg;
        Sidebar.Background = bg;
        Sidebar.BorderBrush = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(36, 36, 36))
            : new SolidColorBrush(Color.FromRgb(238, 238, 238));
        EditorTextBox.Background = Brushes.Transparent;
        EditorTextBox.Foreground = fg;
        PlaceholderText.Foreground = _isDarkMode ? new SolidColorBrush(Color.FromRgb(70, 70, 70)) : new SolidColorBrush(Color.FromRgb(187, 187, 187));
        ThemeButton.Content = _isDarkMode ? "Light Mode" : "Dark Mode";
        SidebarTitleText.Foreground = fg;
        FolderPathText.Foreground = soft;
        DeleteDialog.Background = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(28, 28, 28))
            : Brushes.White;
        DeleteDialogTitle.Foreground = fg;
        DeleteDialogBody.Foreground = soft;

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

    private void EditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePlaceholder();
        if (!_isLoadingEntry && _selectedEntry?.EntryType == EntryType.Text)
        {
            _store.SaveEntry(_selectedEntry, EditorTextBox.Text);
            RenderHistory();
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
        var currentIndex = Array.FindIndex(_fontSizes, size => Math.Abs(size - EditorTextBox.FontSize) < 0.01);
        var next = _fontSizes[(currentIndex + 1) % _fontSizes.Length];
        EditorTextBox.FontSize = next;
        PlaceholderText.FontSize = next;
        FontSizeButton.Content = $"{(int)next}px";
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
        SetFont(font);
    }

    private void SetFont(string font)
    {
        _selectedFont = font;
        ApplyFont();
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

    private void Video_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var recordItem = new MenuItem { Header = "Record Video" };
        recordItem.Click += async (_, _) => await RecordVideoWithWindowsCameraAsync();
        menu.Items.Add(recordItem);
        menu.Items.Add(ChatMenuItem("Choose Video", ChooseVideoFile));
        menu.PlacementTarget = VideoButton;
        menu.IsOpen = true;
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
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Windows camera capture failed. Use Choose Video instead.\n\n{ex.Message}", "Freewrite");
        }
        finally
        {
            if (transcriptCaptureStarted)
            {
                await transcriptCapture.StopAsync();
            }
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
        var sourceText = CurrentChatSourceText();
        var menu = new ContextMenu();
        if (!_isVideoEntryVisible && EditorTextBox.Text.TrimStart().StartsWith("Hi. Welcome to Freewrite", StringComparison.OrdinalIgnoreCase))
        {
            menu.Items.Add(new MenuItem { Header = "Yo. Sorry, you can't chat with the guide lol. Please write your own entry.", IsEnabled = false });
        }
        else if (!_isVideoEntryVisible && sourceText.Length < 350)
        {
            menu.Items.Add(new MenuItem { Header = "Write at least 350 characters first.", IsEnabled = false });
        }
        else
        {
            menu.Items.Add(ChatMenuItem("ChatGPT", () => OpenChat("https://chat.openai.com/?prompt=", ChatGptPrompt, sourceText)));
            menu.Items.Add(ChatMenuItem("Claude", () => OpenChat("https://claude.ai/new?q=", ClaudePrompt, sourceText)));
            menu.Items.Add(ChatMenuItem("Copy Prompt", () => CopyPrompt(ChatGptPrompt, sourceText)));
        }

        menu.PlacementTarget = ChatButton;
        menu.IsOpen = true;
    }

    private MenuItem ChatMenuItem(string label, Action action)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => action();
        return item;
    }

    private void OpenChat(string baseUrl, string prompt, string sourceText)
    {
        var fullText = prompt + "\n\n" + sourceText;
        var encoded = Uri.EscapeDataString(fullText);
        if ((baseUrl + encoded).Length > 6000)
        {
            CopyPrompt(prompt, sourceText);
            MessageBox.Show(this, "Prompt was too long for URL launch, so it was copied instead.", "Freewrite");
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
        var menu = new ContextMenu();
        menu.Items.Add(ChatMenuItem("Open Folder", () =>
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{_store.RootDirectory}\"", UseShellExecute = true });
        }));
        menu.Items.Add(ChatMenuItem("Choose Folder", ChooseFolder));
        menu.PlacementTarget = SidebarFolderButton;
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
        LoadExistingEntries();
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
        VideoPlayer.Position = TimeSpan.Zero;
        VideoPlayer.Play();
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
