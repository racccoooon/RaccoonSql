using System.Diagnostics;
using Bogus;
using Humanizer;
using RaccoonSql.Core;
using RaccoonSql.Core.Storage;
using RaccoonSql.Core.Storage.Querying;
using RaccoonSql.Demo.Models;

var modelStoreOptions = new ModelStoreOptions
{
    DefaultInsertConflictBehavior = ConflictBehavior.Ignore,
    DefaultRemoveConflictBehavior = ConflictBehavior.Ignore,
    DefaultUpdateConflictBehavior = ConflictBehavior.Ignore,
    FindDefaultConflictBehavior = ConflictBehavior.Ignore,
    Root = "TestDB",
};
var modelStore = new ModelStore(modelStoreOptions);

var persons = modelStore.Set<PersonModel>();
/*
var addressFaker = new Faker<Address>()
    .RuleFor(x => x.City, f => f.Address.City())
    .RuleFor(x => x.Street, f => f.Address.StreetName());

var personFaker = new Faker<PersonModel>()
    .RuleFor(x => x.Address, () => addressFaker)
    .RuleFor(x => x.Birthday, f => f.Date.PastDateOnly(50))
    .RuleFor(x => x.Height, f => f.Random.Number(110, 220))
    .RuleFor(x => x.Name, f => f.Person.FullName);

var personModels = personFaker.GenerateForever().Take(100_000).ToList();

foreach (var t in personModels)
{
    persons.Insert(t);
}

return;
*/
var heightIndex = new BPlusTree<int, Guid>(10);
var bdayIndex = new BPlusTree<DateOnly, Guid>(100);
foreach (var person in persons.All())
{
    heightIndex.Insert(person.Height, person.Id);
    bdayIndex.Insert(person.Birthday, person.Id);
}
void range<T>(BPlusTree<T, Guid> index, T from, T to) where T : IComparable<T>, IEquatable<T>
{
    var count = 100;
    Console.WriteLine($"range from {from} to {to}:");
    List<Guid> indexResults = [];
    var indexStopwatch = Stopwatch.StartNew();
    for (var i = 0; i < count; i++)
    {
        indexStopwatch.Start();
        indexResults = index.FunkyRange(from, to, false, false, from.CompareTo(to) > 0).ToList();
        indexStopwatch.Stop();
    }
    
    Console.WriteLine($"found {indexResults.Count} results in {indexStopwatch.Elapsed.Humanize()}");
}


//var stopwatch = Stopwatch.StartNew();
/*
foreach (var t in personModels)
{
    persons.Insert(t);
}*/

//stopwatch.Stop();


//Console.WriteLine(TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds).Humanize());

// range(heightIndex, 0, 90);
// range(heightIndex, 150, 150);
// range(heightIndex, 160, 180);
// range(heightIndex, 150, 156);
// range(heightIndex, 140, 200);
// range(heightIndex, 130, 210);
// range(heightIndex, 110, 220);

range(bdayIndex, DateOnly.Parse("2007-11-23"), DateOnly.Parse("2010-01-02"));
range(bdayIndex, DateOnly.Parse("1994-12-16"), DateOnly.Parse("1996-09-11"));
range(bdayIndex, DateOnly.Parse("1000-12-16"), DateOnly.Parse("2100-09-11"));