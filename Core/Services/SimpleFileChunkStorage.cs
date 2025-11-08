using Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Chunk;
using WebApi;

namespace Core.Services;

public class SimpleFileChunkStorage : IChunkStorage
{
    private readonly string _rootDirectory;
    private readonly ILogger<SimpleFileChunkStorage> _logger;

    public SimpleFileChunkStorage(IOptions<NodeSettings> settings, ILogger<SimpleFileChunkStorage> logger)
    {
         _logger = logger;
        _rootDirectory = settings.Value.StorageDirectory;

        if (string.IsNullOrWhiteSpace(_rootDirectory))
            throw new ApplicationException("Путь к директории хранения чанков не задан");

        try
        {
            if (!Directory.Exists(_rootDirectory))
            {
                Directory.CreateDirectory(_rootDirectory);
            }

            _logger.LogInformation("Директория хранения {Dir} существует", _rootDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                message: "Не удалось создать директорию для хранения чанков по пути {Path}",
                _rootDirectory
            );
            throw;
        }
    }

    public async Task StoreAsync(ChunkId chunkId, ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var name = $"{chunkId.Value:N}.chunk";
        var path = Path.Combine(_rootDirectory, name);

        var tempName = $"{name}.{Guid.NewGuid():N}.tmp";
        var tempPath = Path.Combine(_rootDirectory, tempName);

        try
        {
            await using (var fs = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                await fs.WriteAsync(data, ct);
                await fs.FlushAsync(ct);
            }

            if (File.Exists(path))
            {
                _logger.LogWarning(
                    "Файл для чанка {ChunkId} уже существует по пути {Path}",
                    chunkId,
                    path
                );

                SafeDelete(tempPath);
                return;
            }

            File.Move(tempPath, path);

            _logger.LogInformation(
                "Чанк {ChunkId} размером {Size} успешно сохранён по пути {Path}",
                chunkId,
                data.Length,
                path
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                message: "Ошибка при сохранении чанка {ChunkId} в файл {Path}",
                chunkId,
                path
            );

            SafeDelete(tempPath);

            throw;
        }
    }

    private void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogDebug("Удалён временный файл {Path}", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось удалить временный файл {Path}", path);
        }
    }
}