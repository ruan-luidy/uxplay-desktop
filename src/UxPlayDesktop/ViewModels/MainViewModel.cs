using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using UxPlayDesktop.Models;
using UxPlayDesktop.Services;

namespace UxPlayDesktop.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ICoreProcessManager _processManager;
    private readonly IBonjourService _bonjourService;
    private readonly IAutostartService _autostartService;
    private readonly IUpdateService _updateService;
    private readonly INetworkDiagnosticService _diagnosticService;
    private readonly IWindowManagementService _windowService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly DispatcherTimer _sessionTimer;

    private AppSettings _settings;
    private ServerState _serverState = ServerState.Stopped;
    private string? _activePinCode;
    private string _formattedDuration = "00:00:00";
    private BonjourStatus _bonjourStatus;

    private UpdateInfo? _latestUpdate;
    private bool _isUpdateAvailable;
    private string _updateVersionText = "";
    private bool _isUpdating;
    private int _updateProgress;

    private bool _isRunningDiagnostic;
    private bool _showDiagnosticDialog;
    private NetworkDiagnosticReport? _diagnosticReport;
    private string? _lastScreenshotPath;
    private string? _toastTitle;
    private string? _toastMessage;
    private Tone _toastTone;
    private bool _isToastVisible;
    private CancellationTokenSource? _toastTimer;
    private AppPage _selectedPage = AppPage.Dashboard;
    private IReadOnlyList<DisplayInfo>? _availableDisplays;

    public AppSettings Settings
    {
        get => _settings;
        set => SetField(ref _settings, value);
    }

    public ServerState ServerState
    {
        get => _serverState;
        private set
        {
            if (SetField(ref _serverState, value))
            {
                UpdateDuration();
                OnPropertyChanged(nameof(StatusTitle));
                OnPropertyChanged(nameof(StatusSubtitle));
                OnPropertyChanged(nameof(StatusDescription));
                OnPropertyChanged(nameof(StatusTone));
                OnPropertyChanged(nameof(IsStreaming));
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsServerRunning));
                OnPropertyChanged(nameof(StartStopButtonText));
                OnPropertyChanged(nameof(DisplayClientName));
                OnPropertyChanged(nameof(DisplayClientIp));
                OnPropertyChanged(nameof(SessionActiveText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public AirPlaySession Session => _processManager.CurrentSession;
    public LogsViewModel Logs { get; }
    public ILocalizationService Loc => LocalizationService.Instance;

    public LanguageItem SelectedLanguage
    {
        get
        {
            var code = Settings.Language ?? "auto";
            foreach (var lang in Loc.SupportedLanguages)
            {
                if (lang.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) return lang;
            }
            return Loc.SupportedLanguages[0];
        }
        set
        {
            if (value != null && Settings.Language != value.Code)
            {
                Settings.Language = value.Code;
                Loc.SetLanguage(value.Code);
                if (PresetOptions != null)
                {
                    foreach (var opt in PresetOptions)
                    {
                        opt.Refresh();
                    }
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusTitle));
                OnPropertyChanged(nameof(StatusSubtitle));
                OnPropertyChanged(nameof(StatusDescription));
                OnPropertyChanged(nameof(StartStopButtonText));
                OnPropertyChanged(nameof(PresetDescription));
                OnPropertyChanged(nameof(SelectedPresetOption));
                OnPropertyChanged(nameof(SelectedPreset));
                OnPropertyChanged(nameof(DisplayClientName));
                OnPropertyChanged(nameof(DisplayClientIp));
                OnPropertyChanged(nameof(DisplayTargetFps));
                OnPropertyChanged(nameof(DisplayAudioDelay));
                OnPropertyChanged(nameof(DisplayStep3Desc));
                _settingsService.Save(Settings);
            }
        }
    }

    public bool IsServerRunning => ServerState is ServerState.Listening or ServerState.Connecting or ServerState.Streaming;
    public bool IsStreaming => ServerState == ServerState.Streaming;
    public bool IsBusy => ServerState is ServerState.Starting or ServerState.Connecting or ServerState.Stopping;

    public AppPage SelectedPage
    {
        get => _selectedPage;
        set => SetField(ref _selectedPage, value);
    }

    public string StartStopButtonText => IsServerRunning ? Loc["StopServer"] : Loc["StartServer"];

    public string StatusTitle => ServerState switch
    {
        ServerState.Stopped => Loc["ServerStopped"],
        ServerState.Starting => Loc["StartingEngine"],
        ServerState.Listening => Loc["ReadyToConnect"],
        ServerState.Connecting => Loc["StartingEngine"],
        ServerState.Streaming => string.Format(Loc["StreamingActiveDesc"], Session.ClientName ?? "Apple Device"),
        ServerState.Stopping => Loc["StoppingServer"],
        ServerState.Error => Loc["ServerError"],
        _ => "Idle"
    };

    public string StatusSubtitle => ServerState switch
    {
        ServerState.Stopped => Loc["ServerStoppedDesc"],
        ServerState.Starting => Loc["StartingEngineDesc"],
        ServerState.Listening => Loc["ReadyToConnectSummary"],
        ServerState.Connecting => Loc["StartingEngineDesc"],
        ServerState.Streaming => $"{Session.ActiveResolution} • {FormattedDuration}",
        ServerState.Stopping => Loc["StoppingServerDesc"],
        ServerState.Error => Loc["ServerErrorDesc"],
        _ => string.Empty
    };

    public string StatusDescription => ServerState switch
    {
        ServerState.Stopped => Loc["ServerStoppedDesc"],
        ServerState.Starting => Loc["StartingEngineDesc"],
        ServerState.Listening => string.Format(Loc["ReadyToConnectDesc"], Settings.ServerName),
        ServerState.Connecting => string.Format(Loc["ConnectingDesc"], Session.ClientIpAddress ?? "AirPlay"),
        ServerState.Streaming => $"{Session.ActiveResolution} @ {Session.ActiveCodec} • {FormattedDuration}",
        ServerState.Stopping => Loc["StoppingServerDesc"],
        ServerState.Error => Loc["ServerErrorDesc"],
        _ => string.Empty
    };

    public string DisplayClientName => string.IsNullOrWhiteSpace(Session.ClientName) ? Loc["NoDevice"] : Session.ClientName;
    public string DisplayClientIp => string.IsNullOrWhiteSpace(Session.ClientIpAddress) ? Loc["WaitingConnection"] : Session.ClientIpAddress;
    public string SessionActiveText => ServerState == ServerState.Streaming ? Loc["LiveSession"] : Loc["WaitingConnection"];
    public string DisplayTargetFps => string.Format(Loc["TargetFpsFormat"], Settings.TargetFps);
    public string DisplayAudioDelay => string.Format(Loc["AudioDelayFormat"], Settings.AudioDelayMs);
    public string DisplayStep3Desc => string.Format(Loc["Step3Desc"], Settings.ServerName);
    public string AppVersion => typeof(MainViewModel).Assembly.GetName().Version is { } v ? $"v{v.Major}.{v.Minor}.{v.Build}" : string.Empty;

    // The about bullets without the leading dot the strings carry, so each can sit on its own row
    public IReadOnlyList<string> AboutHighlights =>
        new[] { "AboutBullet1", "AboutBullet2", "AboutBullet3", "AboutBullet4" }
            .Select(key => Loc[key].TrimStart('•', ' '))
            .ToList();
    public string LocalNetworkInfo => GetLocalNetworkInfo();

    public bool StartOnBoot
    {
        get => Settings.StartOnBoot;
        set
        {
            if (Settings.StartOnBoot != value)
            {
                Settings.StartOnBoot = value;
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    public bool StartMinimized
    {
        get => Settings.StartMinimized;
        set
        {
            if (Settings.StartMinimized != value)
            {
                Settings.StartMinimized = value;
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    public bool IsRecording
    {
        get => Settings.EnableRecording;
        set
        {
            if (Settings.EnableRecording != value)
            {
                Settings.EnableRecording = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecordButtonText));
                SaveSettings();
            }
        }
    }

    public string RecordButtonText => IsRecording ? Loc["StopRecordButton"] : Loc["RecordButton"];

    public double AudioVolume
    {
        get => Settings.AudioVolume;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(Settings.AudioVolume - clamped) > 0.001)
            {
                Settings.AudioVolume = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VolumeDisplay));
                SaveSettings();
            }
        }
    }

    public bool IsMuted
    {
        get => Settings.IsMuted;
        set
        {
            if (Settings.IsMuted != value)
            {
                Settings.IsMuted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VolumeDisplay));
                OnPropertyChanged(nameof(MuteButtonText));
                SaveSettings();
            }
        }
    }

    public string VolumeDisplay => IsMuted ? Loc["MuteButton"] : $"{(int)(AudioVolume * 100)}%";
    public string MuteButtonText => IsMuted ? Loc["UnmuteButton"] : Loc["MuteButton"];

    public bool AlwaysOnTop
    {
        get => Settings.AlwaysOnTop;
        set
        {
            if (Settings.AlwaysOnTop != value)
            {
                Settings.AlwaysOnTop = value;
                OnPropertyChanged();
                _windowService.SetAlwaysOnTop(value);
                SaveSettings();
            }
        }
    }

    public bool EnableGlobalHotkeys
    {
        get => Settings.EnableGlobalHotkeys;
        set
        {
            if (Settings.EnableGlobalHotkeys != value)
            {
                Settings.EnableGlobalHotkeys = value;
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    public string AccentColor
    {
        get => string.IsNullOrWhiteSpace(Settings.AccentColor) ? "#3778BF" : Settings.AccentColor;
        set
        {
            if (Settings.AccentColor != value)
            {
                Settings.AccentColor = value;
                OnPropertyChanged();
                ApplyAccentColor(value);
                SaveSettings();
            }
        }
    }

    public bool IsLightTheme
    {
        get => string.Equals(Settings.Theme, "Light", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (IsLightTheme != value)
            {
                Settings.Theme = value ? "Light" : "Dark";
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDarkTheme));
                ThemeService.Apply(dark: !value);
                SaveSettings();
            }
        }
    }

    public bool IsDarkTheme
    {
        get => !IsLightTheme;
        set => IsLightTheme = !value;
    }

    public IReadOnlyList<string> AvailableAccentColors { get; } =
    [
        "#3778BF", // Blue
        "#38BDF8", // Sky Blue
        "#A855F7", // Neon Purple
        "#22C55E", // Emerald Green
        "#F59E0B", // Amber Gold
        "#F43F5E"  // Rose Red
    ];

    private double _windowWidth = 1020;
    private double _windowHeight = 740;
    private double _windowMinWidth = 860;
    private double _windowMinHeight = 620;

    public bool IsCompactMode
    {
        get => Settings.CompactMode;
        set
        {
            if (Settings.CompactMode != value)
            {
                Settings.CompactMode = value;
                OnPropertyChanged();
                UpdateWindowDimensions(value);
                SaveSettings();
            }
        }
    }

    private void UpdateWindowDimensions(bool compact)
    {
        if (compact)
        {
            WindowMinWidth = 480;
            WindowMinHeight = 120;
            WindowWidth = 620;
            WindowHeight = 140;
        }
        else
        {
            WindowMinWidth = 860;
            WindowMinHeight = 620;
            WindowWidth = 1020;
            WindowHeight = 740;
        }
    }

    public double WindowWidth
    {
        get => _windowWidth;
        set => SetField(ref _windowWidth, value);
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set => SetField(ref _windowHeight, value);
    }

    public double WindowMinWidth
    {
        get => _windowMinWidth;
        set => SetField(ref _windowMinWidth, value);
    }

    public double WindowMinHeight
    {
        get => _windowMinHeight;
        set => SetField(ref _windowMinHeight, value);
    }

    // Enumerated once: a fresh list on every read never contains the selected instance, and the picker shows empty
    public IReadOnlyList<DisplayInfo> AvailableDisplays => _availableDisplays ??= _windowService.GetConnectedDisplays();

    public DisplayInfo? SelectedDisplay
    {
        get => AvailableDisplays.FirstOrDefault(d => d.Index == Settings.TargetDisplayIndex) ?? AvailableDisplays.FirstOrDefault();
        set
        {
            if (value != null && Settings.TargetDisplayIndex != value.Index)
            {
                Settings.TargetDisplayIndex = value.Index;
                OnPropertyChanged();
                _windowService.MoveVideoWindowToDisplay(value.Index);
                SaveSettings();
            }
        }
    }

    public bool IsRunningDiagnostic
    {
        get => _isRunningDiagnostic;
        set => SetField(ref _isRunningDiagnostic, value);
    }

    public bool ShowDiagnosticDialog
    {
        get => _showDiagnosticDialog;
        set => SetField(ref _showDiagnosticDialog, value);
    }

    public NetworkDiagnosticReport? DiagnosticReport
    {
        get => _diagnosticReport;
        set => SetField(ref _diagnosticReport, value);
    }

    public string? LastScreenshotPath
    {
        get => _lastScreenshotPath;
        set => SetField(ref _lastScreenshotPath, value);
    }

    public string? ToastTitle
    {
        get => _toastTitle;
        private set => SetField(ref _toastTitle, value);
    }

    public string? ToastMessage
    {
        get => _toastMessage;
        private set => SetField(ref _toastMessage, value);
    }

    public Tone ToastTone
    {
        get => _toastTone;
        private set => SetField(ref _toastTone, value);
    }

    public bool IsToastVisible
    {
        get => _isToastVisible;
        private set => SetField(ref _isToastVisible, value);
    }

    public Tone StatusTone => ServerState switch
    {
        ServerState.Streaming => Tone.Success,
        ServerState.Listening => Tone.Accent,
        ServerState.Error => Tone.Danger,
        ServerState.Starting or ServerState.Connecting => Tone.Warning,
        _ => Tone.Neutral
    };

    public string FormattedDuration
    {
        get => _formattedDuration;
        private set => SetField(ref _formattedDuration, value);
    }

    public string? ActivePinCode
    {
        get => _activePinCode;
        set => SetField(ref _activePinCode, value);
    }

    public BonjourStatus BonjourStatus
    {
        get => _bonjourStatus;
        private set
        {
            if (SetField(ref _bonjourStatus, value))
            {
                OnPropertyChanged(nameof(BonjourStatusText));
                OnPropertyChanged(nameof(BonjourTone));
            }
        }
    }

    public string BonjourStatusText => BonjourStatus switch
    {
        BonjourStatus.Running => Loc["BonjourStatusActive"],
        BonjourStatus.InstalledNotRunning => Loc["BonjourStatusStopped"],
        _ => Loc["BonjourStatusEmbedded"]
    };

    public Tone BonjourTone => BonjourStatus switch
    {
        BonjourStatus.Running => Tone.Success,
        BonjourStatus.InstalledNotRunning => Tone.Warning,
        _ => Tone.Neutral
    };

    private PresetOption _selectedPresetOption = null!;
    public PresetOption SelectedPresetOption
    {
        get => _selectedPresetOption;
        set
        {
            if (value != null && !ReferenceEquals(_selectedPresetOption, value))
            {
                _selectedPresetOption = value;
                OnPropertyChanged();
                SelectedPreset = value.Preset;
            }
        }
    }

    public PerformancePreset SelectedPreset
    {
        get => Settings.Preset;
        set
        {
            if (Settings.Preset != value)
            {
                Settings.Preset = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PresetDescription));
                _settingsService.Save(Settings);
            }
            if (_selectedPresetOption == null || _selectedPresetOption.Preset != value)
            {
                var match = PresetOptions?.FirstOrDefault(p => p.Preset == value);
                if (match != null)
                {
                    _selectedPresetOption = match;
                    OnPropertyChanged(nameof(SelectedPresetOption));
                }
            }
        }
    }

    public string PresetDescription => Settings.Preset.GetDescription();

    public IReadOnlyList<PresetOption> PresetOptions { get; }

    public IReadOnlyList<PerformancePreset> AvailablePresets { get; } =
    [
        PerformancePreset.TeachingAndPresentation,
        PerformancePreset.LowLatencyGaming,
        PerformancePreset.HighQualityCinema,
        PerformancePreset.AudioOnly,
        PerformancePreset.Custom
    ];

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetField(ref _isUpdateAvailable, value);
    }

    public string UpdateVersionText
    {
        get => _updateVersionText;
        set => SetField(ref _updateVersionText, value);
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        set => SetField(ref _isUpdating, value);
    }

    public int UpdateProgress
    {
        get => _updateProgress;
        set => SetField(ref _updateProgress, value);
    }

    public ICommand ToggleServerCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand FixBonjourCommand { get; }
    public ICommand DismissPinCommand { get; }
    public ICommand ApplyUpdateCommand { get; }
    public ICommand DismissUpdateCommand { get; }

    public ICommand ToggleRecordingCommand { get; }
    public ICommand OpenRecordingsFolderCommand { get; }
    public ICommand CaptureScreenshotCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand ToggleAlwaysOnTopCommand { get; }
    public ICommand ToggleCompactModeCommand { get; }
    public ICommand RunDiagnosticCommand { get; }
    public ICommand CloseDiagnosticCommand { get; }
    public ICommand SetAccentColorCommand { get; }
    public ICommand DismissToastCommand { get; }

    public MainViewModel(
        ISettingsService settingsService,
        ICoreProcessManager processManager,
        IBonjourService bonjourService,
        IAutostartService autostartService,
        IUpdateService? updateService = null,
        INetworkDiagnosticService? diagnosticService = null,
        IWindowManagementService? windowService = null,
        IGlobalHotkeyService? hotkeyService = null)
    {
        _settingsService = settingsService;
        _processManager = processManager;
        _bonjourService = bonjourService;
        _autostartService = autostartService;
        _updateService = updateService ?? new UpdateService();
        _diagnosticService = diagnosticService ?? new NetworkDiagnosticService(_bonjourService);
        _windowService = windowService ?? new WindowManagementService();
        _hotkeyService = hotkeyService ?? new GlobalHotkeyService();

        _settings = _settingsService.CurrentSettings;
        PresetOptions =
        [
            new PresetOption(PerformancePreset.TeachingAndPresentation),
            new PresetOption(PerformancePreset.LowLatencyGaming),
            new PresetOption(PerformancePreset.HighQualityCinema),
            new PresetOption(PerformancePreset.AudioOnly),
            new PresetOption(PerformancePreset.Custom)
        ];
        _selectedPresetOption = PresetOptions.FirstOrDefault(p => p.Preset == _settings.Preset) ?? PresetOptions[0];

        Logs = new LogsViewModel();
        Logs.AddEntry(new LogEntry { Level = LogLevel.Info, Message = "UxPlay Desktop started. Waiting for the AirPlay server or an incoming connection..." });

        ApplyAccentColor(AccentColor);
        UpdateWindowDimensions(_settings.CompactMode);

        Loc.SetLanguage(_settings.Language);
        Loc.PropertyChanged += (_, _) =>
        {
            foreach (var opt in PresetOptions)
            {
                opt.Refresh();
            }
            OnPropertyChanged(nameof(StatusTitle));
            OnPropertyChanged(nameof(StatusSubtitle));
            OnPropertyChanged(nameof(StatusDescription));
            OnPropertyChanged(nameof(StartStopButtonText));
            OnPropertyChanged(nameof(PresetDescription));
            OnPropertyChanged(nameof(SelectedPresetOption));
            OnPropertyChanged(nameof(SelectedPreset));
            OnPropertyChanged(nameof(DisplayClientName));
            OnPropertyChanged(nameof(DisplayClientIp));
            OnPropertyChanged(nameof(DisplayTargetFps));
            OnPropertyChanged(nameof(DisplayAudioDelay));
            OnPropertyChanged(nameof(DisplayStep3Desc));
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(RecordButtonText));
            OnPropertyChanged(nameof(VolumeDisplay));
            OnPropertyChanged(nameof(MuteButtonText));
            OnPropertyChanged(nameof(SessionActiveText));
            OnPropertyChanged(nameof(BonjourStatusText));
            OnPropertyChanged(nameof(AboutHighlights));
        };

        _processManager.StateChanged += state => RunOnUi(() => ServerState = state);
        _processManager.SessionUpdated += _ => RunOnUi(OnSessionUpdated);
        _processManager.LogReceived += entry => RunOnUi(() => Logs.AddEntry(entry));
        _processManager.PinCodePrompt += pin => RunOnUi(() => ActivePinCode = pin);

        ToggleServerCommand = new RelayCommand(async () => await ToggleServerAsync());
        SaveSettingsCommand = new RelayCommand(() =>
        {
            SaveSettings();
            ShowToast(Loc["SettingsSaved"]);
        });
        FixBonjourCommand = new RelayCommand(FixBonjour);
        DismissPinCommand = new RelayCommand(() => ActivePinCode = null);
        ApplyUpdateCommand = new RelayCommand(async () => await ApplyUpdateAsync());
        DismissUpdateCommand = new RelayCommand(() => IsUpdateAvailable = false);

        ToggleRecordingCommand = new RelayCommand(async () => await ToggleRecordingAsync());
        OpenRecordingsFolderCommand = new RelayCommand(OpenRecordingsFolder);
        CaptureScreenshotCommand = new RelayCommand(CaptureScreenshot);
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        ToggleAlwaysOnTopCommand = new RelayCommand(ToggleAlwaysOnTop);
        ToggleCompactModeCommand = new RelayCommand(ToggleCompactMode);
        RunDiagnosticCommand = new RelayCommand(async () => await RunDiagnosticsAsync());
        CloseDiagnosticCommand = new RelayCommand(() => ShowDiagnosticDialog = false);
        SetAccentColorCommand = new RelayCommand(color => { if (color is string hex) AccentColor = hex; });
        DismissToastCommand = new RelayCommand(() => IsToastVisible = false);

        // Global hotkey events
        _hotkeyService.OnToggleFullscreen += () => RunOnUi(() =>
        {
            Settings.ForceFullscreen = !Settings.ForceFullscreen;
            OnPropertyChanged(nameof(Settings));
            SaveSettings();
        });
        _hotkeyService.OnToggleMute += () => RunOnUi(ToggleMute);
        _hotkeyService.OnToggleRecording += () => RunOnUi(async () => await ToggleRecordingAsync());
        _hotkeyService.OnToggleServer += () => RunOnUi(async () => await ToggleServerAsync());
        _hotkeyService.OnToggleAlwaysOnTop += () => RunOnUi(ToggleAlwaysOnTop);
        _hotkeyService.OnCaptureScreenshot += () => RunOnUi(CaptureScreenshot);

        _sessionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _sessionTimer.Tick += (_, _) => UpdateDuration();
        _sessionTimer.Start();

        CheckBonjourStatus();

        if (Settings.AutoStartServer)
        {
            _ = StartServerAsync();
        }

        _ = CheckForUpdatesAsync();
    }

    private async Task ApplyUpdateAsync()
    {
        if (IsUpdating) return;
        if (_latestUpdate == null || string.IsNullOrEmpty(_latestUpdate.DownloadUrl)) return;
        IsUpdating = true;
        UpdateProgress = 0;
        bool ok = await _updateService.DownloadAndApplyUpdateAsync(_latestUpdate.DownloadUrl, p => RunOnUi(() => UpdateProgress = p));
        if (!ok)
        {
            IsUpdating = false;
            var details = string.IsNullOrEmpty(_updateService.LastError) ? string.Empty : $" ({_updateService.LastError})";
            ShowToast(Loc["UpdateFailed"], Loc["UpdateFailedDesc"] + details, Tone.Danger, seconds: 10);
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var info = await _updateService.CheckForUpdatesAsync();
            if (info != null && info.IsUpdateAvailable)
            {
                RunOnUi(() =>
                {
                    _latestUpdate = info;
                    UpdateVersionText = info.LatestVersion;
                    IsUpdateAvailable = true;
                });
            }
        }
        catch { }
    }

    private void FixBonjour()
    {
        if (_bonjourService.CheckStatus() == BonjourStatus.Running)
        {
            ShowToast("Bonjour (mDNS)", Loc["BonjourAlreadyRunning"]);
            return;
        }

        if (_bonjourService.RepairOrInstallService() || _bonjourService.TryStartService())
        {
            CheckBonjourStatus();
            ShowToast("Bonjour (mDNS)", Loc["BonjourRepaired"]);
        }
        else
        {
            ShowToast("Bonjour (mDNS)", Loc["BonjourRepairFailed"], Tone.Warning, seconds: 10);
        }
    }

    private async Task ToggleServerAsync()
    {
        if (IsServerRunning)
        {
            await _processManager.StopAsync();
        }
        else
        {
            await StartServerAsync();
        }
    }

    private async Task StartServerAsync()
    {
        _settingsService.Save(Settings);
        await _processManager.StartAsync(Settings);
    }

    private void SaveSettings()
    {
        _settingsService.Save(Settings);
        _autostartService.SetAutostart(Settings.StartOnBoot, Settings.StartMinimized);
        OnPropertyChanged(nameof(DisplayTargetFps));
        OnPropertyChanged(nameof(DisplayAudioDelay));
        OnPropertyChanged(nameof(DisplayStep3Desc));
        OnPropertyChanged(nameof(StatusDescription));
        OnPropertyChanged(nameof(StatusSubtitle));
        OnPropertyChanged(nameof(StartOnBoot));
        OnPropertyChanged(nameof(StartMinimized));
    }

    private static string GetLocalNetworkInfo()
    {
        try
        {
            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                             ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                             !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                             !ni.Description.Contains("Pseudo", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var ni in interfaces)
            {
                var props = ni.GetIPProperties();
                var ipv4 = props.UnicastAddresses
                    .FirstOrDefault(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                                          !System.Net.IPAddress.IsLoopback(ua.Address))?.Address;

                if (ipv4 != null)
                {
                    return $"{ipv4} ({ni.Name})";
                }
            }
        }
        catch { }
        return "127.0.0.1";
    }

    private void CheckBonjourStatus()
    {
        BonjourStatus = _bonjourService.CheckStatus();
    }

    private void OnSessionUpdated()
    {
        UpdateDuration();
        OnPropertyChanged(nameof(Session));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusSubtitle));
        OnPropertyChanged(nameof(StatusDescription));
        OnPropertyChanged(nameof(DisplayClientName));
        OnPropertyChanged(nameof(DisplayClientIp));
        OnPropertyChanged(nameof(SessionActiveText));
    }

    private void UpdateDuration()
    {
        if (Session.ConnectedAt.HasValue && ServerState == ServerState.Streaming)
        {
            var span = DateTime.Now - Session.ConnectedAt.Value;
            FormattedDuration = span.ToString(@"hh\:mm\:ss");
        }
        else
        {
            FormattedDuration = "00:00:00";
        }
    }

    public void InitializeHotkeys(Window window)
    {
        if (Settings.EnableGlobalHotkeys)
        {
            _hotkeyService.Initialize(window);
        }
    }

    public void DisposeHotkeys()
    {
        _hotkeyService.Dispose();
    }

    private async Task ToggleRecordingAsync()
    {
        IsRecording = !IsRecording;
        if (IsServerRunning)
        {
            await _processManager.StopAsync();
            await StartServerAsync();
        }
    }

    private void OpenRecordingsFolder()
    {
        try
        {
            var dir = string.IsNullOrWhiteSpace(Settings.RecordingDirectory)
                ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "UxPlay")
                : Settings.RecordingDirectory;

            System.IO.Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void CaptureScreenshot()
    {
        var path = _windowService.CaptureScreenshot(Settings.ScreenshotsDirectory);
        if (!string.IsNullOrEmpty(path))
        {
            LastScreenshotPath = path;
            ShowToast(Loc["ScreenshotSaved"], path);
        }
    }

    /// <summary>Shows a short notice at the top of the window; a newer one replaces it and restarts the timer.</summary>
    public void ShowToast(string title, string? message = null, Tone tone = Tone.Success, int seconds = 4)
    {
        ToastTitle = title;
        ToastMessage = message;
        ToastTone = tone;
        IsToastVisible = false;
        IsToastVisible = true;

        _toastTimer?.Cancel();
        var timer = _toastTimer = new CancellationTokenSource();
        _ = HideToastAsync(timer.Token, seconds);
    }

    private async Task HideToastAsync(CancellationToken token, int seconds)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        RunOnUi(() => IsToastVisible = false);
    }

    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    private void ToggleAlwaysOnTop()
    {
        AlwaysOnTop = !AlwaysOnTop;
    }

    private void ToggleCompactMode()
    {
        IsCompactMode = !IsCompactMode;
    }

    private async Task RunDiagnosticsAsync()
    {
        ShowDiagnosticDialog = true;
        IsRunningDiagnostic = true;
        DiagnosticReport = await _diagnosticService.RunDiagnosticsAsync();
        IsRunningDiagnostic = false;
    }

    public static void ApplyAccentColor(string hex)
    {
        ThemeService.SetAccent(hex);
    }

    private static void RunOnUi(Action action)
    {
        var app = Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }
}
