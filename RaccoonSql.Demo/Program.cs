using System.Diagnostics;
using Bogus;
using Humanizer;
using RaccoonSql.Core;
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
var addressFaker = new Faker<Address>()
    .RuleFor(x => x.City, f => f.Address.City())
    .RuleFor(x => x.Street, f => f.Address.StreetName());

var personFaker = new Faker<PersonModel>()
    .RuleFor(x => x.Address, () => addressFaker)
    .RuleFor(x => x.Birthday, f => f.Date.PastDateOnly(50))
    .RuleFor(x => x.Height, f => f.Random.Number(110, 220))
    .RuleFor(x => x.Name, f => f.Person.FullName);

var personModels = personFaker.GenerateForever().Take(100_000).ToList();

var stopwatch = Stopwatch.StartNew();

foreach (var t in personModels)
{
    persons.Insert(t);
}

stopwatch.Stop();

Console.WriteLine(TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds).Humanize());