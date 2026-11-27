using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0105Tests(TestContext testContext)
{
    private const string Types = """

                                 public sealed class Holder
                                 {
                                     public int Value;
                                     public Holder? Inner;
                                     public int[] Cells = new int[2];
                                     public event System.EventHandler? Changed;
                                     public void Raise() => Changed?.Invoke(this, System.EventArgs.Empty);
                                 }

                                 public struct Point
                                 {
                                     public int X;
                                     public int Y;
                                 }

                                 public interface IHasCount
                                 {
                                     int Count { get; set; }
                                 }

                                 public struct Counter : IHasCount
                                 {
                                     public int Count { get; set; }
                                 }

                                 public static class Lib
                                 {
                                     public static int Bump(ref int value) => ++value;
                                     public static int Peek(ref readonly int value) => value;
                                 }
                                 """;

    [TestMethod]
    public Task FP0105_StaticCacheWrite_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Pricing
                           {
                               private static readonly Dictionary<string, decimal> Cache = new();

                               public static decimal Price(string sku, decimal basePrice)
                               {
                                   if (Cache.TryGetValue(sku, out var cached)) return cached;
                                   var price = basePrice * 1.2m;
                                   {|FP0105:Cache[sku]|} = price;
                                   {|FP0105:Pricing.Cache["last"]|} += price;
                                   return price;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_InstanceListAdd_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed class Basket
                           {
                               private readonly List<string> _items = new();
                               private readonly List<string>? _maybe = null;

                               public int Add(string item)
                               {
                                   _items.{|FP0105:Add|}(item);
                                   this._items.{|FP0105:Insert|}(0, item);
                                   _maybe?.{|FP0105:Remove|}(item);
                                   {|FP0105:_items[0]|} = item;
                                   _items.{|FP0105:Sort|}();
                                   _items.{|FP0105:Reverse|}();
                                   return _items.Count;
                               }

                               public System.Action Later() => () => _items.{|FP0105:Clear|}();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_ArrayElementIncrement_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Tally
                           {
                               private readonly int[] _totals = new int[2];
                               private static readonly int[,] Grid = new int[2, 2];

                               public int Bump()
                               {
                                   {|FP0105:_totals[0]|}++;
                                   --{|FP0105:this._totals[^1]|};
                                   {|FP0105:(_totals[1])|} *= 2;
                                   ({|FP0105:_totals[0]|}, var other) = (1, 2);
                                   {|FP0105:Grid[0, 1]|} = other;
                                   return _totals[0];
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_StringBuilderFieldAppend_Reports()
    {
        return VerifyAsync("""
                           using System.Text;

                           [Fprog.FunctionalCore]
                           public sealed class Log
                           {
                               private readonly StringBuilder _text = new();

                               public string Write(string line)
                               {
                                   _text.{|FP0105:Append|}(line).Append('!');
                                   _text.{|FP0105:AppendLine|}();
                                   _text.{|FP0105:AppendFormat|}("{0}", 1);
                                   _text.{|FP0105:AppendJoin|}(",", "a", "b");
                                   _text.{|FP0105:Replace|}("a", "b");
                                   _text.{|FP0105:Insert|}(0, "x");
                                   _text.{|FP0105:Remove|}(0, 1);
                                   {|FP0105:_text.Length|} = 0;
                                   {|FP0105:_text[0]|} = 'y';
                                   _text.{|FP0105:Clear|}();
                                   return _text.ToString();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_ImmutableBuilderField_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public sealed class Draft
                           {
                               private readonly ImmutableArray<int>.Builder _numbers = ImmutableArray.CreateBuilder<int>();
                               private readonly ImmutableDictionary<string, int>.Builder _counts = ImmutableDictionary.CreateBuilder<string, int>();
                               private readonly ImmutableHashSet<int>.Builder _seen = ImmutableHashSet.CreateBuilder<int>();

                               public ImmutableArray<int> Add(int value)
                               {
                                   _numbers.{|FP0105:Add|}(value);
                                   _counts.{|FP0105:Remove|}("a");
                                   {|FP0105:_counts["b"]|} = value;
                                   _seen.{|FP0105:UnionWith|}([value]);
                                   return _numbers.{|FP0105:DrainToImmutable|}();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_AutoPropertyHoldingCollection_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed class Registry
                           {
                               private static List<string> Seen { get; } = [];

                               public Dictionary<string, int> Index { get; } = new();

                               public HashSet<int> Ids { get; init; } = [];

                               public int Register(string name)
                               {
                                   Seen.{|FP0105:Add|}(name);
                                   {|FP0105:Index[name]|} = Seen.Count;
                                   this.Ids.{|FP0105:Add|}(Seen.Count);
                                   return Seen.Count;
                               }
                           }

                           [Fprog.FunctionalCore]
                           public sealed record Bag(List<int> Items, int[] Slots)
                           {
                               public int Put(int item)
                               {
                                   Items.{|FP0105:Add|}(item);
                                   {|FP0105:Slots[0]|} = item;
                                   return Items.Count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_StaticArrayHelperOnField_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public sealed class Table
                           {
                               private readonly int[] _a = new int[4];
                               private readonly string[] _names = new string[4];
                               private static readonly int[] Shared = new int[4];

                               public int Shuffle(int[] source)
                               {
                                   Array.{|FP0105:Sort|}(_a);
                                   Array.{|FP0105:Sort|}<int>(this._a);
                                   Array.{|FP0105:Reverse|}(_a);
                                   Array.{|FP0105:Clear|}(_a);
                                   Array.{|FP0105:Fill|}(_a, 1);
                                   Array.{|FP0105:Copy|}(source, _a, 2);
                                   Array.{|FP0105:ConstrainedCopy|}(source, 0, Shared, 0, 1);
                                   var keys = new int[4];
                                   Array.{|FP0105:Sort|}(keys, _names);
                                   return _a[0];
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_RefAndOutArgumentIntoFieldElement_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Reader
                           {
                               private readonly int[] _cells = new int[2];
                               private readonly Holder _holder = new();

                               public int Read(string text)
                               {
                                   var ok = int.TryParse(text, out {|FP0105:_cells[0]|});
                                   Lib.Bump(ref {|FP0105:_holder.Value|});
                                   System.Threading.Interlocked.Increment(ref {|FP0105:_cells[1]|});
                                   return ok ? _cells[0] : Lib.Peek(ref _holder.Value);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_StaticFieldOfNonCoreTypeMutatedFromCore_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public static class Globals
                           {
                               public static readonly List<string> Names = new();
                               public static readonly int[] Slots = new int[2];
                               public static readonly Holder Current = new();
                           }

                           [Fprog.FunctionalCore]
                           public static class Ops
                           {
                               public static int Touch(string name)
                               {
                                   Globals.Names.{|FP0105:Add|}(name);
                                   {|FP0105:Globals.Slots[0]|} = 1;
                                   {|FP0105:Globals.Current.Value|}++;
                                   {|FP0105:Globals.Current.Inner!.Cells[0]|} = 2;
                                   return Globals.Names.Count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_ConcurrentMemoAndOtherCollections_Reports()
    {
        return VerifyAsync("""
                           using System.Collections;
                           using System.Collections.Concurrent;
                           using System.Collections.Generic;

                           public sealed class Bag : List<int>;

                           [Fprog.FunctionalCore]
                           public sealed class Memo
                           {
                               private static readonly ConcurrentDictionary<int, long> Squares = new();
                               private readonly IList<int> _list = new List<int>();
                               private readonly IDictionary<string, int> _map = new Dictionary<string, int>();
                               private readonly Bag _bag = new();
                               private readonly Queue<int> _queue = new();
                               private readonly BitArray _bits = new(4);
                               private readonly LinkedList<int> _linked = new();

                               public static long Square(int n) => Squares.{|FP0105:GetOrAdd|}(n, static k => (long)k * k);

                               public int Mixed(int value)
                               {
                                   _list.{|FP0105:Add|}(value);
                                   {|FP0105:_map["a"]|} = value;
                                   _bag.{|FP0105:Add|}(value);
                                   _queue.{|FP0105:Enqueue|}(value);
                                   _bits.{|FP0105:SetAll|}(true);
                                   _linked.{|FP0105:AddFirst|}(value);
                                   return _queue.{|FP0105:Dequeue|}();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_ObjectHeldInFieldWritten_Reports()
    {
        return VerifyAsync("""
                           public struct Wrapper
                           {
                               public Holder Box;
                               public Point Point;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Owner
                           {
                               private readonly Holder _holder = new();
                               private readonly Wrapper _wrapper = new() { Box = new Holder() };

                               public int Set()
                               {
                                   {|FP0105:_holder.Value|} = 1;
                                   {|FP0105:_holder.Inner!.Cells[0]|}++;
                                   {|FP0105:_holder?.Value|} = 2;
                                   {|FP0105:_wrapper.Box.Value|} = 3;
                                   return _holder.Value;
                               }
                           }

                           [Fprog.FunctionalCore]
                           public struct Cursor
                           {
                               private readonly System.Collections.Generic.List<int> _seen;

                               public Cursor() => _seen = [];

                               public int Visit(int value)
                               {
                                   _seen.{|FP0105:Add|}(value);
                                   return _seen.Count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_StaticFieldWrittenInInstanceConstructor_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed class Tracked
                           {
                               private static readonly List<Tracked> All = new();
                               private readonly List<int> _own = new();

                               public Tracked()
                               {
                                   All.{|FP0105:Add|}(this);
                                   _own.Add(All.Count);
                               }

                               public int Count => _own.Count;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_InUnionBody_ReportsOnce()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public record Cat;

                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   private static readonly List<string> Names = new();

                                   public static int Name(string name)
                                   {
                                       Names.{|FP0105:Add|}(name);
                                       return Names.Count;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_OutsideCore_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public sealed class Shell
                           {
                               private static readonly Dictionary<string, int> Cache = new();
                               private readonly List<int> _items = new();
                               private readonly int[] _cells = new int[1];

                               public int Use(string key)
                               {
                                   Cache[key] = 1;
                                   _items.Add(1);
                                   _cells[0]++;
                                   System.Array.Sort(_cells);
                                   return int.TryParse(key, out _cells[0]) ? _items.Count : 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_RootIsLocalOrParameter_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           using System.Linq;
                           using System.Text;

                           [Fprog.FunctionalCore]
                           public sealed record Named(string Name)
                           {
                               private bool PrintMembers(StringBuilder builder)
                               {
                                   builder.Append("Name = ").Append(Name);
                                   return true;
                               }
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Nine
                           {
                               private readonly List<int> _items = new();

                               public static string Join(IEnumerable<string> parts)
                                   => parts.Aggregate(new StringBuilder(), (builder, part) => builder.Append(part)).ToString();

                               public static string Render(ImmutableArray<string> lines)
                               {
                                   var text = new StringBuilder();
                                   foreach (var line in lines) AppendLine(text, line);
                                   return text.ToString();
                               }

                               private static StringBuilder AppendLine(StringBuilder text, string line) => text.Append(line).Append('\n');

                               public static ImmutableArray<int> Evens(ImmutableArray<int> values, int[] cells, List<int> list)
                               {
                                   var result = ImmutableArray.CreateBuilder<int>();
                                   foreach (var value in values) if (value % 2 == 0) result.Add(value);
                                   var local = new int[2];
                                   local[0]++;
                                   System.Array.Sort(local);
                                   list.Add(1);
                                   cells[0] = 2;
                                   System.Array.Sort(cells);
                                   return result.ToImmutable();
                               }

                               public int Other(Nine other, Holder holder)
                               {
                                   other._items.Add(1);
                                   holder.Cells[0] = 1;
                                   return int.TryParse("1", out holder.Value) ? other._items.Count : 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_ConstructorAndInitializerFillOwnFields_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public sealed class Index
                           {
                               private static readonly Dictionary<string, int> Defaults = new();
                               private static readonly int[] Table = new int[4];
                               private static readonly int Seed = Table[0] = 1;
                               private readonly Dictionary<string, int> _lengths = new();
                               private readonly int[] _cells = new int[2];
                               private readonly List<string> _keys = new();

                               static Index()
                               {
                                   Defaults["a"] = 1;
                                   Defaults.Add("b", 2);
                                   System.Array.Fill(Table, Seed);
                               }

                               public Index(IEnumerable<string> keys)
                               {
                                   foreach (var key in keys) _lengths[key] = key.Length;
                                   _cells[0]++;
                                   int.TryParse("1", out _cells[1]);
                                   System.Array.Sort(_cells);
                                   keys.ToList().ForEach(key => _keys.Add(key));
                               }

                               public int Length(string key) => _lengths.TryGetValue(key, out var length) ? length : 0;
                           }

                           public abstract class Base
                           {
                               protected readonly List<int> Items = new();
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Derived : Base
                           {
                               public Derived(int first) => Items.Add(first);

                               public int Count => Items.Count;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Primary(int size)
                           {
                               private readonly int[] _cells = new int[size];

                               public Primary(int size, int first) : this(size) => _cells[0] = first;

                               public int First => _cells[0];
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_AssigningFieldItself_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public struct Inner
                           {
                               public Point Point;
                               public int Value;

                               public int Move()
                               {
                                   Point.X = 1;
                                   Value++;
                                   return Point.X;
                               }
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Counters
                           {
                               private static int total;
                               private int _count;
                               private int[] _cells = new int[1];
                               private Point _point;
                               private Counter _counter;

                               public static int Total { get; set; }

                               public int Bump()
                               {
                                   _count = 1;
                                   _count++;
                                   total += 2;
                                   Total += 1;
                                   _point.X = 3;
                                   (_point.Y, _count) = (4, 5);
                                   _cells = new int[2];
                                   System.Array.Resize(ref _cells, 4);
                                   System.Threading.Interlocked.Increment(ref _count);
                                   int.TryParse("6", out _point.X);
                                   ((IHasCount)_counter).Count = 7;
                                   return _count + total;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_ReadsAndImmutableCollectionsInFields_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Frozen;
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           using System.Linq;
                           using System.Text;

                           [Fprog.FunctionalCore]
                           public sealed class Catalog
                           {
                               private static readonly ImmutableDictionary<string, decimal> Rates =
                                   ImmutableDictionary<string, decimal>.Empty.Add("standard", 1.2m);
                               private static readonly FrozenSet<string> Known = new[] { "a" }.ToFrozenSet();
                               private readonly ImmutableList<string> _items = [];
                               private readonly ImmutableArray<int> _numbers = [];
                               private readonly List<int> _list = [1];
                               private readonly int[] _values = [1, 2];
                               private readonly HashSet<int> _set = [1];
                               private readonly SortedSet<int> _sorted = [1];
                               private readonly Dictionary<string, int> _map = new();
                               private readonly StringBuilder _text = new();
                               private readonly Holder _holder = new();

                               public Catalog Add(string item)
                               {
                                   var added = _items.Add(item);
                                   var more = _numbers.Add(1).Insert(0, 2);
                                   var rates = Rates.SetItem("x", 1m).Remove("standard");
                                   return added.Count + more.Length + rates.Count > 0 ? this : this;
                               }

                               public int Read(string key)
                               {
                                   var found = _map.TryGetValue(key, out var value) ? value : _map.Count;
                                   var has = _list.Contains(1) && _set.Contains(2) && Known.Contains(key) && Rates.ContainsKey(key);
                                   IEnumerable<int> appended = _list.Append(3);
                                   IEnumerable<int> reversed = _values.Reverse();
                                   IEnumerable<int> backwards = _sorted.Reverse();
                                   IEnumerable<int> union = _set.Union(appended);
                                   var copy = _list.ToArray();
                                   copy[0] = _values[0] + _list[0] + _holder.Value + _holder.Cells[0];
                                   _holder.Raise();
                                   return found + (has ? 1 : 0) + appended.Count() + reversed.Count() + backwards.Count() + union.Count()
                                          + _text.ToString().Length + _text.Length + copy[0];
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_ComputedPropertyAndMethodResults_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed class Fresh
                           {
                               private readonly List<int> _items = [1];

                               public List<int> Computed => new();

                               public List<int> Copy => new(_items);

                               private static List<int> Make() => [];

                               public int Use()
                               {
                                   Computed.Add(1);
                                   Copy.Add(2);
                                   Make().Add(3);
                                   _items.GetRange(0, 1).Add(4);
                                   new List<int>(_items).Add(5);
                                   return _items.Count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_FieldReachedThroughLocalAlias_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Concurrent;

                           [Fprog.FunctionalCore]
                           public static class Nine
                           {
                               private static readonly ConcurrentDictionary<int, long> Memo = new();

                               public static int AliasOfField()
                               {
                                   var memo = Memo;
                                   memo.{|FP0105:TryAdd|}(-1, 1);
                                   return memo.Count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_ReferenceInsideValueFieldThroughPatternOrElementCall_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           using System.Linq;

                           public readonly record struct HolderPair(Holder First, Holder Second);

                           [Fprog.FunctionalCore]
                           public sealed class Cache
                           {
                               private readonly HolderPair? _pair = new HolderPair(new Holder(), new Holder());
                               private readonly ImmutableArray<Holder> _items = [new()];

                               public int Update()
                               {
                                   if (_pair is HolderPair pair)
                                       {|FP0105:pair.First.Cells[0]|} = 1;

                                   var item = _items.First();
                                   {|FP0105:item.Value|}++;
                                   return item.Value;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_FieldRootUsesSharedAliasesAndWriters_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public sealed class FieldCounter
                           {
                               private readonly int[] cells = new int[4];
                               private readonly List<Holder> items = [new()];
                               public IDictionary<int, Holder> Entries { get; set; } = new Dictionary<int, Holder>();

                               public int Change(int[] source)
                               {
                                   var span = cells.AsSpan()[1..];
                                   span.{|FP0105:Fill|}(1);
                                   var same = cells;
                                   same = this.cells;
                                   {|FP0105:same[0]|}++;
                                   ref var slot = ref same[1];
                                   {|FP0105:slot|} = 4;

                                   Entries.{|FP0105:TryAdd|}(2, new Holder());
                                   CollectionExtensions.{|FP0105:Remove|}(Entries, 2, out _);
                                   {|FP0105:Entries[1]|} = new Holder();
                                   if (Entries.TryGetValue(3, out var value))
                                       {|FP0105:value.Value|} = 1;
                                   var item = items.First();
                                   {|FP0105:item.Value|} = 2;
                                   foreach (var each in items)
                                       {|FP0105:each.Value|}++;
                                   var alias = (Ready: true, List: items);
                                   var (_, list) = alias;
                                   list.{|FP0105:Add|}(item);
                                   Buffer.{|FP0105:BlockCopy|}(source, 0, cells, 0, 4);
                                   source.{|FP0105:CopyTo|}(span);
                                   return slot;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_EventSubscriptions_ReportsAtEventName()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public sealed class Watcher
                           {
                               private readonly Holder holder = new();
                               public event EventHandler? Changed;
                               public static event EventHandler? Any;

                               public Watcher(EventHandler handler)
                               {
                                   Changed += handler;
                                   holder.Changed += handler;
                               }

                               public void Watch(EventHandler handler, Holder other)
                               {
                                   {|FP0105:Changed|} += handler;
                                   this.{|FP0105:Changed|} -= handler;
                                   Watcher.{|FP0105:Any|} += handler;
                                   var alias = holder;
                                   alias.{|FP0105:Changed|} += handler;
                                   alias.{|FP0105:Changed|} -= handler;
                                   other.Changed += handler;
                                   var local = new Holder();
                                   local.Changed += handler;
                               }

                               public void Fire() => Changed?.Invoke(this, EventArgs.Empty);
                               public static void FireAny() => Any?.Invoke(null, EventArgs.Empty);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_ConstructionViaLocalAliases_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed class FieldCounter
                           {
                               private readonly List<int> items;
                               private static readonly int[] Totals = new int[1];

                               static FieldCounter()
                               {
                                   var totals = Totals;
                                   totals[0] = 1;
                               }

                               public FieldCounter(int value)
                               {
                                   items = [];
                                   var same = items;
                                   same.Add(value);
                               }

                               public int Count => items.Count + Totals.Length;
                           }
                           """);
    }

    [TestMethod]
    [Timeout(5000)]
    public Task FP0105_WriteThroughDeepProjectionChain_Reports()
    {
        const int depth = 64;
        var source = new StringBuilder("""
                                       public abstract class ChainStep
                                       {
                                           public abstract ChainLeft Left { get; }
                                           public abstract ChainRight Right { get; }
                                           public int Value { get; set; }
                                       }

                                       public abstract class ChainLeft : ChainStep;
                                       public abstract class ChainRight : ChainStep;
                                       public static class ChainSource { public static ChainStep Make() => null!; }

                                       [Fprog.FunctionalCore]
                                       public static class Ops
                                       {
                                           private static readonly ChainStep Origin = ChainSource.Make();

                                           public static int Write(bool chooseLeft)
                                           {
                                               var x0 = Origin;
                                       """).AppendLine();
        for (var index = 0; index < depth; index++)
        {
            source.AppendLine($"        if (!(x{index} is {{ Left: var a{index}, Right: var b{index} }})) return 0;");
            source.AppendLine($"        var x{index + 1} = chooseLeft ? (ChainStep)a{index} : (ChainStep)b{index};");
        }

        source.AppendLine($$"""
                                    return {|FP0105:x{{depth}}.Value|} = 1;
                                }
                            }
                            """);
        return VerifyAsync(source.ToString());
    }

    [TestMethod]
    public Task FP0105_WriteThroughConvertedTupleBranch_Reports()
    {
        return VerifyAsync("""
                           public abstract class Figure { public int Sides { get; set; } }
                           public sealed class Square : Figure;
                           public sealed class Triangle : Figure;

                           [Fprog.FunctionalCore]
                           public sealed class Drawing(Square square, Triangle triangle)
                           {
                               private readonly Square _square = square;
                               private readonly Triangle _triangle = triangle;

                               public int Grow(bool pick)
                               {
                                   (Figure Child, int Index) pair = pick ? (_square, 0) : (_triangle, 1);
                                   return ++{|FP0105:pair.Child.Sides|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_NamedTupleSlotsTrackFieldStorageThroughRefAssignments_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed class Scores
                           {
                               private readonly List<int> _values = [];

                               public int Reset()
                               {
                                   var pair = (Items: _values, Kept: _values);
                                   ref var slot = ref pair.Item1;
                                   slot = new List<int>();
                                   pair.Kept.{|FP0105:Add|}(42);
                                   return pair.Item2.Count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0105_FreshListAssignedThroughRefToTupleSlot_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed class Scores
                           {
                               private readonly List<int> _values = [];

                               public int Reset()
                               {
                                   var pair = (Items: _values, Count: 0);
                                   ref var slot = ref pair.Item1;
                                   slot = new List<int>();
                                   pair.Items.Clear();
                                   slot.Clear();
                                   return pair.Count;
                               }
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0105", source + Types, testContext.CancellationToken);
    }
}