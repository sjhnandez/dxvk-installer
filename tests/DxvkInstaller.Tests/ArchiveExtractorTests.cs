using System.Formats.Tar;
using System.IO.Compression;
using DxvkInstaller.Services;
using Xunit;

namespace DxvkInstaller.Tests;

public sealed class ArchiveExtractorTests : IDisposable
{
    private readonly string _root;

    public ArchiveExtractorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dxvk-installer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }

    [Fact]
    public async Task Extracts_tar_gz_and_finds_arch_folder_without_hardcoded_root_name()
    {
        var archivePath = Path.Combine(_root, "release.tar.gz");
        WriteTestArchive(archivePath);

        var extractDir = Path.Combine(_root, "extracted");
        await ArchiveExtractor.ExtractAsync(archivePath, extractDir);

        Assert.True(File.Exists(Path.Combine(extractDir, "some-weird-root", "x64", "d3d11.dll")));

        var archDirectory = ArchiveExtractor.FindArchDirectory(extractDir, "x64");
        Assert.NotNull(archDirectory);
        Assert.EndsWith(Path.Combine("some-weird-root", "x64"), archDirectory);

        Assert.Null(ArchiveExtractor.FindArchDirectory(extractDir, "x86"));
    }

    [Fact]
    public async Task Second_extraction_reuses_the_completed_folder()
    {
        var archivePath = Path.Combine(_root, "release.tar.gz");
        WriteTestArchive(archivePath);
        var extractDir = Path.Combine(_root, "extracted");

        await ArchiveExtractor.ExtractAsync(archivePath, extractDir);
        var marker = Path.Combine(extractDir, "some-weird-root", "x64", "extra.dll");
        File.WriteAllText(marker, "sentinel");

        await ArchiveExtractor.ExtractAsync(archivePath, extractDir);

        Assert.True(File.Exists(marker)); // folder was not deleted and re-extracted
    }

    private static void WriteTestArchive(string path)
    {
        using var fileStream = File.Create(path);
        using var gzipStream = new GZipStream(fileStream, CompressionLevel.SmallestSize);
        using var tarWriter = new TarWriter(gzipStream);
        WriteFileEntry(tarWriter, "some-weird-root/x64/d3d11.dll");
        WriteFileEntry(tarWriter, "some-weird-root/x64/dxgi.dll");
        WriteFileEntry(tarWriter, "some-weird-root/x32/d3d9.dll");
    }

    private static void WriteFileEntry(TarWriter tarWriter, string name)
    {
        var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
        {
            DataStream = new MemoryStream(new byte[] { 1, 2, 3 })
        };
        tarWriter.WriteEntry(entry);
    }
}
