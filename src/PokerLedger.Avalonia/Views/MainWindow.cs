using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Diagnostics;
using PokerLedger.Models;
using PokerLedger.Services;

namespace PokerLedger.Views;

public sealed class MainWindow : Window
{
    private readonly AppPaths _paths = new();
    private readonly JsonStore _store;
    private readonly ReceiptExporter _receiptExporter;
    private AppSettings _settings;
    private PokerSession? _session;
    private string? _sessionPath;
    private Player? _selectedPlayer;
    private Player? _cashOutPlayer;
    private string? _lastExportPath;
    private string? _startNotice;
    private bool _isDirty;
    private bool _allowWindowClose;
    private bool _refreshingPlayerList;
    private bool _refreshingCashOutList;
    private bool _restoringHistory;
    private bool _applyingNameSuggestion;
    private WindowState _windowStateBeforeFullscreen = WindowState.Normal;
    private string _currentScreen = "start";
    private string? _pendingNameSuggestion;
    private string? _typedNamePrefix;
    private readonly List<string> _undoStack = [];
    private readonly List<string> _redoStack = [];
    private readonly List<(MenuItem Item, Func<bool> Enabled)> _menuStateRules = [];

    private readonly Grid _root = new();
    private readonly ContentControl _content = new();
    private Menu? _mainMenu;
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _startNoticeTimer = new();

    private readonly TextBox _sessionName = new();
    private readonly StackPanel _presetPanel = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
    private readonly StackPanel _blindRows = new() { Spacing = 6 };
    private readonly StackPanel _denomRows = new() { Spacing = 6 };
    private readonly List<TextBox> _blindBoxes = [];
    private readonly List<TextBox> _denomBoxes = [];
    private readonly ListBox _archiveList = new();
    private string? _archiveSelectionAnchorPath;
    private bool _selectingArchiveRange;

    private TextBlock _header = new();
    private TextBlock _elapsed = new();
    private readonly ListBox _playerList = new();
    private readonly TextBox _newPlayerName = new();
    private readonly TextBox _newPlayerBuyIn = new();
    private readonly ListBox _nameSuggestions = new() { IsVisible = false, MaxHeight = 120 };
    private readonly Border _selectedPlayerBox = new();
    private TextBox _selectedName = new();
    private TextBox _selectedInitial = new();
    private TextBox _rebuyAmount = new();
    private TextBox _playerNote = new();
    private ListBox _transactionList = new();
    private readonly TextBlock _selectedTotals = new();

    private TextBox _finalRake = new();
    private TextBlock _summaryTotals = new();
    private TextBlock _summaryBuyIns = new();
    private TextBlock _summaryCashOuts = new();
    private TextBlock _balanceLine = new();
    private ListBox _cashOutPlayerList = new();
    private StackPanel _cashOutChipPanel = new() { Spacing = 8 };
    private TextBlock _cashOutPreview = new();
    private TextBlock _finishStatus = new();

    private static readonly FontFamily UiFont = FontFamily.Parse("Segoe UI Variable Text, Segoe UI, Inter, Arial");
    private static readonly FontFamily MenuFont = FontFamily.Parse("Segoe UI, Arial");
    private static readonly FontFamily MonoFont = FontFamily.Parse("Courier New");
    private static readonly SolidColorBrush PageBrush = new(Color.Parse("#203f61"));
    private static readonly SolidColorBrush PanelBrush = new(Color.Parse("#2f5e70"));
    private static readonly SolidColorBrush InputBrush = new(Color.Parse("#102b40"));
    private static readonly SolidColorBrush HeaderBrush = new(Color.Parse("#4f765e"));
    private static readonly SolidColorBrush MenuBrush = new(Color.Parse("#3b699c"));
    private static readonly SolidColorBrush ButtonBrush = new(Color.Parse("#3e7a82"));
    private static readonly SolidColorBrush DisabledBrush = new(Color.Parse("#2a485a"));
    private static readonly SolidColorBrush TextBrush = new(Color.Parse("#fff8dc"));
    private static readonly SolidColorBrush MenuTextBrush = new(Color.Parse("#fff4d5"));
    private static readonly SolidColorBrush MutedTextBrush = new(Color.Parse("#cdd7b6"));
    private static readonly SolidColorBrush SuccessBrush = new(Color.Parse("#91bd7f"));
    private static readonly SolidColorBrush WarningBrush = new(Color.Parse("#f7dd73"));
    private static readonly IBrush GoldBrush = CreateGoldFoilBrush();
    private static readonly ContrastPalette[] ContrastPalettes =
    [
        new(
            "BlueGreenGold",
            "Blue / Green / Gold",
            "#203f61",
            "#2f5e70",
            "#102b40",
            "#4f765e",
            "#3b699c",
            "#3e7a82",
            "#2a485a",
            "#fff8dc",
            "#fff4d5",
            "#cdd7b6",
            "#91bd7f",
            "#f7dd73"),
        new(
            "GoldFoilBurgundy",
            "Burgundy / Gold",
            "#53142e",
            "#691b3a",
            "#431025",
            "#8b3159",
            "#8b3159",
            "#762045",
            "#4a2537",
            "#fff2df",
            "#f3ddc6",
            "#c995a9",
            "#9beca6",
            "#ffb3a8"),
        new(
            "RedYellowPurple",
            "Red / Yellow",
            "#4e170c",
            "#642011",
            "#250b06",
            "#5a2c5e",
            "#5a2c5e",
            "#762a19",
            "#3b1d18",
            "#fff3be",
            "#fff2c7",
            "#d9ba42",
            "#91bd7f",
            "#f0d35d"),
        new(
            "WhiteBlueTeal",
            "White / Blue",
            "#f3f2e2",
            "#dfe8d8",
            "#fbfaf0",
            "#a4bca2",
            "#a4bca2",
            "#a4bca2",
            "#ccd5c7",
            "#122c3b",
            "#122c3b",
            "#557079",
            "#4f7e51",
            "#8b7418"),
        new(
            "BlackYellow",
            "Black / Yellow",
            "#0a0b04",
            "#17180b",
            "#050602",
            "#4c4810",
            "#0f1007",
            "#24240d",
            "#202013",
            "#f8f6e9",
            "#f8f6e9",
            "#f1e54e",
            "#91bd7f",
            "#f1e54e")
    ];
    private const int MaxSetupPresets = 3;
    private const int MaxUndoSnapshots = 100;

    private sealed record ContrastPalette(
        string Id,
        string Name,
        string Page,
        string Panel,
        string Input,
        string Header,
        string Menu,
        string Button,
        string Disabled,
        string Text,
        string MenuText,
        string Muted,
        string Success,
        string Warning);

