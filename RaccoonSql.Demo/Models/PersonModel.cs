using RaccoonSql.Core;

namespace RaccoonSql.Demo.Models;

public partial record PersonModel : IModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Birthday { get; set; }
}