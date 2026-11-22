// SPDX-License-Identifier: GPL-3.0-only
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TufControl;

internal sealed class ConfirmWindow : Window
{
    public ConfirmWindow(string title, string message, string acceptText = "Confirmar")
    {
        Title = title;
        Width = 415;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        Background = Ui.Bg;
        var column = Ui.V(17);
        column.Children.Add(Ui.Heading(title));
        column.Children.Add(Ui.Label(message, 13));
        var bar = Ui.H(10);
        bar.HorizontalAlignment = HorizontalAlignment.Right;
        var cancel = Ui.Button("Cancelar");
        cancel.Click += (_, _) => Close(false);
        var accept = Ui.Button(acceptText, true);
        accept.Click += (_, _) => Close(true);
        bar.Children.Add(cancel);
        bar.Children.Add(accept);
        column.Children.Add(bar);
        Content = new Border { Padding = new Thickness(20), Child = column };
    }
}
