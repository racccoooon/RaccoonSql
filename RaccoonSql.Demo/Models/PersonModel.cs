using RaccoonSql.Core;

namespace RaccoonSql.Demo.Models;

public record PersonModel : IModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required DateOnly Birthday { get; set; }
    
    public required string Name { get; set; }

    public required int Height { get; set; }
    
    public Address Address { get; set; }
}

public record Address
{
    public required string Street { get; init; }
    public required string City { get; init; }
}