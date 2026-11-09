namespace ShadeFlow.Models;

// Coordinates are physical desktop pixels, including negative coordinates.
public readonly record struct DisplayBounds(int X, int Y, int Width, int Height);
public readonly record struct DisplayResolution(int Width, int Height);

public sealed record DisplayInfo(
    string Id,
    string StableId,
    string DeviceName,
    string FriendlyName,
    string? MonitorDevicePath,
    DisplayBounds Bounds,
    DisplayResolution Resolution,
    bool Primary,
    bool HasStableHardwareIdentity);
