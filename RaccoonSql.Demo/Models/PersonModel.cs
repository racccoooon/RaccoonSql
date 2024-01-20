using MemoryPack;
using RaccoonSql.Core;

namespace RaccoonSql.Demo.Models;

[MemoryPackable]
public partial class PersonModel : ModelBase
{
    [Index(IndexType.BTree)]
    public virtual required DateOnly Birthday { get; set; }
    
    public virtual required string Name { get; set; }

    [Index(IndexType.BTree)]
    public virtual required int Height { get; set; }
    
    public virtual Address Address { get; set; }
}

[MemoryPackable]
public partial record Address
{
    public required string Street { get; init; }
    
    public required string City { get; init; }
}