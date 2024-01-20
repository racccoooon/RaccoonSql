using System.Diagnostics;
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

void range<T>(BPlusTree<T, Guid> index, T from, T to) where T : IComparable<T>, IEquatable<T>
{
    var count = 100;
    Console.WriteLine($"range from {from} to {to}:");
    List<Guid> indexResults = [];
    var indexStopwatch = Stopwatch.StartNew();
    for (var i = 0; i < count; i++)
    {
        indexStopwatch.Start();
        indexResults = index.FunkyRange(from, to, true, true, false, false, from.CompareTo(to) > 0).ToList();
        indexStopwatch.Stop();
    }
    
    Console.WriteLine($"found {indexResults.Count} results in {indexStopwatch.Elapsed.Humanize()}");
}


// select * from persons where height > 180 or "20.1.2002" < birthday < "12.12.2012"::date order by address.city
var parameters = new QueryPlanParameterBag([10, 10], new Dictionary<string, object>
{
    { "height", 180 }
});
var sortComparer = new QueryPlanSortComparer<PersonModel, string>(x => x.Address.City);
var queryPlan = new QueryPlan<PersonModel>
{
    Root = new QueryPlanLimit<PersonModel>
    {
        Skip = new PositionalQueryPlanParameter<int>(0),
        Take = new PositionalQueryPlanParameter<int>(1),
        Child = new QueryPlanMergeSorted<PersonModel>
        {
            Children = [
                new QueryPlanSort<PersonModel>
                {
                    Child = new QueryPlanIndexScan<PersonModel>
                    {
                        Descending = false,
                        Name = "btree:Height",
                        Ranges = new ScanRanges
                        {
                            Ranges = [new ParameterizedScanRange
                                {
                                    Start = new NamedQueryPlanParameter<IComparable>("height"),
                                    End = new ConstantQueryPlanParameter<IComparable>(0),
                                    StartSet = true,
                                    EndSet = false,
                                    StartInclusive = false,
                                    EndInclusive = false,
                                }],
                        },
                    },
                    Comparer = sortComparer,
                },
                new QueryPlanSort<PersonModel>
                {
                    Child = new QueryPlanIndexScan<PersonModel>
                    {
                        Descending = false,
                        Name = "btree:Birthday",
                        Ranges = new ScanRanges
                        {
                            Ranges = [new ParameterizedScanRange
                            {
                                Start = new ConstantQueryPlanParameter<IComparable>(DateOnly.Parse("1-20-2002")),
                                End = new ConstantQueryPlanParameter<IComparable>(DateOnly.Parse("12-12-2012")),
                                StartSet = true,
                                EndSet = true,
                                StartInclusive = false,
                                EndInclusive = false,
                            }],
                        },
                    },
                    Comparer = sortComparer,
                }
            ],
            Comparer = sortComparer,
        }
    } 
};

var stopwatch = Stopwatch.StartNew();

List<Row<PersonModel>> list = [];
for (int i = 0; i < 100; i++)
{
    list = queryPlan.Execute(persons, parameters).ToList();
}

stopwatch.Stop();

foreach (var row in list)
{
    Debug.Assert(
        row.Model.Height > 180
        || (row.Model.Birthday > DateOnly.Parse("01/20/2002")
        && row.Model.Birthday < DateOnly.Parse("12/12/2012")));
}

Console.WriteLine(list.Select(x => x.Model.Address.City).Humanize());

/*
foreach (var t in personModels)
{
    persons.Insert(t);
}*/



Console.WriteLine(TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds).Humanize());

// range(heightIndex, 0, 90);
// range(heightIndex, 150, 150);
// range(heightIndex, 160, 180);
// range(heightIndex, 150, 156);
// range(heightIndex, 140, 200);
// range(heightIndex, 130, 210);
// range(heightIndex, 110, 220);
//
// range(bdayIndex, DateOnly.Parse("2007-11-23"), DateOnly.Parse("2010-01-02"));
// range(bdayIndex, DateOnly.Parse("1994-12-16"), DateOnly.Parse("1996-09-11"));
// range(bdayIndex, DateOnly.Parse("1000-12-16"), DateOnly.Parse("1996-09-11"));