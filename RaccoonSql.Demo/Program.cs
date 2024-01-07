using System.Diagnostics;
using System.IO.Abstractions;
using Bogus;
using Humanizer;
using RaccoonSql.Core;
using RaccoonSql.Core.Serialization.Json;
using RaccoonSql.Core.Storage;
using RaccoonSql.Core.Storage.Persistence.FileSystem;
using RaccoonSql.Demo.Models;

var persistenceOptions = new FileSystemPersistenceOptions
{
    FileSystem = new FileSystem(),
    SerializationEngineFactory = new JsonSerializationEngineFactory(),
    Path = "TestDB",
};
var fileSystemStorageEngineOptions = new StorageEngineOptions
{
    PersistenceProviderFactory = new FileSystemPersistenceEngineFactory(persistenceOptions),
};
var modelStoreOptions = new ModelStoreOptions
{
    DefaultInsertConflictBehavior = ConflictBehavior.Ignore,
    DefaultRemoveConflictBehavior = ConflictBehavior.Ignore,
    DefaultUpdateConflictBehavior = ConflictBehavior.Ignore,
    FindDefaultConflictBehavior = ConflictBehavior.Ignore,
};
var modelStore = new ModelStore(modelStoreOptions, new StorageEngineFactory(fileSystemStorageEngineOptions));

var persons = modelStore.Set<PersonModel>();
var addressFaker = new Faker<Address>()
    .RuleFor(x => x.City, f => f.Address.City())
    .RuleFor(x => x.Street, f => f.Address.StreetName());

var personFaker = new Faker<PersonModel>()
    .RuleFor(x => x.Address, () => addressFaker)
    .RuleFor(x => x.Birthday, f => f.Date.PastDateOnly(50))
    .RuleFor(x => x.Height, f => f.Random.Number(110, 220))
    .RuleFor(x => x.Name, f => f.Person.FullName);
/*
var stopwatch = Stopwatch.StartNew();

for (int i = 0; i < 100_000; i++)
{
    persons.Insert(personFaker);
}

stopwatch.Stop();

Console.WriteLine(TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds).Humanize());*/
var all = personFaker.GenerateForever().Take(100).ToList();
var bPlusTree = new BPlusTree<DateOnly, PersonModel>(20);
foreach (var personModel in all)
{
    bPlusTree.Insert(personModel.Birthday, personModel);
}

var from = all[0].Birthday;
var to = all[1].Birthday;

if (from > to)
{
    (from, to) = (to, from);
}

Console.WriteLine($"{from}:{to}");

var slowWatch = Stopwatch.StartNew();
var enumerable = all.Where(x => from <= x.Birthday && to >= x.Birthday).ToList();
slowWatch.Stop();
Console.WriteLine(enumerable.Count);
Console.WriteLine(TimeSpan.FromMilliseconds(slowWatch.ElapsedMilliseconds).Humanize());

var filterWatch = Stopwatch.StartNew();

var result = bPlusTree.Range(from, to, false, false).ToList();

filterWatch.Stop();
Console.WriteLine(result.Count);
Console.WriteLine(TimeSpan.FromMilliseconds(filterWatch.ElapsedMilliseconds).Humanize());

var notFound = enumerable.Except(result).First();
Console.WriteLine(notFound);
Console.WriteLine(result.Any(x => x.Id == notFound.Id));