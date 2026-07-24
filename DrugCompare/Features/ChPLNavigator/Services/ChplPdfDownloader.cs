using System.Net;
using System.Net.Http;
using System.IO;

namespace DrugCompare.Features.ChPLNavigator.Services;

public sealed record DownloadedChplPdf(string LocalFilePath, Uri SourceUri);

/// <summary>
/// Downloads one user-selected ChPL document. This deliberately does not crawl
/// registry pages or follow links embedded in HTML.
/// </summary>
public sealed class ChplPdfDownloader
{
    private const long MaxPdfSizeBytes = 50L * 1024 * 1024;
    private readonly HttpClient _httpClient;

    public ChplPdfDownloader()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async Task<DownloadedChplPdf> DownloadAsync(string sourceUrl, long? rplProductId, CancellationToken cancellationToken = default)
    {
        ChplImportDiagnostics.Write($"Download started. RPL ID: {rplProductId?.ToString() ?? "none"}; URL: {sourceUrl}");
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri) ||
            (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Link ChPL musi być prawidłowym adresem HTTP lub HTTPS.");
        }

        using var response = await _httpClient.GetAsync(
            sourceUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        ChplImportDiagnostics.Write($"Download response. HTTP: {(int)response.StatusCode}; Type: {response.Content.Headers.ContentType}; Length: {response.Content.Headers.ContentLength?.ToString() ?? "unknown"}");

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Serwer ChPL zwrócił HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        if (response.Content.Headers.ContentLength is > MaxPdfSizeBytes)
        {
            throw new InvalidOperationException("Plik ChPL przekracza bezpieczny limit 50 MB.");
        }

        var targetDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClinicDaddy",
            "ChPL");
        Directory.CreateDirectory(targetDirectory);

        var stableName = rplProductId is null
            ? "chpl"
            : $"rpl{rplProductId.Value}";
        var targetPath = Path.Combine(targetDirectory, $"{stableName}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.pdf");
        var temporaryPath = targetPath + ".download";

        try
        {
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            var buffer = new byte[81920];
            var totalBytes = 0L;
            var firstBytes = new byte[5];
            var firstBytesCount = 0;

            while (true)
            {
                var bytesRead = await source.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;
                if (totalBytes > MaxPdfSizeBytes)
                {
                    throw new InvalidOperationException("Plik ChPL przekracza bezpieczny limit 50 MB.");
                }

                var bytesToCopy = Math.Min(firstBytes.Length - firstBytesCount, bytesRead);
                if (bytesToCopy > 0)
                {
                    Buffer.BlockCopy(buffer, 0, firstBytes, firstBytesCount, bytesToCopy);
                    firstBytesCount += bytesToCopy;
                }

                await target.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            if (!HasPdfHeader(firstBytes, firstBytesCount))
            {
                throw new InvalidOperationException(
                    "Link nie zwrócił pliku PDF ChPL. Wklej bezpośredni link do PDF, a nie stronę internetową.");
            }

            await target.FlushAsync(cancellationToken);
            // Windows cannot reliably rename a file while this FileShare.None
            // handle is open. Close it before promoting the download.
            await target.DisposeAsync();
            File.Move(temporaryPath, targetPath);
            ChplImportDiagnostics.Write($"Download completed. File: {targetPath}; Bytes: {totalBytes}");
            return new DownloadedChplPdf(targetPath, sourceUri);
        }
        catch (Exception ex)
        {
            ChplImportDiagnostics.Write("Download failed.", ex);
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    internal static bool HasPdfHeader(byte[] bytes, int count) =>
        count >= 5 &&
        bytes[0] == (byte)'%' &&
        bytes[1] == (byte)'P' &&
        bytes[2] == (byte)'D' &&
        bytes[3] == (byte)'F' &&
        bytes[4] == (byte)'-';
}
