// SPDX-License-Identifier: GPL-3.0-only
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;

namespace TufControl;

internal sealed class AdvancedWindow : Window
{
    private readonly MainWindow _owner;
    private readonly Func<Task> _refresh;
    private readonly TextBlock _turboInfo = Ui.Secondary("Comprobando controlador de CPU…");
    private readonly TextBlock _driverInfo = Ui.Secondary("Driver: —");
    private readonly TextBlock _fanInfo = Ui.Secondary("Ventiladores: comprobando…");
    private readonly TextBlock _powerInfo = Ui.Secondary("Límites de potencia: comprobando…");
    private readonly TextBlock _status = Ui.Secondary("Solo se aplicarán cambios que confirmes.");
    private readonly Button _boostOn = Ui.Button("Activar Boost");
    private readonly Button _boostOff = Ui.Button("Desactivar Boost", true);
    private readonly TextBlock _cpuMetric = Ui.Label("CPU: —", 12);
    private readonly TextBlock _gpuMetric = Ui.Label("GPU: —", 12);
    private bool _working;
    private Snapshot? _last;

    public AdvancedWindow(MainWindow owner, Func<Task> refresh)
    {
        _owner = owner;
        _refresh = refresh;
        Width = 730;
        Height = 730;
        MinWidth = 575;
        MinHeight = 520;
        Title = "TUF Control · Fans + Power";
        Background = Ui.Bg;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var outer = Ui.V(13);
        outer.Margin = new Thickness(16);
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var left = Ui.V(2);
        left.Children.Add(Ui.Label("FANS + POWER", 20, Ui.Text, true));
        left.Children.Add(Ui.Secondary("ASUS TUF F15 · Panel avanzado · Intel Core i5-11400H"));
        top.Children.Add(left);
        var close = Ui.Button("Cerrar"); close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1); top.Children.Add(close);
        outer.Children.Add(top);

