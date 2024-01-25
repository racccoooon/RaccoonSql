using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using Bogus;
using Humanizer;
using RaccoonSql.CoreRework;

Console.WriteLine("Hello, Raccoon-World!");

var raccoonFaker = new Faker<Raccoon>()
    .Rules((faker, raccoon) =>
    {
        raccoon.FirstName = faker.Person.FirstName;
        raccoon.LastName = faker.Person.LastName;
        raccoon.Age = faker.Random.Int(0, 100);
        raccoon.Gender = faker.PickRandom( "male", "female", "what?", "yes", "no", null);
        raccoon.CutenessLevel = faker.Random.Int(1, 10);
        raccoon.Floofines = faker.Random.Float();
        raccoon.City = faker.Address.City();
        raccoon.Street = faker.Address.StreetAddress();
        raccoon.TrashCanNumber = faker.Random.Int(1, 3);
        raccoon.ZipCode = faker.Address.ZipCode();
        raccoon.Motto = faker.Company.CatchPhrase();
    });

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

    raccoons.Add(raccoonFaker.Generate());

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
    .Select(_ => raccoonFaker.Generate())
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
        .Select(_ => raccoonFaker.Generate())
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
    var addedRaccoon = raccoonFaker.Generate();
    addedRaccoon.CutenessLevel = 150;
    raccoonId = addedRaccoon.Id;
    raccoons.Add(addedRaccoon, raccoonFaker.Generate(), raccoonFaker.Generate());

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
    public virtual required string FirstName { get; set; }
    public virtual required string LastName { get; set; }
    public virtual required int Age { get; set; }
    public virtual required string? Gender { get; set; }
    public virtual required int CutenessLevel { get; set; }
    public virtual required float Floofines { get; set; }
    
    public virtual required string City { get; set; }
    public virtual required string Street { get; set; }
    public virtual required int TrashCanNumber { get; set; }
    public virtual required string ZipCode { get; set; }
    
    public virtual required string Motto { get; set; }
}