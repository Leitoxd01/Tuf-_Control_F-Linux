// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;

namespace TufControl;

internal sealed class Preset
{
    public string AsusProfile { get; set; } = "Balanced";
}

internal sealed class AppSettings
{
    public Preset Cool { get; set; } = new() { AsusProfile = "Balanced" };
    public Preset Balanced { get; set; } = new() { AsusProfile = "Balanced" };
    public Preset Gaming { get; set; } = new() { AsusProfile = "Performance" };
    public bool AutoByPower { get; set; } = false;
    public bool AutoAtStart { get; set; } = false;
    public string AcPreset { get; set; } = "Gaming";
    public string BatteryPreset { get; set; } = "Fresco";
    public bool StartWithDesktop { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoRefreshRate { get; set; } = false;
    public string LastKeyboardColor { get; set; } = "00D4FF";
    public string LastKeyboardMode { get; set; } = "Static";
    public string LastBrightness { get; set; } = "med";

    public Preset GetPreset(string name) => name switch
    {
        "Fresco" => Cool,
        "Gaming" => Gaming,
        _ => Balanced
    };
}

internal static class SettingsStore
{
    private static string ConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "tuf-control");
    private static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");
    private static string AutostartFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart", "tuf-control.desktop");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile)) ?? new AppSettings();
            settings.Cool ??= new Preset { AsusProfile = "Balanced" };
            settings.Balanced ??= new Preset { AsusProfile = "Balanced" };
            settings.Gaming ??= new Preset { AsusProfile = "Performance" };
            return settings;
        }
        catch { return new AppSettings(); }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var file = SettingsFile + ".tmp";
        File.WriteAllText(file, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(file, SettingsFile, true);
    }

    // Only manages this application's own autostart entry; never changes system-wide services.
    public static void SetAutostart(bool enabled)
    {
        if (!enabled)
        {
            if (File.Exists(AutostartFile)) File.Delete(AutostartFile);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(AutostartFile)!);
        var launch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "tuf-control");
        // Desktop Exec fields are not shell commands; escape backslashes and quotes.
        launch = launch.Replace("\\", "\\\\").Replace("\"", "\\\"");
        File.WriteAllText(AutostartFile,
            "[Desktop Entry]\nType=Application\nName=TUF Control\n" +
            "Exec=\"" + launch + "\"\nTerminal=false\n" +
            "X-GNOME-Autostart-enabled=true\nOnlyShowIn=KDE;\n");
    }
}
