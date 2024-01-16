using MemoryPack;
using RaccoonSql.Core;

namespace RaccoonSql.Demo.Models;

[MemoryPackable]
public partial class CarModel : IModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public int OwnerId { get; set; }
}