        var cpu = Ui.V(10);
        cpu.Children.Add(Ui.Heading("CPU · Intel Turbo Boost"));
        cpu.Children.Add(Ui.Secondary("Activa o desactiva el Turbo Boost real, sin modificar BIOS ni parámetros de overclock."));
        cpu.Children.Add(_turboInfo);
        cpu.Children.Add(_driverInfo);
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 8 };
        Grid.SetColumn(_boostOn, 0); actions.Children.Add(_boostOn);
        Grid.SetColumn(_boostOff, 1); actions.Children.Add(_boostOff);
        cpu.Children.Add(actions);
        _boostOn.Click += async (_, _) => await SetBoost(true);
        _boostOff.Click += async (_, _) => await SetBoost(false);
        var note = Ui.Secondary("Desactivar Boost puede bajar el calor y limitar el rendimiento de CPU. La preferencia se guarda y se restaura al arrancar Nobara.");
        cpu.Children.Add(note);
        outer.Children.Add(Ui.Card(cpu));

        var fans = Ui.V(9);
        fans.Children.Add(Ui.Heading("Ventiladores · CPU / GPU"));
        var fanGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        fanGrid.Children.Add(_cpuMetric);
        Grid.SetColumn(_gpuMetric, 1); fanGrid.Children.Add(_gpuMetric);
        fans.Children.Add(fanGrid);
        fans.Children.Add(_fanInfo);
        fans.Children.Add(Ui.Secondary("El firmware controla automáticamente la ventilación. En tu FX506HC, asusctl no publica la interfaz FanCurves, por lo que esta app no escribirá curvas ni velocidades manuales."));
        var detect = Ui.Button("Detectar soporte de curvas ASUS");
        detect.Click += async (_, _) =>
        {
            _status.Text = "Consultando capacidades de hardware…";
            var r = await Task.Run(Hardware.FanSupport);
            _status.Text = r.Ok ? "El equipo anuncia la interfaz FanCurves; se requiere validar las curvas actuales antes de modificar algo." : r.Message;
        };
        fans.Children.Add(detect);
        outer.Children.Add(Ui.Card(fans));

        var power = Ui.V(8);
        power.Children.Add(Ui.Heading("Potencia y controles avanzados"));
        power.Children.Add(_powerInfo);
        power.Children.Add(Ui.Secondary("RGB disponible desde la ventana Iluminación. PL1/PL2, MSR, overclock de NVIDIA y curvas de ventiladores siguen sin controles de escritura por seguridad."));
        outer.Children.Add(Ui.Card(power));
        outer.Children.Add(_status);

        Content = new ScrollViewer { Content = outer,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    public void Update(Snapshot s)
    {
        _last = s;
        _turboInfo.Text = s.TurboEnabled.HasValue
            ? $"Turbo Boost: {(s.TurboEnabled.Value ? "ACTIVADO" : "DESACTIVADO")}" +
              (s.SavedTurboEnabled.HasValue ? $" · Guardado: {(s.SavedTurboEnabled.Value ? "ACTIVADO" : "DESACTIVADO")}" : " · Sin preferencia guardada")
            : "No se detectó una interfaz compatible con Turbo Boost";
        _driverInfo.Text = "Controlador CPU: " + s.CpuDriver;
        _boostOn.IsEnabled = _boostOff.IsEnabled = !_working && s.TurboEnabled.HasValue && Hardware.BoostControlInstalled;
        Ui.Select(_boostOn, s.TurboEnabled == true);
        Ui.Select(_boostOff, s.TurboEnabled == false);
        _cpuMetric.Text = $"CPU: {(s.CpuFan.HasValue ? $"{s.CpuFan} RPM" : "sin lectura")}";
        _gpuMetric.Text = $"GPU: {(s.GpuFan.HasValue ? $"{s.GpuFan} RPM" : "sin lectura")}";
        _fanInfo.Text = s.Capabilities.FanCurves ? "Interfaz FanCurves detectada: edición deshabilitada por seguridad." : "Curvas CPU/GPU: no compatibles con asusctl en este FX506HC.";
        var rapl = Directory.Exists("/sys/class/powercap/intel-rapl");
        _powerInfo.Text = rapl ? "Intel RAPL detectado · modo solo lectura en v0.2"
            : "Intel RAPL no detectado · límites de potencia no disponibles";
        if (!Hardware.BoostControlInstalled) _status.Text = "Ejecuta install.sh de v0.4 para habilitar Boost persistente.";
    }

    private async Task SetBoost(bool enabled)
    {
        if (_working || _last?.TurboEnabled is null || !Hardware.BoostControlInstalled) return;
        var confirmation = new ConfirmWindow("Intel Turbo Boost",
            (enabled ? "¿Activar" : "¿Desactivar") + " Turbo Boost del Intel i5-11400H?\n\n" +
            (enabled ? "El procesador podrá alcanzar frecuencias más altas y generar más calor."
                : "El procesador limitará los picos de frecuencia; puede reducir temperaturas y FPS.") +
            "\n\nKDE solicitará autorización. La elección se conservará tras reiniciar.", "Aplicar");
        if (!await confirmation.ShowDialog<bool>(this)) return;
        _working = true;
        _boostOn.IsEnabled = _boostOff.IsEnabled = false;
        _status.Text = "Esperando autorización…";
        try
        {
            var r = await Hardware.SetTurbo(enabled);
            _status.Text = r.Ok ? $"Turbo Boost {(enabled ? "activado" : "desactivado")} y preferencia guardada." : "No se pudo cambiar: " + r.Message;
        }
        catch (Exception e) { _status.Text = "Error: " + e.Message; }
        finally { _working = false; await _refresh(); }
    }

    private static Control CreateCurvePreview()
    {
        // Solo gráfica estática; NO existe funcionalidad de escritura de curva.
        var canvas = new Canvas { Width = 560, Height = 132, HorizontalAlignment = HorizontalAlignment.Left };
        var xs = new[] { 10d, 80d, 150d, 220d, 290d, 360d, 430d, 500d };
        var cpuY = new[] { 111d, 109d, 101d, 85d, 63d, 37d, 17d, 8d };
        var gpuY = new[] { 119d, 118d, 112d, 94d, 75d, 49d, 30d, 13d };
        for (var i = 0; i < 4; i++)
        {
            var y = 12 + i * 35;
            canvas.Children.Add(new Line { StartPoint = new Point(10, y), EndPoint = new Point(520, y),
                Stroke = Ui.Border, StrokeThickness = 1 });
        }
        for (int i = 1; i < xs.Length; i++)
        {
            canvas.Children.Add(new Line { StartPoint = new Point(xs[i - 1], cpuY[i - 1]),
                EndPoint = new Point(xs[i], cpuY[i]), Stroke = Ui.Accent, StrokeThickness = 2.5 });
            canvas.Children.Add(new Line { StartPoint = new Point(xs[i - 1], gpuY[i - 1]),
                EndPoint = new Point(xs[i], gpuY[i]), Stroke = Ui.Warning, StrokeThickness = 2.5 });
        }
        for (int i = 0; i < xs.Length; i++)
        {
            AddDot(canvas, xs[i], cpuY[i], Ui.Accent);
            AddDot(canvas, xs[i], gpuY[i], Ui.Warning);
        }
        return new ScrollViewer { Content = canvas,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    private static void AddDot(Canvas canvas, double x, double y, Avalonia.Media.IBrush color)
    {
        var dot = new Ellipse { Width = 7, Height = 7, Fill = color };
        Canvas.SetLeft(dot, x - 3.5); Canvas.SetTop(dot, y - 3.5);
        canvas.Children.Add(dot);
    }
}
