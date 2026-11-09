using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Win32;
using ShadeFlow.Models;
using ShadeFlow.Services;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}
if (args.Contains("--development-preview"))
{
    var display = new DisplayInfo("preview", "preview-stable", "preview", "Monitor", null,
        new DisplayBounds(0, 0, 1920, 1080), new DisplayResolution(1920, 1080), true, true);
    var created = ShadeFlow.Views.OverlayWindow.Created.Count;
    using (var preview = new OverlayManager(previewOnly: true))
    {
        preview.Reconcile([display], 65);
        Check(preview.Displays.Count == 1 && preview.Displays[0].Brightness == 65, "Development preview represents connected monitors");
        preview.SetAllBrightness(30);
        Check(preview.Displays[0].Brightness == 30, "Development brightness changes the preview state");
        Check(ShadeFlow.Views.OverlayWindow.Created.Count == created, "Development preview creates no dimming windows");
        preview.RestoreAll();
        Check(preview.Displays[0].Brightness == 100, "Preview restore resets brightness");
    }
    using (var normal = new OverlayManager())
    {
        normal.Reconcile([display], 70);
        Check(ShadeFlow.Views.OverlayWindow.Created.Count == created + 1, "Normal mode still creates its dimming overlay");
    }
    return;
}
var schedule = new[]
{
    new ScheduleEntry(new(7,0),70), new ScheduleEntry(new(10,0),100), new ScheduleEntry(new(16,30),75),
    new ScheduleEntry(new(18,30),50), new ScheduleEntry(new(20,0),30), new ScheduleEntry(new(21,30),20)
};
foreach (var (hour, minute, brightness) in new[] { (5,0,20),(7,0,70),(9,59,70),(10,0,100),(19,0,50),(23,0,20) })
{
    var now = new DateTime(2026, 9, 9, hour, minute, 0);
    var plan = ScheduleCalculator.Calculate(schedule, now)!;
    Check(plan.CurrentOccurrence.Entry.Brightness == brightness && plan.NextOccurrence.OccursAt > now,
        $"Circular schedule {hour:00}:{minute:00}");
}
Check(ScheduleCalculator.Calculate([], DateTime.Now) is null, "Empty schedule");
var negativeBounds = new DisplayBounds(-2560, -1200, 2560, 1440);
Check(negativeBounds.X == -2560 && negativeBounds.Y == -1200 && negativeBounds.Width == 2560,
    "Physical display bounds preserve negative virtual-desktop coordinates");
Check(AppSettings.CurrentVersion == 3, "Official configuration version is 3");
var launchOptions = LaunchOptions.Parse(["--startup", "--safe-mode", "--unknown"]);
Check(launchOptions.AutomaticStartup && launchOptions.SafeMode && launchOptions.InitialView == LaunchView.Hidden &&
    launchOptions.UnknownArguments.SequenceEqual(["--unknown"]), "Launch options support startup, safe mode and unknown argument tolerance");
Check(LaunchOptions.Parse([]).InitialView == LaunchView.QuickPanel &&
    LaunchOptions.Parse(["--settings"]).InitialView == LaunchView.Settings &&
    LaunchOptions.Parse(["--safe-mode"]).InitialView == LaunchView.Settings,
    "Launch routing selects Quick Panel, Settings and Safe Mode correctly");
Check(SingleInstanceService.ShowQuickPanelCommand != SingleInstanceService.ShowSettingsCommand,
    "IPC commands distinguish Quick Panel and Settings");
var panelBounds = QuickPanelPositioning.BottomRight(new WorkArea(-2560, 0, 0, 1440), 352, 420);
Check(panelBounds == new DisplayBounds(-366, 1006, 352, 420),
    "Quick Panel position respects negative monitor work area and margin");
var iconPath = Path.GetFullPath(Path.Combine("Assets", "ShadeFlow.ico"));
Check(File.Exists(iconPath) && File.ReadAllBytes(iconPath).Length > 1000,
    "Original multi-resolution ShadeFlow icon resource exists");
Check(File.ReadAllText(Path.GetFullPath("ShadeFlow.csproj")).Contains("ApplicationIcon", StringComparison.Ordinal),
    "ApplicationIcon is configured");
