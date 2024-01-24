// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using RaccoonSql.CoreRework;

Console.WriteLine("Hello, World!");

var raccoonGenerator = () => new Raccoon
{
    CutenessLevel = new Random().Next(99, 2000),
};

var store = new ModelStore();
Guid raccoonId;

{
    using var transaction = store.Transaction();

    var raccoons = transaction.Set<Raccoon>();
    var addedRaccoon = raccoonGenerator();
    addedRaccoon.CutenessLevel = 150;
    raccoonId = addedRaccoon.Id;
    raccoons.Add(addedRaccoon, raccoonGenerator(), raccoonGenerator());

    Debug.Assert(raccoons.Find(Guid.NewGuid()) is null);

    transaction.Commit();
}

{
    using var transaction = store.Transaction();
    
    var raccoons = transaction.Set<Raccoon>();
    var raccoon = raccoons.Find(raccoonId);
    Debug.Assert(raccoon != null);
    Debug.Assert(raccoon.Id == raccoonId);
    Debug.Assert(raccoon.CutenessLevel == 150);

    raccoon.CutenessLevel = 160;
    
    transaction.Commit();
}

{
    using var transaction = store.Transaction();
    
    var raccoons = transaction.Set<Raccoon>();
    var raccoon = raccoons.Find(raccoonId);
    Debug.Assert(raccoon != null);
    Debug.Assert(raccoon.CutenessLevel == 160);

    raccoons.Remove(raccoon);
    
    transaction.Commit();
}

{
    using var transaction = store.Transaction();
    
    var raccoons = transaction.Set<Raccoon>();
    var raccoon = raccoons.Find(raccoonId);
    Debug.Assert(raccoon == null);
}

public class Raccoon : ModelBase
{
    public virtual required int CutenessLevel { get; set; }
}