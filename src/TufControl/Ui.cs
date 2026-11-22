// SPDX-License-Identifier: GPL-3.0-only
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TufControl;

internal static class Ui
{
    public static readonly IBrush Bg = Brush("#11141B");
    public static readonly IBrush Panel = Brush("#1D2531");
    public static readonly IBrush Elevated = Brush("#263244");
    public static readonly IBrush Muted = Brush("#9DADC0");
    public static readonly IBrush Text = Brush("#E9F0F8");
    public static readonly IBrush Accent = Brush("#42CBED");
    public static readonly IBrush Border = Brush("#37475B");
    public static readonly IBrush Green = Brush("#59D5A0");
    public static readonly IBrush Warning = Brush("#FFBE77");

    public static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
    public static TextBlock Label(string text, double size = 12, IBrush? color = null, bool bold = false) =>
        new() { Text = text, FontSize = size, Foreground = color ?? Text,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap };

    public static Border Card(Control child, Thickness? padding = null) => new()
    {
        Background = Panel,
        BorderBrush = Border,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = padding ?? new Thickness(13),
        Child = child
    };
    public static StackPanel V(double spacing = 9) => new() { Spacing = spacing };
    public static StackPanel H(double spacing = 7) => new() { Orientation = Orientation.Horizontal, Spacing = spacing };
    public static Button Button(string text, bool accent = false) => new()
    {
        Content = text, Background = accent ? Brush("#15596D") : Elevated,
        BorderBrush = accent ? Accent : Border, BorderThickness = new Thickness(1),
        Foreground = Text, CornerRadius = new CornerRadius(7),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        Padding = new Thickness(12, 10), MinHeight = 39
    };
    public static void Select(Button b, bool selected)
    {
        b.Background = selected ? Brush("#15596D") : Elevated;
        b.BorderBrush = selected ? Accent : Border;
    }
    public static TextBlock Secondary(string s) => Label(s, 11, Muted);
    public static Border Divider() => new() { Background = Border, Height = 1, Margin = new Thickness(0, 3) };
    public static TextBlock Heading(string s) => Label(s, 14, Text, true);
    public static TextBlock Metric() => Label("—", 23, Accent, true);
    public static Control Space(double height) => new Border { Height = height };
}
