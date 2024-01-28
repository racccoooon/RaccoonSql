using MemoryPack;

namespace RaccoonSql.CoreRework.Internal.Persistence;

[MemoryPackable]
public partial class ModelStoreMetadata
{
    public ulong Version { get; set; }
}