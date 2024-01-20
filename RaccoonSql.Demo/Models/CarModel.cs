using MemoryPack;
using RaccoonSql.Core;

namespace RaccoonSql.Demo.Models;

[MemoryPackable]
[Trigger(typeof(CarModelTrigger))]
public partial class CarModel : IModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [TypedCheckConstraint(typeof(CarModelNameConstraint))]
    [LengthCheckConstraint(50)]
    public virtual string Name { get; set; }
    
    [ForeignKeyConstraint(typeof(PersonModel))]
    public virtual int OwnerId { get; set; }
}

public class CarModelNameConstraint : ICheckConstraint<CarModel, string>
{
    public bool Check(CarModel model, string value)
    {
        // programmatic check that is not supported by default check constraints
        return value.Length % 2 == 0;
    }
}

public class CarModelTrigger : IDeleteTrigger<CarModel>, IUpdateTrigger<CarModel>
{
    public void OnDelete(CarModel model)
    {
        Console.WriteLine($"{model.Name} got deleted.");
    }

    public void OnUpdate(CarModel model, Dictionary<string, object?> changes)
    {
        Console.WriteLine($"{changes.Keys.First()} got changed");
    }
}