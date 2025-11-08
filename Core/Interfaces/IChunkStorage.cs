using Models.Chunk;

namespace Core.Interfaces;

/// <summary>
/// Контракт CRUD операций над чанками
/// </summary>
public interface IChunkStorage
{
    Task StoreAsync(
        ChunkId chunkId,
        ReadOnlyMemory<byte> data,
        CancellationToken ct = default
    );
}