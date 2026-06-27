namespace ReadyOrNotModManager.Core.Archives;

public sealed record ArchiveFormat(string Name, string Extension)
{
    public static readonly ArchiveFormat Zip = new("ZIP", ".zip");
    public static readonly ArchiveFormat Rar = new("RAR", ".rar");
    public static readonly ArchiveFormat SevenZip = new("7z", ".7z");
}

public static class ArchiveFormatDetector
{
    public static ArchiveFormat Detect(string archivePath)
    {
        Span<byte> header = stackalloc byte[8];
        using var stream = File.OpenRead(archivePath);
        var read = stream.Read(header);

        if (read >= 4 && header[0] == 0x50 && header[1] == 0x4B)
        {
            return ArchiveFormat.Zip;
        }

        if (read >= 7 &&
            header[0] == 0x52 &&
            header[1] == 0x61 &&
            header[2] == 0x72 &&
            header[3] == 0x21 &&
            header[4] == 0x1A &&
            header[5] == 0x07)
        {
            return ArchiveFormat.Rar;
        }

        if (read >= 6 &&
            header[0] == 0x37 &&
            header[1] == 0x7A &&
            header[2] == 0xBC &&
            header[3] == 0xAF &&
            header[4] == 0x27 &&
            header[5] == 0x1C)
        {
            return ArchiveFormat.SevenZip;
        }

        throw new InvalidDataException("The downloaded file is not a supported archive. Open the Nexus page and import the completed mod archive manually.");
    }
}
