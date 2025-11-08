namespace Models.Chunk;

public readonly record struct ChunkId(Guid Value)
{
    public static ChunkId New() => new(Guid.NewGuid());

    public static ChunkId Empty = new ChunkId();

    public override string ToString() => Value.ToString();
}