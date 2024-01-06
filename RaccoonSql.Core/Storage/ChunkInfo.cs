using MemoryPack;

namespace RaccoonSql.Core.Storage;

[MemoryPackable]
public partial record struct ChunkInfo(int ChunkId, int Offset);