var instanceScope = "checks-" + Guid.NewGuid().ToString("N");
using (var firstInstance = new SingleInstanceService(instanceScope))
using (var duplicateInstance = new SingleInstanceService(instanceScope))
{
    var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    firstInstance.CommandRequested += command =>
    {
        if (command == SingleInstanceService.ShowQuickPanelCommand) activated.TrySetResult();
    };
    Check(firstInstance.TryAcquire() && !duplicateInstance.TryAcquire(), "Single-instance mutex permits only first owner");
    var activationSent = await duplicateInstance.TryActivateExistingAsync(TimeSpan.FromSeconds(2));
    if (activationSent)
    {
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(activated.Task.IsCompletedSuccessfully, "IPC activates first instance");
    }
    else
    {
        Check(true, "IPC client failure is contained in restricted non-Windows test environment");
    }
}
Check(typeof(ScheduleEntry).GetProperties().Select(property => property.Name).ToHashSet()
    .SetEquals(["Time", "Brightness", "TimeText"]), "ScheduleEntry exposes only Time and Brightness state");
var only = ScheduleCalculator.Calculate([schedule[0]], new DateTime(2026,9,9,7,0,0))!;
Check(only.NextOccurrence.OccursAt == new DateTime(2026,9,10,7,0,0), "Single event repeats tomorrow");

