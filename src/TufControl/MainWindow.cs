// SPDX-License-Identifier: GPL-3.0-only
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TufControl;

public sealed class MainWindow : Window
{
    private readonly TextBlock _cpu = Ui.Metric();
    private readonly TextBlock _gpu = Ui.Metric();
    private readonly TextBlock _cpuFan = Ui.Metric();
    private readonly TextBlock _gpuFan = Ui.Metric();
    private readonly TextBlock _gpuStatus = Ui.Secondary("GPU: consultando…");
    private readonly TextBlock _disks = Ui.Secondary("SSD: —");
    private readonly TextBlock _profileInfo = Ui.Secondary("Perfil: consultando…");
    private readonly TextBlock _batteryInfo = Ui.Label("—");
    private readonly TextBlock _limitInfo = Ui.Secondary("Comprobando límite…");
    private readonly TextBlock _boostInfo = Ui.Secondary("CPU Boost: comprobando…");
    private readonly TextBlock _status = Ui.Secondary("Intel Turbo Boost se guarda al cambiarlo en Fans + Power.");
    private readonly TextBlock _screenStatus = Ui.Secondary("Cambio rápido de Hz (ahorra batería en 60 Hz)");
    private readonly ProgressBar _batteryBar = new() { Minimum = 0, Maximum = 100, Height = 9, ShowProgressText = false };
    private readonly NumericUpDown _batteryLimit = new() { Minimum = 40, Maximum = 100, Increment = 5, Value = 80, Width = 83 };
    private readonly Button _limitButton = Ui.Button("Aplicar");
    private readonly Dictionary<string, Button> _profiles = new();
    private readonly DispatcherTimer _timer;
    private bool _refreshing, _editing, _forceExit;
    private Snapshot? _last;
    private AdvancedWindow? _advanced;
    private LightingWindow? _lighting;
    private TelemetryWindow? _telemetry;
    private TrayIcon? _trayIcon;
    private readonly List<TelemetrySample> _history = new();
    private AppSettings _settings = SettingsStore.Load();
    private readonly TextBlock _powerSource = Ui.Secondary("Alimentación: detectando…");
    private readonly TextBlock _autoStatus = Ui.Secondary("Automatización: desactivada");
    private readonly TextBlock _lightStatus = Ui.Secondary("Iluminación: detectando…");
    private bool? _powerCandidate, _stablePower;
    private int _powerSamples;

    public MainWindow()
    {
        Title = "TUF Control v0.5 · Linux";
        Background = Ui.Bg;
        Width = 525;
        Height = 920;
        MinWidth = 460;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var content = Ui.V(12);
        content.Margin = new Thickness(15, 15, 15, 13);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var title = Ui.V(2);
        title.Children.Add(Ui.Label("TUF CONTROL v0.5", 19, Ui.Text, true));
        title.Children.Add(Ui.Secondary("ASUS TUF F15 · Nobara / KDE"));
        header.Children.Add(title);
        var refresh = Ui.Button("↻ Actualizar");
        refresh.Click += async (_, _) => await RefreshAsync();
        Grid.SetColumn(refresh, 1); header.Children.Add(refresh);
        content.Children.Add(header);

        var stats = Ui.V(10);
        var metricGrid = MetricGrid();
        AddMetrics(metricGrid);
        stats.Children.Add(metricGrid);
        stats.Children.Add(_gpuStatus);
        stats.Children.Add(_disks);
        content.Children.Add(Ui.Card(stats));

        var performance = Ui.V();
        performance.Children.Add(Ui.Secondary("MODO DE RENDIMIENTO"));
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 7 };
        AddOption(row, _profiles, 0, "Silencioso", "LowPower", async value => await SetProfile(value));
        AddOption(row, _profiles, 1, "Equilibrado", "Balanced", async value => await SetProfile(value));
        AddOption(row, _profiles, 2, "Turbo", "Performance", async value => await SetProfile(value));
        performance.Children.Add(row);
        performance.Children.Add(_profileInfo);
        content.Children.Add(Ui.Card(performance));

