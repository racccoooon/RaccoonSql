using System.Diagnostics;
using System.Linq.Expressions;
using Bogus;
using Bogus.DataSets;
using Humanizer;
using RaccoonSql.Core;
using RaccoonSql.Core.Storage;
using RaccoonSql.Core.Storage.Querying;
using RaccoonSql.Demo.Models;

// var modelStoreOptions = new ModelStoreOptions
// {
//     DefaultInsertConflictBehavior = ConflictBehavior.Throw,
//     DefaultRemoveConflictBehavior = ConflictBehavior.Throw,
//     DefaultUpdateConflictBehavior = ConflictBehavior.Throw,
//     DefaultUpsertConflictBehavior = ConflictBehavior.Throw,
//     FindDefaultConflictBehavior = ConflictBehavior.Throw,
//     StoragePath = "TestDB",
// };
// var modelStore = new ModelStore(modelStoreOptions);
// var persons = modelStore.Set<PersonModel>();

/*
var cars = modelStore.Set<CarModel>();

var car1 = new CarModel
{
    Name = "New Car",
    OwnerId = default,
};
cars.Insert(car1);

var found = cars.Find(car1.Id)!;
found.Name = "New Name!";
cars.Update(found);
found.Name = "Super mega long name that is way too long or the check constraint >~<";
try
{
    cars.Update(found);
}
catch (Exception)
{
    Console.WriteLine("oopsie");
}

cars.Remove(car1.Id);

*/

/*
var personFaker = new Faker<PersonModel>()
    .RuleFor(x => x.City, f => f.Address.City())
    .RuleFor(x => x.Birthday, f => f.Date.PastDateOnly(50))
    .RuleFor(x => x.Height, f => f.Random.Number(110, 220))
    .RuleFor(x => x.Name, f => f.Person.FullName);

var personModels = personFaker.GenerateForever().Take(100_000).ToList();

var s = Stopwatch.StartNew();
foreach (var t in personModels)
{
    persons.Insert(t);
}
s.Stop();
Console.WriteLine(s.Elapsed.Humanize());
*/

Console.WriteLine(QueryExpression
    .FromPredicateExpression<PersonModel>(
        x => x.Height > 180 || (x.City == x.Name + "random")
                            && x.Height == 12
                            && 12 == 13
                            || true && (false || true && (false && true))
             || (DateOnly.Parse("1-20-2002").AddDays(20) < x.Birthday
                 && x.Birthday < DateOnly.Parse("12-12-2012"))));

return;
// select * from persons where height > 180 or "20.1.2002" < birthday < "12.12.2012"::date order by address.city
/*
var generatedQueryPlan = persons.AsQueryable()
    .Where(x => x.Height > 180
                || (DateOnly.Parse("1-20-2002").AddDays(20) < x.Birthday
                    && x.Birthday < DateOnly.Parse("12-12-2012")))
    .OrderBy(x => x.City)
    .Skip(20)
    .Take(120)
    .Plan();

Console.WriteLine(generatedQueryPlan);

void Expressionizing(Expression<Func<PersonModel, bool>> e)
{
    var woah = QueryExpression.FromPredicateExpression(e);
    Console.WriteLine(woah);
}

var wife = "Karolin";
Expressionizing(x => x.Height > 180 && x.Name.StartsWith(wife + " <3")
                     || (DateOnly.Parse("1-20-2002").AddDays(20) < x.Birthday
                         && x.Birthday < DateOnly.Parse("12-12-2012")));

return;

// select * from persons where height > 180 or "20.1.2002" < birthday < "12.12.2012"::date order by address.city
var parameters = new QueryPlanParameterBag([10, 10], new Dictionary<string, object>
{
    { "height", 180 }
});
var sortComparer = new QueryPlanSortComparer<PersonModel>(x => x.City, false);
var queryPlan = new QueryPlan<PersonModel>
{
    Root = new QueryPlanLimit<PersonModel>
    {
        Skip = new PositionalQueryPlanParameter<int>(0),
        Take = new PositionalQueryPlanParameter<int>(1),
        Child = new QueryPlanMergeSorted<PersonModel>
        {
            Children =
            [
                new QueryPlanSort<PersonModel>
                {
                    Child = new QueryPlanIndexScan<PersonModel>
                    {
                        Descending = false,
                        Name = "btree:Height",
                        Ranges = new ScanRanges
                        {
                            Ranges =
                            [
                                new ParameterizedScanRange
                                {
                                    Start = new NamedQueryPlanParameter<IComparable>("height"),
                                    End = new ConstantQueryPlanParameter<IComparable>(0),
                                    StartSet = true,
                                    EndSet = false,
                                    StartInclusive = false,
                                    EndInclusive = false,
                                }
                            ],
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
                            Ranges =
                            [
                                new ParameterizedScanRange
                                {
                                    Start = new ConstantQueryPlanParameter<IComparable>(DateOnly.Parse("1-20-2002")),
                                    End = new ConstantQueryPlanParameter<IComparable>(DateOnly.Parse("12-12-2012")),
                                    StartSet = true,
                                    EndSet = true,
                                    StartInclusive = false,
                                    EndInclusive = false,
                                }
                            ],
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

Console.WriteLine(list.Select(x => x.Model.City).Humanize());

/*
foreach (var t in personModels)
{
    persons.Insert(t);
}#1#


Console.WriteLine(TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds).Humanize());
*/

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