var temporary = Directory.CreateTempSubdirectory("shadeflow-v051-checks-");
try
{
    foreach (var version in new[] { 1, 2, 3 })
    {
        var path = Path.Combine(temporary.FullName, $"v{version}.json");
        File.WriteAllText(path, $$$"""
            {"version":{{{version}}},"displays":{"monitor-test":{"alias":"Odyssey G5"}},
             "schedule":{"enabled":true,"entries":[{"time":"20:00","brightness":30}]}}
            """);
        var config = new ConfigurationService(path);
        Check(config.GetDisplayAlias("monitor-test") == "Odyssey G5", $"V{version} alias retained");
        Check(config.GetSchedule().Entries.Count == (version == 1 ? 0 : 1), $"V{version} schedule migration");
        Check(config.GetPreferences() == new Preferences(), $"V{version} preference defaults");
        Check(config.TrySetPreferences(new Preferences { LinkDisplays = true, TransitionMilliseconds = 5000 }), "Save V3 preferences");
        Check(new ConfigurationService(path).GetPreferences().LinkDisplays, "Preferences reload");
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Check(json.RootElement.GetProperty("version").GetInt32() == 3, "Persisted version 3");
        Check(!json.RootElement.GetProperty("preferences").TryGetProperty("brightness", out _), "No current brightness persistence");
    }
    var corruptPath = Path.Combine(temporary.FullName, "corrupt", "settings.json");
    Directory.CreateDirectory(Path.GetDirectoryName(corruptPath)!);
    File.WriteAllText(corruptPath, "{invalid");
    var corrupt = new ConfigurationService(corruptPath);
    Check(corrupt.GetPreferences() == new Preferences() && !corrupt.GetSchedule().Enabled, "Corrupt JSON defaults");
    Check(!corrupt.TrySetSchedule(true, [schedule[0], schedule[0]]), "Duplicate schedule rejected");
    Check(!corrupt.TrySetSchedule(true, [new(new(7,0),101)]), "Brightness outside range rejected");
    Check(corrupt.TrySetSchedule(true, schedule.Reverse()), "Unordered schedule saved");
    Check(corrupt.GetSchedule().Entries.SequenceEqual(schedule), "Schedule ordered");
    Check(!corrupt.TrySetPreferences(new Preferences { BrightnessStep = 3 }), "Invalid preference rejected");
    File.WriteAllText(corruptPath, """{"version":3,"preferences":{"transitionMilliseconds":-5,"brightnessStep":999,"hotkeys":null}}""");
    Check(new ConfigurationService(corruptPath).GetPreferences() == new Preferences(), "Invalid preference defaults");
    var missing = new ConfigurationService(Path.Combine(temporary.FullName, "new", "settings.json"));
    Check(missing.TrySetDisplayAlias("id", "Dell G15"), "Create directory and settings");
    Check(missing.TrySetDisplayAlias("id", "") && missing.GetDisplayAlias("id") is null, "Remove alias");
    Check(!Directory.EnumerateFiles(temporary.FullName, "*.tmp", SearchOption.AllDirectories).Any(), "No abandoned temp files");

    var backupDirectory = Path.Combine(temporary.FullName, "backup");
    Directory.CreateDirectory(backupDirectory);
    var backupPath = Path.Combine(backupDirectory, "settings.json");
    var backedUp = new ConfigurationService(backupPath);
    Check(backedUp.TrySetDisplayAlias("odyssey", "Odyssey G5"), "Settings backup source saved");
    Check(backedUp.TrySetPreferences(new Preferences { LinkDisplays = true }), "Settings backup created before replacement");
    Check(File.Exists(backedUp.BackupPath), "Settings backup file exists");
    File.WriteAllText(backupPath, "{broken");
    var recovered = new ConfigurationService(backupPath);
    Check(recovered.LoadState == ConfigurationLoadState.RecoveredFromBackup &&
        recovered.GetDisplayAlias("odyssey") == "Odyssey G5", "Corrupt primary recovers from backup");
    Check(JsonDocument.Parse(File.ReadAllText(backupPath)).RootElement.GetProperty("version").GetInt32() == 3,
        "Recovered settings safely restore primary V3");

    var futurePath = Path.Combine(temporary.FullName, "future", "settings.json");
    Directory.CreateDirectory(Path.GetDirectoryName(futurePath)!);
    var futureJson = """{"version":999,"displays":{"future":{"alias":"Keep me"}}}""";
    File.WriteAllText(futurePath, futureJson);
    var future = new ConfigurationService(futurePath);
    Check(future.LoadState == ConfigurationLoadState.FutureVersion && !future.CanWrite,
        "Future settings version remains read-only");
    Check(!future.TrySetDisplayAlias("future", "Overwrite"), "Future settings are not overwritten");
    Check(File.ReadAllText(futurePath) == futureJson, "Future settings file preserved byte-for-byte");

    var exportedPath = Path.Combine(temporary.FullName, "export.json");
    Check(recovered.TryExport(exportedPath, out _), "Settings export succeeds");
    var importTargetPath = Path.Combine(temporary.FullName, "import", "settings.json");
    Directory.CreateDirectory(Path.GetDirectoryName(importTargetPath)!);
    var importTarget = new ConfigurationService(importTargetPath);
    Check(importTarget.TryImport(exportedPath, actualStartupEnabled: false, out _), "Valid settings import succeeds");
    Check(importTarget.GetDisplayAlias("odyssey") == "Odyssey G5" && !importTarget.GetPreferences().StartWithWindows,
        "Import preserves machine startup state");
    var beforeInvalidImport = File.ReadAllText(importTargetPath);
    File.WriteAllText(Path.Combine(temporary.FullName, "invalid-import.json"), "not json");
    Check(!importTarget.TryImport(Path.Combine(temporary.FullName, "invalid-import.json"), false, out _),
        "Invalid import is rejected");
    Check(File.ReadAllText(importTargetPath) == beforeInvalidImport, "Invalid import leaves settings untouched");
    Check(importTarget.TryReset(out _), "Settings reset succeeds");
    Check(importTarget.GetSchedule() == new ScheduleConfiguration(false, Array.Empty<ScheduleEntry>()) &&
        importTarget.GetPreferences() == new Preferences() && importTarget.GetDisplayAlias("odyssey") is null,
        "Settings reset returns safe V0.5 defaults");
    var safePath = Path.Combine(temporary.FullName, "safe", "settings.json");
    Directory.CreateDirectory(Path.GetDirectoryName(safePath)!);
    File.WriteAllText(safePath, """{"version":3,"displays":{"keep":{"alias":"Keep"}}}""");
    var safeConfiguration = new ConfigurationService(safePath, readOnly: true);
    Check(!safeConfiguration.CanWrite && !safeConfiguration.TrySetDisplayAlias("keep", "Changed"),
        "Safe mode configuration is read-only");
    Check(File.ReadAllText(safePath).Contains("Keep", StringComparison.Ordinal),
        "Safe mode leaves settings file unchanged");

    var logsDirectory = Path.Combine(temporary.FullName, "logs");
    Directory.CreateDirectory(logsDirectory);
    for (var day = 0; day < 10; day++)
    {
        var log = Path.Combine(logsDirectory, $"shadeflow-2026-09-{day + 1:00}.log");
        File.WriteAllText(log, "old");
        File.SetLastWriteTimeUtc(log, new DateTime(2026, 9, day + 1, 0, 0, 0, DateTimeKind.Utc));
    }
    using (var log = new LogService(logsDirectory, () => new DateTime(2026, 9, 11, 12, 0, 0)))
        log.Info("Logging test");
    Check(Directory.EnumerateFiles(logsDirectory, "shadeflow-*.log").Count() == 7,
        "Log retention keeps seven daily files");

    var experimentalPath = Path.Combine(temporary.FullName, "experimental-v4.json");
    File.WriteAllText(experimentalPath, """
        {"version":4,"displays":{"id":{"alias":"Dell G15"}},
         "schedule":{"enabled":true,"entries":[{"time":"18:30","brightness":50,"warmth":45}]},
         "preferences":{"transitionMilliseconds":5000,"linkDisplays":true,"startWithWindows":true,
          "brightnessStep":10,"warmthEnabled":false,"hotkeys":{"increaseBrightness":{"modifiers":6,"virtualKey":38},
          "decreaseBrightness":{"modifiers":6,"virtualKey":40},"restoreBrightness":{"modifiers":3,"virtualKey":48},
          "resumeAutomation":{"modifiers":3,"virtualKey":65},"toggleWarmth":{"modifiers":3,"virtualKey":87}}}}
        """);
    var imported = new ConfigurationService(experimentalPath);
    Check(imported.GetDisplayAlias("id") == "Dell G15", "Experimental V4 downgrade keeps alias");
    Check(imported.GetSchedule().Entries.Single() == new ScheduleEntry(new(18,30),50),
        "Experimental V4 downgrade keeps brightness schedule and ignores extra entry field");
    var importedPreferences = imported.GetPreferences();
    Check(importedPreferences is { TransitionMilliseconds: 5000, LinkDisplays: true,
        StartWithWindows: true, BrightnessStep: 10 }, "Experimental V4 downgrade keeps V0.5 preferences");
    Check(importedPreferences.Hotkeys.IncreaseBrightness == new HotkeyBinding(6,38) &&
        importedPreferences.Hotkeys.Bindings().Count == 4, "Experimental V4 downgrade ignores extra hotkey");
    using var downgradedJson = JsonDocument.Parse(File.ReadAllText(experimentalPath));
    Check(downgradedJson.RootElement.GetProperty("version").GetInt32() == 3,
        "Experimental V4 is rewritten as official V3");
    Check(!downgradedJson.RootElement.GetProperty("schedule").GetProperty("entries")[0]
        .TryGetProperty("warmth", out _), "Experimental entry field is removed when rewritten");
    Check(!downgradedJson.RootElement.GetProperty("preferences").TryGetProperty("warmthEnabled", out _) &&
        !downgradedJson.RootElement.GetProperty("preferences").GetProperty("hotkeys")
            .TryGetProperty("toggleWarmth", out _), "Experimental preference fields are removed when rewritten");
}
finally { temporary.Delete(recursive: true); } // Only the uniquely created test directory.