    public MainWindow()
    {
        _store = new JsonStore(_paths);
        _receiptExporter = new ReceiptExporter(_paths);
        _settings = _store.LoadSettings();
        ApplyContrastMode(_settings.ContrastMode);

        Title = "Poker Ledger";
        Icon = new WindowIcon("Assets/PokerLedger.ico");
        Width = 1180;
        Height = 760;
        MinWidth = 960;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = PageBrush;
        FontFamily = UiFont;
        FontSize = 15;
        UseLayoutRounding = true;
        InstallThemeResources();
        InstallControlStyles();

        _root.RowDefinitions = new RowDefinitions("Auto,*");
        _mainMenu = BuildMenu();
        _root.Children.Add(_mainMenu);
        Grid.SetRow(_content, 1);
        _root.Children.Add(_content);
        Content = _root;

        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => RefreshHeader();
        _startNoticeTimer.Interval = TimeSpan.FromSeconds(10);
        _startNoticeTimer.Tick += (_, _) =>
        {
            _startNoticeTimer.Stop();
            _startNotice = null;
            if (_currentScreen == "start")
            {
                ShowStartScreen();
            }
        };
        AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        Closing += OnWindowClosing;
        foreach (var textBlock in new[] { _header, _elapsed, _selectedTotals, _summaryTotals, _summaryBuyIns, _summaryCashOuts, _balanceLine, _cashOutPreview, _finishStatus })
        {
            textBlock.Foreground = TextBrush;
            textBlock.FontFamily = UiFont;
        }
        StyleListBox(_archiveList);
        StyleListBox(_playerList);
        StyleListBox(_transactionList);
        StyleListBox(_nameSuggestions);
        _archiveList.SelectionMode = SelectionMode.Multiple;
        _archiveList.ContextMenu = new ContextMenu
        {
            Background = PanelBrush,
            Foreground = TextBrush,
            FontFamily = UiFont,
            ItemsSource = new[]
            {
                MenuItem("Load Selected", async (_, _) => await LoadSelectedArchive()),
                MenuItem("Open Ledger", (_, _) => ShowLedgerScreen()),
                MenuItem("Export Session JSON", async (_, _) => await ExportSelectedArchiveJson()),
                MenuItem("Delete Selected Sessions", async (_, _) => await DeleteSelectedArchive())
            }
        };
        _archiveList.ItemTemplate = ArchiveItemTemplate();
        _archiveList.SelectionChanged += (_, _) =>
        {
            if (!_selectingArchiveRange && _archiveList.SelectedItem is SessionArchiveItem item)
            {
                _archiveSelectionAnchorPath = item.Path;
            }
            RefreshMenuState();
        };
        _playerList.SelectionChanged += (_, _) =>
        {
            if (_refreshingPlayerList)
            {
                return;
            }
            _selectedPlayer = (_playerList.SelectedItem as PlayerTableItem)?.Player;
            RefreshSelectedPlayerPanel();
            RefreshMenuState();
        };
        AttachNumericFilter(_newPlayerBuyIn);
        _newPlayerName.TextChanged += (_, _) => RefreshNameSuggestions();
        _newPlayerName.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Tab && !string.IsNullOrWhiteSpace(_pendingNameSuggestion))
            {
                e.Handled = true;
                AcceptNameSuggestion();
            }
        };

        ResetSetupRows();
        ShowStartScreen();
    }

    private Menu BuildMenu()
    {
        _menuStateRules.Clear();
        var menu = new Menu
        {
            Background = MenuBrush,
            Foreground = MenuTextBrush,
            FontFamily = MenuFont,
            FontSize = 13,
            Padding = new Avalonia.Thickness(8, 1)
        };
        menu.ItemsSource = new[]
        {
            MenuItem("File",
                MenuItem("Save\tCtrl+S", (_, _) => SaveSession(), () => _session is not null),
                MenuItem("Exit\tCtrl+W", async (_, _) => await ExitSessionOrClose()),
                MenuItem("Import Session JSON\tCtrl+Shift+I", async (_, _) => await ImportSessionJson()),
                MenuItem("Export Session JSON\tCtrl+Shift+E", async (_, _) => await ExportSessionJson(), () => _session is not null),
                MenuItem("Export App Data", async (_, _) => await ExportAppData()),
                MenuItem("Import App Data", async (_, _) => await ImportAppData()),
                MenuItem("Purge Archive", async (_, _) => await PurgeArchive(), () => _currentScreen == "start"),
                MenuItem("Purge Receipts", async (_, _) => await PurgeReceipts(), () => _currentScreen == "start")),
            MenuItem("Session",
                MenuItem("Edit Session Name", async (_, _) => await EditSessionName(), () => _session is not null),
                MenuItem("Edit Stakes / Blinds", async (_, _) => await EditSessionBlinds(), () => _session is not null),
                MenuItem("Edit Chip Denominations", async (_, _) => await EditChipDenominations(), () => _session is not null),
                MenuItem("Cash Out / Back\tF8", (_, _) => ToggleCashOutScreen(), () => _session is not null),
                MenuItem("Conclude Session\tCtrl+Shift+S", async (_, _) => await ConcludeSession(), () => _session is not null)),
            MenuItem("Players",
                MenuItem("Add Player\tInsert", (_, _) => AddPlayer(), () => _currentScreen == "players" && _session is not null),
                MenuItem("Remove Player\tDelete", async (_, _) => await RemoveSelectedPlayer(), () => _currentScreen == "players" && _selectedPlayer is not null),
                MenuItem("Cash Out Selected Player", async (_, _) => await CashOutSelectedPlayer(), () => _currentScreen == "players" && _selectedPlayer is not null),
                MenuItem("Add Rebuy", (_, _) => AddRebuy(), () => _currentScreen == "players" && _selectedPlayer is not null),
                MenuItem("Manage Saved Player Names", async (_, _) => await ManageSavedPlayerNames())),
            MenuItem("Edit",
                MenuItem("Undo\tCtrl+Z", (_, _) => UndoLastAction(), () => _session is not null && _undoStack.Count > 0),
                MenuItem("Redo\tCtrl+Y", (_, _) => RedoLastAction(), () => _session is not null && _redoStack.Count > 0),
                MenuItem("Delete Selected Ledger Entry\tCtrl+Delete", async (_, _) => await DeleteSelectedLedgerEntry(false), () => _currentScreen == "players" && _transactionList.SelectedItem is TransactionTableItem),
                MenuItem("Manage Setup Presets", async (_, _) => await ManageSetupPresets(), () => _currentScreen == "start"),
                MenuItem("Reset Current Screen Fields", (_, _) => ResetCurrentScreenFields(), () => _currentScreen is "start" or "players" or "cashout")),
            MenuItem("Appearance", ContrastPalettes.Select(PaletteMenuItem).ToArray()),
            MenuItem("Tools",
                MenuItem("Balance Check\tF7", async (_, _) => await ShowBalanceCheck(), () => _session is not null),
                MenuItem("Open Data Folder", (_, _) => OpenFolder(_paths.DataDirectory)),
                MenuItem("Open Receipts Folder", (_, _) => OpenFolder(_paths.ReceiptsDirectory)),
                MenuItem("Reset to Defaults", async (_, _) => await ResetToDefaults(), () => _currentScreen == "start"),
                MenuItem("Borderless Fullscreen\tF11", (_, _) => ToggleFullscreen())),
            MenuItem("Help",
                MenuItem("Keyboard Shortcuts\tF1", async (_, _) => await ShowKeyboardShortcuts()),
                MenuItem("Data Storage Location", async (_, _) => await ShowDataStorageLocation()))
        };
        RefreshMenuState();
        return menu;
    }

    private static MenuItem MenuItem(string header, params MenuItem[] children)
    {
        var item = new MenuItem
        {
            Header = MenuHeader(header),
            ItemsSource = children,
            Background = MenuBrush,
            Foreground = MenuTextBrush,
            FontFamily = MenuFont,
            FontWeight = FontWeight.Normal,
            FontSize = 13,
            MinHeight = 28,
            Padding = new Avalonia.Thickness(10, 4)
        };
        ApplyMenuItemTheme(item, true, topLevel: true);
        return item;
    }

    private MenuItem PaletteMenuItem(ContrastPalette palette)
    {
        var item = MenuItem(PalettePreview(palette, IsCurrentPalette(palette)), (_, _) => ChangeContrastMode(palette.Id));
        item.MinHeight = 88;
        item.Padding = new Avalonia.Thickness(8, 6);
        ToolTip.SetTip(item, palette.Name);
        return item;
    }

    private MenuItem MenuItem(string header, EventHandler<Avalonia.Interactivity.RoutedEventArgs> click, Func<bool>? enabledWhen = null)
    {
        return MenuItem(MenuHeader(header), click, enabledWhen);
    }

    private MenuItem MenuItem(Control header, EventHandler<Avalonia.Interactivity.RoutedEventArgs> click, Func<bool>? enabledWhen = null)
    {
        var item = new MenuItem
        {
            Header = header,
            Background = PanelBrush,
            Foreground = MenuTextBrush,
            FontFamily = MenuFont,
            FontSize = 13,
            MinHeight = 26,
            Padding = new Avalonia.Thickness(6, 3)
        };
        item.Click += click;
        if (enabledWhen is not null)
        {
            _menuStateRules.Add((item, enabledWhen));
            item.IsEnabled = enabledWhen();
        }
        ApplyMenuItemTheme(item, item.IsEnabled);
        return item;
    }

    private void RefreshMenuState()
    {
        foreach (var (item, enabled) in _menuStateRules)
        {
            var isEnabled = enabled();
            item.IsEnabled = isEnabled;
            ApplyMenuItemTheme(item, isEnabled);
        }
    }

    private void ChangeContrastMode(string mode)
    {
        var palette = ApplyContrastMode(mode);
        _settings.ContrastMode = palette.Id;
        _store.SaveSettings(_settings);
        InstallThemeResources();
        Background = PageBrush;
        RebuildMenu();
        RebuildCurrentScreen();
    }

    private void RebuildMenu()
    {
        if (_mainMenu is not null)
        {
            _root.Children.Remove(_mainMenu);
        }

        _mainMenu = BuildMenu();
        Grid.SetRow(_mainMenu, 0);
        _root.Children.Add(_mainMenu);
    }

    private void RebuildCurrentScreen()
    {
        switch (_currentScreen)
        {
            case "players":
                ShowPlayerScreen();
                break;
            case "cashout":
                ShowCashOutScreen();
                break;
            case "ledger":
                ShowLedgerScreen();
                break;
            default:
                ShowStartScreen();
                break;
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var isTyping = e.Source is TextBox;

        if (e.Key == Key.F1)
        {
            e.Handled = true;
            await ShowKeyboardShortcuts();
        }
        else if (e.Key == Key.F11)
        {
            e.Handled = true;
            ToggleFullscreen();
        }
        else if (ctrl && e.Key == Key.S && shift)
        {
            e.Handled = true;
            await ConcludeSession();
        }
        else if (ctrl && e.Key == Key.S)
        {
            e.Handled = true;
            SaveSession();
        }
        else if (ctrl && e.Key == Key.W)
        {
            e.Handled = true;
            await ExitSessionOrClose();
        }
        else if (ctrl && shift && e.Key == Key.I)
        {
            e.Handled = true;
            await ImportSessionJson();
        }
        else if (ctrl && shift && e.Key == Key.E)
        {
            e.Handled = true;
            await ExportSessionJson();
        }
        else if (ctrl && e.Key == Key.O)
        {
            e.Handled = true;
            ShowLedgerScreen();
        }
        else if (ctrl && e.Key == Key.Z)
        {
            e.Handled = true;
            UndoLastAction();
        }
        else if (ctrl && e.Key == Key.Y)
        {
            e.Handled = true;
            RedoLastAction();
        }
        else if (ctrl && e.Key == Key.Delete)
        {
            e.Handled = true;
            await DeleteSelectedLedgerEntry(false);
        }
        else if (e.Key == Key.F5)
        {
            e.Handled = true;
            RefreshArchive();
        }
        else if (e.Key == Key.F7)
        {
            e.Handled = true;
            await ShowBalanceCheck();
        }
        else if (e.Key == Key.F8)
        {
            e.Handled = true;
            ToggleCashOutScreen();
        }
        else if (e.Key == Key.Insert && !isTyping)
        {
            e.Handled = true;
            AddPlayer();
        }
        else if (e.Key == Key.Delete && !isTyping)
        {
            e.Handled = true;
            if (_currentScreen == "players" && _transactionList.SelectedItem is TransactionTableItem)
            {
                await DeleteSelectedLedgerEntry(false);
            }
            else if (_currentScreen == "start")
            {
                await DeleteSelectedArchive();
            }
            else
            {
                await RemoveSelectedPlayer();
            }
        }
    }

    private void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowDecorations = Avalonia.Controls.WindowDecorations.Full;
            WindowState = _windowStateBeforeFullscreen == WindowState.FullScreen
                ? WindowState.Normal
                : _windowStateBeforeFullscreen;
            return;
        }

        _windowStateBeforeFullscreen = WindowState;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowState = WindowState.FullScreen;
    }

    private void ShowStartScreen()
    {
        _currentScreen = "start";
        _content.Content = null;
        DetachControls(_sessionName, _presetPanel, _blindRows, _denomRows, _archiveList);
        _timer.Stop();
        _selectedPlayer = null;
        _cashOutPlayer = null;
        _session = null;
        _sessionPath = null;
        _isDirty = false;
        _undoStack.Clear();
        _redoStack.Clear();
        ResetSetupRows();
        _sessionName.Text = $"Poker Session {DateTime.Now:yyyy-MM-dd}";
        RefreshPresets();
        RefreshArchive();

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("480,*"),
            RowDefinitions = new RowDefinitions("*"),
            Margin = new Avalonia.Thickness(10),
            ColumnSpacing = 12
        };

        var startStack = new StackPanel { Spacing = 12 };
        startStack.Children.Add(Labeled("Name", _sessionName));
        startStack.Children.Add(_presetPanel);
        startStack.Children.Add(Section("Stakes", new StackPanel { Spacing = 8, Children = { _blindRows, SmallButton("+", (_, _) => AddMoneyRow(_blindRows, _blindBoxes, "Blind")) } }));
        startStack.Children.Add(Section("Chip Denominations", new StackPanel { Spacing = 8, Children = { _denomRows, SmallButton("+", (_, _) => AddMoneyRow(_denomRows, _denomBoxes, "Chip")) } }));
        var startFooter = new StackPanel { Spacing = 8 };
        startFooter.Children.Add(Button("Start Session", async (_, _) => await StartSession(), 150));
        if (!string.IsNullOrWhiteSpace(_startNotice))
        {
            startFooter.Children.Add(new TextBlock { Text = _startNotice, Foreground = SuccessBrush, FontFamily = UiFont, TextWrapping = TextWrapping.Wrap });
            _startNoticeTimer.Stop();
            _startNoticeTimer.Start();
        }
        else
        {
            _startNoticeTimer.Stop();
        }
        var startBody = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 10,
            MinWidth = 0
        };
        startBody.Children.Add(new ScrollViewer
        {
            Content = startStack,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        });
        Grid.SetRow(startFooter, 1);
        startBody.Children.Add(startFooter);
        var startPanel = SectionFill("Start Session", startBody);
        startPanel.MinWidth = 0;
        startPanel.ClipToBounds = true;
        grid.Children.Add(startPanel);

        var archiveGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            MinWidth = 0
        };
        var archivePanel = SectionFill("Session Archive", archiveGrid);
        archivePanel.MinWidth = 0;
        archivePanel.ClipToBounds = true;
        _archiveList.FontFamily = MonoFont;
        _archiveList.MinWidth = 0;
        _archiveList.HorizontalAlignment = HorizontalAlignment.Stretch;
        ScrollViewer.SetHorizontalScrollBarVisibility(_archiveList, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        archiveGrid.Children.Add(_archiveList);

        Button ArchiveButton(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> click, double width)
        {
            var button = Button(text, click, width);
            button.Width = width;
            button.Margin = new Avalonia.Thickness(0, 0, 8, 8);
            return button;
        }

        var archiveButtons = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };
        var loadButton = ArchiveButton("Load Selected", async (_, _) => await LoadSelectedArchive(), 135);
        var refreshButton = ArchiveButton("Refresh", (_, _) => RefreshArchive(), 105);
        var deleteButton = ArchiveButton("Delete Selected", async (_, _) => await DeleteSelectedArchive(), 135);
        var ledgerButton = ArchiveButton("Open Ledger", (_, _) => ShowLedgerScreen(), 120);
        archiveButtons.Children.Add(loadButton);
        archiveButtons.Children.Add(refreshButton);
        archiveButtons.Children.Add(deleteButton);
        archiveButtons.Children.Add(ledgerButton);
        Grid.SetRow(archiveButtons, 1);
        archiveGrid.Children.Add(archiveButtons);
        Grid.SetColumn(archivePanel, 1);
        grid.Children.Add(archivePanel);

        _content.Content = grid;
        RefreshMenuState();
    }

    private void ShowPlayerScreen()
    {
        if (_session is null)
        {
            ShowStartScreen();
            return;
        }

        _currentScreen = "players";
        _content.Content = null;
        DetachControls(_playerList, _newPlayerName, _newPlayerBuyIn, _nameSuggestions, _selectedPlayerBox);
        _timer.Start();
        var page = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Avalonia.Thickness(10),
            RowSpacing = 10
        };
        page.Children.Add(BuildHeader());

        var stack = new StackPanel { Spacing = 10 };
        Grid.SetRow(stack, 1);
        _playerList.FontFamily = MonoFont;
        stack.Children.Add(Section("Players", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                _playerList,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        Label("Player"), Themed(_newPlayerName, 260), Label("Buy-in"), Label("$"), Themed(_newPlayerBuyIn, 90), Button("Add Player", (_, _) => AddPlayer(), 120)
                    }
                }
            }
        }));
        stack.Children.Add(_selectedPlayerBox);
        page.Children.Add(stack);
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        var cashOut = Button("Cash Out", (_, _) => ShowCashOutScreen(), 140);
        cashOut.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(cashOut, 1);
        footer.Children.Add(cashOut);
        Grid.SetRow(footer, 2);
        page.Children.Add(footer);
        _content.Content = page;
        RefreshPlayerLists();
        RefreshSelectedPlayerPanel();
        RefreshMenuState();
    }

    private Control BuildHeader(string? actionText = null, EventHandler<Avalonia.Interactivity.RoutedEventArgs>? action = null)
    {
        var panel = new Grid
        {
            ColumnDefinitions = actionText is null ? new ColumnDefinitions("*,Auto") : new ColumnDefinitions("*,Auto,Auto"),
            Background = PanelBrush,
            Margin = new Avalonia.Thickness(0, 0, 0, 4)
        };
        _header = new TextBlock
        {
            Foreground = TextBrush,
            FontFamily = UiFont,
            Margin = new Avalonia.Thickness(12, 10),
            VerticalAlignment = VerticalAlignment.Center
        };
        _elapsed = new TextBlock
        {
            Foreground = TextBrush,
            FontFamily = UiFont,
            Margin = new Avalonia.Thickness(12, 10),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(_elapsed, 1);
        panel.Children.Add(_header);
        panel.Children.Add(_elapsed);
        if (actionText is not null && action is not null)
        {
            var button = Button(actionText, action, 140);
            button.Margin = new Avalonia.Thickness(8);
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(button, 2);
            panel.Children.Add(button);
        }
        RefreshHeader();
        return Section("Current Session", panel);
    }

    private void RefreshHeader()
    {
        if (_session is null)
        {
            return;
        }
        _header.Text = $"{_session.Name}        Stakes: {_session.Stakes}";
        var elapsed = DateTimeOffset.Now - _session.StartedAt;
        _elapsed.Text = $"Elapsed: {(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private void RefreshSelectedPlayerPanel()
    {
        if (_selectedPlayer is null)
        {
            _selectedPlayerBox.IsVisible = false;
            return;
        }

        _selectedPlayerBox.IsVisible = true;
        _selectedPlayerBox.Child = null;
        var initial = _selectedPlayer.Transactions.FirstOrDefault(t => t.Type == "Buy-In");
        _selectedName = new TextBox { Text = _selectedPlayer.Name };
        _selectedInitial = new TextBox { Text = (initial?.Amount ?? 0).ToString("0.##") };
        _rebuyAmount = new TextBox { Text = _session?.DefaultBuyIn.ToString("0.##") ?? "" };
        _selectedName.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                UpdateSelectedPlayer();
            }
        };
        AttachNumericFilter(_selectedInitial);
        AttachNumericFilter(_rebuyAmount);
        _playerNote = NoteBox("", 42);
        _transactionList = new ListBox { FontFamily = MonoFont };
        _transactionList.SelectionChanged += (_, _) => RefreshMenuState();
        StyleListBox(_transactionList);
        RefreshSelectedTransactionList();
        var selectedHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            Children =
            {
                Label("Selected player:"), Themed(_selectedName, 240),
                Label($"Total in: {Money.Format(PokerCalculator.PlayerTotalIn(_selectedPlayer))}"),
                Label($"Cash out: {Money.Format(PokerCalculator.PlayerCashOutTotal(_selectedPlayer))}"),
                Label($"Net: {Money.Format(PokerCalculator.PlayerCashOutTotal(_selectedPlayer) - PokerCalculator.PlayerTotalIn(_selectedPlayer))}")
            }
        };

        static void AlignActionControl(Control control, double height)
        {
            control.Height = height;
            control.VerticalAlignment = VerticalAlignment.Center;
        }

        Grid MoneyActionRow(string label, TextBox amountBox, string actionText, EventHandler<Avalonia.Interactivity.RoutedEventArgs> action, double actionWidth)
        {
            const double rowHeight = 46;
            var themedAmount = Themed(amountBox, 110);
            AlignActionControl(themedAmount, rowHeight);
            var actionButton = Button(actionText, action, actionWidth);
            AlignActionControl(actionButton, rowHeight);
            return new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("84,Auto,110,16,140,*"),
                ColumnSpacing = 6,
                MinHeight = rowHeight,
                Children =
                {
                    GridCell(Label(label), 0),
                    GridCell(Label("$"), 1),
                    GridCell(themedAmount, 2),
                    GridCell(actionButton, 4)
                }
            };
        }

        var buyInRow = MoneyActionRow("Initial", _selectedInitial, "Update", (_, _) => UpdateSelectedPlayer(), 100);
        var rebuyRow = MoneyActionRow("Rebuy", _rebuyAmount, "Add Rebuy", (_, _) => AddRebuy(), 130);
        var sendNote = Button("Send", (_, _) => AddPlayerNote(), 90);
        sendNote.Height = 42;
        sendNote.VerticalAlignment = VerticalAlignment.Top;
        var notesRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { Label("Notes"), Themed(_playerNote, 560), sendNote }
        };
        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                Button("Remove Player", async (_, _) => await RemoveSelectedPlayer(), 130),
                Button("Delete Entry", async (_, _) => await DeleteSelectedLedgerEntry(false), 110)
            }
        };

        _selectedPlayerBox.Child = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                selectedHeader,
                buyInRow,
                rebuyRow,
                notesRow,
                actionRow,
                _transactionList
            }
        };
        StyleBorder(_selectedPlayerBox, "Selected Player Actions");
    }

    private void ShowCashOutScreen()
    {
        if (_session is null)
        {
            return;
        }
        _currentScreen = "cashout";
        _content.Content = null;
        _timer.Start();
        _summaryTotals = Label("");
        _summaryBuyIns = Label("");
        _summaryCashOuts = Label("");
        _balanceLine = Label("");
        _cashOutPreview = Label("");
        _finishStatus = Label("");
        _finalRake = new TextBox { Text = _session.FinalRake.ToString("0.##") };
        AttachNumericFilter(_finalRake);
        _finalRake.GotFocus += (_, _) => PushUndoSnapshot();
        _finalRake.TextChanged += (_, _) =>
        {
            if (_session is null)
            {
                return;
            }
            _session.FinalRake = ParseMoneyOrZero(_finalRake.Text);
            MarkDirty();
            RefreshCashOutScreenValues();
        };
        _cashOutPlayerList = new ListBox { FontFamily = MonoFont };
        StyleListBox(_cashOutPlayerList);
        _cashOutPlayerList.SelectionChanged += (_, _) =>
        {
            if (_refreshingCashOutList)
            {
                return;
            }
            _cashOutPlayer = (_cashOutPlayerList.SelectedItem as PlayerTableItem)?.Player;
            BuildCashOutChipPanel();
        };
        _cashOutChipPanel = new StackPanel { Spacing = 8 };

        var page = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,430"),
            Margin = new Avalonia.Thickness(10),
            RowSpacing = 10,
            ColumnSpacing = 12
        };
        var header = BuildHeader();
        Grid.SetColumnSpan(header, 2);
        page.Children.Add(header);

        _summaryTotals.Margin = new Avalonia.Thickness(12, 8);
        _balanceLine.Margin = new Avalonia.Thickness(12, 0, 12, 8);
        var totals = Section("Session Summary", new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*,Auto,Auto,110"),
                    ColumnSpacing = 22,
                    Margin = new Avalonia.Thickness(12, 8),
                    Children =
                    {
                        GridCell(_summaryBuyIns, 0),
                        GridCell(_summaryCashOuts, 1),
                        GridCell(Label("Final rake"), 2),
                        GridCell(Label("$"), 3),
                        GridCell(Themed(_finalRake, 90), 4)
                    }
                },
                _balanceLine
            }
        });
        Grid.SetRow(totals, 1);
        Grid.SetColumnSpan(totals, 2);
        page.Children.Add(totals);

        _cashOutPlayerList.FontFamily = MonoFont;
        var players = Section("Cash Out Players", _cashOutPlayerList);
        Grid.SetRow(players, 2);
        page.Children.Add(players);

        var chips = Section("Cash-Out Chips", _cashOutChipPanel);
        Grid.SetRow(chips, 2);
        Grid.SetColumn(chips, 1);
        page.Children.Add(chips);

        var finish = new Border
        {
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 12,
                Children = { Button("Back to Players", (_, _) => ShowPlayerScreen(), 130), Button("Conclude Session", async (_, _) => await ConcludeSession(), 150), _finishStatus }
            }
        };
        StyleBorder(finish, "");
        Grid.SetRow(finish, 3);
        Grid.SetColumnSpan(finish, 2);
        page.Children.Add(finish);

        _content.Content = page;
        RefreshCashOutScreenValues();
        BuildCashOutChipPanel();
        RefreshMenuState();
    }

    private void BuildCashOutChipPanel()
    {
        _cashOutChipPanel.Children.Clear();
        if (_session is null || _cashOutPlayer is null)
        {
            _cashOutChipPanel.Children.Add(Label("Select a player on the left to enter or edit cash-out chips."));
            return;
        }

        PokerCalculator.SyncPlayerCashOutRows(_session, _cashOutPlayer);
        _cashOutChipPanel.Children.Add(Label("Player: " + _cashOutPlayer.Name));
        _cashOutChipPanel.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("150,120,120"),
            Children = { GridCell("Denomination", 0), GridCell("Count", 1), GridCell("Value", 2) }
        });

        foreach (var row in _cashOutPlayer.CashOuts)
        {
            var box = Themed(new TextBox
            {
                Text = row.Count.ToString(),
                TextAlignment = Avalonia.Media.TextAlignment.Right
            }, 90);
            AttachNumericFilter(box, integerOnly: true);
            box.GotFocus += (_, _) => PushUndoSnapshot();
            var value = Label(Money.Format(row.Denomination * row.Count));
            box.TextChanged += (_, _) =>
            {
                var count = ParseIntOrZero(box.Text);
                PokerCalculator.SetCashOutCount(_session, _cashOutPlayer, row.Denomination, count);
                value.Text = Money.Format(row.Denomination * count);
                MarkDirty();
                RefreshCashOutScreenValues();
            };
            box.PointerWheelChanged += (_, e) =>
            {
                PushUndoSnapshot();
                var delta = e.Delta.Y > 0 ? 1 : -1;
                box.Text = Math.Max(0, ParseIntOrZero(box.Text) + delta).ToString();
            };
            _cashOutChipPanel.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("150,120,120"),
                Children =
                {
                    GridCell(Money.Format(row.Denomination), 0),
                    GridCell(box, 1),
                    GridCell(value, 2)
                }
            });
        }

        _cashOutPreview.Margin = new Avalonia.Thickness(0, 8, 0, 0);
        Detach(_cashOutPreview);
        _cashOutChipPanel.Children.Add(_cashOutPreview);
        _cashOutChipPanel.Children.Add(Button("Cash Out Player", (_, _) =>
        {
            var player = _cashOutPlayer;
            if (player is null)
            {
                return;
            }
            PushUndoSnapshot();
            PokerCalculator.AddHistory(_session, "Cash Out", $"{player.Name}: {Money.Format(PokerCalculator.PlayerCashOutTotal(player))}");
            MarkDirty();
            _cashOutPlayer = null;
            _cashOutPlayerList.SelectedItem = null;
            RefreshCashOutScreenValues();
            BuildCashOutChipPanel();
        }, 160));
        RefreshCashOutScreenValues();
    }

    private void RefreshCashOutScreenValues()
    {
        if (_session is null)
        {
            return;
        }
        var selectedId = _cashOutPlayer?.Id;
        var playerItems = _session.Players.Select(ToPlayerTableItem).ToList();
        _refreshingCashOutList = true;
        try
        {
            _cashOutPlayerList.ItemsSource = playerItems;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                var selectedItem = playerItems.FirstOrDefault(item => item.Player.Id == selectedId);
                if (selectedItem is not null)
                {
                    _cashOutPlayerList.SelectedItem = selectedItem;
                }
            }
        }
        finally
        {
            _refreshingCashOutList = false;
        }
        var totalIn = PokerCalculator.SessionTotalIn(_session);
        var totalOut = PokerCalculator.SessionTotalOut(_session);
        var difference = PokerCalculator.ReconciliationDifference(_session);
        _summaryTotals.Text = "";
        _summaryBuyIns.Text = $"Buy-ins: {Money.Format(totalIn)}";
        _summaryCashOuts.Text = $"Player cash-outs: {Money.Format(totalOut)}";
        _balanceLine.Text = difference == 0
            ? "Balanced: buy-ins equal cash-outs plus rake."
            : $"{(difference > 0 ? "Over" : "Short")} by {Money.Format(Math.Abs(difference))}";
        _balanceLine.Foreground = difference == 0 ? SuccessBrush : WarningBrush;
        if (_cashOutPlayer is not null)
        {
            _cashOutPreview.Text = "Total: " + Money.Format(PokerCalculator.PlayerCashOutTotal(_cashOutPlayer));
        }
    }

    private void ShowLedgerScreen()
    {
        var activeSession = _session is not null;
        PokerSession? session = _session;
        var selected = GetSelectedArchiveItems().FirstOrDefault();
        if (session is null && selected is not null)
        {
            session = _store.LoadSession(selected.Path);
        }
        if (session is null)
        {
            return;
        }
        _currentScreen = "ledger";

        var players = new ListBox { ItemsSource = session.Players.Select(ToPlayerTableItem).ToList(), FontFamily = MonoFont };
        players.ItemTemplate = LedgerPlayerItemTemplate();
        StyleListBox(players);
        var receipt = ReadOnlyLedgerBox("");
        var ledger = ReadOnlyLedgerBox(ReceiptExporter.BuildSessionSummary(session));
        players.SelectionChanged += (_, _) =>
        {
            if (players.SelectedItem is PlayerTableItem item)
            {
                receipt.Text = ReceiptExporter.BuildPlayerReceipt(session, item.Player);
            }
        };

        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("430,*"), RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Avalonia.Thickness(10), ColumnSpacing = 12, RowSpacing = 8 };
        page.Children.Add(Button("Back", (_, _) =>
        {
            if (activeSession)
            {
                ShowPlayerScreen();
            }
            else
            {
                ShowStartScreen();
            }
        }, 80));
        var playerSection = SectionFill("Players", players);
        Grid.SetRow(playerSection, 1);
        page.Children.Add(playerSection);
        var right = new Grid { RowDefinitions = new RowDefinitions("*,*"), RowSpacing = 8 };
        right.Children.Add(SectionFill("Ledger", ledger));
        var receiptSection = SectionFill("Player Receipt", receipt);
        Grid.SetRow(receiptSection, 1);
        right.Children.Add(receiptSection);
        Grid.SetColumn(right, 1);
        Grid.SetRow(right, 1);
        page.Children.Add(right);
        _content.Content = page;
        RefreshMenuState();
    }

    private void NewSessionCommand()
    {
        if (_session is not null)
        {
            _ = StartNewSessionAfterPrompt();
            return;
        }
        ShowStartScreen();
    }

    private async Task StartNewSessionAfterPrompt()
    {
        if (await ResolveDirtySessionBeforeReplacing())
        {
            ShowStartScreen();
        }
    }

    private async Task StartSession()
    {
        await TryStartSession(showValidation: true);
    }

    private async Task<bool> TryStartSession(bool showValidation)
    {
        var blinds = _blindBoxes.Select(box => ParseMoneyOrZero(box.Text)).Where(v => v > 0).ToList();
        var denoms = _denomBoxes.Select(box => ParseMoneyOrZero(box.Text)).Where(v => v > 0).Distinct().OrderBy(v => v).ToList();
        if (blinds.Count == 0 || denoms.Count == 0)
        {
            if (showValidation)
            {
                await ShowInfo("Start Session", "Enter at least one blind/ante and one chip denomination before starting the session.");
            }

            return false;
        }

        await OfferSaveSetupPreset(blinds, denoms);
        _session = new PokerSession
        {
            Name = string.IsNullOrWhiteSpace(_sessionName.Text) ? $"Poker Session {DateTime.Now:yyyy-MM-dd}" : _sessionName.Text.Trim(),
            Stakes = string.Join(" / ", blinds.Select(Money.Format)),
            Blinds = blinds,
            BigBlind = blinds.Count >= 2 ? blinds[1] : blinds[0],
            DefaultBuyIn = (blinds.Count >= 2 ? blinds[1] : blinds[0]) * 100,
            ChipDenominations = denoms
        };
        PokerCalculator.AddHistory(_session, "Session Created", _session.Name);
        _newPlayerBuyIn.Text = _session.DefaultBuyIn.ToString("0.##");
        _undoStack.Clear();
        _redoStack.Clear();
        MarkDirty();
        ShowPlayerScreen();
        return true;
    }

    private void ToggleCashOutScreen()
    {
        if (_session is null)
        {
            return;
        }

        if (_currentScreen == "cashout")
        {
            ShowPlayerScreen();
        }
        else
        {
            ShowCashOutScreen();
        }
    }

    private async Task EditSessionName()
    {
        if (_session is null)
        {
            return;
        }

        var name = await PromptText("Edit Session Name", "Session name", _session.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        PushUndoSnapshot();
        _session.Name = name.Trim();
        PokerCalculator.AddHistory(_session, "Session Name Updated", _session.Name);
        MarkDirty();
        RefreshHeader();
    }

    private async Task EditSessionBlinds()
    {
        if (_session is null)
        {
            return;
        }

        var blinds = await PromptMoneyList("Edit Stakes / Blinds", _session.Blinds.Count > 0 ? _session.Blinds : [_session.BigBlind], "Blind");
        if (blinds is null || blinds.Count == 0)
        {
            return;
        }

        var oldDefault = _session.DefaultBuyIn;
        var oldBigBlind = _session.BigBlind;
        PushUndoSnapshot();
        _session.Blinds = blinds;
        _session.BigBlind = blinds.Count >= 2 ? blinds[1] : blinds[0];
        _session.Stakes = string.Join(" / ", blinds.Select(Money.Format));
        if (oldDefault <= 0 || oldDefault == oldBigBlind * 100)
        {
            _session.DefaultBuyIn = _session.BigBlind * 100;
            _newPlayerBuyIn.Text = _session.DefaultBuyIn.ToString("0.##");
        }
        PokerCalculator.AddHistory(_session, "Stakes Updated", _session.Stakes);
        MarkDirty();
        RefreshHeader();
    }

    private async Task EditChipDenominations()
    {
        if (_session is null)
        {
            return;
        }

        var denoms = await PromptMoneyList("Edit Chip Denominations", _session.ChipDenominations, "Chip");
        if (denoms is null || denoms.Count == 0)
        {
            return;
        }

        PushUndoSnapshot();
        _session.ChipDenominations = denoms.Distinct().OrderBy(v => v).ToList();
        PokerCalculator.SyncAllCashOutRows(_session);
        PokerCalculator.AddHistory(_session, "Chip Denominations Updated", string.Join(", ", _session.ChipDenominations.Select(Money.Format)));
        MarkDirty();
        RefreshPlayerLists();
        if (_currentScreen == "cashout")
        {
            BuildCashOutChipPanel();
            RefreshCashOutScreenValues();
        }
    }

    private void AddPlayer()
    {
        if (_session is null)
        {
            return;
        }

        var playerName = GetNewPlayerNameForAdd();
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return;
        }

        var amount = ParseMoneyOrZero(_newPlayerBuyIn.Text);
        PushUndoSnapshot();
        _session.DefaultBuyIn = amount;
        var player = new Player { Name = playerName };
        player.Transactions.Add(new TransactionEntry { Type = "Buy-In", Amount = amount });
        _session.Players.Add(player);
        PokerCalculator.SyncPlayerCashOutRows(_session, player);
        PokerCalculator.AddHistory(_session, "Player Added", $"{player.Name}: {Money.Format(amount)}");
        var names = _store.LoadPlayerNames();
        names.Add(player.Name);
        _store.SavePlayerNames(names);
        _pendingNameSuggestion = null;
        _typedNamePrefix = null;
        _newPlayerName.Text = "";
        MarkDirty();
        RefreshPlayerLists();
        RefreshNameSuggestions();
    }

    private void RefreshNameSuggestions()
    {
        _nameSuggestions.IsVisible = false;
        if (_session is null)
        {
            return;
        }
        if (_applyingNameSuggestion)
        {
            return;
        }

        var text = _newPlayerName.Text ?? "";
        var typed = text.Trim();
        if (string.IsNullOrWhiteSpace(typed))
        {
            _pendingNameSuggestion = null;
            _typedNamePrefix = null;
            return;
        }

        var activeNames = _session.Players
            .Select(player => player.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var suggestion = _store.LoadPlayerNames()
            .Where(name => !activeNames.Contains(name))
            .Where(name => name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .Where(name => !name.Equals(typed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name.Length)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(suggestion))
        {
            _pendingNameSuggestion = null;
            _typedNamePrefix = null;
            return;
        }

        _pendingNameSuggestion = suggestion;
        _typedNamePrefix = typed;
        _applyingNameSuggestion = true;
        try
        {
            _newPlayerName.Text = suggestion;
            _newPlayerName.SelectionStart = typed.Length;
            _newPlayerName.SelectionEnd = suggestion.Length;
        }
        finally
        {
            _applyingNameSuggestion = false;
        }
    }

    private void AcceptNameSuggestion()
    {
        if (string.IsNullOrWhiteSpace(_pendingNameSuggestion))
        {
            return;
        }

        _applyingNameSuggestion = true;
        try
        {
            _newPlayerName.Text = _pendingNameSuggestion;
            _newPlayerName.CaretIndex = _newPlayerName.Text.Length;
            _newPlayerName.SelectionStart = _newPlayerName.Text.Length;
            _newPlayerName.SelectionEnd = _newPlayerName.Text.Length;
        }
        finally
        {
            _applyingNameSuggestion = false;
        }
        _typedNamePrefix = null;
        _pendingNameSuggestion = null;
    }

    private string GetNewPlayerNameForAdd()
    {
        if (!string.IsNullOrWhiteSpace(_pendingNameSuggestion)
            && !string.IsNullOrWhiteSpace(_typedNamePrefix)
            && _newPlayerName.SelectionStart == _typedNamePrefix.Length
            && _newPlayerName.SelectionEnd == (_newPlayerName.Text ?? "").Length)
        {
            return _typedNamePrefix.Trim();
        }

        return (_newPlayerName.Text ?? "").Trim();
    }

    private void UpdateSelectedPlayer()
    {
        if (_session is null || _selectedPlayer is null)
        {
            return;
        }
        PushUndoSnapshot();
        _selectedPlayer.Name = string.IsNullOrWhiteSpace(_selectedName.Text) ? "Unnamed Player" : _selectedName.Text.Trim();
        var initial = _selectedPlayer.Transactions.FirstOrDefault(t => t.Type == "Buy-In");
        if (initial is null)
        {
            initial = new TransactionEntry { Type = "Buy-In" };
            _selectedPlayer.Transactions.Insert(0, initial);
        }
        initial.Amount = ParseMoneyOrZero(_selectedInitial.Text);
        PokerCalculator.AddHistory(_session, "Player Updated", _selectedPlayer.Name);
        MarkDirty();
        RefreshPlayerLists();
        RefreshSelectedPlayerPanel();
    }

    private void RefreshSelectedTransactionList(string? selectedTransactionId = null)
    {
        if (_selectedPlayer is null)
        {
            return;
        }

        var items = _selectedPlayer.Transactions.Select(t => new TransactionTableItem { Transaction = t }).ToList();
        _transactionList.ItemsSource = items;
        if (!string.IsNullOrWhiteSpace(selectedTransactionId))
        {
            var selected = items.FirstOrDefault(item => item.Transaction.Id == selectedTransactionId);
            if (selected is not null)
            {
                _transactionList.SelectedItem = selected;
            }
        }
    }

    private void AddRebuy()
    {
        if (_session is null || _selectedPlayer is null)
        {
            return;
        }
        var amount = ParseMoneyOrZero(_rebuyAmount.Text);
        PushUndoSnapshot();
        _selectedPlayer.Transactions.Add(new TransactionEntry { Type = "Rebuy", Amount = amount });
        PokerCalculator.AddHistory(_session, "Rebuy", $"{_selectedPlayer.Name}: {Money.Format(amount)}");
        MarkDirty();
        RefreshPlayerLists();
        RefreshSelectedPlayerPanel();
    }

    private void AddPlayerNote()
    {
        if (_session is null || _selectedPlayer is null)
        {
            return;
        }

        var note = (_playerNote.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        var entry = new TransactionEntry { Type = "Note", Amount = 0, Note = note };
        PushUndoSnapshot();
        _selectedPlayer.Transactions.Add(entry);
        PokerCalculator.AddHistory(_session, "Player Note", $"{_selectedPlayer.Name}: {note.Replace("\r", " ").Replace("\n", " ")}");
        _playerNote.Text = "";
        MarkDirty();
        RefreshPlayerLists();
        RefreshSelectedPlayerPanel();
    }

    private async Task CashOutSelectedPlayer()
    {
        if (_session is null)
        {
            return;
        }
        if (_selectedPlayer is null)
        {
            await ShowInfo("Cash Out Selected Player", "Select a player before using this command.");
            return;
        }

        _cashOutPlayer = _selectedPlayer;
        ShowCashOutScreen();
    }

    private void PushUndoSnapshot()
    {
        if (_session is null || _restoringHistory)
        {
            return;
        }

        _undoStack.Add(_store.BuildSessionExportJson(_session));
        if (_undoStack.Count > MaxUndoSnapshots)
        {
            _undoStack.RemoveAt(0);
        }
        _redoStack.Clear();
        RefreshMenuState();
    }

    private void UndoLastAction()
    {
        if (_session is null || _undoStack.Count == 0)
        {
            return;
        }

        _redoStack.Add(_store.BuildSessionExportJson(_session));
        var snapshot = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        RestoreSessionSnapshot(snapshot);
    }

    private void RedoLastAction()
    {
        if (_session is null || _redoStack.Count == 0)
        {
            return;
        }

        _undoStack.Add(_store.BuildSessionExportJson(_session));
        var snapshot = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        RestoreSessionSnapshot(snapshot);
    }

    private void RestoreSessionSnapshot(string json)
    {
        if (_session is null)
        {
            return;
        }

        var selectedPlayerId = _selectedPlayer?.Id;
        var cashOutPlayerId = _cashOutPlayer?.Id;
        _restoringHistory = true;
        try
        {
            _session = _store.LoadSessionJson(json);
            _selectedPlayer = !string.IsNullOrWhiteSpace(selectedPlayerId)
                ? _session.Players.FirstOrDefault(player => player.Id == selectedPlayerId)
                : null;
            _cashOutPlayer = !string.IsNullOrWhiteSpace(cashOutPlayerId)
                ? _session.Players.FirstOrDefault(player => player.Id == cashOutPlayerId)
                : null;
        }
        finally
        {
            _restoringHistory = false;
        }

        MarkDirty();
        switch (_currentScreen)
        {
            case "cashout":
                ShowCashOutScreen();
                break;
            case "ledger":
                ShowLedgerScreen();
                break;
            default:
                ShowPlayerScreen();
                break;
        }
    }

    private async Task DeleteSelectedLedgerEntry(bool confirm = true)
    {
        if (_session is null || _selectedPlayer is null || _transactionList.SelectedItem is not TransactionTableItem item)
        {
            return;
        }

        if (confirm && !await Confirm("Delete Ledger Entry", $"Delete {_selectedPlayer.Name}'s {item.Transaction.Type} entry for {Money.Format(item.Transaction.Amount)}?", "Delete"))
        {
            return;
        }

        RemoveTransaction(_selectedPlayer, item.Transaction, "Delete Entry");
    }

    private void RemoveTransaction(Player player, TransactionEntry transaction, string action)
    {
        if (_session is null)
        {
            return;
        }

        PushUndoSnapshot();
        var transactionCount = player.Transactions.Count;
        player.Transactions.Remove(transaction);
        if (transactionCount == 1 && transaction.Type.Equals("Buy-In", StringComparison.OrdinalIgnoreCase))
        {
            _session.Players.Remove(player);
            PokerCalculator.AddHistory(_session, action, $"removed player add: {player.Name}");
            if (_selectedPlayer?.Id == player.Id)
            {
                _selectedPlayer = null;
            }
        }
        else
        {
            PokerCalculator.AddHistory(_session, action, $"{player.Name}: removed {transaction.Type} {Money.Format(transaction.Amount)}");
        }

        MarkDirty();
        RefreshPlayerLists();
        RefreshNameSuggestions();
        RefreshSelectedPlayerPanel();
    }

    private async Task RemoveSelectedPlayer()
    {
        if (_session is null || _selectedPlayer is null)
        {
            return;
        }
        if (!await Confirm("Remove Player", $"Remove {_selectedPlayer.Name} from this session?", "Remove"))
        {
            return;
        }
        PushUndoSnapshot();
        _session.Players.Remove(_selectedPlayer);
        PokerCalculator.AddHistory(_session, "Player Removed", _selectedPlayer.Name);
        _selectedPlayer = null;
        MarkDirty();
        RefreshPlayerLists();
        RefreshNameSuggestions();
        RefreshSelectedPlayerPanel();
    }

    private bool SaveSession()
    {
        if (_session is null)
        {
            return false;
        }
        try
        {
            _sessionPath = _store.SaveSession(_session, _sessionPath);
            _isDirty = false;
            Title = "Poker Ledger";
            RefreshArchive();
            RefreshMenuState();
            return true;
        }
        catch (Exception ex)
        {
            LogException("SaveSession", ex);
            _ = ShowInfo("Save Failed", "Poker Ledger could not save the current session.\n\n" + ex.Message);
            return false;
        }
    }

    private async Task ImportSessionJson()
    {
        if (!await ResolveDirtySessionBeforeReplacing())
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Poker Ledger Session JSON",
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType]
        });
        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            _session = _store.LoadSessionJson(await reader.ReadToEndAsync());
            _sessionPath = null;
            _undoStack.Clear();
            _redoStack.Clear();
            MarkDirty();
            ShowPlayerScreen();
        }
        catch (Exception ex)
        {
            LogException("ImportSessionJson", ex);
            await ShowInfo("Import Session JSON Failed", "Poker Ledger could not import that session file.\n\n" + ex.Message);
        }
    }

    private async Task ExportSessionJson()
    {
        if (_session is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Poker Ledger Session JSON",
            SuggestedFileName = $"{JsonStore.SafeFilePart(_session.Name)}.json",
            FileTypeChoices = [JsonFileType]
        });
        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(_store.BuildSessionExportJson(_session));
        }
        catch (Exception ex)
        {
            LogException("ExportSessionJson", ex);
            await ShowInfo("Export Session JSON Failed", "Poker Ledger could not export the current session.\n\n" + ex.Message);
        }
    }

    private async Task ExportSelectedArchiveJson()
    {
        var item = GetSelectedArchiveItems().FirstOrDefault();
        if (item is null)
        {
            return;
        }

        PokerSession session;
        try
        {
            session = _store.LoadSession(item.Path);
        }
        catch (Exception ex)
        {
            LogException("ExportSelectedArchiveJson.Load", ex);
            await ShowInfo("Export Session JSON Failed", "Poker Ledger could not read the selected archived session.\n\n" + ex.Message);
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Poker Ledger Session JSON",
            SuggestedFileName = $"{JsonStore.SafeFilePart(session.Name)}.json",
            FileTypeChoices = [JsonFileType]
        });
        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(_store.BuildSessionExportJson(session));
        }
        catch (Exception ex)
        {
            LogException("ExportSelectedArchiveJson.Write", ex);
            await ShowInfo("Export Session JSON Failed", "Poker Ledger could not export the selected session.\n\n" + ex.Message);
        }
    }

    private async Task ExportAppData()
    {
        var options = await PromptAppDataExportOptions();
        if (options is null)
        {
            return;
        }
        var (defaults, sessions, receipts) = options.Value;
        if (!defaults && !sessions && !receipts)
        {
            await ShowInfo("Export App Data", "Choose at least one data type to export.");
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Poker Ledger App Data",
            SuggestedFileName = $"poker-ledger-app-data-{DateTime.Now:yyyyMMdd-HHmm}.json",
            FileTypeChoices = [JsonFileType]
        });
        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(_store.BuildAppDataExportJson(defaults, sessions, receipts));
            _startNotice = "App data exported.";
        }
        catch (Exception ex)
        {
            LogException("ExportAppData", ex);
            await ShowInfo("Export App Data Failed", "Poker Ledger could not export app data.\n\n" + ex.Message);
        }
    }

    private async Task ImportAppData()
    {
        if (!await ResolveDirtySessionBeforeReplacing())
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Poker Ledger App Data",
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType]
        });
        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        AppDataBundle bundle;
        try
        {
            bundle = _store.LoadAppDataBundleJson(await reader.ReadToEndAsync());
        }
        catch (Exception ex)
        {
            await ShowInfo("Import App Data", ex.Message);
            return;
        }

        var options = await PromptAppDataImportOptions(bundle);
        if (options is null)
        {
            return;
        }
        var (overwriteDefaults, overwriteSessions, overwriteReceipts) = options.Value;
        try
        {
            _store.ImportAppData(bundle, overwriteDefaults, overwriteSessions, overwriteReceipts);
            _settings = _store.LoadSettings();
            _settings.ContrastMode = ApplyContrastMode(_settings.ContrastMode).Id;
            _session = null;
            _sessionPath = null;
            _isDirty = false;
            _startNotice = "App data imported.";
            ShowStartScreen();
        }
        catch (Exception ex)
        {
            LogException("ImportAppData", ex);
            await ShowInfo("Import App Data Failed", "Poker Ledger could not import app data.\n\n" + ex.Message);
        }
    }

    private async Task ResetToDefaults()
    {
        if (!await Confirm("Reset to Defaults", "Clear setup presets and saved player names? Saved sessions and receipts are not deleted.", "Reset"))
        {
            return;
        }
        _store.ResetDefaults();
        _settings = _store.LoadSettings();
        _settings.ContrastMode = ApplyContrastMode(_settings.ContrastMode).Id;
        _startNotice = "Defaults reset.";
        if (_currentScreen == "start")
        {
            ShowStartScreen();
        }
    }

    private async Task PurgeArchive()
    {
        if (!await Confirm("Purge Archive", "Delete all saved sessions from the archive? Receipts are not deleted.", "Purge"))
        {
            return;
        }
        try
        {
            _store.PurgeArchive();
            _startNotice = "Session archive purged.";
            if (_currentScreen == "start")
            {
                ShowStartScreen();
            }
            else
            {
                RefreshArchive();
            }
        }
        catch (Exception ex)
        {
            LogException("PurgeArchive", ex);
            await ShowInfo("Purge Archive Failed", ex.Message);
        }
    }

    private async Task PurgeReceipts()
    {
        if (!await Confirm("Purge Receipts", "Delete all exported receipt files? Saved session logs stay in the archive.", "Purge"))
        {
            return;
        }
        try
        {
            _store.PurgeReceipts();
            _startNotice = "Receipts purged.";
            if (_currentScreen == "start")
            {
                ShowStartScreen();
            }
        }
        catch (Exception ex)
        {
            LogException("PurgeReceipts", ex);
            await ShowInfo("Purge Receipts Failed", ex.Message);
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LogException("OpenFolder", ex);
            _ = ShowInfo("Open Folder Failed", "Poker Ledger could not open that folder.\n\n" + ex.Message);
        }
    }

    private async Task ConcludeSession()
    {
        if (_session is null)
        {
            return;
        }

        var finalRake = ParseMoneyOrZero(_finalRake.Text);
        var difference = PokerCalculator.SessionTotalIn(_session) - PokerCalculator.SessionTotalOut(_session) - finalRake;
        if (difference != 0)
        {
            var direction = difference > 0
                ? "bank has more than cash-outs plus rake"
                : "cash-outs plus rake are higher than buy-ins";
            var message = $"This session is unbalanced by {Money.Format(Math.Abs(difference))}: {direction}.\n\nConclude anyway?";
            if (!await Confirm("Unbalanced Session", message, "Conclude Anyway"))
            {
                return;
            }
        }

        PushUndoSnapshot();
        _session.FinalRake = finalRake;
        _session.FinalizedAt = DateTimeOffset.Now;
        MarkDirty();
        if (!SaveSession())
        {
            return;
        }

        try
        {
            _lastExportPath = _receiptExporter.Export(_session);
        }
        catch (Exception ex)
        {
            LogException("ConcludeSession.Export", ex);
            await ShowInfo("Receipt Export Failed", "The finalized session was saved, but receipts could not be written.\n\n" + ex.Message);
            return;
        }

        MarkDirty();
        if (!SaveSession())
        {
            await ShowInfo("Session Saved With Warning", "Receipts were written, but Poker Ledger could not save the export history to the session log.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_sessionPath))
        {
            try
            {
                _store.LockSessionFile(_sessionPath);
            }
            catch (Exception ex)
            {
                LogException("ConcludeSession.Lock", ex);
                await ShowInfo("Session Locked With Warning", "Receipts were written, but Poker Ledger could not mark the saved session as read-only.\n\n" + ex.Message);
            }
        }

        _finishStatus.Text = "Receipts written to " + _lastExportPath;
        _startNotice = "Session concluded. Receipts written to " + _lastExportPath;
        ShowStartScreen();
    }

    private async Task LoadSelectedArchive()
    {
        var item = GetSelectedArchiveItems().FirstOrDefault();
        if (item is null)
        {
            await ShowInfo("Load Session", "Select a saved session to load.");
            return;
        }
        await LoadArchiveItemIfEditable(item);
    }

    private async Task<bool> LoadArchiveItemIfEditable(SessionArchiveItem item)
    {
        if (item.IsReadOnly || IsReadOnlySessionFile(item.Path))
        {
            await ShowInfo("Load Session", "This session is finalized and read-only. Use Open Ledger to review it, or export a copy if you need the data elsewhere.");
            return false;
        }

        _session = _store.LoadSession(item.Path);
        _sessionPath = item.Path;
        _isDirty = false;
        _undoStack.Clear();
        _redoStack.Clear();
        Title = "Poker Ledger";
        ShowPlayerScreen();
        return true;
    }

    private static bool IsReadOnlySessionFile(string path)
    {
        return File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly);
    }

    private async Task DeleteSelectedArchive()
    {
        var items = GetSelectedArchiveItems();
        if (items.Count == 0)
        {
            return;
        }

        var message = items.Count == 1
            ? $"Delete saved session \"{items[0].SessionName}\"?"
            : $"Delete {items.Count} saved sessions?";
        var title = items.Count == 1 ? "Delete Saved Session" : "Delete Saved Sessions";
        if (!await Confirm(title, message, "Delete"))
        {
            return;
        }
        foreach (var item in items)
        {
            try
            {
                _store.DeleteSession(item.Path);
            }
            catch (Exception ex)
            {
                LogException("DeleteSelectedArchive", ex);
                await ShowInfo("Delete Saved Session Failed", $"Poker Ledger could not delete \"{item.SessionName}\".\n\n" + ex.Message);
            }
        }
        RefreshArchive();
    }

    private List<SessionArchiveItem> GetSelectedArchiveItems()
    {
        var selected = _archiveList.SelectedItems?
            .OfType<SessionArchiveItem>()
            .DistinctBy(item => item.Path)
            .ToList() ?? [];

        if (selected.Count == 0 && _archiveList.SelectedItem is SessionArchiveItem item)
        {
            selected.Add(item);
        }

        return selected;
    }

    private void SelectArchiveRange(SessionArchiveItem clicked)
    {
        if (_archiveList.ItemsSource is not IEnumerable<SessionArchiveItem> items)
        {
            return;
        }

        var archiveItems = items.ToList();
        var clickedIndex = archiveItems.FindIndex(item => item.Path == clicked.Path);
        if (clickedIndex < 0)
        {
            return;
        }

        var anchorIndex = !string.IsNullOrWhiteSpace(_archiveSelectionAnchorPath)
            ? archiveItems.FindIndex(item => item.Path == _archiveSelectionAnchorPath)
            : -1;
        if (anchorIndex < 0)
        {
            anchorIndex = clickedIndex;
            _archiveSelectionAnchorPath = clicked.Path;
        }

        var start = Math.Min(anchorIndex, clickedIndex);
        var end = Math.Max(anchorIndex, clickedIndex);

        if (_archiveList.SelectedItems is null)
        {
            return;
        }

        _selectingArchiveRange = true;
        try
        {
            _archiveList.SelectedItems.Clear();
            for (var index = start; index <= end; index++)
            {
                _archiveList.SelectedItems.Add(archiveItems[index]);
            }
        }
        finally
        {
            _selectingArchiveRange = false;
        }

        RefreshMenuState();
    }

    private void RefreshArchive()
    {
        _archiveList.ItemsSource = _store.GetArchive();
        RefreshMenuState();
    }

    private async Task ManageSavedPlayerNames()
    {
        var names = _store.LoadPlayerNames();
        var rows = new StackPanel { Spacing = 4 };
        var checks = new Dictionary<CheckBox, string>();
        var selectAll = new CheckBox { Content = "Select all", Foreground = TextBrush, FontFamily = UiFont };
        var dialog = new Window
        {
            Title = "Manage Saved Player Names",
            Width = 520,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PageBrush
        };

        void RebuildRows()
        {
            rows.Children.Clear();
            checks.Clear();
            foreach (var name in names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                var check = new CheckBox { Content = name, Foreground = TextBrush, FontFamily = UiFont };
                checks[check] = name;
                rows.Children.Add(check);
            }
        }

        selectAll.Click += (_, _) =>
        {
            var checkedValue = selectAll.IsChecked == true;
            foreach (var check in checks.Keys)
            {
                check.IsChecked = checkedValue;
            }
        };

        RebuildRows();
        var rename = Button("Rename Selected", async (_, _) =>
        {
            var selected = checks.Where(pair => pair.Key.IsChecked == true).Select(pair => pair.Value).ToList();
            if (selected.Count != 1)
            {
                await ShowInfo("Saved Player Names", "Select exactly one saved name to rename.");
                return;
            }
            var newName = await PromptText("Rename Saved Player", "Player name", selected[0]);
            if (string.IsNullOrWhiteSpace(newName))
            {
                return;
            }
            names.RemoveAll(name => name.Equals(selected[0], StringComparison.OrdinalIgnoreCase));
            names.Add(newName.Trim());
            _store.SavePlayerNames(names);
            names = _store.LoadPlayerNames();
            RebuildRows();
            RefreshNameSuggestions();
        }, 130);
        var delete = Button("Delete Selected", async (_, _) =>
        {
            var selected = checks.Where(pair => pair.Key.IsChecked == true).Select(pair => pair.Value).ToList();
            if (selected.Count == 0)
            {
                return;
            }
            if (!await Confirm("Delete Saved Player Names", $"Delete {selected.Count} saved player name(s)?", "Delete"))
            {
                return;
            }
            names.RemoveAll(name => selected.Contains(name, StringComparer.OrdinalIgnoreCase));
            _store.SavePlayerNames(names);
            names = _store.LoadPlayerNames();
            RebuildRows();
            RefreshNameSuggestions();
        }, 120);

        dialog.Content = Section("Saved Player Names", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                selectAll,
                new ScrollViewer { Content = rows, Height = 300 },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { rename, delete, Button("Close", (_, _) => dialog.Close(), 90) }
                }
            }
        });
        await dialog.ShowDialog(this);
    }

    private void RefreshPlayerLists()
    {
        if (_session is null)
        {
            return;
        }
        var selectedId = _selectedPlayer?.Id;
        var items = _session.Players.Select(ToPlayerTableItem).ToList();
        _refreshingPlayerList = true;
        try
        {
            _playerList.ItemsSource = items;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                var selectedItem = items.FirstOrDefault(item => item.Player.Id == selectedId);
                if (selectedItem is not null)
                {
                    _playerList.SelectedItem = selectedItem;
                }
            }
        }
        finally
        {
            _refreshingPlayerList = false;
        }
    }

    private void ResetCurrentScreenFields()
    {
        switch (_currentScreen)
        {
            case "start":
                ResetSetupRows();
                _sessionName.Text = $"Poker Session {DateTime.Now:yyyy-MM-dd}";
                break;
            case "players":
                _newPlayerName.Text = "";
                _newPlayerBuyIn.Text = _session?.DefaultBuyIn.ToString("0.##") ?? "";
                RefreshNameSuggestions();
                break;
            case "cashout":
                if (_session is not null)
                {
                    PushUndoSnapshot();
                    _finalRake.Text = "0";
                    _session.FinalRake = 0;
                    if (_cashOutPlayer is not null)
                    {
                        foreach (var row in _cashOutPlayer.CashOuts)
                        {
                            row.Count = 0;
                        }
                    }
                    MarkDirty();
                    RefreshCashOutScreenValues();
                    BuildCashOutChipPanel();
                }
                break;
        }
    }

    private PlayerTableItem ToPlayerTableItem(Player player)
    {
        return new PlayerTableItem
        {
            Player = player,
            TotalIn = PokerCalculator.PlayerTotalIn(player),
            CashOut = PokerCalculator.PlayerCashOutTotal(player)
        };
    }

    private void RefreshPresets()
    {
        EnsureSetupPresetSlots();
        _presetPanel.Children.Clear();
        for (var i = 0; i < MaxSetupPresets; i++)
        {
            var slot = i;
            var preset = _settings.SetupPresets[i];
            if (HasSetupPreset(preset))
            {
                var presetButton = Button(string.IsNullOrWhiteSpace(preset.Name) ? $"Preset {i + 1}" : preset.Name, (_, _) => ApplyPreset(preset), 130);
                presetButton.ContextMenu = new ContextMenu
                {
                    Background = PanelBrush,
                    Foreground = TextBrush,
                    FontFamily = UiFont,
                    ItemsSource = new[]
                    {
                        MenuItem("Rename", async (_, _) => await RenameSetupPreset(slot)),
                        MenuItem("Delete", async (_, _) => await DeleteSetupPreset(slot))
                    }
                };
                _presetPanel.Children.Add(presetButton);
            }
            else
            {
                var empty = Button("Empty Preset", (_, _) => { }, 130);
                empty.Foreground = MutedTextBrush;
                empty.Opacity = 0.65;
                empty.IsHitTestVisible = false;
                _presetPanel.Children.Add(empty);
            }
        }
    }

    private void ApplyPreset(SetupPreset preset)
    {
        if (!HasSetupPreset(preset))
        {
            return;
        }
        _blindRows.Children.Clear();
        _blindBoxes.Clear();
        _denomRows.Children.Clear();
        _denomBoxes.Clear();
        foreach (var blind in preset.Blinds)
        {
            AddMoneyRow(_blindRows, _blindBoxes, "Blind", blind.ToString("0.####"));
        }
        foreach (var denom in preset.Denominations)
        {
            AddMoneyRow(_denomRows, _denomBoxes, "Chip", denom.ToString("0.####"));
        }
    }

    private async Task OfferSaveSetupPreset(List<decimal> blinds, List<decimal> denoms)
    {
        EnsureSetupPresetSlots();
        if (_settings.SetupPresets.Any(preset => SetupPresetMatches(preset, blinds, denoms)))
        {
            return;
        }

        var emptySlot = _settings.SetupPresets.FindIndex(preset => !HasSetupPreset(preset));
        if (emptySlot < 0)
        {
            return;
        }

        if (!await Confirm("Save Setup Preset", "Save these blinds and chip denominations as a setup preset?", "Save"))
        {
            return;
        }

        await SaveSetupPreset(emptySlot, blinds, denoms, requireOverwriteConfirm: false);
    }

    private async Task SaveCurrentSetupPreset(int slot)
    {
        var (blinds, denoms) = CurrentSetupValues();
        if (blinds.Count == 0 || denoms.Count == 0)
        {
            await ShowInfo("Setup Preset", "Enter at least one blind/ante and one chip denomination before saving a preset.");
            return;
        }

        await SaveSetupPreset(slot, blinds, denoms, requireOverwriteConfirm: true);
    }

    private async Task SaveSetupPreset(int slot, List<decimal> blinds, List<decimal> denoms, bool requireOverwriteConfirm)
    {
        EnsureSetupPresetSlots();
        var current = _settings.SetupPresets[slot];
        if (requireOverwriteConfirm && HasSetupPreset(current))
        {
            var name = string.IsNullOrWhiteSpace(current.Name) ? $"Preset {slot + 1}" : current.Name;
            if (!await Confirm("Overwrite Setup Preset", $"Replace \"{name}\" with the current setup?", "Replace"))
            {
                return;
            }
        }

        var initialName = string.IsNullOrWhiteSpace(current.Name)
            ? SuggestedSetupPresetName(blinds)
            : current.Name;
        var presetName = await PromptText("Preset name", "", initialName, compact: true);
        if (presetName is null)
        {
            return;
        }

        _settings.SetupPresets[slot] = new SetupPreset
        {
            Name = string.IsNullOrWhiteSpace(presetName) ? $"Preset {slot + 1}" : presetName.Trim(),
            Blinds = blinds.ToList(),
            Denominations = denoms.ToList()
        };
        _store.SaveSettings(_settings);
        RefreshPresets();
    }

    private (List<decimal> blinds, List<decimal> denoms) CurrentSetupValues()
    {
        var blinds = _blindBoxes
            .Select(box => ParseMoneyOrZero(box.Text))
            .Where(v => v > 0)
            .ToList();
        var denoms = _denomBoxes
            .Select(box => ParseMoneyOrZero(box.Text))
            .Where(v => v > 0)
            .Distinct()
            .OrderBy(v => v)
            .ToList();
        return (blinds, denoms);
    }

    private async Task ManageSetupPresets()
    {
        EnsureSetupPresetSlots();
        var rows = new StackPanel { Spacing = 8 };
        var dialog = new Window
        {
            Title = "Manage Setup Presets",
            Width = 780,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PageBrush
        };

        void RebuildRows()
        {
            rows.Children.Clear();
            for (var i = 0; i < MaxSetupPresets; i++)
            {
                var slot = i;
                var preset = _settings.SetupPresets[i];
                var hasPreset = HasSetupPreset(preset);
                var saveButton = Button("Save Current", async (_, _) =>
                {
                    await SaveCurrentSetupPreset(slot);
                    RebuildRows();
                }, 110);
                var applyButton = Button("Apply", (_, _) => ApplyPreset(_settings.SetupPresets[slot]), 80);
                applyButton.IsEnabled = hasPreset;
                var renameButton = Button("Rename", async (_, _) =>
                {
                    await RenameSetupPreset(slot);
                    RebuildRows();
                }, 90);
                renameButton.IsEnabled = hasPreset;
                var deleteButton = Button("Delete", async (_, _) =>
                {
                    await DeleteSetupPreset(slot);
                    RebuildRows();
                }, 80);
                deleteButton.IsEnabled = hasPreset;

                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("80,*,110,80,90,80"),
                    ColumnSpacing = 10,
                    Children =
                    {
                        GridCell(Label($"Preset {i + 1}"), 0),
                        GridCell(new TextBlock
                        {
                            Text = hasPreset ? PresetSummary(preset) : "Empty Preset",
                            Foreground = hasPreset ? TextBrush : MutedTextBrush,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalAlignment = VerticalAlignment.Center
                        }, 1),
                        GridCell(saveButton, 2),
                        GridCell(applyButton, 3),
                        GridCell(renameButton, 4),
                        GridCell(deleteButton, 5)
                    }
                };
                rows.Children.Add(row);
            }
        }

        RebuildRows();
        dialog.Content = Section("Setup Presets", new StackPanel
        {
            Spacing = 12,
            Children =
            {
                rows,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { Button("Close", (_, _) => dialog.Close(), 90) } }
            }
        });
        await dialog.ShowDialog(this);
    }

    private async Task RenameSetupPreset(int slot)
    {
        EnsureSetupPresetSlots();
        if (!HasSetupPreset(_settings.SetupPresets[slot]))
        {
            return;
        }
        var current = _settings.SetupPresets[slot];
        var name = await PromptText("Preset name", "", current.Name, compact: true);
        if (name is null)
        {
            return;
        }
        current.Name = name.Trim();
        _store.SaveSettings(_settings);
        RefreshPresets();
    }

    private async Task DeleteSetupPreset(int slot)
    {
        EnsureSetupPresetSlots();
        if (!HasSetupPreset(_settings.SetupPresets[slot]))
        {
            return;
        }
        if (!await Confirm("Delete Setup Preset", $"Delete \"{_settings.SetupPresets[slot].Name}\"?", "Delete"))
        {
            return;
        }
        _settings.SetupPresets[slot] = new SetupPreset();
        _store.SaveSettings(_settings);
        RefreshPresets();
    }

    private void EnsureSetupPresetSlots()
    {
        while (_settings.SetupPresets.Count < MaxSetupPresets)
        {
            _settings.SetupPresets.Add(new SetupPreset());
        }
        if (_settings.SetupPresets.Count > MaxSetupPresets)
        {
            _settings.SetupPresets.RemoveRange(MaxSetupPresets, _settings.SetupPresets.Count - MaxSetupPresets);
        }
    }

    private static bool HasSetupPreset(SetupPreset preset)
    {
        return preset.Blinds.Any(v => v > 0) && preset.Denominations.Any(v => v > 0);
    }

    private static bool SetupPresetMatches(SetupPreset preset, IReadOnlyList<decimal> blinds, IReadOnlyList<decimal> denoms)
    {
        if (!HasSetupPreset(preset))
        {
            return false;
        }

        return SameMoneySequence(preset.Blinds.Where(v => v > 0).ToList(), blinds)
            && SameMoneySequence(preset.Denominations.Where(v => v > 0).Distinct().OrderBy(v => v).ToList(), denoms);
    }

    private static bool SameMoneySequence(IReadOnlyList<decimal> left, IReadOnlyList<decimal> right)
    {
        return left.Count == right.Count && left.Zip(right).All(pair => pair.First == pair.Second);
    }

    private static string SuggestedSetupPresetName(IEnumerable<decimal> blinds)
    {
        var parts = blinds.Select(Money.Key).ToList();
        return parts.Count == 0 ? "Preset" : string.Join("/", parts);
    }

    private static string PresetSummary(SetupPreset preset)
    {
        return $"Blinds: {string.Join(" / ", preset.Blinds.Select(Money.Format))}    Chips: {string.Join(", ", preset.Denominations.Select(Money.Format))}";
    }

    private void ResetSetupRows()
    {
        _blindRows.Children.Clear();
        _blindBoxes.Clear();
        _denomRows.Children.Clear();
        _denomBoxes.Clear();
        AddMoneyRow(_blindRows, _blindBoxes, "Blind");
        AddMoneyRow(_blindRows, _blindBoxes, "Blind");
        AddMoneyRow(_denomRows, _denomBoxes, "Chip");
        AddMoneyRow(_denomRows, _denomBoxes, "Chip");
    }

    private void AddMoneyRow(StackPanel panel, List<TextBox> boxes, string label, string value = "")
    {
        var box = new TextBox { Text = value };
        AttachNumericFilter(box);
        boxes.Add(box);
        RefreshMoneyRows(panel, boxes, label);
    }

    private static void RefreshMoneyRows(StackPanel panel, List<TextBox> boxes, string label)
    {
        DetachMoneyRows(panel);
        panel.Children.Clear();
        for (var i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            var rowLabel = label.Equals("Chip", StringComparison.OrdinalIgnoreCase)
                ? $"Chip {i + 1}"
                : BlindLabel(i, boxes.Count);

            var removeButton = SmallButton("x", (_, _) => RemoveMoneyRow(panel, boxes, box, label));
            removeButton.VerticalContentAlignment = VerticalAlignment.Center;
            removeButton.HorizontalContentAlignment = HorizontalAlignment.Center;
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("125,110,44"),
                ColumnSpacing = 8,
                Children =
                {
                    GridCell(Label(rowLabel), 0),
                    GridCell(Themed(box, 110), 1),
                    GridCell(removeButton, 2)
                }
            };
            panel.Children.Add(row);
        }
    }

    private static void DetachMoneyRows(StackPanel panel)
    {
        foreach (var child in panel.Children.OfType<Panel>())
        {
            child.Children.Clear();
        }
    }

    private static string BlindLabel(int index, int count)
    {
        if (count == 1)
        {
            return "Blind / Ante";
        }
        return index switch
        {
            0 => "Small blind",
            1 => "Big blind",
            _ => $"Blind {index + 1}"
        };
    }

    private static void RemoveMoneyRow(StackPanel panel, List<TextBox> boxes, TextBox box, string label)
    {
        boxes.Remove(box);
        RefreshMoneyRows(panel, boxes, label);
    }

    private void MarkDirty()
    {
        if (_session is null)
        {
            return;
        }
        _isDirty = true;
        Title = "* Poker Ledger";
        RefreshMenuState();
    }

    private void LogException(string context, Exception ex)
    {
        try
        {
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(_paths.CrashLogPath, line);
        }
        catch
        {
            // Logging must never make the original app problem worse.
        }
    }

    private async Task ExitSessionOrClose()
    {
        if (_session is null)
        {
            _allowWindowClose = true;
            Close();
            return;
        }

        if (!_isDirty)
        {
            ShowStartScreen();
            return;
        }

        var decision = await ShowSaveDiscardCancelDialog("Save the current session before returning to the start screen?");
        switch (decision)
        {
            case "save":
                if (SaveSession())
                {
                    ShowStartScreen();
                }
                break;
            case "discard":
                ShowStartScreen();
                break;
        }
    }

    private async Task<bool> ResolveDirtySessionBeforeReplacing()
    {
        if (_session is null || !_isDirty)
        {
            return true;
        }

        var decision = await ShowSaveDiscardCancelDialog("Save the current session before continuing?");
        switch (decision)
        {
            case "save":
                return SaveSession();
            case "discard":
                return true;
            default:
                return false;
        }
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowWindowClose || _session is null || !_isDirty)
        {
            return;
        }

        e.Cancel = true;
        var decision = await ShowSaveDiscardCancelDialog("Save the current session before closing Poker Ledger?");
        switch (decision)
        {
            case "save":
                if (SaveSession())
                {
                    _allowWindowClose = true;
                    Close();
                }
                break;
            case "discard":
                _allowWindowClose = true;
                Close();
                break;
        }
    }

    private async Task<string?> ShowSaveDiscardCancelDialog(string message)
    {
        var dialog = new Window
        {
            Title = "Poker Ledger",
            Width = 460,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PageBrush
        };
        var save = Button("Save and Exit", (_, _) =>
        {
            dialog.Tag = "save";
            dialog.Close();
        }, 130);
        var discard = Button("Exit Without Saving", (_, _) =>
        {
            dialog.Tag = "discard";
            dialog.Close();
        }, 170);
        var cancel = Button("Cancel", (_, _) =>
        {
            dialog.Tag = "cancel";
            dialog.Close();
        }, 100);
        dialog.Content = Section("Unsaved Session", new StackPanel
        {
            Spacing = 16,
            Children =
            {
                Label(message),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, Children = { save, discard, cancel } }
            }
        });
        await dialog.ShowDialog(this);
        return dialog.Tag as string;
    }

    private async Task<bool> Confirm(string title, string message, string confirmText)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 430,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PageBrush
        };
        dialog.Content = Section(title, new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = message, Foreground = TextBrush, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        Button(confirmText, (_, _) =>
                        {
                            dialog.Tag = true;
                            dialog.Close();
                        }, 100),
                        Button("Cancel", (_, _) =>
                        {
                            dialog.Tag = false;
                            dialog.Close();
                        }, 90)
                    }
                }
            }
        });
        await dialog.ShowDialog(this);
        return dialog.Tag is true;
    }

    private async Task<string?> PromptText(string title, string label, string initial, bool compact = false)
    {
        var input = Themed(new TextBox { Text = initial }, compact ? 300 : 320);
        var dialog = new Window
        {
            Title = title,
            Width = compact ? 420 : 500,
            Height = compact ? 180 : 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PageBrush
        };
        var body = new StackPanel
        {
            Spacing = 12
        };
        if (!string.IsNullOrWhiteSpace(label))
        {
            body.Children.Add(Label(label));
        }
        body.Children.Add(input);
        body.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children =
            {
                Button("OK", (_, _) =>
                {
                    dialog.Tag = input.Text?.Trim();
                    dialog.Close();
                }, 80),
                Button("Cancel", (_, _) =>
                {
                    dialog.Tag = null;
                    dialog.Close();
                }, 90)
            }
        });
        dialog.Content = Section(title, body);
        await dialog.ShowDialog(this);
        return dialog.Tag as string;
    }

    private async Task<List<decimal>?> PromptMoneyList(string title, IEnumerable<decimal> initial, string label)
    {
        var rows = new StackPanel { Spacing = 6 };
        var boxes = new List<TextBox>();
        foreach (var value in initial.Where(v => v > 0))
        {
            AddMoneyRow(rows, boxes, label, value.ToString("0.####"));
        }
        if (boxes.Count == 0)
        {
            AddMoneyRow(rows, boxes, label);
        }

        var dialog = new Window
        {
            Title = title,
            Width = 520,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PageBrush
        };
        dialog.Content = Section(title, new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new ScrollViewer { Content = rows, Height = 260 },
                SmallButton("+", (_, _) => AddMoneyRow(rows, boxes, label)),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        Button("OK", (_, _) =>
                        {
                            dialog.Tag = boxes
                                .Select(box => ParseMoneyOrZero(box.Text))
                                .Where(v => v > 0)
                                .ToList();
                            dialog.Close();
                        }, 80),
                        Button("Cancel", (_, _) =>
                        {
                            dialog.Tag = null;
                            dialog.Close();
                        }, 90)
                    }
                }
            }
        });
        await dialog.ShowDialog(this);
        return dialog.Tag as List<decimal>;
    }

    private async Task<(bool defaults, bool sessions, bool receipts)?> PromptAppDataExportOptions()
    {
        var defaults = new CheckBox { Content = "Defaults and saved player names", Foreground = TextBrush, FontFamily = UiFont, IsChecked = true };
        var sessions = new CheckBox { Content = "Saved session archive", Foreground = TextBrush, FontFamily = UiFont, IsChecked = true };
        var receipts = new CheckBox { Content = "Receipt files", Foreground = TextBrush, FontFamily = UiFont, IsChecked = true };
        var dialog = new Window
        {
            Title = "Export App Data",
            Width = 480,
            Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PageBrush
        };
        dialog.Content = Section("Export App Data", new StackPanel
        {
            Spacing = 12,
            Children =
            {
                defaults,
                sessions,
                receipts,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        Button("Export", (_, _) =>
                        {
                            dialog.Tag = (defaults: defaults.IsChecked == true, sessions: sessions.IsChecked == true, receipts: receipts.IsChecked == true);
                            dialog.Close();
                        }, 90),
                        Button("Cancel", (_, _) => dialog.Close(), 90)
                    }
                }
            }
        });
        await dialog.ShowDialog(this);
        return dialog.Tag is ValueTuple<bool, bool, bool> result ? result : null;
    }

    private async Task<(bool overwriteDefaults, bool overwriteSessions, bool overwriteReceipts)?> PromptAppDataImportOptions(AppDataBundle bundle)
    {
        var defaults = new CheckBox
        {
            Content = "Overwrite defaults and saved names instead of merging names",
            Foreground = TextBrush,
            FontFamily = UiFont,
            IsEnabled = bundle.Settings is not null || bundle.PlayerNames is not null
        };
        var sessions = new CheckBox
        {
            Content = "Overwrite saved session archive instead of inserting sessions",
            Foreground = TextBrush,
            FontFamily = UiFont,
            IsEnabled = bundle.Sessions is { Count: > 0 }
        };
        var receipts = new CheckBox
        {
            Content = "Overwrite receipts folder instead of inserting receipts",
            Foreground = TextBrush,
            FontFamily = UiFont,
            IsEnabled = bundle.Receipts is { Count: > 0 }
        };
        var summary = $"Defaults: {(defaults.IsEnabled ? "included" : "not included")}\nSessions: {bundle.Sessions?.Count ?? 0}\nReceipts: {bundle.Receipts?.Count ?? 0}";
        var dialog = new Window
        {
            Title = "Import App Data",
            Width = 620,
            Height = 330,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PageBrush
        };
        dialog.Content = Section("Import App Data", new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = summary, Foreground = TextBrush, TextWrapping = TextWrapping.Wrap },
                defaults,
                sessions,
                receipts,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        Button("Import", (_, _) =>
                        {
                            dialog.Tag = (overwriteDefaults: defaults.IsChecked == true, overwriteSessions: sessions.IsChecked == true, overwriteReceipts: receipts.IsChecked == true);
                            dialog.Close();
                        }, 90),
                        Button("Cancel", (_, _) => dialog.Close(), 90)
                    }
                }
            }
        });
        await dialog.ShowDialog(this);
        return dialog.Tag is ValueTuple<bool, bool, bool> result ? result : null;
    }

    private async Task ShowInfo(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 520,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PageBrush
        };
        dialog.Content = Section(title, new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = message, Foreground = TextBrush, TextWrapping = TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { Button("OK", (_, _) => dialog.Close(), 80) } }
            }
        });
        await dialog.ShowDialog(this);
    }

    private async Task ShowBalanceCheck()
    {
        if (_session is null)
        {
            await ShowInfo("Balance Check", "No active session is open.");
            return;
        }

        _session.FinalRake = ParseMoneyOrZero(_finalRake.Text);
        var totalIn = PokerCalculator.SessionTotalIn(_session);
        var totalOut = PokerCalculator.SessionTotalOut(_session);
        var difference = PokerCalculator.ReconciliationDifference(_session);
        var status = difference == 0
            ? "Balanced: buy-ins equal cash-outs plus rake."
            : $"{(difference > 0 ? "Over" : "Short")} by {Money.Format(Math.Abs(difference))}.";
        await ShowInfo("Balance Check",
            $"Buy-ins: {Money.Format(totalIn)}\nPlayer cash-outs: {Money.Format(totalOut)}\nFinal rake: {Money.Format(_session.FinalRake)}\n\n{status}");
    }

    private async Task ShowKeyboardShortcuts()
    {
        var rows = new StackPanel { Spacing = 6 };
        foreach (var (action, key) in new[]
        {
            ("Save", "Ctrl+S"),
            ("Exit", "Ctrl+W"),
            ("Undo", "Ctrl+Z"),
            ("Redo", "Ctrl+Y"),
            ("Delete selected ledger entry", "Ctrl+Delete"),
            ("Conclude session", "Ctrl+Shift+S"),
            ("Import session JSON", "Ctrl+Shift+I"),
            ("Export session JSON", "Ctrl+Shift+E"),
            ("Open ledger", "Ctrl+O"),
            ("Keyboard shortcuts", "F1"),
            ("Refresh archive", "F5"),
            ("Balance check", "F7"),
            ("Cash Out / Back to Players", "F8"),
            ("Borderless fullscreen", "F11"),
            ("Add player", "Insert"),
            ("Remove/delete selected", "Delete")
        })
        {
            rows.Children.Add(ShortcutRow(action, key));
        }

        var dialog = new Window
        {
            Title = "Keyboard Shortcuts",
            Width = 620,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PageBrush
        };
        dialog.Content = Section("Keyboard Shortcuts", new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new ScrollViewer { Content = rows, Height = 390 },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { Button("Close", (_, _) => dialog.Close(), 90) }
                }
            }
        });
        await dialog.ShowDialog(this);
    }

    private async Task ShowDataStorageLocation()
    {
        await ShowInfo("Data Storage Location",
            $"App data root:\n{_paths.AppDataRoot}\n\nSaved sessions:\n{_paths.DataDirectory}\n\nReceipts:\n{_paths.ReceiptsDirectory}\n\nSettings:\n{_paths.SettingsPath}\n\nSaved player names:\n{_paths.PlayerProfilesPath}");
    }

    private async Task ShowAbout()
    {
        await ShowInfo("About Poker Ledger", "Poker Ledger\nC# + Avalonia desktop port\nPersonal poker session ledger.");
    }

    private static readonly FilePickerFileType JsonFileType = new("Poker Ledger Session JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };

    private static IBrush CreateGoldFoilBrush()
    {
        return new LinearGradientBrush
        {
            StartPoint = new Avalonia.RelativePoint(0.04, 0.08, Avalonia.RelativeUnit.Relative),
            EndPoint = new Avalonia.RelativePoint(0.96, 0.92, Avalonia.RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#fffbe0"), 0.000),
                new GradientStop(Color.Parse("#fffbe0"), 0.030),
                new GradientStop(Color.Parse("#8d5b12"), 0.031),
                new GradientStop(Color.Parse("#8d5b12"), 0.060),
                new GradientStop(Color.Parse("#f5c84f"), 0.061),
                new GradientStop(Color.Parse("#f5c84f"), 0.115),
                new GradientStop(Color.Parse("#fff3a1"), 0.116),
                new GradientStop(Color.Parse("#fff3a1"), 0.160),
                new GradientStop(Color.Parse("#b67919"), 0.161),
                new GradientStop(Color.Parse("#b67919"), 0.210),
                new GradientStop(Color.Parse("#fff9ca"), 0.211),
                new GradientStop(Color.Parse("#fff9ca"), 0.245),
                new GradientStop(Color.Parse("#d79a24"), 0.246),
                new GradientStop(Color.Parse("#d79a24"), 0.305),
                new GradientStop(Color.Parse("#6f430d"), 0.306),
                new GradientStop(Color.Parse("#6f430d"), 0.345),
                new GradientStop(Color.Parse("#ffe66b"), 0.346),
                new GradientStop(Color.Parse("#ffe66b"), 0.410),
                new GradientStop(Color.Parse("#a86f18"), 0.411),
                new GradientStop(Color.Parse("#a86f18"), 0.455),
                new GradientStop(Color.Parse("#fff6b8"), 0.456),
                new GradientStop(Color.Parse("#fff6b8"), 0.515),
                new GradientStop(Color.Parse("#c4861c"), 0.516),
                new GradientStop(Color.Parse("#c4861c"), 0.575),
                new GradientStop(Color.Parse("#ffef91"), 0.576),
                new GradientStop(Color.Parse("#ffef91"), 0.620),
                new GradientStop(Color.Parse("#7b4b0f"), 0.621),
                new GradientStop(Color.Parse("#7b4b0f"), 0.660),
                new GradientStop(Color.Parse("#e5b43c"), 0.661),
                new GradientStop(Color.Parse("#e5b43c"), 0.720),
                new GradientStop(Color.Parse("#fffbd7"), 0.721),
                new GradientStop(Color.Parse("#fffbd7"), 0.760),
                new GradientStop(Color.Parse("#996215"), 0.761),
                new GradientStop(Color.Parse("#996215"), 0.820),
                new GradientStop(Color.Parse("#f9d45d"), 0.821),
                new GradientStop(Color.Parse("#f9d45d"), 0.885),
                new GradientStop(Color.Parse("#fff4aa"), 0.886),
                new GradientStop(Color.Parse("#fff4aa"), 0.930),
                new GradientStop(Color.Parse("#73460f"), 0.931),
                new GradientStop(Color.Parse("#73460f"), 1.000)
            }
        };
    }

    private static ContrastPalette ApplyContrastMode(string? mode)
    {
        var palette = ContrastPalettes.FirstOrDefault(p => p.Id.Equals(mode, StringComparison.OrdinalIgnoreCase))
            ?? ContrastPalettes[0];

        SetBrush(PageBrush, palette.Page);
        SetBrush(PanelBrush, palette.Panel);
        SetBrush(InputBrush, palette.Input);
        SetBrush(HeaderBrush, palette.Header);
        SetBrush(MenuBrush, palette.Menu);
        SetBrush(ButtonBrush, palette.Button);
        SetBrush(DisabledBrush, palette.Disabled);
        SetBrush(TextBrush, palette.Text);
        SetBrush(MenuTextBrush, palette.MenuText);
        SetBrush(MutedTextBrush, palette.Muted);
        SetBrush(SuccessBrush, palette.Success);
        SetBrush(WarningBrush, palette.Warning);
        return palette;
    }

    private static void SetBrush(SolidColorBrush brush, string color)
    {
        brush.Color = Color.Parse(color);
    }

    private void InstallThemeResources()
    {
        var resourcePairs = new Dictionary<string, object>
        {
            ["SystemAccentColor"] = Color.Parse("#d7c643"),
            ["SystemAccentColorLight1"] = Color.Parse("#fff4aa"),
            ["SystemAccentColorLight2"] = Color.Parse("#fff9ca"),
            ["SystemAccentColorDark1"] = Color.Parse("#b67919"),
            ["TextControlBackground"] = InputBrush,
            ["TextControlBackgroundPointerOver"] = InputBrush,
            ["TextControlBackgroundFocused"] = InputBrush,
            ["TextControlBackgroundDisabled"] = DisabledBrush,
            ["TextControlForeground"] = TextBrush,
            ["TextControlForegroundPointerOver"] = TextBrush,
            ["TextControlForegroundFocused"] = TextBrush,
            ["TextControlForegroundDisabled"] = MutedTextBrush,
            ["TextControlBorderBrush"] = GoldBrush,
            ["TextControlBorderBrushPointerOver"] = GoldBrush,
            ["TextControlBorderBrushFocused"] = GoldBrush,
            ["TextControlBorderBrushDisabled"] = GoldBrush,
            ["TextControlPlaceholderForeground"] = MutedTextBrush,
            ["TextControlSelectionHighlightColor"] = HeaderBrush,
            ["TextControlSelectionForeground"] = TextBrush,
            ["MenuFlyoutPresenterBackground"] = PanelBrush,
            ["MenuFlyoutPresenterBorderBrush"] = GoldBrush,
            ["MenuItemBackground"] = PanelBrush,
            ["MenuItemBackgroundPointerOver"] = HeaderBrush,
            ["MenuItemBackgroundPressed"] = HeaderBrush,
            ["MenuItemBackgroundSelected"] = HeaderBrush,
            ["MenuItemBackgroundDisabled"] = DisabledBrush,
            ["MenuItemForeground"] = MenuTextBrush,
            ["MenuItemForegroundPointerOver"] = MenuTextBrush,
            ["MenuItemForegroundPressed"] = MenuTextBrush,
            ["MenuItemForegroundDisabled"] = MutedTextBrush,
            ["ButtonBackground"] = ButtonBrush,
            ["ButtonBackgroundPointerOver"] = HeaderBrush,
            ["ButtonBackgroundPressed"] = InputBrush,
            ["ButtonBackgroundDisabled"] = DisabledBrush,
            ["ButtonForeground"] = TextBrush,
            ["ButtonForegroundDisabled"] = MutedTextBrush,
            ["ButtonBorderBrush"] = GoldBrush,
            ["ButtonBorderBrushPointerOver"] = GoldBrush,
            ["ButtonBorderBrushPressed"] = GoldBrush,
            ["ButtonBorderBrushDisabled"] = GoldBrush
        };

        foreach (var pair in resourcePairs)
        {
            Resources[pair.Key] = pair.Value;
        }
    }

    private void InstallControlStyles()
    {
        Styles.Add(ThemedTextBoxStyle());
        Styles.Add(ThemedTextBoxStyle(":focus"));
        Styles.Add(ThemedTextBoxStyle(":pointerover"));
        Styles.Add(ThemedMenuItemStyle());
        Styles.Add(ThemedMenuItemStyle(":pointerover"));
        Styles.Add(ThemedMenuItemStyle(":selected"));
        Styles.Add(ThemedMenuItemStyle(":pressed"));
        Styles.Add(ThemedMenuItemStyle(":focus"));
        Styles.Add(ThemedMenuItemStyle(":disabled"));
        Styles.Add(ThemedListBoxItemStyle());
        Styles.Add(ThemedListBoxItemStyle(":pointerover"));
        Styles.Add(ThemedListBoxItemStyle(":selected"));
        Styles.Add(ThemedListBoxItemStyle(":focus"));
    }

    private static Style ThemedTextBoxStyle(string? pseudoClass = null)
    {
        return new Style(selector =>
        {
            var result = selector.OfType<TextBox>().Class("poker-themed-textbox");
            return pseudoClass is null ? result : result.Class(pseudoClass);
        })
        {
            Setters =
            {
                new Setter(TextBox.BackgroundProperty, InputBrush),
                new Setter(TextBox.ForegroundProperty, TextBrush),
                new Setter(TextBox.BorderBrushProperty, GoldBrush),
                new Setter(TextBox.BorderThicknessProperty, new Avalonia.Thickness(2)),
                new Setter(TextBox.CaretBrushProperty, GoldBrush),
                new Setter(TextBox.SelectionBrushProperty, HeaderBrush),
                new Setter(TextBox.SelectionForegroundBrushProperty, TextBrush),
                new Setter(TextBox.FontFamilyProperty, MonoFont)
            }
        };
    }

    private static Style ThemedMenuItemStyle(string? pseudoClass = null)
    {
        var active = pseudoClass is ":pointerover" or ":selected" or ":pressed" or ":focus";
        var disabled = pseudoClass is ":disabled";
        var background = disabled ? DisabledBrush : active ? HeaderBrush : MenuBrush;
        return new Style(selector =>
        {
            var result = selector.OfType<MenuItem>();
            return pseudoClass is null ? result : result.Class(pseudoClass);
        })
        {
            Setters =
            {
                new Setter(Avalonia.Controls.MenuItem.BackgroundProperty, background),
                new Setter(Avalonia.Controls.MenuItem.ForegroundProperty, disabled ? MutedTextBrush : MenuTextBrush),
                new Setter(Avalonia.Controls.MenuItem.FontFamilyProperty, MenuFont),
                new Setter(Avalonia.Controls.MenuItem.FontSizeProperty, 13.0),
                new Setter(Avalonia.Controls.MenuItem.FontWeightProperty, FontWeight.Normal),
                new Setter(Avalonia.Controls.MenuItem.PaddingProperty, new Avalonia.Thickness(6, 3))
            }
        };
    }

    private static Style ThemedListBoxItemStyle(string? pseudoClass = null)
    {
        var active = pseudoClass is ":pointerover" or ":selected" or ":focus";
        return new Style(selector =>
        {
            var result = selector.OfType<ListBoxItem>();
            return pseudoClass is null ? result : result.Class(pseudoClass);
        })
        {
            Setters =
            {
                new Setter(ListBoxItem.BackgroundProperty, active ? HeaderBrush : InputBrush),
                new Setter(ListBoxItem.ForegroundProperty, TextBrush),
                new Setter(ListBoxItem.FontFamilyProperty, MonoFont),
                new Setter(ListBoxItem.PaddingProperty, new Avalonia.Thickness(6, 3)),
                new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)
            }
        };
    }

    private static void ApplyMenuItemTheme(MenuItem item, bool enabled, bool topLevel = false)
    {
        item.Background = enabled
            ? topLevel ? MenuBrush : PanelBrush
            : DisabledBrush;
        item.Foreground = enabled ? MenuTextBrush : MutedTextBrush;
        item.Opacity = 1;

        if (item.Header is Control header)
        {
            ApplyMenuHeaderState(header, enabled);
        }
    }

    private static void ApplyMenuHeaderState(Control control, bool enabled)
    {
        if (control is TextBlock textBlock)
        {
            var isShortcut = string.Equals(textBlock.Tag as string, "shortcut", StringComparison.Ordinal);
            textBlock.Foreground = enabled
                ? isShortcut ? MutedTextBrush : MenuTextBrush
                : MutedTextBrush;
            return;
        }

        if (control is Panel panel)
        {
            foreach (var child in panel.Children.OfType<Control>())
            {
                ApplyMenuHeaderState(child, enabled);
            }
        }
        else if (control is ContentControl contentControl && contentControl.Content is Control child)
        {
            ApplyMenuHeaderState(child, enabled);
        }
    }

    private bool IsCurrentPalette(ContrastPalette palette)
    {
        var currentId = ContrastPalettes.FirstOrDefault(p => p.Id.Equals(_settings.ContrastMode, StringComparison.OrdinalIgnoreCase))?.Id
            ?? ContrastPalettes[0].Id;
        return currentId.Equals(palette.Id, StringComparison.OrdinalIgnoreCase);
    }

    private static Control PalettePreview(ContrastPalette palette, bool selected)
    {
        var preview = new Grid
        {
            RowDefinitions = new RowDefinitions("9,*"),
            RowSpacing = 4
        };

        var menuStrip = new Border
        {
            Background = PreviewBrush(palette.Menu),
            CornerRadius = new Avalonia.CornerRadius(1)
        };
        preview.Children.Add(menuStrip);

        var mockScreen = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("74,*"),
            ColumnSpacing = 5
        };
        Grid.SetRow(mockScreen, 1);

        var setupPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("9,9,9,*"),
            RowSpacing = 4
        };
        setupPanel.Children.Add(PreviewBlock(palette.Header, 0, 0));
        setupPanel.Children.Add(PreviewBlock(palette.Input, 1, 0));
        setupPanel.Children.Add(PreviewBlock(palette.Input, 2, 0));
        setupPanel.Children.Add(PreviewBlock(palette.Button, 3, 0, 26));
        mockScreen.Children.Add(PreviewFramed(palette.Panel, setupPanel, 0));

        var tablePanel = new Grid
        {
            RowDefinitions = new RowDefinitions("10,8,8,8,*"),
            RowSpacing = 3
        };
        tablePanel.Children.Add(PreviewBlock(palette.Header, 0, 0));
        tablePanel.Children.Add(PreviewBlock(palette.Input, 1, 0));
        tablePanel.Children.Add(PreviewBlock(palette.Input, 2, 0));
        tablePanel.Children.Add(PreviewBlock(palette.Input, 3, 0));
        mockScreen.Children.Add(PreviewFramed(palette.Panel, tablePanel, 1));
        preview.Children.Add(mockScreen);

        var outer = new Border
        {
            Width = 238,
            Height = 76,
            Background = PreviewBrush(palette.Page),
            BorderBrush = selected ? PreviewBrush(palette.Warning) : GoldBrush,
            BorderThickness = selected ? new Avalonia.Thickness(3) : new Avalonia.Thickness(2),
            Padding = new Avalonia.Thickness(5),
            CornerRadius = new Avalonia.CornerRadius(2),
            Child = preview
        };

        return outer;
    }

    private static Border PreviewFramed(string background, Control child, int column)
    {
        var border = new Border
        {
            Background = PreviewBrush(background),
            BorderBrush = GoldBrush,
            BorderThickness = new Avalonia.Thickness(1),
            Padding = new Avalonia.Thickness(4),
            Child = child
        };
        Grid.SetColumn(border, column);
        return border;
    }

    private static Border PreviewBlock(string color, int row, int column, double? width = null)
    {
        var border = new Border
        {
            Background = PreviewBrush(color),
            BorderBrush = GoldBrush,
            BorderThickness = new Avalonia.Thickness(0.5),
            HorizontalAlignment = width is null ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            Width = width ?? double.NaN
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        return border;
    }

    private static SolidColorBrush PreviewBrush(string color)
    {
        return new SolidColorBrush(Color.Parse(color));
    }

    private static Control MenuHeader(string text)
    {
        var parts = text.Split('\t', 2);
        if (parts.Length == 1)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = MenuTextBrush,
                FontFamily = MenuFont,
                FontSize = 13,
                FontWeight = FontWeight.Normal
            };
        }

        return new Grid
        {
            Width = 300,
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                GridCell(new TextBlock
                {
                    Text = parts[0],
                    Foreground = MenuTextBrush,
                    FontFamily = MenuFont,
                    FontSize = 13,
                    FontWeight = FontWeight.Normal
                }, 0),
                GridCell(new TextBlock
                {
                    Text = parts[1],
                    Foreground = MutedTextBrush,
                    FontFamily = MonoFont,
                    FontSize = 13,
                    Tag = "shortcut",
                    HorizontalAlignment = HorizontalAlignment.Right
                }, 1)
            }
        };
    }

    private static Border Section(string title, Control child)
    {
        var label = new TextBlock
        {
            Text = title,
            Foreground = TextBrush,
            FontFamily = UiFont,
            FontWeight = FontWeight.SemiBold,
            FontSize = 18,
            Margin = new Avalonia.Thickness(0, 0, 0, 12)
        };
        var stack = new StackPanel { Children = { label, child } };
        var border = new Border { Child = stack };
        StyleBorder(border, title);
        return border;
    }

    private static Border SectionFill(string title, Control child)
    {
        var label = new TextBlock
        {
            Text = title,
            Foreground = TextBrush,
            FontFamily = UiFont,
            FontWeight = FontWeight.SemiBold,
            FontSize = 18,
            Margin = new Avalonia.Thickness(0, 0, 0, 12)
        };
        child.HorizontalAlignment = HorizontalAlignment.Stretch;
        child.VerticalAlignment = VerticalAlignment.Stretch;
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        grid.Children.Add(label);
        Grid.SetRow(child, 1);
        grid.Children.Add(child);
        var border = new Border { Child = grid };
        StyleBorder(border, title);
        return border;
    }

    private static void StyleBorder(Border border, string _)
    {
        border.BorderBrush = GoldBrush;
        border.BorderThickness = new Avalonia.Thickness(3);
        border.Background = PanelBrush;
        border.Padding = new Avalonia.Thickness(18);
        border.Margin = new Avalonia.Thickness(0);
    }

    private static TextBlock Label(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = TextBrush,
            FontFamily = UiFont,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static TextBlock TableText(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = TextBrush,
            FontFamily = MonoFont,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static Control ShortcutRow(string action, string key)
    {
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,170"),
            Children =
            {
                GridCell(new TextBlock
                {
                    Text = action,
                    Foreground = TextBrush,
                    FontFamily = UiFont,
                    VerticalAlignment = VerticalAlignment.Center
                }, 0),
                GridCell(new TextBlock
                {
                    Text = key,
                    Foreground = MutedTextBrush,
                    FontFamily = MonoFont,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                }, 1)
            }
        };
    }

    private static TextBox ReadOnlyLedgerBox(string text)
    {
        var textBox = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = MonoFont,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinWidth = 0,
            MinHeight = 0
        };
        ApplyTextBoxTheme(textBox);
        ScrollViewer.SetHorizontalScrollBarVisibility(textBox, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(textBox, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        return textBox;
    }

    private static TextBox NoteBox(string text, double height)
    {
        return new TextBox
        {
            Text = text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = height,
            VerticalContentAlignment = VerticalAlignment.Top
        };
    }

    private static void AttachNumericFilter(TextBox textBox, bool integerOnly = false)
    {
        var updating = false;
        textBox.GotFocus += (_, _) => Dispatcher.UIThread.Post(textBox.SelectAll);
        textBox.TextChanged += (_, _) =>
        {
            if (updating)
            {
                return;
            }

            var original = textBox.Text ?? "";
            var filtered = FilterNumericText(original, integerOnly);
            if (filtered == original)
            {
                return;
            }

            updating = true;
            var caret = Math.Min(textBox.CaretIndex, filtered.Length);
            textBox.Text = filtered;
            textBox.CaretIndex = caret;
            updating = false;
        };
    }

    private static string FilterNumericText(string text, bool integerOnly)
    {
        var result = new List<char>(text.Length);
        var usedDecimal = false;
        foreach (var ch in text)
        {
            if (char.IsDigit(ch))
            {
                result.Add(ch);
            }
            else if (!integerOnly && ch == '.' && !usedDecimal)
            {
                result.Add(ch);
                usedDecimal = true;
            }
        }
        return new string(result.ToArray());
    }

    private static Control Labeled(string label, Control control)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { Label(label), Themed(control, 260) }
        };
    }

    private static Button Button(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> click, double width = 100)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = width,
            FontFamily = UiFont,
            FontWeight = FontWeight.SemiBold,
            Padding = new Avalonia.Thickness(10, 5),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ApplyButtonTheme(button);
        button.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == "IsEnabled")
            {
                ApplyButtonTheme(button);
            }
        };
        button.Click += click;
        return button;
    }

    private static void ApplyButtonTheme(Button button)
    {
        button.Background = button.IsEnabled ? ButtonBrush : DisabledBrush;
        button.Foreground = button.IsEnabled ? TextBrush : MutedTextBrush;
        button.BorderBrush = GoldBrush;
        button.BorderThickness = new Avalonia.Thickness(3);
        button.FontFamily = UiFont;
        button.FontWeight = FontWeight.SemiBold;
        button.Opacity = 1;
    }

    private static Button SmallButton(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> click)
    {
        var button = Button(text, click, 34);
        if (text.Equals("x", StringComparison.OrdinalIgnoreCase))
        {
            button.Content = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse("M 3,3 L 13,13 M 13,3 L 3,13"),
                Stroke = TextBrush,
                StrokeThickness = 2.6,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                Width = 16,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        button.MinWidth = 34;
        button.Height = 34;
        button.Padding = new Avalonia.Thickness(0);
        button.FontSize = 16;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        return button;
    }

    private static void DetachControls(params Control[] controls)
    {
        foreach (var control in controls)
        {
            Detach(control);
        }
    }

    private static void Detach(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, control):
                contentControl.Content = null;
                break;
            case Border border when ReferenceEquals(border.Child, control):
                border.Child = null;
                break;
        }
    }

    private static T Themed<T>(T control, double width) where T : Control
    {
        control.Width = width;
        if (control is TextBox textBox)
        {
            ApplyTextBoxTheme(textBox);
            if (!textBox.Classes.Contains("poker-themed-textbox"))
            {
                textBox.Classes.Add("poker-themed-textbox");
                textBox.GotFocus += (_, _) => ApplyTextBoxTheme(textBox);
                textBox.TextChanged += (_, _) => ApplyTextBoxTheme(textBox);
            }
        }
        else if (control is Button button)
        {
            ApplyButtonTheme(button);
        }
        return control;
    }

    private static void ApplyTextBoxTheme(TextBox textBox)
    {
        textBox.Resources["TextControlBackground"] = InputBrush;
        textBox.Resources["TextControlBackgroundPointerOver"] = InputBrush;
        textBox.Resources["TextControlBackgroundFocused"] = InputBrush;
        textBox.Resources["TextControlForeground"] = TextBrush;
        textBox.Resources["TextControlForegroundPointerOver"] = TextBrush;
        textBox.Resources["TextControlForegroundFocused"] = TextBrush;
        textBox.Resources["TextControlBorderBrush"] = GoldBrush;
        textBox.Resources["TextControlBorderBrushPointerOver"] = GoldBrush;
        textBox.Resources["TextControlBorderBrushFocused"] = GoldBrush;
        textBox.Resources["TextControlSelectionHighlightColor"] = HeaderBrush;
        textBox.Resources["TextControlSelectionForeground"] = TextBrush;
        textBox.Background = InputBrush;
        textBox.Foreground = TextBrush;
        textBox.BorderBrush = GoldBrush;
        textBox.BorderThickness = new Avalonia.Thickness(2);
        textBox.FontFamily = MonoFont;
        textBox.Padding = textBox.AcceptsReturn
            ? new Avalonia.Thickness(6, 6)
            : new Avalonia.Thickness(8, 0);
        textBox.MinHeight = textBox.AcceptsReturn ? 0 : 38;
        textBox.VerticalContentAlignment = textBox.AcceptsReturn
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
        textBox.CaretBrush = GoldBrush;
        textBox.SelectionBrush = HeaderBrush;
        textBox.SelectionForegroundBrush = TextBrush;
    }

    private static void StyleListBox(ListBox listBox)
    {
        listBox.Background = InputBrush;
        listBox.Foreground = TextBrush;
        listBox.BorderBrush = GoldBrush;
        listBox.BorderThickness = new Avalonia.Thickness(2);
        listBox.FontFamily = MonoFont;
        listBox.Padding = new Avalonia.Thickness(1);
    }

    private static IDataTemplate LedgerPlayerItemTemplate()
    {
        return new FuncDataTemplate<PlayerTableItem>((item, _) =>
        {
            if (item is null)
            {
                return null;
            }

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,82,Auto,82"),
                ColumnSpacing = 8,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            row.Children.Add(GridCell(new TextBlock
            {
                Text = item.Player.Name,
                Foreground = TextBrush,
                FontFamily = MonoFont,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            }, 0));
            row.Children.Add(GridCell(TableText("In:"), 1));
            row.Children.Add(GridCell(new TextBlock
            {
                Text = Money.Format(item.TotalIn),
                Foreground = TextBrush,
                FontFamily = MonoFont,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            }, 2));
            row.Children.Add(GridCell(TableText("Out:"), 3));
            row.Children.Add(GridCell(new TextBlock
            {
                Text = Money.Format(item.CashOut),
                Foreground = TextBrush,
                FontFamily = MonoFont,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            }, 4));
            return row;
        });
    }

    private IDataTemplate ArchiveItemTemplate()
    {
        return new FuncDataTemplate<SessionArchiveItem>((item, _) =>
        {
            if (item is null)
            {
                return null;
            }

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("130,*,105,105"),
                ColumnSpacing = 14,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Brushes.Transparent
            };
            row.PointerPressed += async (_, e) =>
            {
                var point = e.GetCurrentPoint(row);
                if (point.Properties.IsLeftButtonPressed && e.ClickCount == 2)
                {
                    _archiveList.SelectedItem = item;
                    await LoadArchiveItemIfEditable(item);
                    e.Handled = true;
                    return;
                }

                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
                    point.Properties.IsLeftButtonPressed)
                {
                    SelectArchiveRange(item);
                    e.Handled = true;
                }
            };
            row.Children.Add(GridCell(new TextBlock
            {
                Text = $"{item.Modified:MM-dd HH:mm}",
                Foreground = TextBrush,
                FontFamily = MonoFont,
                VerticalAlignment = VerticalAlignment.Center
            }, 0));
            row.Children.Add(GridCell(new TextBlock
            {
                Text = item.SessionName,
                Foreground = TextBrush,
                FontFamily = MonoFont,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            }, 1));
            row.Children.Add(GridCell(new TextBlock
            {
                Text = $"{item.Players} {(item.Players == 1 ? "player" : "players")}",
                Foreground = TextBrush,
                FontFamily = MonoFont,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            }, 2));
            row.Children.Add(GridCell(new TextBlock
            {
                Text = item.IsReadOnly ? "Read only" : "",
                Foreground = MutedTextBrush,
                FontFamily = MonoFont,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            }, 3));
            return row;
        });
    }

    private static Control GridCell(string text, int column)
    {
        return GridCell(Label(text), column);
    }

    private static Control GridCell(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private static decimal ParseMoneyOrZero(string? text)
    {
        return decimal.TryParse(text?.Replace("$", "").Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? Math.Max(0, value)
            : 0;
    }

    private static int ParseIntOrZero(string? text)
    {
        return int.TryParse(text, out var value) ? Math.Max(0, value) : 0;
    }
}