        var quick = Ui.V(9);
        quick.Children.Add(Ui.Secondary("PRESETS · SOLO PERFIL ASUS"));
        var presets = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 7 };
        foreach (var (name, index) in new[] { ("Fresco", 0), ("Equilibrado", 1), ("Gaming", 2) })
        {
            var button = Ui.Button(name, name == "Fresco");
            button.Click += async (_, _) => await ApplyPreset(name);
            Grid.SetColumn(button, index);
            presets.Children.Add(button);
        }
        quick.Children.Add(presets);
        quick.Children.Add(_boostInfo);
        var setupPresets = Ui.Button("⚙  Editar presets y automatización");
        setupPresets.Click += (_, _) => OpenProfiles();
        quick.Children.Add(setupPresets);
        quick.Children.Add(_powerSource);
        quick.Children.Add(_autoStatus);
        content.Children.Add(Ui.Card(quick));

        // PANTALLA REFRESH RATE
        var screenCard = Ui.V(8);
        screenCard.Children.Add(Ui.Secondary("PANTALLA · TASA DE REFRESCO"));
        var screenGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 7 };
        var btn60 = Ui.Button("🔋 60 Hz · Ahorro batería");
        var btn144 = Ui.Button("⚡ 144 Hz · Fluido / Gaming", true);
        btn60.Click += async (_, _) => await ApplyChange(() => Hardware.SetScreenRefreshRate(60), "Pantalla a 60 Hz (Modo ahorro)");
        btn144.Click += async (_, _) => await ApplyChange(() => Hardware.SetScreenRefreshRate(144), "Pantalla a 144 Hz (Modo fluido)");
        Grid.SetColumn(btn60, 0); screenGrid.Children.Add(btn60);
        Grid.SetColumn(btn144, 1); screenGrid.Children.Add(btn144);
        screenCard.Children.Add(screenGrid);
        screenCard.Children.Add(_screenStatus);
        content.Children.Add(Ui.Card(screenCard));

        // BATERIA
        var battery = Ui.V(9);
        battery.Children.Add(Ui.Secondary("BATERÍA · LÍMITE DE CARGA"));
        battery.Children.Add(_batteryInfo);
        battery.Children.Add(_batteryBar);
        
        var fastBatteryGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 7 };
        var b60 = Ui.Button("🛡 60% Cuidado");
        var b80 = Ui.Button("⚖ 80% Diario");
        var b100 = Ui.Button("✈ 100% Total");
        b60.Click += async (_, _) => await QuickSetBatteryLimit(60);
        b80.Click += async (_, _) => await QuickSetBatteryLimit(80);
        b100.Click += async (_, _) => await QuickSetBatteryLimit(100);
        Grid.SetColumn(b60, 0); fastBatteryGrid.Children.Add(b60);
        Grid.SetColumn(b80, 1); fastBatteryGrid.Children.Add(b80);
        Grid.SetColumn(b100, 2); fastBatteryGrid.Children.Add(b100);
        battery.Children.Add(fastBatteryGrid);

        var batteryLine = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 9 };
        batteryLine.Children.Add(Ui.Label("Personalizado (%)", 12));
        Grid.SetColumn(_batteryLimit, 1); batteryLine.Children.Add(_batteryLimit);
        Grid.SetColumn(_limitButton, 2); batteryLine.Children.Add(_limitButton);
        _limitButton.Click += async (_, _) => await SetBatteryLimit();
        battery.Children.Add(batteryLine);
        battery.Children.Add(_limitInfo);
        content.Children.Add(Ui.Card(battery));

        // ILUMINACION AURA
        var lights = Ui.V(8);
        lights.Children.Add(Ui.Secondary("ILUMINACIÓN ASUS AURA RGB"));
        lights.Children.Add(_lightStatus);
        var lighting = Ui.Button("🎨 Colores, RGB y brillo del teclado", true);
        lighting.Click += (_, _) => OpenLighting();
        lights.Children.Add(lighting);
        content.Children.Add(Ui.Card(lights));

        var telemetry = Ui.Button("◉  Monitor en vivo · Gráficos de Temp y RPM");
        telemetry.Click += (_, _) => OpenTelemetry();
        content.Children.Add(telemetry);

        var advance = Ui.Button("⚙  Fans + Power · Configuración avanzada CPU/Boost");
        advance.Click += (_, _) => OpenAdvanced();
        content.Children.Add(advance);

        _status.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        content.Children.Add(_status);

        Content = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        
        Opened += async (_, _) =>
        {
            SetupTrayIcon();
            _timer.Start();
            await RefreshAsync();
        };

        Closing += (sender, e) =>
        {
            if (!_forceExit && _settings.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
            }
        };

        Closed += (_, _) =>
        {
            _timer.Stop();
            _advanced?.Close();
            _lighting?.Close();
            _telemetry?.Close();
            if (_trayIcon != null) _trayIcon.IsVisible = false;
        };
    }

    private void SetupTrayIcon()
    {
        try
        {
            _trayIcon = new TrayIcon
            {
                ToolTipText = "TUF Control v0.5",
                IsVisible = true
            };

            var iconPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "icons", "hicolor", "512x512", "apps", "tuf-control.png");
            if (System.IO.File.Exists(iconPath))
            {
                _trayIcon.Icon = new WindowIcon(iconPath);
            }

            var menu = new NativeMenu();

            var itemShow = new NativeMenuItem("Mostrar TUF Control");
            itemShow.Click += (_, _) => { Show(); Activate(); };
            menu.Add(itemShow);

            menu.Add(new NativeMenuItemSeparator());

            var itemQuiet = new NativeMenuItem("Perfil: Silencioso");
            itemQuiet.Click += async (_, _) => await SetProfile("LowPower");
            menu.Add(itemQuiet);

            var itemBalanced = new NativeMenuItem("Perfil: Equilibrado");
            itemBalanced.Click += async (_, _) => await SetProfile("Balanced");
            menu.Add(itemBalanced);

            var itemTurbo = new NativeMenuItem("Perfil: Turbo");
            itemTurbo.Click += async (_, _) => await SetProfile("Performance");
            menu.Add(itemTurbo);

            menu.Add(new NativeMenuItemSeparator());

            var itemBoostOn = new NativeMenuItem("Activar Turbo Boost");
            itemBoostOn.Click += async (_, _) => await ApplyChange(() => Hardware.SetTurbo(true), "Turbo Boost activado");
            menu.Add(itemBoostOn);

            var itemBoostOff = new NativeMenuItem("Desactivar Turbo Boost");
            itemBoostOff.Click += async (_, _) => await ApplyChange(() => Hardware.SetTurbo(false), "Turbo Boost desactivado");
            menu.Add(itemBoostOff);

            menu.Add(new NativeMenuItemSeparator());

            var item60 = new NativeMenuItem("Pantalla: 60 Hz");
            item60.Click += async (_, _) => await ApplyChange(() => Hardware.SetScreenRefreshRate(60), "Pantalla a 60 Hz");
            menu.Add(item60);

            var item144 = new NativeMenuItem("Pantalla: 144 Hz");
            item144.Click += async (_, _) => await ApplyChange(() => Hardware.SetScreenRefreshRate(144), "Pantalla a 144 Hz");
            menu.Add(item144);

            menu.Add(new NativeMenuItemSeparator());

            var itemExit = new NativeMenuItem("Salir de TUF Control");
            itemExit.Click += (_, _) =>
            {
                _forceExit = true;
                Close();
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.Shutdown();
            };
            menu.Add(itemExit);

            _trayIcon.Menu = menu;
            _trayIcon.Clicked += (_, _) =>
            {
                if (IsVisible) Hide();
                else { Show(); Activate(); }
            };

            var trayIcons = new TrayIcons { _trayIcon };
            TrayIcon.SetIcons(Application.Current!, trayIcons);
        }
        catch { /* Bandeja no soportada en este entorno */ }
    }

    private static Grid MetricGrid()
        => new() { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 8, RowSpacing = 12 };

    private void AddMetrics(Grid grid)
    {
        AddMetric(grid, 0, 0, "CPU", _cpu);
        AddMetric(grid, 1, 0, "NVIDIA", _gpu);
        AddMetric(grid, 0, 1, "VENT. CPU", _cpuFan);
        AddMetric(grid, 1, 1, "VENT. GPU", _gpuFan);
    }

    private static void AddMetric(Grid grid, int column, int row, string name, TextBlock value)
    {
        var stack = Ui.V(1);
        stack.Children.Add(Ui.Secondary(name));
        stack.Children.Add(value);
        Grid.SetColumn(stack, column);
        Grid.SetRow(stack, row);
        grid.Children.Add(stack);
    }

    private static void AddOption(Grid grid, Dictionary<string, Button> dictionary, int column, string label,
        string value, Func<string, Task> action)
    {
        var button = Ui.Button(label);
        button.Click += async (_, _) => await action(value);
        dictionary[value] = button;
        Grid.SetColumn(button, column);
        grid.Children.Add(button);
    }

    public async Task RefreshAsync()
    {
        if (_refreshing || _editing) return;
        _refreshing = true;
        Snapshot? snapshot = null;
        try
        {
            var s = await Task.Run(Hardware.Collect);
            snapshot = s;
            _last = s;

            _cpu.Text = FormatTemp(s.Cpu);
            _cpu.Foreground = Ui.TempBrush(s.Cpu);

            if (s.GpuRuntime == "suspended")
            {
                _gpu.Text = "Reposo";
                _gpu.Foreground = Ui.Muted;
            }
            else
            {
                _gpu.Text = FormatTemp(s.Gpu);
                _gpu.Foreground = Ui.TempBrush(s.Gpu);
            }

            _cpuFan.Text = s.CpuFan.HasValue ? $"{s.CpuFan} RPM" : "—";
            _gpuFan.Text = s.GpuFan.HasValue ? $"{s.GpuFan} RPM" : "—";
            _gpuStatus.Text = $"RTX 3050: {s.GpuRuntime switch { "suspended" => "suspendida · ahorro activo", "active" => "activa", _ => s.GpuRuntime }}";
            _disks.Text = s.Disks.Count > 0 ? "SSD: " + string.Join(" · ", s.Disks.Select(temp => FormatTemp(temp))) : "SSD: sin lectura";

            foreach (var pair in _profiles)
            {
                pair.Value.IsEnabled = s.AvailableProfiles.Contains(pair.Key);
                Ui.Select(pair.Value, s.Profile == pair.Key);
            }
            _profileInfo.Text = "Perfil actual: " + s.Profile;
            _batteryInfo.Text = s.BatteryLevel.HasValue ? $"Batería: {s.BatteryLevel}% · {s.BatteryState}" : "Batería no detectada";
            _batteryBar.Value = s.BatteryLevel ?? 0;
            _batteryLimit.IsEnabled = _limitButton.IsEnabled = s.BatteryThresholdSupported && Hardware.HelperInstalled;
            if (s.BatteryThreshold.HasValue && !_batteryLimit.IsFocused) _batteryLimit.Value = s.BatteryThreshold;
            _limitInfo.Text = !s.BatteryThresholdSupported ? "Control de carga no disponible en el firmware"
                : !Hardware.HelperInstalled ? "Falta instalar el helper con install.sh"
                : $"Límite actual: {s.BatteryThreshold?.ToString() ?? "—"}% · autorización al aplicar";
            _boostInfo.Text = !s.TurboEnabled.HasValue ? "Intel Turbo Boost: control no disponible" :
                $"Intel Turbo Boost: {(s.TurboEnabled.Value ? "activado" : "desactivado")} · " +
                (s.SavedTurboEnabled.HasValue ? $"guardado: {(s.SavedTurboEnabled.Value ? "activado" : "desactivado")}" : "sin preferencia guardada");
            _powerSource.Text = s.AcConnected switch { true => "Cargador: conectado", false => "Cargador: desconectado · batería", _ => "Cargador: estado no disponible" };
            _autoStatus.Text = _settings.AutoByPower ?
                $"Auto: AC {_settings.AcPreset} / bat {_settings.BatteryPreset}" + (_settings.AutoRefreshRate ? " · Auto Hz activo" : "") :
                "Automatización desactivada";
            _lightStatus.Text = s.Capabilities.Aura ?
                $"Aura compatible · {s.Capabilities.AuraModes.Count} efectos · Brillo: {s.LedBrightness}" :
                "Aura no disponible en el controlador actual";

            if (_trayIcon != null)
            {
                var gpuStr = s.GpuRuntime == "suspended" ? "Reposo" : FormatTemp(s.Gpu);
                _trayIcon.ToolTipText = $"TUF Control v0.5\nCPU: {FormatTemp(s.Cpu)} · GPU: {gpuStr}\nPerfil: {s.Profile} · Bat: {s.BatteryLevel?.ToString() ?? "—"}%";
            }

            _history.Add(new TelemetrySample(DateTime.Now, s.Cpu, s.Gpu,
                s.Disks.Count > 0 ? s.Disks.Max() : null, s.CpuFan, s.GpuFan));
            if (_history.Count > 60) _history.RemoveAt(0);
            if (_telemetry is { IsVisible: true }) _telemetry.Update(_history);
            if (_advanced is { IsVisible: true }) _advanced.Update(s);
        }
        catch (Exception ex) { _status.Text = "Error al actualizar: " + ex.Message; }
        finally { _refreshing = false; }
        if (snapshot is not null) await CheckAutomaticPowerProfile(snapshot);
    }

    private static string FormatTemp(double? value) => value.HasValue ? $"{value.Value:0.#} °C" : "—";

    private async Task ApplyChange(Func<Task<CommandResult>> operation, string success)
    {
        if (_editing) return;
        _editing = true;
        _status.Text = "Aplicando…";
        try
        {
            var result = await operation();
            _status.Text = result.Ok ? success : "No se pudo aplicar: " + result.Message;
        }
        catch (Exception e) { _status.Text = "Error: " + e.Message; }
        finally { _editing = false; await RefreshAsync(); }
    }

    private async Task SetProfile(string profile)
    {
        if (_last is null || !_last.AvailableProfiles.Contains(profile)) return;
        await ApplyChange(() => Hardware.SetProfile(profile), $"Perfil aplicado: {profile}");
    }

    private async Task QuickSetBatteryLimit(int limit)
    {
        _batteryLimit.Value = limit;
        await SetBatteryLimit();
    }

    private async Task SetBatteryLimit()
    {
        if (_last is null || !_last.BatteryThresholdSupported || !Hardware.HelperInstalled) return;
        var limit = (int)(_batteryLimit.Value ?? 80);
        if (limit < 40 || limit > 100) return;
        var confirm = new ConfirmWindow("Proteger batería", $"¿Cambiar el límite de carga a {limit}%?\nSe pedirá tu contraseña de administrador.", "Aplicar");
        if (await confirm.ShowDialog<bool>(this))
            await ApplyChange(() => Hardware.SetBatteryLimit(limit), $"Límite de carga configurado en {limit}%");
    }

    private async Task ApplyPreset(string name)
    {
        if (_editing || _last is null) return;
        var preset = _settings.GetPreset(name);
        if (!_last.AvailableProfiles.Contains(preset.AsusProfile))
        {
            _status.Text = $"El perfil ASUS '{preset.AsusProfile}' no está disponible.";
            return;
        }
        var confirm = new ConfirmWindow("Aplicar preset " + name,
            $"Se seleccionará {preset.AsusProfile}.\nNo se modificará Intel Turbo Boost ni el modo de GPU.", "Aplicar");
        if (!await confirm.ShowDialog<bool>(this)) return;
        await ApplyChange(() => Hardware.SetProfile(preset.AsusProfile),
            $"Preset {name}: perfil ASUS aplicado; Boost sin modificar.");
    }

    private async Task CheckAutomaticPowerProfile(Snapshot snapshot)
    {
        if (!_settings.AutoByPower || _editing || snapshot.AcConnected is not bool power)
        {
            _powerCandidate = null; _powerSamples = 0; return;
        }
        if (_powerCandidate != power) { _powerCandidate = power; _powerSamples = 1; return; }
        if (++_powerSamples < 2) return; // prevent transient cable status changes
        if (_stablePower is null)
        {
            _stablePower = power;
            if (!_settings.AutoAtStart) return;
        }
        else
        {
            if (_stablePower == power) return;
            _stablePower = power;
        }
        
        var name = power ? _settings.AcPreset : _settings.BatteryPreset;
        var preset = _settings.GetPreset(name);
        if (!snapshot.AvailableProfiles.Contains(preset.AsusProfile))
        {
            _status.Text = $"Auto: {preset.AsusProfile} no está disponible en asusctl."; return;
        }
        
        _editing = true;
        try
        {
            var result = await Hardware.SetProfile(preset.AsusProfile);
            if (!result.Ok) { _status.Text = "Auto: " + result.Message; return; }
            
            // Auto screen refresh rate switch if enabled
            if (_settings.AutoRefreshRate)
            {
                var targetHz = power ? 144 : 60;
                _ = Task.Run(() => Hardware.SetScreenRefreshRate(targetHz));
            }

            _status.Text = $"Auto: perfil ASUS {name} aplicado" + (_settings.AutoRefreshRate ? (power ? " (144 Hz)" : " (60 Hz)") : "");
        }
        catch (Exception ex) { _status.Text = "Auto: " + ex.Message; }
        finally { _editing = false; await RefreshAsync(); }
    }

    private void OpenProfiles()
    {
        var win = new ProfilesWindow(_settings, _last?.AvailableProfiles ?? Array.Empty<string>(), settings =>
        {
            _settings = settings;
            _powerCandidate = _stablePower = null; _powerSamples = 0;
            _autoStatus.Text = "Configuración guardada; esperando siguiente lectura.";
        });
        win.Show(this);
    }

    private void OpenLighting()
    {
        if (_lighting is null || !_lighting.IsVisible)
        {
            _lighting = new LightingWindow(); _lighting.Show(this);
        }
        else _lighting.Activate();
    }

    private void OpenTelemetry()
    {
        if (_telemetry is null || !_telemetry.IsVisible)
        {
            _telemetry = new TelemetryWindow(); _telemetry.Show(this);
        }
        else _telemetry.Activate();
        _telemetry.Update(_history);
    }

    private void OpenAdvanced()
    {
        if (_advanced is null || !_advanced.IsVisible)
        {
            _advanced = new AdvancedWindow(this, RefreshAsync);
            _advanced.Show(this);
        }
        else { _advanced.Activate(); }
        if (_last != null) _advanced.Update(_last);
    }
}