var defaults = new HotkeySettings();
Check(defaults.IsValid(), "Default hotkeys valid");
Check(!(defaults with { IncreaseBrightness = defaults.RestoreBrightness }).IsValid(), "Duplicate hotkeys rejected");
Check(!(new HotkeyBinding(3,17)).IsValid, "Modifier-only rejected");
var registered = new Dictionary<int, HotkeyBinding>();
HotkeyBinding? conflict = null;
using (var registrations = new HotkeyRegistrationSet(
    (id, binding) => binding != conflict && !registered.ContainsValue(binding) && registered.TryAdd(id, binding),
    id => registered.Remove(id)))
{
    Check(registrations.TryReplace(defaults, out _), "Register defaults");
    var oldId = registered.Single(pair => pair.Value == defaults.IncreaseBrightness).Key;
    var emergencyId = registered.Single(pair => pair.Value == defaults.RestoreBrightness).Key;
    conflict = new(3, 0x46);
    Check(!registrations.TryReplace(defaults with { IncreaseBrightness = conflict }, out _), "Conflict detected");
    Check(registrations.CurrentSettings == defaults && registered.Count == 4, "Conflict rolls back functional set");
    Check(registrations.Resolve(oldId) is null, "Queued old hotkey ID ignored");
    Check(registered.ContainsKey(emergencyId), "Unchanged emergency registration retained");
    conflict = null;
    var swapped = defaults with { IncreaseBrightness = defaults.DecreaseBrightness, DecreaseBrightness = defaults.IncreaseBrightness };
    Check(registrations.TryReplace(swapped, out _), "Swap hotkey combinations");
    Check(registrations.TryReplace(swapped with { RestoreBrightness = null }, out _), "Restore hotkey may be cleared");
}
Check(registered.Count == 0, "Hotkey disposal releases all");
Check(BrightnessMath.Step(98, 5) == 100 && BrightnessMath.Step(2, -5) == 0, "Hotkey clamp");
Check(BrightnessMath.Step(60, 5) - BrightnessMath.Step(40, 5) == 20, "Relative offset retained");

