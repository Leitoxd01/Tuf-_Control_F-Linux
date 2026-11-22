// SPDX-License-Identifier: GPL-3.0-only
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TufControl;

internal static class Ui
{
    // G-Helper Inspired Dark Palette
    public static readonly IBrush Bg = Brush("#18181A");
    public static readonly IBrush Panel = Brush("#222225");
    public static readonly IBrush Elevated = Brush("#2B2B30");
    public static readonly IBrush GButtonBg = Brush("#29292E");
    public static readonly IBrush GButtonBorder = Brush("#3A3A40");
    public static readonly IBrush Muted = Brush("#9D9DA8");
    public static readonly IBrush Text = Brush("#F0F0F5");
    public static readonly IBrush Accent = Brush("#0A84FF");
    public static readonly IBrush AccentBg = Brush("#16283D");
    public static readonly IBrush TurboBorder = Brush("#FF3B30");
    public static readonly IBrush TurboBg = Brush("#381A1A");
    public static readonly IBrush Border = Brush("#303036");
    public static readonly IBrush Cold = Brush("#30D158");
    public static readonly IBrush Green = Brush("#30D158");
    public static readonly IBrush Warning = Brush("#FF9F0A");
    public static readonly IBrush Hot = Brush("#FF453A");

    public static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    public static TextBlock Label(string text, double size = 12, IBrush? color = null, bool bold = false) =>
        new() { Text = text, FontSize = size, Foreground = color ?? Text,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap };

    public static IBrush TempBrush(double? temp)
    {
        if (!temp.HasValue) return Accent;
        if (temp.Value < 55) return Cold;
        if (temp.Value < 75) return Accent;
        if (temp.Value < 85) return Warning;
        return Hot;
    }

    public static Border Card(Control child, Thickness? padding = null) => new()
    {
        Background = Panel,
        BorderBrush = Border,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = padding ?? new Thickness(12, 10),
        Child = child
    };

    public static StackPanel V(double spacing = 8) => new() { Spacing = spacing };
    public static StackPanel H(double spacing = 8) => new() { Orientation = Orientation.Horizontal, Spacing = spacing };

    public static Button Button(string text, bool accent = false) => new()
    {
        Content = text, Background = accent ? AccentBg : GButtonBg,
        BorderBrush = accent ? Accent : GButtonBorder, BorderThickness = new Thickness(1),
        Foreground = Text, CornerRadius = new CornerRadius(6),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        Padding = new Thickness(10, 8), MinHeight = 35
    };

    public static Button GButton(string icon, string label, double height = 64)
    {
        var sp = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!string.IsNullOrEmpty(icon))
        {
            sp.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            });
        }
        sp.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = FontWeight.Medium,
            Foreground = Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });

        return new Button
        {
            Content = sp,
            Background = GButtonBg,
            BorderBrush = GButtonBorder,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = height,
            Padding = new Thickness(4)
        };
    }

    public static void Select(Button b, bool selected, bool isTurbo = false)
    {
        if (selected)
        {
            b.Background = isTurbo ? TurboBg : AccentBg;
            b.BorderBrush = isTurbo ? TurboBorder : Accent;
            b.BorderThickness = new Thickness(1.5);
        }
        else
        {
            b.Background = GButtonBg;
            b.BorderBrush = GButtonBorder;
            b.BorderThickness = new Thickness(1.5);
        }
    }

    public static Grid SectionHeader(string icon, string title, Control rightControl)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 6) };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(icon))
            left.Children.Add(new TextBlock { Text = icon, FontSize = 13, Foreground = Text, VerticalAlignment = VerticalAlignment.Center });
        left.Children.Add(new TextBlock { Text = title, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = Text, VerticalAlignment = VerticalAlignment.Center });
        grid.Children.Add(left);
        Grid.SetColumn(rightControl, 1);
        grid.Children.Add(rightControl);
        return grid;
    }

    public static TextBlock Secondary(string s) => Label(s, 11, Muted);
    public static Border Divider() => new() { Background = Border, Height = 1, Margin = new Thickness(0, 2) };
    public static TextBlock Heading(string s) => Label(s, 13, Text, true);
    public static TextBlock Metric() => Label("—", 12, Accent, true);
    public static Control Space(double height) => new Border { Height = height };
}
