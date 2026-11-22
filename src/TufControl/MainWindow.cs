// SPDX-License-Identifier: GPL-3.0-only
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TufControl;

public sealed class MainWindow : Window
{
    // Metrics & Headers
    private readonly TextBlock _titleModo = Ui.Label("Modo: Consultando…", 12.5, Ui.Text, true);
    private readonly TextBlock _cpuMetrics = Ui.Label("CPU: —  Fan: —", 11.5, Ui.Muted);
    
    private readonly TextBlock _titleGpu = Ui.Label("Modo de GPU: RTX 3050", 12.5, Ui.Text, true);
    private readonly TextBlock _gpuMetrics = Ui.Label("GPU: —  Fan: —", 11.5, Ui.Muted);

    private readonly TextBlock _titleScreen = Ui.Label("Pantalla: 1080p", 12.5, Ui.Text, true);
    private readonly TextBlock _screenMetrics = Ui.Label("Auto: Inactivo", 11.5, Ui.Muted);

    private readonly TextBlock _titleBattery = Ui.Label("Batería: —", 12.5, Ui.Text, true);
    private readonly TextBlock _batteryMetrics = Ui.Label("Límite: —", 11.5, Ui.Muted);

    private readonly TextBlock _status = Ui.Secondary("TUF Control listo.");

    // Buttons Collections
    private readonly Dictionary<string, Button> _profileButtons = new();
    private Button? _btnScreenAuto, _btnScreen60, _btnScreen144;
    private Button? _btnBat60, _btnBat80, _btnBat100;
    private Button? _btnBoostOn, _btnBoostOff;

    // Keyboard Aura controls
    private readonly ComboBox _auraEffect = new() { Width = 115, Height = 32 };
    private readonly ComboBox _auraBrightness = new() { Width = 95, Height = 32, ItemsSource = new[] { "Apagado", "Bajo", "Medio", "Alto" } };
    private readonly Border _currentColorBox = new() { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = Ui.Brush("#00E5FF") };
    private Button? _colorPickerBtn;

    private readonly DispatcherTimer _timer;
    private bool _refreshing, _editing, _forceExit;
    private Snapshot? _last;
    private AdvancedWindow? _advanced;
    private LightingWindow? _lighting;
    private TelemetryWindow? _telemetry;
    private TrayIcon? _trayIcon;
    private readonly List<TelemetrySample> _history = new();
    private AppSettings _settings = SettingsStore.Load();
    private bool? _powerCandidate, _stablePower;
    private int _powerSamples;