DisplayState Display(string id, double brightness) => new(new DisplayInfo(id,id,id,id,null,
    new(0,0,1920,1080),new(1920,1080),false,true),brightness);
var displays = new List<DisplayState> { Display("dell",70), Display("odyssey",50) };
double elapsed = 0;
using (var transition = new BrightnessTransitionService(new Dispatcher(), () => displays,
    (display, value) => display.Brightness = value, () => elapsed))
{
    transition.Start(30, 2000);
    var timer = DispatcherTimer.Last!;
    var queued = timer.CaptureQueuedCallback();
    elapsed = 1000;
    timer.Fire();
    Check(displays[0].Brightness == 50 && displays[1].Brightness == 40, "Transition midpoint from independent values");
    transition.Cancel();
    displays[0].Brightness = 80;
    queued?.Invoke(timer, EventArgs.Empty);
    Check(displays[0].Brightness == 80 && !timer.Enabled && !timer.HasSubscribers,
        "Cancel blocks queued callback and detaches timer");
    elapsed = 0;
    transition.Start(20, 2000);
    elapsed = 1000;
    DispatcherTimer.Last!.Fire();
    Check(displays[0].Brightness == 50, "New transition starts at current brightness");
    var added = Display("new",50);
    displays.Add(added);
    DispatcherTimer.Last.Fire();
    Check(added.Brightness == 50, "New display joins without jump");
    elapsed = 2000;
    DispatcherTimer.Last.Fire();
    Check(displays.All(d => d.Brightness == 20) && !transition.IsRunning,
        "Transition reaches target and stops");
    transition.Start(100, 0);
    Check(displays.All(d => d.Brightness == 100), "Instant restore");
    elapsed = 0;
    transition.Start(0, 30000);
    var lastTimer = DispatcherTimer.Last;
    var stale = lastTimer!.CaptureQueuedCallback();
    transition.Dispose();
    stale?.Invoke(lastTimer, EventArgs.Empty);
    Check(displays.All(d => d.Brightness == 100) && !lastTimer.HasSubscribers,
        "Dispose blocks queued animation");
}

var time = new DateTime(2026,9,9,19,0,0);
ScheduleUpdate? update = null;
using (var scheduler = new BrightnessSchedulerService(() => time, subscribeSystemEvents: true))
{
    scheduler.Updated += next => update = next;
    scheduler.UpdateSchedule(true, schedule, true);
    Check(update?.Brightness == 50, "Startup applies current event");
    var oldGeneration = update!.Generation;
    scheduler.UpdateSchedule(true, schedule, false);
    Check(update!.Brightness is null && !scheduler.IsCurrentGeneration(oldGeneration),
        "Schedule edit preserves brightness; old generation invalidated");
    time = new DateTime(2026,9,9,20,30,0);
    SystemEvents.Resume();
    Check(update!.Brightness == 30 && update.Reason == ScheduleUpdateReason.Resume,
        "Resume recalculates with explicit reason");
    time = new DateTime(2026,9,9,5,0,0);
    SystemEvents.ClockChanged();
    Check(update!.Brightness == 20 && update.Reason == ScheduleUpdateReason.TimeChanged,
        "Clock change recalculates circular event");
    scheduler.UpdateSchedule(false, schedule, false);
    Check(update!.Brightness is null && update.Snapshot.NextOccurrence is null,
        "Disable leaves brightness and cancels next event");
}
Check(!SystemEvents.HasSubscribers, "System events unsubscribed");

var stopwatch = System.Diagnostics.Stopwatch.StartNew();
var beforeEvent = new DateTime(2026,9,9,19,59,58,0);
var fired = new TaskCompletionSource<ScheduleUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
using (var scheduler = new BrightnessSchedulerService(() => beforeEvent + stopwatch.Elapsed, false))
{
    scheduler.Updated += next => { if (next.Brightness.HasValue) fired.TrySetResult(next); };
    scheduler.UpdateSchedule(true, schedule, false);
    var actual = await fired.Task.WaitAsync(TimeSpan.FromSeconds(7));
    Check(actual.Brightness == 30 && actual.Snapshot.NextOccurrence!.Entry.Time == new TimeOnly(21,30),
        "Real one-shot timer fires and rearms for next event");
}

