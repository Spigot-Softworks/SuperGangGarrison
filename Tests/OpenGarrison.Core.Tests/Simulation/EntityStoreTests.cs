using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class EntityStoreTests
{
    [Fact]
    public void AddGetRemoveRoundTrip()
    {
        var store = new EntityStore();
        var entity = new TestEntity(7);

        store.Add(entity);

        Assert.Same(entity, store.Get(entity.Id));
        Assert.True(store.Remove(entity.Id));
        Assert.Null(store.Get(entity.Id));
    }

    [Fact]
    public void GetReturnsNullForAbsentId()
    {
        var store = new EntityStore();

        Assert.Null(store.Get(7));
    }

    [Fact]
    public void RemoveReturnsFalseForAbsentId()
    {
        var store = new EntityStore();

        Assert.False(store.Remove(7));
    }

    [Fact]
    public void DuplicateAddThrowsArgumentException()
    {
        var store = new EntityStore();
        store.Add(new TestEntity(7));

        Assert.Throws<ArgumentException>(() => store.Add(new TestEntity(7)));
    }

    [Fact]
    public void SetOverwritesExistingEntryAndAddsMissingEntry()
    {
        var store = new EntityStore();
        var original = new TestEntity(7);
        var replacement = new TestEntity(7);
        var added = new TestEntity(8);
        store.Add(original);

        store.Set(7, replacement);
        store.Set(8, added);

        Assert.Same(replacement, store.Get(7));
        Assert.Same(added, store.Get(8));
    }

    [Fact]
    public void AllEnumeratesExactlyTheLiveEntries()
    {
        var store = new EntityStore();
        var first = new TestEntity(1);
        var second = new TestEntity(2);
        var third = new TestEntity(3);
        store.Add(first);
        store.Add(second);
        store.Remove(second.Id);
        store.Add(third);

        Assert.Equal(new SimulationEntity[] { first, third }, store.All());
    }

    [Fact]
    public void AsReadOnlyReflectsLiveContents()
    {
        var store = new EntityStore();
        var first = new TestEntity(1);
        var second = new TestEntity(2);
        var readOnly = store.AsReadOnly();

        store.Add(first);
        store.Add(second);

        Assert.Equal(2, readOnly.Count);
        Assert.Same(first, readOnly[1]);
        Assert.Same(second, readOnly[2]);

        store.Remove(first.Id);

        Assert.False(readOnly.ContainsKey(first.Id));
        Assert.True(readOnly.ContainsKey(second.Id));
    }

    private sealed class TestEntity : SimulationEntity
    {
        public TestEntity(int id)
            : base(id)
        {
        }
    }
}