    public MainWindow()
    {
        Title = "TUF Control — ASUS TUF F15 FX506HC";
        Background = Ui.Bg;
        Width = 470;
        Height = 740;
        MinWidth = 430;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = Ui.V(10);
        root.Margin = new Thickness(14, 12, 14, 14);

        // TOP APP HEADER (G-Helper Style)
        var topHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock { Text = "🛡️", FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        titleStack.Children.Add(new TextBlock { Text = "TUF Control", FontSize = 13, FontWeight = FontWeight.Bold, Foreground = Ui.Text, VerticalAlignment = VerticalAlignment.Center });
        titleStack.Children.Add(new TextBlock { Text = "— ASUS TUF F15 (FX506HC)", FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center });
        topHeader.Children.Add(titleStack);

        var topButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var teleBtn = Ui.Button("◉", false);
        ToolTip.SetTip(teleBtn, "Monitor de telemetría y gráficos");
        teleBtn.Padding = new Thickness(7, 4);
        teleBtn.MinHeight = 28;
        teleBtn.Click += (_, _) => OpenTelemetry();
        topButtons.Children.Add(teleBtn);

        var refreshBtn = Ui.Button("↻", false);
        ToolTip.SetTip(refreshBtn, "Actualizar métricas ahora");
        refreshBtn.Padding = new Thickness(7, 4);
        refreshBtn.MinHeight = 28;
        refreshBtn.Click += async (_, _) => await RefreshAsync();
        topButtons.Children.Add(refreshBtn);

        Grid.SetColumn(topButtons, 1);
        topHeader.Children.Add(topButtons);
        root.Children.Add(topHeader);

        // 1. MODO DE RENDIMIENTO (CPU & FANS)
        var secModo = Ui.V(6);
        secModo.Children.Add(Ui.SectionHeader(Icons.Speedometer, "", _cpuMetrics));
        // Replace header title with dynamic _titleModo
        ((StackPanel)((Grid)secModo.Children[0]).Children[0]).Children[1] = _titleModo;

        var gridModo = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 6 };
        var btnQuiet = Ui.GButton(Icons.Bicycle, "Silencio");
        btnQuiet.Click += async (_, _) => await SetProfile("LowPower");
        _profileButtons["LowPower"] = btnQuiet;
        Grid.SetColumn(btnQuiet, 0); gridModo.Children.Add(btnQuiet);

        var btnBal = Ui.GButton(Icons.Car, "Equilibrado");
        btnBal.Click += async (_, _) => await SetProfile("Balanced");
        _profileButtons["Balanced"] = btnBal;
        Grid.SetColumn(btnBal, 1); gridModo.Children.Add(btnBal);

        var btnTurbo = Ui.GButton(Icons.Rocket, "Turbo");
        btnTurbo.Click += async (_, _) => await SetProfile("Performance");
        _profileButtons["Performance"] = btnTurbo;
        Grid.SetColumn(btnTurbo, 2); gridModo.Children.Add(btnTurbo);

        var btnAdv = Ui.GButton(Icons.Fan, "Ventiladores\n+ Energía");
        btnAdv.Click += (_, _) => OpenAdvanced();
        Grid.SetColumn(btnAdv, 3); gridModo.Children.Add(btnAdv);

        secModo.Children.Add(gridModo);
        root.Children.Add(Ui.Card(secModo));

        // 2. MODO DE GPU & BOOST
        var secGpu = Ui.V(6);
        secGpu.Children.Add(Ui.SectionHeader(Icons.Rocket, "", _gpuMetrics));
        ((StackPanel)((Grid)secGpu.Children[0]).Children[0]).Children[1] = _titleGpu;

        var gridGpu = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 6 };
        var btnEco = Ui.GButton(Icons.Leaf, "Fresco / Eco");
        btnEco.Click += async (_, _) => await ApplyPreset("Fresco");
        Grid.SetColumn(btnEco, 0); gridGpu.Children.Add(btnEco);

        _btnBoostOff = Ui.GButton(Icons.Lock, "Boost Off");
        _btnBoostOff.Click += async (_, _) => await ApplyChange(() => Hardware.SetTurbo(false), "Intel Turbo Boost desactivado.");
        Grid.SetColumn(_btnBoostOff, 1); gridGpu.Children.Add(_btnBoostOff);

        _btnBoostOn = Ui.GButton(Icons.Lightning, "Boost On");
        _btnBoostOn.Click += async (_, _) => await ApplyChange(() => Hardware.SetTurbo(true), "Intel Turbo Boost activado.");
        Grid.SetColumn(_btnBoostOn, 2); gridGpu.Children.Add(_btnBoostOn);

        secGpu.Children.Add(gridGpu);
        root.Children.Add(Ui.Card(secGpu));

        // 3. PANTALLA (TASA DE REFRESCO)
        var secScreen = Ui.V(6);
        secScreen.Children.Add(Ui.SectionHeader(Icons.Display, "", _screenMetrics));
        ((StackPanel)((Grid)secScreen.Children[0]).Children[0]).Children[1] = _titleScreen;