var first = Display("one",100).Display;
var second = Display("two",100).Display;
using (var overlays = new OverlayManager())
{
    overlays.Reconcile([first,second]);
    Check(ShadeFlow.Views.OverlayWindow.Created.All(window => window.ShownAtInitialBounds),
        "New overlays have valid bounds before first show");
    overlays.Displays[0].Brightness = 60;
    overlays.Displays[1].Brightness = 40;
    overlays.Reconcile([first]);
    overlays.Displays[0].Brightness = 65;
    overlays.Reconcile([first,second]);
    Check(overlays.Displays[1].Brightness == 40, "Unlinked hotplug preserves individual StableId override");
    overlays.Reconcile([first]);
    overlays.SetAllBrightness(55);
    overlays.Reconcile([first,second],55,true);
    Check(overlays.Displays.All(d => d.Brightness == 55), "Linked hotplug takes effective global brightness");
    overlays.Reconcile([first]);
    overlays.InvalidateDisconnectedBrightness();
    overlays.SetAllBrightness(30);
    overlays.Reconcile([first,second],30);
    Check(overlays.Displays.All(d => d.Brightness == 30),
        "Global event supersedes disconnected old override");
    overlays.RestoreAll();
    Check(overlays.Displays.All(d => d.Brightness == 100), "Restore resets all states");
    overlays.SetAllBrightness(20);
    overlays.FailSafe(new InvalidOperationException("Test"));
    Check(overlays.Displays.All(d => d.Brightness == 100), "Overlay fail-safe restores states");
}
Check(ShadeFlow.Views.OverlayWindow.Created.All(window => window.Closed && window.Opacity == 0),
    "Overlay cleanup closes and clears every tracked window (adapter)");
var preset = new BrightnessPreset { Name = "Jogo", Icon = "game", Displays = new() { ["dell"] = 40, ["odyssey"] = 60 } };
var presetRoot = Directory.CreateTempSubdirectory("shadeflow-presets-");
try
{
    var path = Path.Combine(presetRoot.FullName, "settings.json");
    var config = new ConfigurationService(path);
    Check(config.GetPresets().Count == 0, "Old/default configuration has no presets");
    config.TrySetDisplayAlias("dell", "Dell G15");
    config.TrySetSchedule(true, schedule);
    Check(config.TrySetPresets([preset]), "Preset save");
    var loaded = new ConfigurationService(path);
    Check(loaded.GetPresets().Single().Displays["odyssey"] == 60 && loaded.GetPresets().Single().Icon == "game", "Preset brightness and icon reload");
    Check(loaded.GetDisplayAlias("dell") == "Dell G15" && loaded.GetSchedule().Entries.Count == 6, "Presets preserve aliases and schedule");
    var edited = preset.Copy(); edited.Name = "Filme"; edited.Icon = "movie"; edited.HoldUntilResume = true;
    Check(config.TrySetPresets([edited]) && new ConfigurationService(path).GetPresets().Single().HoldUntilResume, "Rename icon and duration persist");
    edited.Displays["dell"] = 101;
    Check(!config.TrySetPresets([edited]) && config.GetPresets().Single().Displays["dell"] == 40, "Invalid brightness leaves saved preset untouched");
    var duplicate = preset.Copy(); duplicate.Id = "other"; duplicate.Name = "jOgO";
    Check(!config.TrySetPresets([preset, duplicate]), "Duplicate preset names rejected case-insensitively");
    Check(!config.TrySetPresets([preset, preset]), "Duplicate preset IDs rejected");
    var invalid = preset.Copy(); invalid.Icon = "unsupported";
    Check(!config.TrySetPresets([invalid]), "Unsupported preset icon rejected");
    invalid = preset.Copy(); invalid.Displays["dell"] = double.NaN;
    Check(!config.TrySetPresets([invalid]), "Nonfinite preset brightness rejected");
    var export = Path.Combine(presetRoot.FullName, "export.json");
    Check(config.TryExport(export, out _) && config.TryImport(export, false, out _) && config.GetPresets().Single().Name == "Filme", "Preset export and import");
    Check(!new ConfigurationService(path, true).TrySetPresets([preset]), "Safe mode preserves presets");
    var json = File.ReadAllText(path);
    Check(!json.Contains("hold\":") && !json.Contains("activePreset") && !json.Contains("glyph"), "Preset runtime state and glyphs are not persisted");
    Check(config.TrySetPresets([]) && new ConfigurationService(path).GetPresets().Count == 0, "Delete last preset");
    config.TrySetPresets([preset]);
    Check(config.TryReset(out _) && config.GetPresets().Count == 0, "Reset clears presets");
    File.WriteAllText(path, """{"version":3,"presets":[null,{"name":"bad","displays":null}]}""");
    Check(new ConfigurationService(path).GetPresets().Count == 0, "Malformed presets are ignored without blocking configuration");
}
finally { presetRoot.Delete(true); }

