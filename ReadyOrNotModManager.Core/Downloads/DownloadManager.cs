using ReadyOrNotModManager.Core.Archives;

namespace ReadyOrNotModManager.Core.Downloads;

public sealed class DownloadManager(HttpClient httpClient)
{
    public async Task<string> DownloadAsync(Uri uri, string destinationDirectory, string preferredFileName, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);
        var destination = Path.Combine(destinationDirectory, SanitizeFileName(preferredFileName));

        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        {
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = File.Create(destination);
            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                readTotal += read;
                if (total is > 0)
                {
                    progress?.Report(readTotal / (double)total.Value);
                }
            }
        }

        progress?.Report(1);
        return RenameToDetectedArchiveExtension(destination);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "mod.zip" : sanitized;
    }

    private static string RenameToDetectedArchiveExtension(string destination)
    {
        var format = ArchiveFormatDetector.Detect(destination);
        if (Path.GetExtension(destination).Equals(format.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return destination;
        }

        var corrected = Path.ChangeExtension(destination, format.Extension);
        if (File.Exists(corrected))
        {
            File.Delete(corrected);
        }

        File.Move(destination, corrected);
        return corrected;
    }
}
