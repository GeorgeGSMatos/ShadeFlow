using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ShadeFlow.Services;

public sealed record DisplayIdentity(
    string StableId,
    string FriendlyName,
    string? MonitorDevicePath,
    bool HasStableHardwareIdentity);

public sealed class DisplayIdentityService
{
    private const uint QueryOnlyActivePaths = 0x00000002;
    private const uint GetSourceName = 1;
    private const uint GetTargetName = 2;
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;
    private const int MaxTopologyRetries = 5;

    public IReadOnlyDictionary<string, DisplayIdentity> GetActiveIdentities()
    {
        ValidateInteropLayout();
        var paths = QueryActivePaths();
        var identities = new Dictionary<string, DisplayIdentity>(StringComparer.OrdinalIgnoreCase);

        // QueryDisplayConfig returns paths in priority order. The GDI source name is the
        // correlation key; array position is never used to match an EnumDisplayMonitors result.
        foreach (var path in paths)
        {
            if (!TryGetSourceName(path.SourceInfo, out var deviceName))
                continue;

            if (!TryGetTargetIdentity(path.TargetInfo, deviceName, out var identity))
                identity = CreateFallback(deviceName);

            // Clone paths may share one GDI source. A logical GDI display can only consume one
            // identity, so keep the highest-priority active path returned by Windows.
            identities.TryAdd(deviceName, identity);
        }

        return identities;
    }

    public static DisplayIdentity CreateFallback(string deviceName) => new(
        CreateStableId("fallback", deviceName),
        deviceName,
        null,
        false);

    private static DisplayConfigPathInfo[] QueryActivePaths()
    {
        for (var attempt = 0; attempt < MaxTopologyRetries; attempt++)
        {
            uint pathCount = 0;
            uint modeCount = 0;
            var result = GetDisplayConfigBufferSizes(QueryOnlyActivePaths, ref pathCount, ref modeCount);
            if (result != ErrorSuccess) throw new Win32Exception(result);
            if (pathCount == 0) return [];

            var paths = new DisplayConfigPathInfo[checked((int)pathCount)];
            var modeBytes = checked((int)modeCount * Marshal.SizeOf<DisplayConfigModeInfo>());
            var modeBuffer = Marshal.AllocHGlobal(Math.Max(1, modeBytes));
            try
            {
                result = QueryDisplayConfig(
                    QueryOnlyActivePaths,
                    ref pathCount,
                    paths,
                    ref modeCount,
                    modeBuffer,
                    0);
            }
            finally
            {
                Marshal.FreeHGlobal(modeBuffer);
            }

            if (result == ErrorInsufficientBuffer)
                continue; // Topology changed between sizing and query; obtain fresh sizes.
            if (result != ErrorSuccess)
                throw new Win32Exception(result);

            if (pathCount < paths.Length)
                Array.Resize(ref paths, checked((int)pathCount));
            return paths;
        }

        throw new Win32Exception(ErrorInsufficientBuffer,
            "A topologia de displays continuou mudando durante a consulta de identidade.");
    }

