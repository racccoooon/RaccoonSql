using System.Reflection;
using FluentAssertions;
using RaccoonSql.CoreRework.Internal;

namespace RaccoonSql.CoreRework.Test.Internal;

public class ChangeSetTest
{
    public class C : ModelBase;

    public static IEnumerable<object[]> GetChanges()
    {
        var c = new C();
        var id = Guid.NewGuid();
        yield return [new List<C>{c}, new HashSet<Guid>{id}, new List<C>{c}, true];
        yield return [new List<C>{c}, new HashSet<Guid>{id}, new List<C>(), true];
        yield return [new List<C>{c}, new HashSet<Guid>(), new List<C>{c}, true];
        yield return [new List<C>{c}, new HashSet<Guid>(), new List<C>(), true];    
        yield return [new List<C>(), new HashSet<Guid>{id}, new List<C>{c}, true]; 
        yield return [new List<C>(), new HashSet<Guid>{id}, new List<C>(), true]; 
        yield return [new List<C>(), new HashSet<Guid>(), new List<C>{c}, false]; 
        yield return [new List<C>(), new HashSet<Guid>(), new List<C>(), false];
    }
    
    [Theory]
    [MemberData(nameof(GetChanges))]
    public void EmptyChangeset_DoesNotHaveChanges(List<C> added, HashSet<Guid> deleted, List<C> changed, bool expected)
    {
        // arrange
        var changeSet = new ChangeSet(
            typeof(C), 
            added.Cast<ModelBase>().ToList(), 
            deleted,
            []);
        
        // assert
        changeSet.HasChanges.Should().Be(expected);
    }
}