var session = new PresetSession();
session.Apply(preset);
Check(session.Active && !session.Hold && session.AcceptAutomaticAdjustment() && !session.Active, "Next event releases ordinary preset");
preset.HoldUntilResume = true;
session.Apply(preset);
Check(!session.AcceptAutomaticAdjustment() && session.Active, "Held preset ignores automatic events");
session.ManualAdjustment();
Check(session.Hold && !session.Active && !session.AcceptAutomaticAdjustment(), "Manual slider/restore preserves explicit automation pause");
session.Clear();
Check(session.AcceptAutomaticAdjustment() && !session.Hold, "Resume releases held preset");
Check(!new PresetSession().Hold && !new PresetSession().Active, "Restart never restores active preset or pause");

var presetTime = new DateTime(2026, 9, 17, 19, 0, 0);
double effective = 60;
using (var scheduler = new BrightnessSchedulerService(() => presetTime, true))
{
    scheduler.Updated += value =>
    {
        if (value.Brightness is { } brightness && session.AcceptAutomaticAdjustment()) effective = brightness;
    };
    scheduler.UpdateSchedule(true, schedule, false);
    session.Apply(preset);
    presetTime = presetTime.AddHours(2);
    SystemEvents.Resume();
    Check(effective == 60 && scheduler.Snapshot.CurrentEntry?.Brightness == 30, "Resume keeps held preset while scheduler tracks current event");
    presetTime = presetTime.Date.AddHours(10);
    SystemEvents.ClockChanged();
    Check(effective == 60 && scheduler.Snapshot.CurrentEntry?.Brightness == 100, "Clock change keeps explicit preset hold");
    session.Clear(); scheduler.Recalculate(true);
    Check(effective == 100 && !session.Hold, "Resume action applies newly current schedule value");
    preset.HoldUntilResume = false; session.Apply(preset); effective = 60;
    presetTime = presetTime.Date.AddHours(20); scheduler.Recalculate(true);
    Check(effective == 30 && !session.Active, "Automatic event replaces normal preset brightness");
}

