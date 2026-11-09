using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using ShadeFlow.Models;

namespace ShadeFlow.Services;

public enum ConfigurationLoadState
{
    Loaded,
    Migrated,
    RecoveredFromBackup,
    Defaults,
    FutureVersion
}

public sealed class ConfigurationService
{
    private const int MaxAliasLength = 80;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly string _backupPath;
    private readonly bool _readOnly;
    private AppSettings _settings;
    private bool _futureVersion;
    private ConfigurationLoadState _loadState;

    public ConfigurationService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ShadeFlow",
        "settings.json"), readOnly: false)
    {
    }

    public ConfigurationService(bool readOnly) : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ShadeFlow",
        "settings.json"), readOnly)
    {
    }

    internal ConfigurationService(string settingsPath, bool readOnly = false)
    {
        _settingsPath = settingsPath;
        _backupPath = Path.Combine(Path.GetDirectoryName(settingsPath)!, "settings.backup.json");
        _readOnly = readOnly;
        _settings = Load(out var migrated);
        if (migrated && !_readOnly)
        {
            try { Save(); }
            catch (Exception ex) when (IsFileError(ex))
            {
                Trace.TraceError("Settings migration save failed: {0}", ex);
                LogService.Current?.Error("Settings migration save failed.", ex);
            }
        }
    }

    public string SettingsPath => _settingsPath;
    public string BackupPath => _backupPath;
    public ConfigurationLoadState LoadState => _loadState;
    public bool CanWrite => !_futureVersion && !_readOnly;

    public Preferences GetPreferences() => (_settings.Preferences ?? new Preferences()).Normalize();

    public IReadOnlyList<BrightnessPreset> GetPresets() => (_settings.Presets ?? []).Select(p => p.Copy()).ToArray();

    public bool TrySetPresets(IEnumerable<BrightnessPreset> presets)
    {
        if (!CanWrite) return false;
        var next = presets.ToList();
        if (next.Count > 24 || next.Any(p => p is null || !p.IsValid) ||
            next.Select(p => p.Id).Distinct().Count() != next.Count ||
            next.Select(p => p.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != next.Count) return false;
        var previous = _settings.Presets;
        _settings.Presets = next.Select(p => p.Copy()).ToList();
        try { Save(); return true; }
        catch (Exception ex) when (IsFileError(ex))
        {
            _settings.Presets = previous;
            LogService.Current?.Error("Preset save failed.", ex);
            return false;
        }
    }

    public bool TrySetPreferences(Preferences preferences)
    {
        if (!CanWrite) return false;
        if (preferences != preferences.Normalize()) return false;
        var previous = _settings.Preferences;
        _settings.Preferences = preferences;
        try { Save(); return true; }
        catch (Exception ex) when (IsFileError(ex))
        {
            Trace.TraceError("Preferences save failed: {0}", ex);
            LogService.Current?.Error("Preferences save failed.", ex);
            _settings.Preferences = previous;
            return false;
        }
    }

    public string? GetDisplayAlias(string stableId)
    {
        var displays = _settings.Displays;
        if (displays is null || !displays.TryGetValue(stableId, out var display) || display is null)
            return null;
        return NormalizeAlias(display.Alias);
    }

    public bool TrySetDisplayAlias(string stableId, string? alias)
    {
        if (!CanWrite) return false;
        if (string.IsNullOrWhiteSpace(stableId)) return false;

        var normalized = NormalizeAlias(alias);
        _settings.Displays ??= new Dictionary<string, DisplaySettings>();
        var hadPrevious = _settings.Displays.TryGetValue(stableId, out var previous);
        var previousAlias = previous?.Alias;

        if (NormalizeAlias(previousAlias) == normalized)
            return true;

        if (normalized is null)
            _settings.Displays.Remove(stableId);
        else
            _settings.Displays[stableId] = new DisplaySettings { Alias = normalized };

        try
        {
            Save();
            return true;
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            Trace.TraceError("Settings save failed: {0}", ex);
            LogService.Current?.Error("Display alias save failed.", ex);
            if (hadPrevious)
                _settings.Displays[stableId] = new DisplaySettings { Alias = previousAlias };
            else
                _settings.Displays.Remove(stableId);
            return false;
        }
    }

    public ScheduleConfiguration GetSchedule()
    {
        var schedule = _settings.Schedule ?? new ScheduleSettings();
        var entries = ParseAndSortEntries(schedule.Entries, logInvalid: false);
        return new ScheduleConfiguration(schedule.Enabled, entries);
    }

    public bool TrySetSchedule(bool enabled, IEnumerable<ScheduleEntry> entries)
    {
        if (!CanWrite) return false;
        var ordered = entries.OrderBy(entry => entry.Time).ToArray();
        if (ordered.Any(entry => entry.Brightness is < 0 or > 100) ||
            ordered.Select(entry => entry.Time).Distinct().Count() != ordered.Length)
            return false;

        var previous = _settings.Schedule;
        _settings.Schedule = new ScheduleSettings
        {
            Enabled = enabled,
            Entries = ordered.Select(entry => new ScheduleEntrySettings
            {
                Time = entry.TimeText,
                Brightness = entry.Brightness
            }).ToList()
        };

        try
        {
            Save();
            return true;
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            Trace.TraceError("Schedule save failed: {0}", ex);
            LogService.Current?.Error("Schedule save failed.", ex);
            _settings.Schedule = previous;
            return false;
        }
    }

    public bool TryExport(string destinationPath, out string error)
    {
        error = string.Empty;
        if (!CanWrite)
        {
            error = "A configuração local pertence a uma versão futura e não será sobrescrita.";
            return false;
        }
        try
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new IOException("Caminho de exportação inválido.");
            Directory.CreateDirectory(directory);
            File.WriteAllText(destinationPath, JsonSerializer.Serialize(_settings, JsonOptions));
            return true;
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryImport(string sourcePath, bool actualStartupEnabled, out string error)
    {
        error = string.Empty;
        if (!CanWrite)
        {
            error = "A configuração local pertence a uma versão futura e não pode ser substituída.";
            return false;
        }
        try
        {
            var imported = ReadAndNormalize(sourcePath, out var migrated, out var futureVersion);
            if (futureVersion)
            {
                error = "O arquivo importado foi criado por uma versão mais nova do ShadeFlow.";
                return false;
            }
            imported.Preferences = (imported.Preferences ?? new Preferences()) with
            {
                StartWithWindows = actualStartupEnabled
            };
            var previous = _settings;
            _settings = imported;
            try { Save(); }
            catch
            {
                _settings = previous;
                throw;
            }
            _loadState = migrated ? ConfigurationLoadState.Migrated : ConfigurationLoadState.Loaded;
            return true;
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryReset(out string error)
    {
        error = string.Empty;
        if (!CanWrite)
        {
            error = "A configuração local pertence a uma versão futura e não pode ser substituída.";
            return false;
        }
        var previous = _settings;
        _settings = CreateDefault();
        try { Save(); return true; }
        catch (Exception ex) when (IsFileError(ex))
        {
            _settings = previous;
            error = ex.Message;
            return false;
        }
    }

    private AppSettings Load(out bool migrated)
    {
        migrated = false;
        if (!File.Exists(_settingsPath))
        {
            _loadState = ConfigurationLoadState.Defaults;
            return CreateDefault();
        }

        try
        {
            var loaded = ReadAndNormalize(_settingsPath, out migrated, out var futureVersion);
            if (futureVersion)
            {
                _futureVersion = true;
                _loadState = ConfigurationLoadState.FutureVersion;
                Trace.TraceWarning("Settings version is newer than supported; preserving {0}", _settingsPath);
                LogService.Current?.Warn("Future settings version was preserved.");
                return CreateDefault();
            }
            _loadState = migrated ? ConfigurationLoadState.Migrated : ConfigurationLoadState.Loaded;
            return loaded;
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            Trace.TraceError("Settings load failed; trying backup: {0}", ex);
            LogService.Current?.Warn("Primary settings invalid; trying backup.");
            try
            {
                var recovered = ReadAndNormalize(_backupPath, out migrated, out var futureVersion);
                if (futureVersion) throw new JsonException("Backup belongs to a future settings version.");
                _loadState = ConfigurationLoadState.RecoveredFromBackup;
                if (!_readOnly) RestorePrimaryFromBackup(recovered);
                Trace.TraceWarning("Settings recovered from backup.");
                LogService.Current?.Warn("Settings recovered from backup.");
                return recovered;
            }
            catch (Exception backupEx) when (IsFileError(backupEx))
            {
                Trace.TraceError("Settings backup recovery failed; defaults will be used: {0}", backupEx);
                LogService.Current?.Warn("Settings and backup invalid; using defaults.");
            }
            _loadState = ConfigurationLoadState.Defaults;
            return CreateDefault();
        }
    }

    private static AppSettings ReadAndNormalize(string path, out bool migrated, out bool futureVersion)
    {
        migrated = false;
        futureVersion = false;
        var json = File.ReadAllText(path);
        var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
            ?? throw new JsonException("Missing settings content.");
        if (loaded.Version > AppSettings.CurrentVersion && loaded.Version != 4)
        {
            futureVersion = true;
            return CreateDefault();
        }
        if (loaded.Version == 1)
        {
            loaded.Version = 2;
            loaded.Schedule = new ScheduleSettings();
            migrated = true;
        }
        if (loaded.Version == 2)
        {
            loaded.Version = 3;
            loaded.Preferences = new Preferences();
            migrated = true;
        }
        if (loaded.Version == 4)
        {
            // V4 was an unreleased Warmth experiment. Extra fields are intentionally ignored;
            // saving later returns this one-off compatibility input to the released V3 schema.
            loaded.Version = 3;
            migrated = true;
            LogService.Current?.Info("Downgrading experimental V4 settings to released V3.");
        }
        if (loaded.Version != AppSettings.CurrentVersion)
            throw new JsonException("Unsupported settings version.");

        loaded.Displays ??= new Dictionary<string, DisplaySettings>();
        // Optional V3 extension; old settings start with an empty preset list.
        loaded.Presets = (loaded.Presets ?? []).Where(p => p is not null && p.IsValid)
            .DistinctBy(p => p.Id).DistinctBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase).Take(24).ToList();
        loaded.Preferences = (loaded.Preferences ?? new Preferences()).Normalize();
        loaded.Schedule ??= new ScheduleSettings();
        loaded.Schedule.Entries = ParseAndSortEntries(loaded.Schedule.Entries, logInvalid: true)
            .Select(entry => new ScheduleEntrySettings { Time = entry.TimeText, Brightness = entry.Brightness }).ToList();
        return loaded;
    }

    private void Save()
    {
        if (_futureVersion || _readOnly) throw new IOException("Settings are read-only in this execution.");
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"settings.{Guid.NewGuid():N}.tmp");

        try
        {
            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(temporaryPath, json);
            BackupCurrentFileIfValid();
            File.Move(temporaryPath, _settingsPath, true);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Temporary settings cleanup failed: {0}", ex);
            }
        }
    }

    private void BackupCurrentFileIfValid()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            _ = ReadAndNormalize(_settingsPath, out _, out var futureVersion);
            if (!futureVersion) File.Copy(_settingsPath, _backupPath, true);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            // An externally-corrupted primary must never replace a known-good backup.
            Trace.TraceWarning("Existing settings were not backed up: {0}", ex.Message);
        }
    }

    private void RestorePrimaryFromBackup(AppSettings recovered)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"settings.recovery.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(recovered, JsonOptions));
            File.Move(temporaryPath, _settingsPath, true);
        }
        finally
        {
            try { File.Delete(temporaryPath); } catch (Exception) { }
        }
    }

    private static AppSettings CreateDefault() => new()
    {
        Version = AppSettings.CurrentVersion,
        Displays = new Dictionary<string, DisplaySettings>(),
        Schedule = new ScheduleSettings()
    };

    private static IReadOnlyList<ScheduleEntry> ParseAndSortEntries(
        IEnumerable<ScheduleEntrySettings>? settings,
        bool logInvalid)
    {
        var entries = new List<ScheduleEntry>();
        var usedTimes = new HashSet<TimeOnly>();
        foreach (var setting in settings ?? [])
        {
            if (setting is null ||
                !TimeOnly.TryParseExact(setting.Time, "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var time) ||
                setting.Brightness is < 0 or > 100 ||
                !usedTimes.Add(time))
            {
                if (logInvalid)
                    Trace.TraceWarning("Ignoring invalid or duplicate schedule entry.");
                continue;
            }
            entries.Add(new ScheduleEntry(time, setting.Brightness));
        }
        return entries.OrderBy(entry => entry.Time).ToArray();
    }

    private static string? NormalizeAlias(string? alias)
    {
        var normalized = alias?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        return normalized.Length <= MaxAliasLength ? normalized : normalized[..MaxAliasLength];
    }

    private static bool IsFileError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException;
}
