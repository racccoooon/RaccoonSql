using MemoryPack;
using RaccoonSql.Core;

namespace RaccoonSql.Demo.Models;

[MemoryPackable]
public partial class PersonModel : IModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Birthday { get; set; }
}