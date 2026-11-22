// SPDX-License-Identifier: GPL-3.0-only
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System.Text.Json;

namespace TufControl;

internal sealed class ProfilesWindow : Window
{
    private readonly AppSettings _draft;
    private readonly ComboBox _cool;
    private readonly ComboBox _balanced;
    private readonly ComboBox _gaming;
    private readonly CheckBox _auto = new() { Content = "Cambiar de perfil al conectar/desconectar el cargador" };
    private readonly CheckBox _applyStart = new() { Content = "Aplicar el perfil correspondiente al abrir la aplicación" };
    private readonly CheckBox _autostart = new() { Content = "Abrir TUF Control al iniciar KDE" };
    private readonly ComboBox _ac = new() { ItemsSource = new[] { "Fresco", "Equilibrado", "Gaming" }, Width = 144 };
    private readonly ComboBox _battery = new() { ItemsSource = new[] { "Fresco", "Equilibrado", "Gaming" }, Width = 144 };
    private readonly TextBlock _status = Ui.Secondary("Los perfiles no modifican GPU, ventiladores ni límites PL1/PL2.");
    private readonly Action<AppSettings> _onSave;

    public ProfilesWindow(AppSettings current, IReadOnlyList<string> available, Action<AppSettings> onSave)
    {
        _draft = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(current)) ?? new AppSettings();
        _onSave = onSave;
        Title = "TUF Control · Perfiles y automatización";
        Background = Ui.Bg; Width = 560; Height = 720; MinWidth = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var outer = Ui.V(13); outer.Margin = new Thickness(16);
        outer.Children.Add(Ui.Heading("PERFILES PERSONALIZADOS"));
        outer.Children.Add(Ui.Secondary("Elige el perfil ASUS para cada preset. Turbo Boost se controla por separado en Fans + Power."));
        var choices = new[] { "LowPower", "Balanced", "Performance" }.Where(x => available.Contains(x)).ToArray();
        if (choices.Length == 0) choices = new[] { "LowPower", "Balanced", "Performance" };
        _cool = CreateEditor(outer, "Fresco · baja temperatura", _draft.Cool, choices);
        _balanced = CreateEditor(outer, "Equilibrado · uso cotidiano", _draft.Balanced, choices);
        _gaming = CreateEditor(outer, "Gaming · juegos y tareas exigentes", _draft.Gaming, choices);

        var automation = Ui.V(11);
        automation.Children.Add(Ui.Heading("AUTOMATIZACIÓN POR ENERGÍA"));
        _auto.IsChecked = _draft.AutoByPower;
        _applyStart.IsChecked = _draft.AutoAtStart;
        _autostart.IsChecked = _draft.StartWithDesktop;
        automation.Children.Add(_auto);
        automation.Children.Add(_applyStart);
        automation.Children.Add(_autostart);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), RowSpacing = 8 };
        grid.Children.Add(Ui.Label("Con cargador:", 12));
        Grid.SetColumn(_ac, 1); grid.Children.Add(_ac);
        var second = Ui.Label("Con batería:", 12); Grid.SetRow(second, 1); grid.Children.Add(second);
        Grid.SetRow(_battery, 1); Grid.SetColumn(_battery, 1); grid.Children.Add(_battery);
        automation.Children.Add(grid);
        _ac.SelectedItem = _draft.AcPreset;
        _battery.SelectedItem = _draft.BatteryPreset;
        automation.Children.Add(Ui.Secondary("Funciona mientras TUF Control esté abierto y solo modifica perfiles ASUS. Nunca cambia Intel Turbo Boost ni GPU."));
        outer.Children.Add(Ui.Card(automation));
        _status.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        outer.Children.Add(_status);
        var save = Ui.Button("Guardar configuración", true);
        save.Click += (_, _) => Save();
        outer.Children.Add(save);
        Content = new ScrollViewer { Content = outer };
    }

    private static ComboBox CreateEditor(StackPanel outer, string title, Preset preset, string[] profiles)
    {
        var card = Ui.V(7);
        card.Children.Add(Ui.Heading(title));
        var profile = new ComboBox { ItemsSource = profiles, Width = 185 };
        profile.SelectedItem = profiles.Contains(preset.AsusProfile) ? preset.AsusProfile : profiles[0];
        card.Children.Add(profile);
        outer.Children.Add(Ui.Card(card));
        return profile;
    }

    private void Save()
    {
        try
        {
            UpdatePreset(_draft.Cool, _cool);
            UpdatePreset(_draft.Balanced, _balanced);
            UpdatePreset(_draft.Gaming, _gaming);
            _draft.AutoByPower = _auto.IsChecked == true;
            _draft.AutoAtStart = _applyStart.IsChecked == true;
            _draft.StartWithDesktop = _autostart.IsChecked == true;
            _draft.AcPreset = _ac.SelectedItem as string ?? "Gaming";
            _draft.BatteryPreset = _battery.SelectedItem as string ?? "Fresco";
            SettingsStore.SetAutostart(_draft.StartWithDesktop);
            SettingsStore.Save(_draft);
            _onSave(_draft);
            Close();
        }
        catch (Exception ex) { _status.Text = "No se pudieron guardar los ajustes: " + ex.Message; }
    }

    private static void UpdatePreset(Preset target, ComboBox profile)
    {
        target.AsusProfile = profile.SelectedItem as string ?? "Balanced";
    }
}
