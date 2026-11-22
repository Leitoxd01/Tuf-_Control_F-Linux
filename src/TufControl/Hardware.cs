// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TufControl;

internal sealed record CommandResult(bool Ok, string Output, string Error)
{
    public string Message => Ok ? Output : (string.IsNullOrWhiteSpace(Error) ? "No disponible" : Error);
}

internal sealed record HardwareCapabilities(bool Aura, bool Backlight, bool FanCurves, IReadOnlyList<string> AuraModes);

internal sealed record Snapshot(
    double? Cpu, double? Gpu, int? CpuFan, int? GpuFan,
    IReadOnlyList<double> Disks, string GpuRuntime,
    string Profile, IReadOnlyList<string> AvailableProfiles,
    int? BatteryLevel, string BatteryState, int? BatteryThreshold,
    bool BatteryThresholdSupported, bool? TurboEnabled, bool? SavedTurboEnabled, string TurboPath,
    string CpuDriver, string ScreenModes, bool? AcConnected, HardwareCapabilities Capabilities, string LedBrightness);

internal static class Hardware
{
    private const string NvidiaRuntime = "/sys/bus/pci/devices/0000:01:00.0/power/runtime_status";
    private const string IntelTurbo = "/sys/devices/system/cpu/intel_pstate/no_turbo";
    private const string GenericTurbo = "/sys/devices/system/cpu/cpufreq/boost";
    private const string Helper = "/usr/local/libexec/tuf-control-helper";
    private const string BoostCommand = "/usr/local/bin/tuf-control-boost";
    private const string SavedBoost = "/etc/tuf-control/boost.state";
    private static readonly Lazy<HardwareCapabilities> Features = new(DetectFeatures);
    private static readonly Lazy<IReadOnlyList<string>> Profiles = new(DetectProfiles);
    private static HardwareCapabilities DetectFeatures()
    {
        var info = Run("asusctl", 6000, "info", "--show-supported");
        if (!info.Ok) return new(false, false, false, Array.Empty<string>());
        var modes = new List<string>();
        var inModes = false;
        foreach (var line in info.Output.Split('\n'))
        {
            if (line.StartsWith("Supported Aura Modes:", StringComparison.Ordinal)) { inModes = true; continue; }
            if (inModes)
            {
                if (line.Contains(']')) break;
                var m = Regex.Match(line, @"\b(Static|Breathe|RainbowCycle|RainbowWave|Pulse)\b");
                if (m.Success) modes.Add(m.Groups[1].Value);
            }
        }
        return new(info.Output.Contains("\"xyz.ljones.Aura\"", StringComparison.Ordinal),
            info.Output.Contains("\"xyz.ljones.Backlight\"", StringComparison.Ordinal),
            info.Output.Contains("\"xyz.ljones.FanCurves\"", StringComparison.Ordinal), modes);
    }
    public static HardwareCapabilities Capabilities => Features.Value;

