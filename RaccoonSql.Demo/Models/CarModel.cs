using MemoryPack;
using RaccoonSql.Core;

namespace RaccoonSql.Demo.Models;

[Trigger(typeof(CarModelTrigger))]
public class CarModel : ModelBase
{
    [LengthCheckConstraint(50)]
    public virtual string Name { get; set; }
    
    //[ForeignKeyConstraint(typeof(PersonModel))]
    public virtual Guid OwnerId { get; set; }
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