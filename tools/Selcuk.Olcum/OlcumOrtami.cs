using System.Runtime.InteropServices;

namespace Selcuk.Olcum;

/// <summary>Ölçümün yapıldığı makine. Kullanıcı adı veya makine adı yazılmaz.</summary>
public sealed record OlcumOrtami(string IsletimSistemi, string DotNet, string Islemci, int MantiksalCekirdek, double RamGb)
{
    public static OlcumOrtami Topla() => new(
        RuntimeInformation.OSDescription.Trim() + " (" + RuntimeInformation.OSArchitecture + ")",
        RuntimeInformation.FrameworkDescription,
        IslemciAdi(),
        Environment.ProcessorCount,
        Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024d / 1024 / 1024, 1));

    private static string IslemciAdi()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var anahtar = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return (anahtar?.GetValue("ProcessorNameString") as string)?.Trim() ?? "bilinmiyor";
            }

            if (OperatingSystem.IsLinux())
            {
                return File.ReadLines("/proc/cpuinfo")
                    .FirstOrDefault(s => s.StartsWith("model name", StringComparison.Ordinal))?.Split(':', 2)[1].Trim()
                    ?? "bilinmiyor";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }

        return "bilinmiyor";
    }
}
