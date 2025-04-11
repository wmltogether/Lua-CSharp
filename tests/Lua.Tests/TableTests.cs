namespace Lua.Tests;

public class TableTests
{
    [Test]
    public void Test_Indexer()
    {
        var table = new LuaTable();
        table[1] = "foo";
        table["bar"] = 2;
        table[true] = "baz";

        Assert.That(table[1], Is.EqualTo(new LuaValue("foo")));
        Assert.That(table["bar"], Is.EqualTo(new LuaValue(2)));
        Assert.That(table[true], Is.EqualTo(new LuaValue("baz")));
    }

    [Test]
    public void Test_EnsureCapacity()
    {
        var table = new LuaTable(2, 2);
        table[32] = 10; // hash part

        for (int i = 1; i <= 31; i++)
        {
            table[i] = 10;
        }

        Assert.That(table[32], Is.EqualTo(new LuaValue(10)));
    }

    [Test]
    public void Test_RemoveAt()
    {
        var table = new LuaTable();
        table[1] = 1;
        table[2] = 2;
        table[3] = 3;

        var value = table.RemoveAt(2);
        Assert.That(value, Is.EqualTo(new LuaValue(2)));
        Assert.That(table[2], Is.EqualTo(new LuaValue(3)));
    }

    [Test]
    public void Test_TableResize()
    {
        var table = new LuaTable();
        for (int i = 1; i < 1000; i++)
        {
            table[i] = i;
        }

        for (int i = 30; i >= 2; i--)
        {
            var key = 1 << i;
            table[key] = key;
        }

        table[int.MaxValue - 1] = 0;

        int p = 1 << 4;
        Assert.That(table[p], Is.EqualTo(new LuaValue(p)));
        p = 1 << 16;
        Assert.That(table[p], Is.EqualTo(new LuaValue(p)));
        p = 1 << 22;
        Assert.That(table[p], Is.EqualTo(new LuaValue(p)));
    }
}