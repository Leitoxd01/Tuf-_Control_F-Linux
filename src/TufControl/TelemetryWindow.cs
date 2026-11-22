// SPDX-License-Identifier: GPL-3.0-only
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace TufControl;

internal sealed record TelemetrySample(DateTime At, double? Cpu, double? Gpu, double? Ssd, int? CpuFan, int? GpuFan);

internal sealed class TelemetryWindow : Window
{
    private readonly Canvas _temps = new() { Width = 580, Height = 178 };
    private readonly Canvas _fans = new() { Width = 580, Height = 178 };
    private readonly TextBlock _summary = Ui.Secondary("Esperando las primeras lecturas de sensores…");

    public TelemetryWindow()
    {
        Title = "TUF Control · Monitoreo"; Background = Ui.Bg;
        Width = 665; Height = 545; MinWidth = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var outer = Ui.V(13); outer.Margin = new Thickness(17);
        outer.Children.Add(Ui.Heading("MONITOREO EN TIEMPO REAL"));
        outer.Children.Add(Ui.Secondary("Hasta 5 minutos de historial · lecturas cada 5 segundos · solo lectura"));
        var temp = Ui.V(8);
        temp.Children.Add(Ui.Heading("CPU (celeste) · SSD más caliente (naranja) · GPU activa (verde)"));
        temp.Children.Add(_temps);
        outer.Children.Add(Ui.Card(temp));
        var fans = Ui.V(8);
        fans.Children.Add(Ui.Heading("RPM CPU (celeste) · RPM GPU (naranja)"));
        fans.Children.Add(_fans);
        outer.Children.Add(Ui.Card(fans));
        outer.Children.Add(_summary);
        Content = new ScrollViewer { Content = outer };
    }

    public void Update(IReadOnlyList<TelemetrySample> samples)
    {
        Draw(_temps, samples, 110, new[] {
            (new Func<TelemetrySample, double?>(s => s.Cpu), Ui.Accent),
            (new Func<TelemetrySample, double?>(s => s.Ssd), Ui.Warning),
            (new Func<TelemetrySample, double?>(s => s.Gpu), Ui.Green)
        });
        Draw(_fans, samples, 6500, new[] {
            (new Func<TelemetrySample, double?>(s => s.CpuFan), Ui.Accent),
            (new Func<TelemetrySample, double?>(s => s.GpuFan), Ui.Warning)
        });
        if (samples.Count > 0)
        {
            var latest = samples[^1];
            _summary.Text = $"Última lectura {latest.At:HH:mm:ss} · CPU {Format(latest.Cpu, "°C")} · " +
                $"SSD {Format(latest.Ssd, "°C")} · GPU {(latest.Gpu.HasValue ? Format(latest.Gpu, "°C") : "suspendida/sin dato")}";
        }
    }

    private static string Format(double? value, string unit) => value.HasValue ? $"{value.Value:0.#} {unit}" : "—";

    private static void Draw(Canvas canvas, IReadOnlyList<TelemetrySample> samples, double upper,
        IEnumerable<(Func<TelemetrySample, double?> GetValue, IBrush Color)> series)
    {
        canvas.Children.Clear();
        const double left = 22, top = 12, width = 535, height = 146;
        for (int i = 0; i <= 4; i++)
        {
            var y = top + height * i / 4;
            canvas.Children.Add(new Line { StartPoint = new Point(left, y), EndPoint = new Point(left + width, y),
                Stroke = Ui.Border, StrokeThickness = 1 });
        }
        if (samples.Count < 2) return;
        // X always spans the last 60 samples, avoiding a graph that jumps with each refresh.
        var start = Math.Max(0, samples.Count - 60);
        var view = samples.Skip(start).ToArray();
        foreach (var (valueAt, brush) in series)
        {
            for (int i = 1; i < view.Length; i++)
            {
                var before = valueAt(view[i - 1]);
                var after = valueAt(view[i]);
                if (!before.HasValue || !after.HasValue) continue; // gaps when NVIDIA suspended
                var x1 = left + width * (i - 1) / 59d;
                var x2 = left + width * i / 59d;
                var y1 = top + height * (1 - Math.Clamp(before.Value, 0, upper) / upper);
                var y2 = top + height * (1 - Math.Clamp(after.Value, 0, upper) / upper);
                canvas.Children.Add(new Line { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2),
                    Stroke = brush, StrokeThickness = 2.5 });
            }
        }
    }
}
