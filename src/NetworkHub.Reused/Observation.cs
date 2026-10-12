using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Net;

namespace NetworkHub.Core;
public sealed record ProcessObservation(int Pid, string? Path, DateTime? StartedUtc, bool MetadataAccessible);
public static class ProcessObserver
{
    public static ProcessObservation[] Find(string name)
    {
        var result = new List<ProcessObservation>();
        foreach (var p in Process.GetProcessesByName(name))
        {
            using (p)
            {
                try { result.Add(new(p.Id, p.MainModule?.FileName, p.StartTime.ToUniversalTime(), true)); }
                catch { result.Add(new(p.Id, null, null, false)); }
            }
        }
        return result.ToArray();
    }
}
public sealed record Listener(int Pid, string Address, int Port);
public static class ListenerObserver
{
    [DllImport("iphlpapi.dll")] private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);
    // Read-only IPv4 owner PID table. Never creates a socket or a probe connection.
    public static Listener[] Read()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        int size = 0; var error = GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 3, 0);
        if (error != 122 && error != 0) throw new InvalidOperationException("TCP table query failed: " + error);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                error = GetExtendedTcpTable(buffer, ref size, false, 2, 3, 0);
                if (error == 122) continue;
                if (error != 0) throw new InvalidOperationException("TCP table query failed: " + error);
                var count = Marshal.ReadInt32(buffer); var rows = new List<Listener>();
                if (count < 0 || count > (size - 4) / 24) throw new InvalidOperationException("Invalid TCP table");
                for (int i = 0; i < count; i++)
                {
                    int o = 4 + i * 24;
                    if (Marshal.ReadInt32(buffer, o) != 2) continue;
                    var address = new IPAddress(unchecked((uint)Marshal.ReadInt32(buffer, o + 4))).ToString();
                    int raw = Marshal.ReadInt32(buffer, o + 8); int port = ((raw & 255) << 8) | ((raw >> 8) & 255);
                    rows.Add(new(Marshal.ReadInt32(buffer, o + 20), address, port));
                }
                return rows.ToArray();
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new InvalidOperationException("TCP table kept changing");
    }
    public static string Assess(Listener[] rows, int port, IReadOnlyCollection<int> ownedPids)
    {
        var matches = rows.Where(x => x.Port == port).ToArray();
        if (matches.Length == 0) return "Absent";
        if (matches.Any(x => !ownedPids.Contains(x.Pid))) return "OccupiedByExternalProcess";
        return matches.All(x => x.Address == "127.0.0.1") ? "OwnedLoopbackListener" : "UnsafeBind";
    }
}



