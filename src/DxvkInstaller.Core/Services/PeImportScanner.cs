using System.Reflection.PortableExecutable;
using System.Text;
using DxvkInstaller.Models;

namespace DxvkInstaller.Services;

/// <summary>
/// Reads the PE import and delay-load import tables of an executable or DLL and
/// returns the machine architecture, the union of both tables and each table
/// separately (hard/startup imports vs. delay-load imports).
/// </summary>
public static class PeImportScanner
{
    public static (PeArchitecture Architecture, IReadOnlyList<string> Modules, IReadOnlyList<string> HardImports, IReadOnlyList<string> DelayImports) Scan(string filePath)
    {
        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var pe = new PEReader(stream);

        var header = pe.PEHeaders.PEHeader;
        if (header is null)
        {
            return (PeArchitecture.Unknown, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }

        var architecture = pe.PEHeaders.CoffHeader.Machine switch
        {
            Machine.Amd64 => PeArchitecture.X64,
            Machine.I386 => PeArchitecture.X86,
            Machine.Arm64 => PeArchitecture.Arm64,
            _ => PeArchitecture.Unknown
        };

        var hardImports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var delayImports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try { ReadImportTable(pe, hardImports); } catch { /* malformed table: keep what we have */ }
        try { ReadDelayImportTable(pe, delayImports); } catch { }

        var modules = new HashSet<string>(hardImports, StringComparer.OrdinalIgnoreCase);
        modules.UnionWith(delayImports);

        return (architecture, modules.ToArray(), hardImports.ToArray(), delayImports.ToArray());
    }

    private static void ReadImportTable(PEReader pe, HashSet<string> hardImports)
    {
        var directory = pe.PEHeaders.PEHeader!.ImportTableDirectory;
        if (directory.RelativeVirtualAddress <= 0 || directory.Size <= 0)
        {
            return;
        }

        var reader = pe.GetSectionData(directory.RelativeVirtualAddress).GetReader();
        while (reader.RemainingBytes >= 20)
        {
            var originalFirstThunk = reader.ReadUInt32();
            var timeDateStamp = reader.ReadUInt32();
            var forwarderChain = reader.ReadUInt32();
            var nameRva = reader.ReadUInt32();
            var firstThunk = reader.ReadUInt32();

            if (originalFirstThunk == 0 && timeDateStamp == 0 && forwarderChain == 0 && nameRva == 0 && firstThunk == 0)
            {
                break; // end of the descriptor array
            }

            AddModule(pe, hardImports, nameRva);
        }
    }

    private static void ReadDelayImportTable(PEReader pe, HashSet<string> delayImports)
    {
        var directory = pe.PEHeaders.PEHeader!.DelayImportTableDirectory;
        if (directory.RelativeVirtualAddress <= 0 || directory.Size <= 0)
        {
            return;
        }

        var reader = pe.GetSectionData(directory.RelativeVirtualAddress).GetReader();
        while (reader.RemainingBytes >= 32)
        {
            var attributes = reader.ReadUInt32();
            var dllNameRva = reader.ReadUInt32();
            reader.Offset += 24; // skip the six remaining fields of the descriptor

            if (attributes == 0 && dllNameRva == 0)
            {
                break;
            }

            AddModule(pe, delayImports, dllNameRva);
        }
    }

    private static void AddModule(PEReader pe, HashSet<string> modules, uint nameRva)
    {
        if (nameRva == 0 || nameRva > int.MaxValue - 1)
        {
            return;
        }

        try
        {
            var name = ReadAsciiZ(pe, (int)nameRva);
            if (!string.IsNullOrWhiteSpace(name))
            {
                modules.Add(name.Trim().ToLowerInvariant());
            }
        }
        catch
        {
            // RVA outside any section: skip this entry.
        }
    }

    private static string ReadAsciiZ(PEReader pe, int rva)
    {
        var reader = pe.GetSectionData(rva).GetReader();
        var builder = new StringBuilder(32);
        while (reader.RemainingBytes > 0)
        {
            var b = reader.ReadByte();
            if (b == 0)
            {
                break;
            }

            builder.Append((char)b);
        }

        return builder.ToString();
    }
}
