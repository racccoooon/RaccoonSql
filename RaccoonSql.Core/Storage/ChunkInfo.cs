using MemoryPack;

namespace RaccoonSql.Core.Storage;

[MemoryPackable]
public record struct ChunkInfo(int ChunkId, int Offset);