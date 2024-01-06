using RaccoonSql.Core;

namespace RaccoonSql.Demo.Models;

public class CarModel : IModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public int OwnerId { get; set; }
}