        var gridScreen = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 6 };
        _btnScreenAuto = Ui.GButton(Icons.Sync, "Automático", 44, 15);
        _btnScreenAuto.Click += (_, _) => ToggleAutoRefresh();
        Grid.SetColumn(_btnScreenAuto, 0); gridScreen.Children.Add(_btnScreenAuto);

        _btnScreen60 = Ui.GButton(Icons.Display, "60Hz", 44, 15);
        _btnScreen60.Click += async (_, _) => await ApplyChange(() => Hardware.SetScreenRefreshRate(60), "Pantalla configurada a 60 Hz");
        Grid.SetColumn(_btnScreen60, 1); gridScreen.Children.Add(_btnScreen60);

        _btnScreen144 = Ui.GButton(Icons.Lightning, "144Hz", 44, 15);
        _btnScreen144.Click += async (_, _) => await ApplyChange(() => Hardware.SetScreenRefreshRate(144), "Pantalla configurada a 144 Hz");
        Grid.SetColumn(_btnScreen144, 2); gridScreen.Children.Add(_btnScreen144);

        secScreen.Children.Add(gridScreen);
        root.Children.Add(Ui.Card(secScreen));

        // 4. BATERÍA (LÍMITE DE CARGA)
        var secBattery = Ui.V(6);
        secBattery.Children.Add(Ui.SectionHeader(Icons.Shield, "", _batteryMetrics));
        ((StackPanel)((Grid)secBattery.Children[0]).Children[0]).Children[1] = _titleBattery;

        var gridBat = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 6 };
        _btnBat60 = Ui.GButton(Icons.Shield, "60% Cuidado", 44, 15);
        _btnBat60.Click += async (_, _) => await QuickSetBatteryLimit(60);
        Grid.SetColumn(_btnBat60, 0); gridBat.Children.Add(_btnBat60);

        _btnBat80 = Ui.GButton(Icons.Heart, "80% Diario", 44, 15);
        _btnBat80.Click += async (_, _) => await QuickSetBatteryLimit(80);
        Grid.SetColumn(_btnBat80, 1); gridBat.Children.Add(_btnBat80);

        _btnBat100 = Ui.GButton(Icons.Airplane, "100% Total", 44, 15);
        _btnBat100.Click += async (_, _) => await QuickSetBatteryLimit(100);
        Grid.SetColumn(_btnBat100, 2); gridBat.Children.Add(_btnBat100);

        secBattery.Children.Add(gridBat);
        root.Children.Add(Ui.Card(secBattery));

        // 5. TECLADO DEL PORTÁTIL (G-HELPER STYLE)
        var secKeyboard = Ui.V(7);
        var kbHeaderRight = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children = { Icons.Create(Icons.Settings, 11, Ui.Text), new TextBlock { Text = "Ajustes", FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center } }
            },
            FontSize = 10.5,
            Padding = new Thickness(7, 3),
            MinHeight = 24,
            Background = Ui.Elevated,
            BorderBrush = Ui.Border
        };
        kbHeaderRight.Click += (_, _) => OpenProfiles();
        secKeyboard.Children.Add(Ui.SectionHeader(Icons.Keyboard, "Teclado del portátil", kbHeaderRight));

        var kbRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = 6 };
        
        // Aura Effect ComboBox
        _auraEffect.ItemsSource = Hardware.Capabilities.AuraModes;
        if (Hardware.Capabilities.AuraModes.Count > 0) _auraEffect.SelectedIndex = 0;
        _auraEffect.SelectionChanged += async (_, _) =>
        {
            if (_auraEffect.SelectedItem is string mode)
                await Hardware.SetLighting(mode, _settings.LastKeyboardColor);
        };
        Grid.SetColumn(_auraEffect, 0); kbRow.Children.Add(_auraEffect);

        // Color Button with visual colored swatch
        _colorPickerBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _currentColorBox, new TextBlock { Text = "Color", FontSize = 11, VerticalAlignment = VerticalAlignment.Center } }
            },
            Background = Ui.GButtonBg,
            BorderBrush = Ui.GButtonBorder,
            Height = 32,
            Padding = new Thickness(8, 4)
        };
        _colorPickerBtn.Click += (_, _) => OpenLighting();
        Grid.SetColumn(_colorPickerBtn, 1); kbRow.Children.Add(_colorPickerBtn);

        // Brightness ComboBox
        _auraBrightness.SelectedIndex = 2;
        _auraBrightness.SelectionChanged += async (_, _) =>
        {
            if (_auraBrightness.SelectedIndex >= 0)
            {
                var lvl = new[] { "off", "low", "med", "high" }[_auraBrightness.SelectedIndex];
                await Hardware.SetBrightness(lvl);
            }
        };
        Grid.SetColumn(_auraBrightness, 2); kbRow.Children.Add(_auraBrightness);

        // Additional / RGB Button
        var btnAuraAdv = new Button
        {
            Content = "Aura RGB",
            Background = Ui.GButtonBg,
            BorderBrush = Ui.GButtonBorder,
            FontSize = 11,
            Height = 32,
            Padding = new Thickness(8, 4)
        };
        btnAuraAdv.Click += (_, _) => OpenLighting();
        Grid.SetColumn(btnAuraAdv, 3); kbRow.Children.Add(btnAuraAdv);

        secKeyboard.Children.Add(kbRow);
        root.Children.Add(Ui.Card(secKeyboard));

        // STATUS BAR AT BOTTOM
        _status.TextAlignment = TextAlignment.Center;
        _status.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(_status);

        Content = new ScrollViewer
        {
            Content = root,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
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

    private void ToggleAutoRefresh()
    {
        _settings.AutoRefreshRate = !_settings.AutoRefreshRate;
        SettingsStore.Save(_settings);
        UpdateAutoRefreshUI();
        _status.Text = _settings.AutoRefreshRate ? "Ajuste automático de Hz activado." : "Ajuste automático de Hz desactivado.";
    }

    private void UpdateAutoRefreshUI()
    {
        if (_btnScreenAuto != null)
            Ui.Select(_btnScreenAuto, _settings.AutoRefreshRate);
        _screenMetrics.Text = _settings.AutoRefreshRate ? "Auto: Activo" : "Auto: Inactivo";
    }

    private void SetupTrayIcon()
    {
        try
        {
            _trayIcon = new TrayIcon
            {
                ToolTipText = "TUF Control — ASUS TUF F15",
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
        catch { }
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

            // 1. MODO & CPU METRICS
            var profileDisplay = s.Profile switch
            {
                "LowPower" => "Silencioso",
                "Balanced" => "Equilibrado",
                "Performance" => "Turbo",
                _ => s.Profile
            };
            _titleModo.Text = $"Modo: {profileDisplay}";
            _cpuMetrics.Text = $"CPU: {FormatTemp(s.Cpu)}  Fan: {(s.CpuFan.HasValue ? $"{s.CpuFan} RPM" : "0 RPM")}";
            _cpuMetrics.Foreground = Ui.TempBrush(s.Cpu);

            foreach (var (key, btn) in _profileButtons)
            {
                btn.IsEnabled = s.AvailableProfiles.Contains(key);
                Ui.Select(btn, s.Profile == key, isTurbo: key == "Performance");
            }

            // 2. GPU METRICS
            var isGpuSuspended = s.GpuRuntime == "suspended";
            _titleGpu.Text = $"Modo de GPU: {(isGpuSuspended ? "RTX 3050 (Ahorro / Reposo)" : "RTX 3050 (Activa)")}";
            _gpuMetrics.Text = isGpuSuspended ? "GPU: Reposo  Fan: 0 RPM" : $"GPU: {FormatTemp(s.Gpu)}  Fan: {(s.GpuFan.HasValue ? $"{s.GpuFan} RPM" : "0 RPM")}";
            _gpuMetrics.Foreground = isGpuSuspended ? Ui.Muted : Ui.TempBrush(s.Gpu);

            // Boost indicator buttons
            if (_btnBoostOn != null && _btnBoostOff != null)
            {
                var boostActive = s.TurboEnabled == true;
                Ui.Select(_btnBoostOn, boostActive, isTurbo: true);
                Ui.Select(_btnBoostOff, !boostActive);
            }

            // 3. PANTALLA
            UpdateAutoRefreshUI();

            // 4. BATERÍA
            var batLvl = s.BatteryLevel.HasValue ? $"{s.BatteryLevel}%" : "—";
            var acStr = s.AcConnected switch { true => "Enchufado", false => "Batería", _ => "" };
            _titleBattery.Text = $"Batería: {batLvl} · {acStr}";
            _batteryMetrics.Text = $"Límite: {s.BatteryThreshold?.ToString() ?? "—"}%";

            if (_btnBat60 != null && _btnBat80 != null && _btnBat100 != null)
            {
                Ui.Select(_btnBat60, s.BatteryThreshold == 60);
                Ui.Select(_btnBat80, s.BatteryThreshold == 80);
                Ui.Select(_btnBat100, s.BatteryThreshold == 100 || !s.BatteryThreshold.HasValue);
            }

            // 5. TECLADO
            _currentColorBox.Background = Ui.Brush("#" + _settings.LastKeyboardColor);

            // TRAY TOOLTIP
            if (_trayIcon != null)
            {
                var gpuStr = isGpuSuspended ? "Reposo" : FormatTemp(s.Gpu);
                _trayIcon.ToolTipText = $"TUF Control — G-Helper Style\nCPU: {FormatTemp(s.Cpu)} · GPU: {gpuStr}\nPerfil: {profileDisplay} · Bat: {batLvl}";
            }

            // TELEMETRY HISTORY
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

    private static string FormatTemp(double? value) => value.HasValue ? $"{value.Value:0.#}°C" : "—";

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
        if (_last is null || !_last.BatteryThresholdSupported || !Hardware.HelperInstalled) return;
        var confirm = new ConfirmWindow("Límite de Batería",
            $"¿Cambiar el límite de carga al {limit}%?\nSe solicitará autorización de administrador.", "Aplicar");
        if (await confirm.ShowDialog<bool>(this))
            await ApplyChange(() => Hardware.SetBatteryLimit(limit), $"Límite de batería configurado en {limit}%");
    }

    private async Task ApplyPreset(string name)
    {
        if (_editing || _last is null) return;
        var preset = _settings.GetPreset(name);
        if (!_last.AvailableProfiles.Contains(preset.AsusProfile))
        {
            _status.Text = $"Perfil ASUS '{preset.AsusProfile}' no disponible.";
            return;
        }
        await ApplyChange(() => Hardware.SetProfile(preset.AsusProfile), $"Preset {name} aplicado.");
    }

    private async Task CheckAutomaticPowerProfile(Snapshot snapshot)
    {
        if (!_settings.AutoByPower || _editing || snapshot.AcConnected is not bool power)
        {
            _powerCandidate = null; _powerSamples = 0; return;
        }
        if (_powerCandidate != power) { _powerCandidate = power; _powerSamples = 1; return; }
        if (++_powerSamples < 2) return;
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
        if (!snapshot.AvailableProfiles.Contains(preset.AsusProfile)) return;

        _editing = true;
        try
        {
            var result = await Hardware.SetProfile(preset.AsusProfile);
            if (!result.Ok) { _status.Text = "Auto: " + result.Message; return; }

            if (_settings.AutoRefreshRate)
            {
                var targetHz = power ? 144 : 60;
                _ = Task.Run(() => Hardware.SetScreenRefreshRate(targetHz));
            }

            _status.Text = $"Auto: perfil {name} aplicado" + (_settings.AutoRefreshRate ? (power ? " (144 Hz)" : " (60 Hz)") : "");
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
            UpdateAutoRefreshUI();
            _status.Text = "Configuración guardada.";
        });
        win.Show(this);
    }

    private void OpenLighting()
    {
        if (_lighting is null || !_lighting.IsVisible)
        {
            _lighting = new LightingWindow();
            _lighting.Closed += (_, _) =>
            {
                _settings = SettingsStore.Load();
                _currentColorBox.Background = Ui.Brush("#" + _settings.LastKeyboardColor);
            };
            _lighting.Show(this);
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
