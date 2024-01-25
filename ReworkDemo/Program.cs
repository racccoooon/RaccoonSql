// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.IO.Abstractions;
using Humanizer;
using RaccoonSql.CoreRework;

Console.WriteLine("Hello, World!");

var options = new ModelStoreOptions
{
    DirectoryPath = "TestDB",
    FileSystem = new FileSystem(),
};
var store = new ModelStore(options);

var hundredThousandRaccoons = Enumerable.Range(0, 100_000)
    .Select(_ => RaccoonGenerator())
    .ToList();

var watch = Stopwatch.StartNew();
{
    using var transaction = store.Transaction();

    var raccoons = transaction.Set<Raccoon>();

    raccoons.Add(hundredThousandRaccoons);

    transaction.Commit();
}
watch.Stop();
Console.WriteLine(watch.Elapsed.Humanize());

watch.Reset();
for (var i = 0; i < 100; i++)
{
    var oneThousandRaccoons = Enumerable.Range(0, 1000)
        .Select(_ => RaccoonGenerator())
        .ToList();
    
    Console.WriteLine($"transaction: {i}");
    watch.Start();
    {
        using var transaction = store.Transaction();

        var raccoons = transaction.Set<Raccoon>();

        raccoons.Add(oneThousandRaccoons);

        transaction.Commit();
    }
    watch.Stop();
}
Console.WriteLine(watch.Elapsed.Humanize());


Guid raccoonId;

{
    using var transaction = store.Transaction();

    var raccoons = transaction.Set<Raccoon>();
    var addedRaccoon = RaccoonGenerator();
    addedRaccoon.CutenessLevel = 150;
    raccoonId = addedRaccoon.Id;
    raccoons.Add(addedRaccoon, RaccoonGenerator(), RaccoonGenerator());

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
return;

Raccoon RaccoonGenerator() => new Raccoon { CutenessLevel = new Random().Next(99, 2000), };

public class Raccoon : ModelBase
{
    public virtual required int CutenessLevel { get; set; }
}