// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using Humanizer;
using RaccoonSql.CoreRework;

Console.WriteLine("Hello, Raccoon-World!");

var startUpWatch = Stopwatch.StartNew();
var options = new ModelStoreOptions
{
    DirectoryPath = "TestDB",
    FileSystem = new FileSystem(),
};
var store = new ModelStore(options);

// force loading
{
    using var transaction = store.Transaction();

    var raccoons = transaction.Set<Raccoon>();

    raccoons.Add(RaccoonGenerator());

    transaction.Commit();
}
startUpWatch.Stop();
using (var transaction = store.Transaction())
{
    var collection = typeof(ModelSet<Raccoon>)
        .GetField("_modelCollection", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(transaction.Set<Raccoon>())!;
    var raccoonCount = collection
        .GetType()
        .GetField("_modelCount", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(collection)!;
    Console.WriteLine($"Loaded {raccoonCount} raccoons in {startUpWatch.Elapsed.Humanize()}");
}

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
Console.WriteLine($"Inserted 100.000 raccoons in a single transaction in {watch.Elapsed.Humanize()}");

watch.Reset();
for (var i = 0; i < 100; i++)
{
    var oneThousandRaccoons = Enumerable.Range(0, 1000)
        .Select(_ => RaccoonGenerator())
        .ToList();
    
    watch.Start();
    {
        using var transaction = store.Transaction();

        var raccoons = transaction.Set<Raccoon>();

        raccoons.Add(oneThousandRaccoons);

        transaction.Commit();
    }
    watch.Stop();
}
Console.WriteLine($"Inserted 100.000 raccoons (100 transactions with 1.000 raccoons) in {watch.Elapsed.Humanize()}");


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