using MemoryPack;

namespace RaccoonSql.CoreRework.Internal.Persistence;

[MemoryPackable]
[MemoryPackUnion(0, typeof(AddChange))]
[MemoryPackUnion(1, typeof(UpdateChange))]
[MemoryPackUnion(2, typeof(DeleteChange))]
public partial interface IChange;

[MemoryPackable]
public partial class AddChange : IChange
{
    public required byte[] Serialized { get; init; }
}

[MemoryPackable]
public partial class UpdateChange : IChange
{
    public required byte[] Serialized { get; init; }
    public required int Index { get; init; }
}

[MemoryPackable]
public partial class DeleteChange : IChange
{
    public int Index { get; set; }
}