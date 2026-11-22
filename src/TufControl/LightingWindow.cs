// SPDX-License-Identifier: GPL-3.0-only
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TufControl;

internal sealed class LightingWindow : Window
{
    private readonly ComboBox _brightness = new() { Width = 130, ItemsSource = new[] { "Apagado", "Bajo", "Medio", "Alto" }, SelectedIndex = 2 };
    private readonly ComboBox _effect = new() { MinWidth = 170 };
    private readonly TextBox _color = new() { Text = "42CBED", Width = 110, Watermark = "RRGGBB", MaxLength = 7 };
    private readonly Button _brightnessApply = Ui.Button("Aplicar brillo");
    private readonly Button _effectApply = Ui.Button("Aplicar efecto", true);
    private readonly TextBlock _status = Ui.Secondary("Selecciona un efecto compatible y pulsa Aplicar.");
    private bool _working;

    public LightingWindow()
    {
        Title = "TUF Control · Aura RGB";
        Background = Ui.Bg;
        Width = 440; Height = 470; MinWidth = 390;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var outer = Ui.V(13); outer.Margin = new Thickness(16);
        outer.Children.Add(Ui.Heading("ILUMINACIÓN DEL TECLADO"));
        var caps = Hardware.Capabilities;
        outer.Children.Add(Ui.Secondary("ASUS Aura / Backlight · se utiliza asusctl 6.5"));
        var bright = Ui.V(9);
        bright.Children.Add(Ui.Heading("Brillo"));
        bright.Children.Add(_brightness);
        bright.Children.Add(_brightnessApply);
        _brightnessApply.IsEnabled = caps.Backlight;
        _brightnessApply.Click += async (_, _) => await ChangeBrightness();
        bright.Children.Add(Ui.Secondary(caps.Backlight ? "Brillo de teclado disponible." : "Este equipo no ofrece control de brillo."));
        outer.Children.Add(Ui.Card(bright));
        var effects = Ui.V(8);
        effects.Children.Add(Ui.Heading("Efecto Aura"));
        _effect.ItemsSource = caps.AuraModes;
        if (caps.AuraModes.Count > 0) _effect.SelectedIndex = 0;
        effects.Children.Add(_effect);
        effects.Children.Add(Ui.Secondary("Color hexadecimal (se usa en Estático, Respiración y Pulso)"));
        effects.Children.Add(_color);
        effects.Children.Add(_effectApply);
        _effectApply.IsEnabled = caps.Aura && caps.AuraModes.Count > 0;
        _effectApply.Click += async (_, _) => await ChangeEffect();
        effects.Children.Add(Ui.Secondary(caps.Aura ? "Solo se muestran los efectos anunciados por tu hardware." : "Aura no detectado en asusctl."));
        outer.Children.Add(Ui.Card(effects));
        _status.TextWrapping = TextWrapping.Wrap;
        outer.Children.Add(_status);
        Content = new ScrollViewer { Content = outer };
    }

    private async Task ChangeBrightness()
    {
        if (_working || !Hardware.Capabilities.Backlight || _brightness.SelectedIndex < 0) return;
        _working = true; _brightnessApply.IsEnabled = false;
        try
        {
            var level = new[] { "off", "low", "med", "high" }[_brightness.SelectedIndex];
            var result = await Hardware.SetBrightness(level);
            _status.Text = result.Ok ? "Brillo actualizado." : "Error: " + result.Message;
        }
        catch (Exception ex) { _status.Text = "Error: " + ex.Message; }
        finally { _working = false; _brightnessApply.IsEnabled = true; }
    }

    private async Task ChangeEffect()
    {
        if (_working || _effect.SelectedItem is not string mode) return;
        _working = true; _effectApply.IsEnabled = false;
        try
        {
            var color = (_color.Text ?? "").Trim().TrimStart('#');
            var result = await Hardware.SetLighting(mode, color);
            _status.Text = result.Ok ? $"Aura: {mode} aplicado." : "Error: " + result.Message;
        }
        catch (Exception ex) { _status.Text = "Error: " + ex.Message; }
        finally { _working = false; _effectApply.IsEnabled = true; }
    }
}
