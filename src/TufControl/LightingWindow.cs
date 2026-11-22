// SPDX-License-Identifier: GPL-3.0-only
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace TufControl;

internal sealed class LightingWindow : Window
{
    private static readonly (string Name, string Hex)[] QuickColors = new[]
    {
        ("Rojo TUF", "FF1E1E"),
        ("Naranja", "FF6600"),
        ("Ámbar", "FFAA00"),
        ("Verde", "00FF55"),
        ("Esmeralda", "00D285"),
        ("Cian ASUS", "00E5FF"),
        ("Azul Cielo", "0088FF"),
        ("Azul Eléctrico", "2244FF"),
        ("Púrpura", "A822FF"),
        ("Magenta", "FF0088"),
        ("Blanco Frío", "E0F7FA"),
        ("Blanco Puro", "FFFFFF")
    };

    private readonly ComboBox _brightness = new() { Width = 140, ItemsSource = new[] { "Apagado", "Bajo", "Medio", "Alto" }, SelectedIndex = 2 };
    private readonly ComboBox _effect = new() { MinWidth = 180 };
    private readonly TextBox _colorHex = new() { Width = 110, Watermark = "RRGGBB", MaxLength = 6, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Border _previewBox = new() { Width = 80, Height = 40, CornerRadius = new CornerRadius(8), BorderBrush = Ui.Border, BorderThickness = new Thickness(1) };
    
    private readonly Slider _sliderR = new() { Minimum = 0, Maximum = 255, Value = 0, Width = 190 };
    private readonly Slider _sliderG = new() { Minimum = 0, Maximum = 255, Value = 229, Width = 190 };
    private readonly Slider _sliderB = new() { Minimum = 0, Maximum = 255, Value = 255, Width = 190 };
    
    private readonly TextBlock _labelR = Ui.Secondary("R: 0");
    private readonly TextBlock _labelG = Ui.Secondary("G: 229");
    private readonly TextBlock _labelB = Ui.Secondary("B: 255");

    private readonly Button _brightnessApply = Ui.Button("Aplicar brillo");
    private readonly Button _effectApply = Ui.Button("✨ Aplicar iluminación", true);
    private readonly TextBlock _status = Ui.Secondary("Selecciona un color rápido o usa los controles deslizantes.");
    private bool _working, _syncingSliders;

    public LightingWindow()
    {
        Title = "TUF Control · Aura RGB";
        Background = Ui.Bg;
        Width = 520; Height = 740; MinWidth = 460; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var outer = Ui.V(13); outer.Margin = new Thickness(16);
        outer.Children.Add(Ui.Heading("ILUMINACIÓN ASUS AURA"));
        var caps = Hardware.Capabilities;
        outer.Children.Add(Ui.Secondary("Personaliza los colores y efectos del teclado RGB de tu TUF F15"));

        // BRIGHTNESS CARD
        var bright = Ui.V(9);
        bright.Children.Add(Ui.Heading("Brillo del teclado"));
        var brightRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        brightRow.Children.Add(_brightness);
        Grid.SetColumn(_brightnessApply, 1); brightRow.Children.Add(_brightnessApply);
        bright.Children.Add(brightRow);
        _brightnessApply.IsEnabled = caps.Backlight;
        _brightnessApply.Click += async (_, _) => await ChangeBrightness();
        outer.Children.Add(Ui.Card(bright));

        // EFFECT & COLOR CARD
        var effects = Ui.V(11);
        effects.Children.Add(Ui.Heading("Efecto y Selector de Color"));
        _effect.ItemsSource = caps.AuraModes;
        if (caps.AuraModes.Count > 0) _effect.SelectedIndex = 0;
        effects.Children.Add(_effect);

        // QUICK SWATCHES PALETTE
        effects.Children.Add(Ui.Secondary("Colores predefinidos rápidos:"));
        var paletteGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), ColumnSpacing = 6, RowSpacing = 6 };
        for (int i = 0; i < QuickColors.Length; i++)
        {
            var (name, hex) = QuickColors[i];
            var btn = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = Ui.Brush("#" + hex), BorderBrush = Ui.Border, BorderThickness = new Thickness(1) },
                        new TextBlock { Text = name, FontSize = 10, VerticalAlignment = VerticalAlignment.Center }
                    }
                },
                Background = Ui.Elevated,
                BorderBrush = Ui.Border,
                Padding = new Thickness(6, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            btn.Click += (_, _) => SetHexColor(hex);
            Grid.SetColumn(btn, i % 4);
            Grid.SetRow(btn, i / 4);
            paletteGrid.Children.Add(btn);
        }
        effects.Children.Add(paletteGrid);

        // RGB SLIDERS & LIVE PREVIEW
        effects.Children.Add(Ui.Secondary("Ajuste manual RGB:"));
        var rgbCard = Ui.V(6);
        rgbCard.Children.Add(CreateSliderRow("R", _sliderR, _labelR, "#FF5555"));
        rgbCard.Children.Add(CreateSliderRow("G", _sliderG, _labelG, "#55FF55"));
        rgbCard.Children.Add(CreateSliderRow("B", _sliderB, _labelB, "#5588FF"));
        
        _sliderR.ValueChanged += (_, _) => UpdateFromSliders();
        _sliderG.ValueChanged += (_, _) => UpdateFromSliders();
        _sliderB.ValueChanged += (_, _) => UpdateFromSliders();

        var previewRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10, Margin = new Thickness(0, 4) };
        previewRow.Children.Add(Ui.Label("Previsualización:", 12));
        Grid.SetColumn(_previewBox, 1); previewRow.Children.Add(_previewBox);
        Grid.SetColumn(_colorHex, 2); previewRow.Children.Add(_colorHex);
        _colorHex.TextChanged += (_, _) => UpdateFromHex();
        rgbCard.Children.Add(previewRow);
        effects.Children.Add(rgbCard);

        _effectApply.IsEnabled = caps.Aura && caps.AuraModes.Count > 0;
        _effectApply.Click += async (_, _) => await ChangeEffect();
        effects.Children.Add(_effectApply);

        outer.Children.Add(Ui.Card(effects));

        _status.TextWrapping = TextWrapping.Wrap;
        outer.Children.Add(_status);

        Content = new ScrollViewer { Content = outer };

        // Set initial color (Cian ASUS: 00E5FF)
        SetHexColor("00E5FF");
    }

    private static Grid CreateSliderRow(string name, Slider slider, TextBlock label, string accentHex)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("30,*,45"), ColumnSpacing = 8 };
        row.Children.Add(new TextBlock { Text = name, FontWeight = FontWeight.Bold, Foreground = Ui.Brush(accentHex), VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(slider, 1); row.Children.Add(slider);
        Grid.SetColumn(label, 2); row.Children.Add(label);
        return row;
    }

    private void SetHexColor(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length != 6) return;
        _colorHex.Text = hex;
        UpdateFromHex();
    }

    private void UpdateFromHex()
    {
        if (_syncingSliders) return;
        var text = (_colorHex.Text ?? "").Trim().TrimStart('#');
        if (text.Length != 6) return;
        try
        {
            var r = Convert.ToByte(text.Substring(0, 2), 16);
            var g = Convert.ToByte(text.Substring(2, 2), 16);
            var b = Convert.ToByte(text.Substring(4, 2), 16);
            _syncingSliders = true;
            _sliderR.Value = r; _labelR.Text = $"R: {r}";
            _sliderG.Value = g; _labelG.Text = $"G: {g}";
            _sliderB.Value = b; _labelB.Text = $"B: {b}";
            _previewBox.Background = Ui.Brush("#" + text);
        }
        catch { }
        finally { _syncingSliders = false; }
    }

    private void UpdateFromSliders()
    {
        if (_syncingSliders) return;
        _syncingSliders = true;
        try
        {
            var r = (byte)_sliderR.Value;
            var g = (byte)_sliderG.Value;
            var b = (byte)_sliderB.Value;
            _labelR.Text = $"R: {r}";
            _labelG.Text = $"G: {g}";
            _labelB.Text = $"B: {b}";
            var hex = $"{r:X2}{g:X2}{b:X2}";
            _colorHex.Text = hex;
            _previewBox.Background = Ui.Brush("#" + hex);
        }
        catch { }
        finally { _syncingSliders = false; }
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
            var color = (_colorHex.Text ?? "").Trim().TrimStart('#');
            if (color.Length != 6) color = "00E5FF";
            var result = await Hardware.SetLighting(mode, color);
            _status.Text = result.Ok ? $"Aura: {mode} (#{color}) aplicado correctamente." : "Error: " + result.Message;
        }
        catch (Exception ex) { _status.Text = "Error: " + ex.Message; }
        finally { _working = false; _effectApply.IsEnabled = true; }
    }
}