    private static bool TryGetSourceName(DisplayConfigPathSourceInfo source, out string deviceName)
    {
        var request = new DisplayConfigSourceDeviceName
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = GetSourceName,
                Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                AdapterId = source.AdapterId,
                Id = source.Id
            },
            ViewGdiDeviceName = string.Empty
        };

        var result = DisplayConfigGetSourceDeviceInfo(ref request);
        deviceName = TrimNullTerminated(request.ViewGdiDeviceName);
        if (result == ErrorSuccess && deviceName.Length > 0) return true;

        Trace.TraceWarning("Display source identity query failed with error {0}.", result);
        deviceName = string.Empty;
        return false;
    }

    private static bool TryGetTargetIdentity(
        DisplayConfigPathTargetInfo target,
        string deviceName,
        out DisplayIdentity identity)
    {
        var request = new DisplayConfigTargetDeviceName
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = GetTargetName,
                Size = (uint)Marshal.SizeOf<DisplayConfigTargetDeviceName>(),
                AdapterId = target.AdapterId,
                Id = target.Id
            },
            MonitorFriendlyDeviceName = string.Empty,
            MonitorDevicePath = string.Empty
        };

        var result = DisplayConfigGetTargetDeviceInfo(ref request);
        if (result != ErrorSuccess)
        {
            Trace.TraceWarning("Display target identity query for {0} failed with error {1}.",
                deviceName, result);
            identity = CreateFallback(deviceName);
            return false;
        }

        var friendlyName = TrimNullTerminated(request.MonitorFriendlyDeviceName);
        var devicePath = TrimNullTerminated(request.MonitorDevicePath);
        if (friendlyName.Length == 0) friendlyName = deviceName;

        identity = devicePath.Length > 0
            ? new DisplayIdentity(CreateStableId("monitor", devicePath), friendlyName, devicePath, true)
            : new DisplayIdentity(CreateStableId("fallback", deviceName), friendlyName, null, false);
        return true;
    }

    private static string CreateStableId(string kind, string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"{kind}-{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string TrimNullTerminated(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var nullIndex = value.IndexOf('\0');
        return (nullIndex >= 0 ? value[..nullIndex] : value).Trim();
    }

    private static void ValidateInteropLayout()
    {
        if (Marshal.SizeOf<Luid>() != 8 ||
            Marshal.SizeOf<DisplayConfigPathSourceInfo>() != 20 ||
            Marshal.SizeOf<DisplayConfigPathTargetInfo>() != 48 ||
            Marshal.SizeOf<DisplayConfigPathInfo>() != 72 ||
            Marshal.SizeOf<DisplayConfigModeInfo>() != 64 ||
            Marshal.SizeOf<DisplayConfigDeviceInfoHeader>() != 20 ||
            Marshal.SizeOf<DisplayConfigSourceDeviceName>() != 84 ||
            Marshal.SizeOf<DisplayConfigTargetDeviceName>() != 420)
        {
            throw new InvalidOperationException("Unexpected Win32 display configuration structure layout.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Luid
    {
        public readonly uint LowPart;
        public readonly int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct DisplayConfigRational
    {
        public readonly uint Numerator;
        public readonly uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct DisplayConfigPathSourceInfo
    {
        public readonly Luid AdapterId;
        public readonly uint Id;
        public readonly uint ModeInfoIndex;
        public readonly uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct DisplayConfigPathTargetInfo
    {
        public readonly Luid AdapterId;
        public readonly uint Id;
        public readonly uint ModeInfoIndex;
        public readonly uint OutputTechnology;
        public readonly uint Rotation;
        public readonly uint Scaling;
        public readonly DisplayConfigRational RefreshRate;
        public readonly uint ScanLineOrdering;
        public readonly int TargetAvailable;
        public readonly uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct DisplayConfigPathInfo
    {
        public readonly DisplayConfigPathSourceInfo SourceInfo;
        public readonly DisplayConfigPathTargetInfo TargetInfo;
        public readonly uint Flags;
    }

    // The mode table is required by QueryDisplayConfig but ShadeFlow does not consume it.
    // Its largest native union member is DISPLAYCONFIG_VIDEO_SIGNAL_INFO (48 bytes),
    // preceded by a 16-byte mode header.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct DisplayConfigModeInfo { }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigSourceDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigTargetDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string MonitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string MonitorDevicePath;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(
        uint flags,
        ref uint numPathArrayElements,
        ref uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathArray,
        ref uint numModeInfoArrayElements,
        nint modeInfoArray,
        nint currentTopologyId);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    private static extern int DisplayConfigGetSourceDeviceInfo(
        ref DisplayConfigSourceDeviceName requestPacket);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    private static extern int DisplayConfigGetTargetDeviceInfo(
        ref DisplayConfigTargetDeviceName requestPacket);
}
