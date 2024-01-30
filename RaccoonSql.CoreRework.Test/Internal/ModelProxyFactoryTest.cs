using FluentAssertions;
using RaccoonSql.CoreRework.Internal;

namespace RaccoonSql.CoreRework.Test.Internal;

public class ModelProxyFactoryTest
{
    public class DemoModel : ModelBase
    {
        public virtual required string String { get; set; }
    }
    
    [Fact]
    public void ModelProxyHasOriginalValues()
    {
        // arrange
        var model = new DemoModel { String = "string" };
        
        // act
        var proxy = ModelProxyFactory.GenerateProxy(model);
        
        // assert
        proxy.Should().BeEquivalentTo(model);
        proxy.Changes.Should().NotBeNull();
        proxy.TrackChanges.Should().BeTrue();
    }
    
    [Fact]
    public void ModelProxyTracksChanges()
    {
        // arrange
        var model = new DemoModel { String = "string" };
        var proxy = ModelProxyFactory.GenerateProxy(model);
        
        // act
        proxy.String = "first change";
        proxy.String = "new string";

        // assert
        proxy.Changes.Should().HaveCount(1);
        proxy.Changes.Should().Contain(x =>
            x.Key.Name == nameof(DemoModel.String)
            && x.Value == "new string");
    }
}