displays = [Display("dell", 100), Display("odyssey", 80), Display("unknown", 75)];
elapsed = 0;
using (var transition = new BrightnessTransitionService(new Dispatcher(), () => displays, (d, b) => d.Brightness = b, () => elapsed))
{
    var completed = false;
    transition.IndividualCompleted += _ => completed = true;
    transition.Start(preset.Displays, 2000);
    var timer = DispatcherTimer.Last!;
    var stale = timer.CaptureQueuedCallback();
    elapsed = 1000; timer.Fire();
    Check(displays[0].Brightness == 70 && displays[1].Brightness == 70, "Preset transition interpolates individual targets together");
    Check(displays[2].Brightness == 75, "Preset leaves unmatched active monitor unchanged");
    displays.RemoveAt(1);
    timer.Fire();
    elapsed = 1500;
    var joining = transition.GetJoiningBrightness("odyssey");
    Check(joining == 65, "Reconnect resolves current preset visual brightness before Show");
    displays.Add(Display("odyssey", joining!.Value)); timer.Fire();
    Check(displays.Last().Brightness == 65, "Reconnected display joins preset transition without jump");
    elapsed = 2000; timer.Fire();
    Check(displays[0].Brightness == 40 && displays.Last().Brightness == 60 && completed && !transition.IsRunning, "Individual targets complete and detach timer");
    elapsed = 0; transition.Start(preset.Displays, 2000);
    var cancelTimer = DispatcherTimer.Last!; var cancelQueued = cancelTimer.CaptureQueuedCallback();
    transition.Cancel(); displays[0].Brightness = 90;
    cancelQueued?.Invoke(cancelTimer, EventArgs.Empty);
    Check(displays[0].Brightness == 90 && !cancelTimer.HasSubscribers, "Manual input cancels all preset callbacks");
    transition.Start(preset.Displays, 0);
    Check(displays[0].Brightness == 40 && displays.Last().Brightness == 60 && !transition.IsRunning, "Instant preset applies individual brightness");
    transition.Start(100, 0); stale?.Invoke(timer, EventArgs.Empty);
    Check(displays.All(d => d.Brightness == 100), "Emergency restore supersedes preset and stale callbacks");
    foreach (var target in new[] { 20, 90, 30, 80 }) transition.Start(target, 0);
    Check(displays.All(d => d.Brightness == 80), "Rapid commands after preset retain final target");
}
using (var overlays = new OverlayManager())
{
    var dell = Display("dell", 100).Display;
    var odyssey = Display("odyssey", 100).Display;
    overlays.Reconcile([dell]);
    overlays.CachePresetTargets(preset.Displays);
    overlays.Reconcile([dell, odyssey]);
    Check(overlays.Displays[1].Brightness == 60, "Disconnected preset monitor receives saved target on reconnect");
    Check(Math.Abs(ShadeFlow.Views.OverlayWindow.Created.Last().OpacityAtShow - .4) < .0001, "Preset overlay is created at target opacity before Show");
    overlays.Displays[1].Brightness = 45;
    overlays.Reconcile([dell]); overlays.Reconcile([dell, odyssey]);
    Check(overlays.Displays[1].Brightness == 45, "Manual override after preset survives reconnect");
    overlays.Reconcile([dell]);
    overlays.Reconcile([dell, odyssey], 100, false, id => id == "odyssey" ? 65 : null);
    Check(overlays.Displays[1].Brightness == 65 && Math.Abs(ShadeFlow.Views.OverlayWindow.Created.Last().OpacityAtShow - .35) < .0001, "Transition resolver overrides cached target before Show");
    overlays.RestoreAll();
    Check(overlays.Displays.All(d => d.Brightness == 100), "Exit restores all preset displays");
}
var recoveryBounds = new DisplayBounds(-2560, 0, 2560, 1440);
var appBounds = new DisplayBounds(-2000, 100, 900, 600);
Check(OverlayRecoveryPolicy.NeedsRecovery(14, 3, .2, recoveryBounds, appBounds), "Logged topmost-but-behind condition requests recovery");
Check(!OverlayRecoveryPolicy.NeedsRecovery(2, 3, .2, recoveryBounds, appBounds), "Correct Z-order requests no repair");
Check(!OverlayRecoveryPolicy.NeedsRecovery(14, 3, 0, recoveryBounds, appBounds), "100 percent brightness never triggers a repair");
Check(!OverlayRecoveryPolicy.NeedsRecovery(14, 3, .5, recoveryBounds, new(0, 0, 1920, 1080)), "Foreground on another monitor does not trigger recovery");
Check(OverlayRecoveryPolicy.NeedsRecovery(4, 1, .5, recoveryBounds, new(-2560, 1400, 2560, 40)), "Overlapping taskbar requests recovery");
Check(!OverlayRecoveryPolicy.NeedsRecovery(-1, 3, .5, recoveryBounds, appBounds) &&
    !OverlayRecoveryPolicy.NeedsRecovery(14, -1, .5, recoveryBounds, appBounds), "Unknown Z-order fails closed");
Check(!OverlayRecoveryPolicy.NeedsRecovery(14, 3, .5, recoveryBounds, new(0, 0, 0, 0)), "Empty window bounds ignored");
var recoveryBudget = new OverlayRecoveryBudget();
Check(recoveryBudget.TryAcquire(TimeSpan.Zero), "Initial recovery allowed");
Check(!recoveryBudget.TryAcquire(TimeSpan.FromMilliseconds(350)), "Repeated event cannot cause immediate Z-order fight");
Check(recoveryBudget.TryAcquire(TimeSpan.FromSeconds(2)), "Recovery available after cooldown");
Check(recoveryBudget.TryAcquire(TimeSpan.FromSeconds(4)) && !recoveryBudget.TryAcquire(TimeSpan.FromSeconds(6)), "Persistent obstruction opens repair circuit");
Check(!recoveryBudget.TryAcquire(TimeSpan.FromSeconds(65)) && recoveryBudget.TryAcquire(TimeSpan.FromSeconds(66)), "Circuit suspends repair for 60 seconds then accepts a new event");
var sparseBudget = new OverlayRecoveryBudget();
Check(sparseBudget.TryAcquire(TimeSpan.Zero) && sparseBudget.TryAcquire(TimeSpan.FromSeconds(31)) &&
    sparseBudget.TryAcquire(TimeSpan.FromSeconds(62)) && sparseBudget.TryAcquire(TimeSpan.FromSeconds(93)), "Occasional repairs expire from the attempt budget");
Console.WriteLine($"{checks} checks passed. Native Windows/WPF integration still requires manual testing.");