    private static IReadOnlyList<string> DetectProfiles()
    {
        var r = Run("asusctl", 3500, "profile", "list");
        if (!r.Ok) return Array.Empty<string>();
        return Regex.Matches(r.Output, @"\b(LowPower|Balanced|Performance|Quiet|Custom)\b")
            .Cast<Match>().Select(m => m.Value).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool? ReadAc()
    {
        try
        {
            foreach (var dir in Directory.GetDirectories("/sys/class/power_supply"))
            {
                var kind = Read(Path.Combine(dir, "type"));
                if (kind is "Mains" or "USB_PD" or "USB_C")
                {
                    var online = Read(Path.Combine(dir, "online"));
                    if (online is "0" or "1") return online == "1";
                }
            }
        }
        catch { }
        return null;
    }

    public static string? Read(string path)
    {
        try { return File.ReadAllText(path).Trim(); }
        catch { return null; }
    }

    public static CommandResult Run(string executable, int timeoutMs = 5000, params string[] arguments)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var arg in arguments) p.StartInfo.ArgumentList.Add(arg);
            p.Start();
            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* already exited */ }
                return new(false, "", $"Tiempo de espera agotado: {Path.GetFileName(executable)}");
            }
            Task.WaitAll(stdoutTask, stderrTask);
            return new(p.ExitCode == 0, stdoutTask.Result.Trim(), stderrTask.Result.Trim());
        }
        catch (Exception ex) { return new(false, "", ex.Message); }
    }

    public static async Task<CommandResult> RunAsync(string executable, int timeoutMs = 120000, params string[] arguments)
        => await Task.Run(() => Run(executable, timeoutMs, arguments));

    public static Snapshot Collect()
    {
        double? cpu = null, gpu = null;
        int? cpuFan = null, gpuFan = null;
        var disks = new List<double>();
        var sensorText = Run("sensors", 3500);
        var block = "";
        if (sensorText.Ok)
        {
            foreach (var line in sensorText.Output.Split('\n'))
            {
                if (line.Length > 0 && !char.IsWhiteSpace(line[0]) &&
                    !line.StartsWith("Adapter:", StringComparison.Ordinal) && !line.Contains(':'))
                    block = line.Trim();
                var temperature = Regex.Match(line, @"\+([0-9]+(?:\.[0-9]+)?)\s*°C");
                double? t = temperature.Success && double.TryParse(temperature.Groups[1].Value,
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
                var fan = Regex.Match(line, @"([0-9]+)\s*RPM");
                int? rpm = fan.Success && int.TryParse(fan.Groups[1].Value, out var r) ? r : null;
                if (block.StartsWith("coretemp") && line.Contains("Package id") && t.HasValue) cpu = t;
                if (block.StartsWith("nvme") && line.Contains("Composite:") && t.HasValue) disks.Add(t.Value);
                if (block.StartsWith("asus") && line.Contains("cpu_fan:") && rpm.HasValue) cpuFan = rpm;
                if (block.StartsWith("asus") && line.Contains("gpu_fan:") && rpm.HasValue) gpuFan = rpm;
            }
        }
        var runtime = Read(NvidiaRuntime) ?? "desconocido";
        if (runtime == "active")
        {
            var n = Run("nvidia-smi", 2500, "--query-gpu=temperature.gpu", "--format=csv,noheader,nounits");
            if (n.Ok && double.TryParse(n.Output.Split('\n').FirstOrDefault(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var nv)) gpu = nv;
        }

        var profileOutput = Run("asusctl", 3500, "profile", "get");
        var match = Regex.Match(profileOutput.Output, @"Active profile:\s*(\w+)");
        var profile = match.Success ? match.Groups[1].Value : "No disponible";
        var allowedProfiles = Profiles.Value;

        int? percent = null, threshold = null;
        var batteryState = "No disponible";
        var supported = false;
        try
        {
            foreach (var dir in Directory.GetDirectories("/sys/class/power_supply", "BAT*"))
            {
                var c = Read(Path.Combine(dir, "capacity"));
                if (int.TryParse(c, out var capacity)) percent = capacity;
                batteryState = Read(Path.Combine(dir, "status")) ?? "No disponible";
                var control = Path.Combine(dir, "charge_control_end_threshold");
                supported = File.Exists(control);
                if (int.TryParse(Read(control), out var current)) threshold = current;
                break;
            }
        }
        catch { /* no sysfs available */ }

        string turboPath = "", driver = Read("/sys/devices/system/cpu/cpu0/cpufreq/scaling_driver") ?? "Desconocido";
        bool? turbo = null;
        var it = Read(IntelTurbo);
        if (it is "0" or "1") { turboPath = IntelTurbo; turbo = it == "0"; }
        else
        {
            var gen = Read(GenericTurbo);
            if (gen is "0" or "1") { turboPath = GenericTurbo; turbo = gen == "1"; }
        }
        var saved = Read(SavedBoost);
        bool? savedTurbo = saved switch { "on" => true, "off" => false, _ => null };
        return new Snapshot(cpu, gpu, cpuFan, gpuFan, disks, runtime, profile, allowedProfiles,
            percent, batteryState, threshold, supported, turbo, savedTurbo, turboPath, driver,
            "No consultado", ReadAc(), Capabilities,
            Capabilities.Backlight ? Run("asusctl", 2500, "leds", "get").Output : "No disponible");
    }

    public static bool HelperInstalled => File.Exists(Helper);
    public static bool BoostControlInstalled => HelperInstalled && File.Exists(BoostCommand);
    public static Task<CommandResult> SetTurbo(bool enabled)
        => RunAsync("pkexec", 120000, Helper, "turbo", enabled ? "on" : "off");

    public static Task<CommandResult> SetBatteryLimit(int limit)
        => RunAsync("pkexec", 120000, Helper, "battery", limit.ToString(CultureInfo.InvariantCulture));

    public static Task<CommandResult> SetProfile(string profile)
        => RunAsync("asusctl", 20000, "profile", "set", profile);

    public static CommandResult FanSupport()
        => new(Capabilities.FanCurves, Capabilities.FanCurves ? "Interfaz FanCurves disponible" :
            "El FX506HC no expone xyz.ljones.FanCurves a través de asusctl.", "");

    public static Task<CommandResult> SetBrightness(string level)
    {
        if (!Capabilities.Backlight || !new[] { "off", "low", "med", "high" }.Contains(level))
            return Task.FromResult(new CommandResult(false, "", "Brillo no compatible o valor inválido."));
        return RunAsync("asusctl", 12000, "leds", "set", level);
    }

    public static Task<CommandResult> SetLighting(string mode, string color)
    {
        if (!Capabilities.Aura || !Capabilities.AuraModes.Contains(mode))
            return Task.FromResult(new CommandResult(false, "", "Efecto Aura no compatible con este equipo."));
        if (!Regex.IsMatch(color, "^[a-fA-F0-9]{6}$"))
            return Task.FromResult(new CommandResult(false, "", "Color RGB inválido (6 caracteres hexadecimales)."));
        string[] args = mode switch
        {
            "Static" => new[] { "aura", "effect", "static", "-c", color },
            "Breathe" => new[] { "aura", "effect", "breathe", "--colour", color, "--colour2", "000000", "--speed", "med" },
            "RainbowCycle" => new[] { "aura", "effect", "rainbow-cycle", "--speed", "med" },
            "RainbowWave" => new[] { "aura", "effect", "rainbow-wave", "--direction", "left", "--speed", "med" },
            "Pulse" => new[] { "aura", "effect", "pulse", "-c", color },
            _ => Array.Empty<string>()
        };
        return args.Length == 0 ? Task.FromResult(new CommandResult(false, "", "Modo inválido")) : RunAsync("asusctl", 15000, args);
    }
}
