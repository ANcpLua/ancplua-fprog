using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0302Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0302_SystemParseMethodsInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Parsing
                           {
                               public static int Number(string text) => int.{|FP0302:Parse|}(text);

                               public static System.Guid Id(string text) => System.Guid.{|FP0302:Parse|}(text);

                               public static System.DayOfWeek Day(string text) => System.Enum.{|FP0302:Parse|}<System.DayOfWeek>(text);

                               public static System.DateTime When(string text)
                                   => System.DateTime.{|FP0302:Parse|}(text, System.Globalization.CultureInfo.InvariantCulture);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ParseThroughIParsableTypeParameter_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Generic
                           {
                               public static T Read<T>(string text) where T : System.IParsable<T> => T.{|FP0302:Parse|}(text, null);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ParseThroughUsingStatic_Reports()
    {
        return VerifyAsync("""
                           using static System.Int32;

                           [Fprog.FunctionalCore]
                           public static class Parsing
                           {
                               public static int Number(string text) => {|FP0302:Parse|}(text);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_EnumerablePartialMethodsAsExtensions_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Lists
                           {
                               public static int Head(IEnumerable<int> xs) => xs.{|FP0302:First|}();

                               public static int Tail(int[] xs) => xs.{|FP0302:Last|}();

                               public static int Only(List<int> xs) => xs.{|FP0302:Single|}(x => x > 0);

                               public static int Third(IEnumerable<int> xs) => xs.{|FP0302:ElementAt|}(2);

                               public static int FromEnd(IEnumerable<int> xs) => xs.{|FP0302:ElementAt|}(^1);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_EnumerablePartialMethodsCalledStatically_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Lists
                           {
                               public static int Head(IEnumerable<int> xs) => Enumerable.{|FP0302:First|}(xs);

                               public static int GenericHead(IEnumerable<int> xs) => Enumerable.{|FP0302:First|}<int>(xs);

                               public static int Tail(IEnumerable<int> xs) => System.Linq.Enumerable.{|FP0302:Last|}(xs, x => x > 0);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_EnumerablePartialMethodInConditionalAccess_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Lists
                           {
                               public static int? Head(List<int>? xs) => xs?.{|FP0302:First|}();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_DictionaryIndexerReads_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static int Concrete(Dictionary<string, int> d, string key) => {|FP0302:d[key]|};

                               public static int Mutable(IDictionary<string, int> d, string key) => {|FP0302:d[key]|} + 1;

                               public static int ReadOnly(IReadOnlyDictionary<string, int> d) => {|FP0302:d["a"]|};

                               public static int Constrained<TDict>(TDict d) where TDict : IReadOnlyDictionary<string, int>
                                   => {|FP0302:d["a"]|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_DictionaryIndexerReadAndWrite_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static Dictionary<string, int> Count(string key)
                               {
                                   var d = new Dictionary<string, int>();
                                   {|FP0302:d[key]|}++;
                                   --{|FP0302:d[key]|};
                                   {|FP0302:d[key]|} += 1;
                                   return d;
                               }

                               public static Dictionary<string, string?> Fill(string key)
                               {
                                   var d = new Dictionary<string, string?>();
                                   {|FP0302:d[key]|} ??= "x";
                                   return d;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_IndexerOfClassDerivedFromDictionary_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public sealed class Registry : Dictionary<string, int> { }

                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static int Get(Registry registry) => {|FP0302:registry["a"]|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ConditionalIndexerSpanIncludesReceiver_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public sealed class Holder
                           {
                               public Dictionary<string, int> Map { get; } = new();
                           }

                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static int? Direct(Dictionary<string, int>? d) => {|FP0302:d?["a"]|};

                               public static int? Member(Holder? holder) => {|FP0302:holder?.Map["a"]|};

                               public static int? Nested(Dictionary<string, Dictionary<string, int>>? d) => {|FP0302:{|FP0302:d?["a"]|}["b"]|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_InLambdaLocalFunctionAndInitializers_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public sealed class Config
                           {
                               private static readonly string[] Raw = ["1", "2"];

                               public static readonly int First = int.{|FP0302:Parse|}({|FP0302:Raw[0]|});

                               public int Second => int.{|FP0302:Parse|}({|FP0302:Raw[1]|});

                               public static IEnumerable<int> All() => Raw.Select(x => int.{|FP0302:Parse|}(x));

                               public static int Local(IEnumerable<int> xs)
                               {
                                   return Head();

                                   int Head() => xs.{|FP0302:First|}();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_InTypeNestedInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public static class Inner
                               {
                                   public static int Number(string text) => int.{|FP0302:Parse|}(text);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_UnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [assembly: Fprog.FunctionalCore]

                           public static class Anywhere
                           {
                               public static int Number(string text) => int.{|FP0302:Parse|}(text);

                               public static int Head(IEnumerable<int> xs) => xs.{|FP0302:First|}();

                               public static int Get(IReadOnlyDictionary<string, int> d) => {|FP0302:d["a"]|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_OutsideCore_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           public static class Shell
                           {
                               public static int Number(string text) => int.Parse(text);

                               public static int Head(IEnumerable<int> xs) => xs.First();

                               public static int Get(IReadOnlyDictionary<string, int> d) => d["a"];
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_TotalAlternativesInCore_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Globalization;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Total
                           {
                               public static int? Number(string text) => int.TryParse(text, out var value) ? value : null;

                               public static System.DateTime? When(string text)
                                   => System.DateTime.TryParseExact(text, "yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
                                       ? value
                                       : null;

                               public static int Head(IEnumerable<int> xs) => xs.FirstOrDefault();

                               public static int Tail(IEnumerable<int> xs) => xs.LastOrDefault(-1);

                               public static int Only(IEnumerable<int> xs) => xs.SingleOrDefault(x => x > 0);

                               public static int Third(IEnumerable<int> xs) => xs.ElementAtOrDefault(2);

                               public static int Get(IReadOnlyDictionary<string, int> d) => d.TryGetValue("a", out var v) ? v : d.GetValueOrDefault("b");

                               public static int Item(List<int> xs) => xs.Count > 0 ? xs[0] : 0;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ParseExact_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Parsing
                           {
                               public static System.DateTime When(string text)
                                   => System.DateTime.{|FP0302:ParseExact|}(text, "yyyy", System.Globalization.CultureInfo.InvariantCulture);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ParseOfPureTypeOutsideSystemNamespace_Reports()
    {
        return VerifyAsync("""
                           namespace Domain
                           {
                               public sealed record Email(string Value)
                               {
                                   public static Email Parse(string text) => new(text);
                               }

                               [Fprog.FunctionalCore]
                               public static class Use
                               {
                                   public static Email Read(string text) => Email.Parse(text);

                                   public static System.Numerics.BigInteger Big(string text) => System.Numerics.BigInteger.{|FP0302:Parse|}(text);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_MethodGroupOfPartialMember_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Groups
                           {
                               public static IEnumerable<int> Numbers(IEnumerable<string> xs) => xs.Select(int.{|FP0302:Parse|});

                               public static System.Func<IEnumerable<int>, int> Head() => Enumerable.{|FP0302:First|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_MethodGroupShapes_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Linq;
                           using static System.Int32;

                           [Fprog.FunctionalCore]
                           public static class Groups
                           {
                               public static Func<string, int> Explicit() => new Func<string, int>(int.{|FP0302:Parse|});

                               public static Func<string, int> Cast() => (Func<string, int>)(int.{|FP0302:Parse|});

                               public static Func<int> WithReceiver(IEnumerable<int> xs) => xs.{|FP0302:First|};

                               public static Func<string, DayOfWeek> Generic() => Enum.{|FP0302:Parse|}<DayOfWeek>;

                               public static Func<string, int> Static() => {|FP0302:Parse|};

                               public static Func<string, Guid> Qualified() => global::System.Guid.{|FP0302:Parse|};

                               public static int Local(string s)
                               {
                                   Func<string, int> f = int.{|FP0302:Parse|};
                                   return f(s);
                               }

                               public static IEnumerable<int> InLambda(IEnumerable<IEnumerable<string>> xss)
                                   => xss.Select(xs => xs.Select(int.{|FP0302:Parse|}).{|FP0302:Last|}());
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_NameOfPartialMember_NoReport()
    {
        return VerifyAsync("""
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Groups
                           {
                               public static string Name() => nameof(int.Parse);

                               public static string Operator() => nameof(Enumerable.First) + nameof(System.Guid.Parse);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_MethodGroupsOfTotalMembersAndOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Linq;

                           namespace Domain
                           {
                               public sealed record Email(string Value)
                               {
                                   public static Email Parse(string text) => new(text);
                               }

                               [Fprog.FunctionalCore]
                               public static class Groups
                               {
                                   public static Func<IEnumerable<int>, int> Head() => Enumerable.FirstOrDefault;

                                   public static Func<string, int> Lookup(Dictionary<string, int> d) => d.GetValueOrDefault;

                                   public static Func<string, Email> Own() => Email.Parse;

                                   public static Func<string, bool> Empty() => string.IsNullOrEmpty;
                               }

                               public static class Shell
                               {
                                   public static IEnumerable<int> Numbers(IEnumerable<string> xs) => xs.Select(int.Parse);

                                   public static Func<IEnumerable<int>, int> Head() => Enumerable.First;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_QueryableFirstIsNotEnumerable_NoReport()
    {
        return VerifyAsync("""
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Queries
                           {
                               public static int Head(IQueryable<int> xs) => xs.First();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_DictionaryIndexerWrites_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Builder
                           {
                               public static Dictionary<string, int> Build(string key)
                               {
                                   var d = new Dictionary<string, int> { ["a"] = 1 };
                                   d[key] = 2;
                                   (d["b"], d["c"]) = (3, 4);
                                   return d;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_IndexersOfOtherSystemCollectionsDictionaries_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Concurrent;
                           using System.Collections.Frozen;
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           using System.Collections.ObjectModel;

                           [Fprog.FunctionalCore]
                           public static class Others
                           {
                               public static int Concurrent(ConcurrentDictionary<string, int> d) => {|FP0302:d["a"]|};

                               public static int Sorted(SortedDictionary<string, int> d) => {|FP0302:d["a"]|};

                               public static int Immutable(ImmutableDictionary<string, int> d) => {|FP0302:d["a"]|};

                               public static int ImmutableSorted(ImmutableSortedDictionary<string, int> d) => {|FP0302:d["a"]|};

                               public static int Frozen(FrozenDictionary<string, int> d) => {|FP0302:d["a"]|};

                               public static ref readonly int FrozenRef(FrozenDictionary<string, int> d) => ref {|FP0302:d["a"]|};

                               public static int ReadOnly(ReadOnlyDictionary<string, int> d) => {|FP0302:d["a"]|};

                               public static int List(SortedList<string, int> d) => {|FP0302:d["a"]|};

                               public static int Ordered(OrderedDictionary<string, int> d) => {|FP0302:d["a"]|};

                               public static int Builder(ImmutableDictionary<string, int>.Builder d) => {|FP0302:d["a"]|};

                               public static int SortedBuilder(ImmutableSortedDictionary<string, int>.Builder d) => {|FP0302:d["a"]|};

                               public static int? Conditional(ImmutableDictionary<string, int>? d) => {|FP0302:d?["a"]|};

                               public static int Generic<TKey>(FrozenDictionary<TKey, int> d, TKey key) where TKey : notnull => {|FP0302:d[key]|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_IndexerOfClassDerivedFromReadOnlyDictionary_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Collections.ObjectModel;

                           public sealed class Registry(IDictionary<string, int> inner) : ReadOnlyDictionary<string, int>(inner) { }

                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static int Get(Registry registry) => {|FP0302:registry["a"]|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_OtherSystemCollectionsDictionaryWritesAndIncrements_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Concurrent;
                           using System.Collections.Generic;
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public static class Writes
                           {
                               public static ImmutableDictionary<string, int> Build(string key)
                               {
                                   var b = ImmutableDictionary.CreateBuilder<string, int>();
                                   b[key] = 1;
                                   (b["x"], b["y"]) = (2, 3);
                                   {|FP0302:b[key]|} += 1;
                                   return b.ToImmutable();
                               }

                               public static ConcurrentDictionary<string, int> Count(ConcurrentDictionary<string, int> d, string key)
                               {
                                   d[key] = 0;
                                   {|FP0302:d[key]|}++;
                                   return d;
                               }

                               public static SortedList<string, int> Seed() => new() { ["a"] = 1 };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_DictionaryKeysAndUnguardedPositions_Reports()
    {
        return VerifyAsync("""
                           using System.Collections;
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           using System.Collections.ObjectModel;
                           using System.Collections.Specialized;
                           using System.Linq;
                           using System.Text.Json.Nodes;

                           public sealed class TotalMap(ImmutableDictionary<string, int> inner, int fallback) : IReadOnlyDictionary<string, int>
                           {
                               public int this[string key] => inner.TryGetValue(key, out var v) ? v : fallback;
                               public IEnumerable<string> Keys => inner.Keys;
                               public IEnumerable<int> Values => inner.Values;
                               public int Count => inner.Count;
                               public bool ContainsKey(string key) => inner.ContainsKey(key);
                               public bool TryGetValue(string key, out int value) => inner.TryGetValue(key, out value);
                               public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => inner.GetEnumerator();
                               IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                           }

                           public sealed class Keyed : KeyedCollection<string, string>
                           {
                               protected override string GetKeyForItem(string item) => item;
                           }

                           [Fprog.FunctionalCore]
                           public static class Use
                           {
                               public static int Total(TotalMap map) => map["a"];

                               public static JsonNode? Json(JsonObject o) => o["x"];

                               public static JsonNode? Node(JsonNode n) => n["x"];

                               public static IEnumerable<int> Lookup(ILookup<string, int> l) => l["a"];

                               public static string Keyed(Keyed k) => {|FP0302:k["a"]|};

                               public static int Items(List<int> xs, int[] ys, ImmutableArray<int> zs, ImmutableList<int> ws)
                                   => {|FP0302:xs[0]|} + {|FP0302:ys[0]|} + {|FP0302:zs[0]|} + {|FP0302:ws[0]|};

                               public static char Letter(string s) => {|FP0302:s[0]|};

                               public static object? Table(Hashtable t) => t["a"];

                               public static string? Names(NameValueCollection c) => c["a"];

                               public static object? Ordered(OrderedDictionary d) => d["a"];

                               public static int Pair(KeyValuePair<string, int> p) => p.Value;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_CompilerGeneratedRecordMembers_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Snapshot(IReadOnlyDictionary<string, int> Values, string Raw);
                           """);
    }

    [TestMethod]
    public Task FP0302_NameTokenSpanAcrossAliasesCommentsAndLineBreaks_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           using Num = System.Int32;

                           [Fprog.FunctionalCore]
                           public static class Spans
                           {
                               public static int Global(string text) => global::System.Int32.{|FP0302:Parse|}(text);

                               public static int Alias(string text) => Num.{|FP0302:Parse|}(text);

                               public static int Suppressed(string? text) => int.{|FP0302:Parse|}(text!);

                               public static int Broken(IEnumerable<int> xs)
                                   => xs
                                       /* head */ . /* of */
                                       {|FP0302:First|} /* list */ ();

                               public static int Cast(object xs) => ((IEnumerable<int>)xs).{|FP0302:Single|}();

                               public static int Chained(IEnumerable<IEnumerable<int>> xs) => xs.{|FP0302:Last|}().{|FP0302:ElementAt|}<int>(1);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ParenthesizedMethodGroupInvocation_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Parsing
                           {
                               public static int Number(string text) => (int.{|FP0302:Parse|})(text);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_IndexerSpanWithCastParenthesesThisAndBase_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public sealed class Table : Dictionary<string, int>
                           {
                               public int Own(string key) => {|FP0302:this[key]|};

                               public int Inherited(string key) => {|FP0302:base[key]|};

                               public static int Cast(ImmutableDictionary<string, int> d) => {|FP0302:((IDictionary<string, int>)d)["a"]|};

                               public static int Paren(Dictionary<string, int> d) => {|FP0302:(d)["a"]|};

                               public static int Lines(Dictionary<string, int> d) => {|FP0302:d
                                   [ /* key */ "a" ]|};

                               public static int Nested(Dictionary<string, Dictionary<string, int>> d) => {|FP0302:{|FP0302:d["a"]|}["b"]|};

                               public static string Receiver(Dictionary<string, int> d) => {|FP0302:d["a"]|}.ToString();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_NestedInitializerOnIndexerReadsIt_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Seeds
                           {
                               public static Dictionary<string, List<int>> Lists(string key)
                                   => new() { {|FP0302:[key]|} = { 1, 2 } };

                               public static Dictionary<string, Dictionary<string, int>> Maps()
                                   => new() { {|FP0302:["a"]|} = { ["b"] = 1 } };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_NullConditionalAssignmentIsAWrite_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public sealed class Holder
                           {
                               public Dictionary<string, int> Map { get; } = new();
                           }

                           [Fprog.FunctionalCore]
                           public static class Writes
                           {
                               public static Holder? Set(Dictionary<string, int>? d, Holder? holder)
                               {
                                   d?["a"] = 1;
                                   holder?.Map["b"] = 2;
                                   return holder;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_NullConditionalCompoundAssignment_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public sealed class Holder
                           {
                               public Dictionary<string, int> Map { get; } = new();
                           }

                           [Fprog.FunctionalCore]
                           public static class Writes
                           {
                               public static Holder? Add(Dictionary<string, int>? d, Holder? holder)
                               {
                                   {|FP0302:d?["a"]|} += 1;
                                   {|FP0302:holder?.Map["b"]|} -= 2;
                                   return holder;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ReadsOnTheRightOfWrites_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Swaps
                           {
                               public static Dictionary<string, int> Swap(IReadOnlyDictionary<string, int> source)
                               {
                                   var d = new Dictionary<string, int>(source);
                                   (d["a"], (d["b"], _)) = ({|FP0302:d["b"]|}, ({|FP0302:d["a"]|}, 0));
                                   d["c"] = d["d"] = {|FP0302:d["a"]|};
                                   return d;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_InConstructorsOperatorsAccessorsAndPrimaryConstructors_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public sealed record Money(decimal Amount)
                           {
                               private readonly int _code;

                               public Money(string text) : this(decimal.{|FP0302:Parse|}(text)) { }

                               public static Money operator +(Money left, string right) => new(left.Amount + decimal.{|FP0302:Parse|}(right));

                               public static explicit operator Money(decimal[] xs) => new(xs.{|FP0302:Last|}());

                               public decimal Rounded
                               {
                                   get { return decimal.{|FP0302:Parse|}(Amount.ToString()); }
                               }

                               public string Code { get => _code.ToString(); init => _code = int.{|FP0302:Parse|}(value); }

                               public int this[IReadOnlyDictionary<string, int> d] => {|FP0302:d["a"]|};
                           }

                           public abstract record Base(int Value);

                           [Fprog.FunctionalCore]
                           public sealed record Derived(string Text) : Base(int.{|FP0302:Parse|}(Text));

                           [Fprog.FunctionalCore]
                           public readonly record struct Point(string X)
                           {
                               public int Parsed { get; } = int.{|FP0302:Parse|}(X);
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Primary(IReadOnlyDictionary<string, int> d)
                           {
                               public int A { get; } = {|FP0302:d["a"]|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_InAsyncIteratorQueryInterpolationPatternAndInitializers_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           using System.Threading.Tasks;

                           public sealed class Box
                           {
                               public int Value { get; init; }
                           }

                           [Fprog.FunctionalCore]
                           public static class Forms
                           {
                               public static async Task<int> Later(Task<string> text) => int.{|FP0302:Parse|}(await text);

                               public static IEnumerable<int> Each(IEnumerable<string> xs)
                               {
                                   foreach (var x in xs)
                                       yield return int.{|FP0302:Parse|}(x);
                               }

                               public static IEnumerable<int> Query(IReadOnlyDictionary<string, int> d, IEnumerable<string> keys)
                                   => from k in keys where {|FP0302:d[k]|} > 0 select {|FP0302:d[k]|};

                               public static string Text(IEnumerable<int> xs) => $"head {xs.{|FP0302:First|}():D2}";

                               public static bool Matches(IReadOnlyDictionary<string, int> d) => {|FP0302:d["a"]|} is > 0 and < 10;

                               public static string Kind(IEnumerable<int> xs) => xs.{|FP0302:Single|}() switch { 0 => "zero", _ => "other" };

                               public static int[] Collect(string a) => [int.{|FP0302:Parse|}(a), 1];

                               public static Box Make(string a) => new() { Value = int.{|FP0302:Parse|}(a) };

                               public static System.Func<string, System.Func<string, int>> Deep()
                                   => a => b => ((System.Func<int>)(() => int.{|FP0302:Parse|}(a + b)))();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_PartialTypeAnnotatedOnAnotherPart_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static partial class Split
                           {
                               public static partial int Read(string text);
                           }

                           public static partial class Split
                           {
                               public static partial int Read(string text) => int.{|FP0302:Parse|}(text);

                               public static int Other(string text) => int.{|FP0302:Parse|}(text);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_InExtensionMembers_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Extensions
                           {
                               extension(IEnumerable<int> xs)
                               {
                                   public int Head => xs.{|FP0302:First|}();

                                   public int Lookup(IReadOnlyDictionary<int, int> d) => {|FP0302:d[xs.{|FP0302:Last|}()]|};

                                   public static int Read(string text) => int.{|FP0302:Parse|}(text);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_IndexerInheritedThroughInterfaceWithNamedArgument_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public interface IRegistry : IReadOnlyDictionary<string, int>
                           {
                               string Name { get; }
                           }

                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static int Get(IRegistry registry) => {|FP0302:registry[key: "a"]|};

                               public static int Keyed(IDictionary<(string, int), int> d) => {|FP0302:d[("a", 1)]|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ImmutableArrayExtensionsAreNotEnumerable_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Immutables
                           {
                               public static int Head(ImmutableArray<int> xs) => xs.{|FP0302:First|}();

                               public static int Tail(ImmutableArray<int> xs) => xs.{|FP0302:Last|}();

                               public static int Only(ImmutableArray<int> xs) => xs.{|FP0302:Single|}();

                               public static int Third(ImmutableArray<int> xs) => xs.{|FP0302:ElementAt|}(2);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_FirstOnImmutableArrayBindsOutsideEnumerable_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Lists
                           {
                               public static int Head(ImmutableArray<int> xs) => xs.{|FP0302:First|}();

                               public static int Tail(ImmutableArray<int> xs) => xs.{|FP0302:Last|}(x => x > 0);

                               public static int Only(ImmutableArray<int> xs) => xs.{|FP0302:Single|}();

                               public static int Third(ImmutableArray<int> xs) => xs.{|FP0302:ElementAt|}(2);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ImmutableArrayExtensionsStaticBuilderAndConditional_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Immutables
                           {
                               public static int Head(ImmutableArray<int> xs) => ImmutableArrayExtensions.{|FP0302:First|}(xs);

                               public static int Only(ImmutableArray<int> xs) => System.Linq.ImmutableArrayExtensions.{|FP0302:Single|}(xs, x => x > 0);

                               public static int Start(ImmutableArray<int>.Builder builder) => builder.{|FP0302:First|}();

                               public static int End(ImmutableArray<int>.Builder builder) => builder.{|FP0302:Last|}();

                               public static int? Maybe(ImmutableArray<int>? xs) => xs?.{|FP0302:Last|}();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ImmutableArrayOrDefaultAndOtherOperators_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Immutable;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Immutables
                           {
                               public static int Head(ImmutableArray<int> xs) => xs.FirstOrDefault();

                               public static int Tail(ImmutableArray<int> xs) => xs.LastOrDefault(x => x > 0);

                               public static int Only(ImmutableArray<int> xs) => xs.SingleOrDefault();

                               public static int Third(ImmutableArray<int> xs) => xs.ElementAtOrDefault(2);

                               public static int Start(ImmutableArray<int>.Builder builder) => builder.FirstOrDefault() + builder.LastOrDefault();

                               public static int Sum(ImmutableArray<int> xs) => xs.Any() ? xs.Aggregate(0, (a, b) => a + b) : xs.Where(x => x > 0).Select(x => x).ToArray().Length;

                               public static int Indexed(ImmutableArray<int> xs) => xs.Length > 0 ? xs[0] : 0;

                               public static Func<ImmutableArray<int>, int> Group() => ImmutableArrayExtensions.FirstOrDefault;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_MethodGroupOfImmutableArrayOperator_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Immutable;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Immutables
                           {
                               public static Func<ImmutableArray<int>, int> Group() => ImmutableArrayExtensions.{|FP0302:First|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ImmutableArrayLookalikesAndShell_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           using System.Linq;

                           namespace Domain
                           {
                               public static class ImmutableArrayExtensions
                               {
                                   public static int First(this ImmutableArray<int> xs, int fallback) => xs.IsEmpty ? fallback : xs[0];
                               }

                               [Fprog.FunctionalCore]
                               public static class Use
                               {
                                   public static int Head(ImmutableArray<int> xs) => xs.First(0);
                               }

                               public static class Shell
                               {
                                   public static int Head(ImmutableArray<int> xs) => xs.First() + xs.Last() + xs.Single() + xs.ElementAt(0);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_LookalikeUserMembers_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           namespace System
                           {
                               public sealed class Parser
                               {
                                   public int Parse(string text) => text.Length;
                               }
                           }

                           namespace Domain
                           {
                               public sealed class Dictionary<TKey, TValue> where TKey : notnull
                               {
                                   public TValue this[TKey key] => default!;
                               }

                               public sealed record Path(IReadOnlyList<int> Steps)
                               {
                                   public int First() => Steps.Count > 0 ? Steps[0] : 0;

                                   public int this[string name] => name.Length;
                               }

                               public static class PathExtensions
                               {
                                   public static int Last(this Path path) => path.Steps.Count;
                               }

                               [Fprog.FunctionalCore]
                               public static class Use
                               {
                                   public static int Instance(System.Parser parser) => parser.Parse("1");

                                   public static int Own(Dictionary<string, int> d) => d["a"];

                                   public static int Members(Path path) => path.First() + path.Last() + path["x"];
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_GeneratedCodeInCore_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Generated
                           {
                               public static int All(int[] xs, IReadOnlyDictionary<string, int> d) => xs.First() + int.Parse("1") + d["a"];
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_InExpressionTreeLambda_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Linq.Expressions;

                           [Fprog.FunctionalCore]
                           public static class Trees
                           {
                               public static Expression<Func<IReadOnlyDictionary<string, int>, int>> Get()
                                   => d => {|FP0302:d["a"]|} + int.{|FP0302:Parse|}("1");
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_DelegateMadeFromMethodGroupReportedAtTheGroupNotAtItsInvocation_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static unsafe class Delegates
                           {
                               public static int Bound() => ((Func<string, int>)int.{|FP0302:Parse|})("1");

                               public static int Pointer()
                               {
                                   delegate*<string, int> parse = &int.{|FP0302:Parse|};
                                   return parse("1");
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_InvokingDelegateParameter_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static unsafe class Delegates
                           {
                               public static int Through(Func<string, int> parse) => parse("1");

                               public static int Pointer(delegate*<string, int> parse) => parse("1");
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_SpanReadWithoutGuard_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Runtime.InteropServices;

                           [Fprog.FunctionalCore]
                           public static class Native
                           {
                               [DllImport("native")]
                               public static extern int Parse(string text);
                           }

                           [Fprog.FunctionalCore]
                           public abstract unsafe partial class Odd<T> where T : unmanaged
                           {
                               public abstract int Abstract(string text);

                               public static int External(string text) => Native.Parse(text);

                               public static T Value(T* p) => p[0];

                               public static int Stack()
                               {
                                   Span<int> s = stackalloc int[2];
                                   return {|FP0302:s[0]|};
                               }

                               public static void Empty() { }

                               public interface INested
                               {
                                   int Parse(string text) => text.Length;
                               }

                               partial void Hook();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_GenericStructAndNestedInterfaceInCore_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public readonly struct Cache<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> map) where TKey : notnull
                           {
                               public TValue Get(TKey key) => {|FP0302:map[key]|};

                               public interface ISource
                               {
                                   TValue Head(IEnumerable<TValue> xs) => xs.{|FP0302:First|}();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ParseAndParseExactThroughNonPureTypeParameter_Reports()
    {
        return VerifyAsync("""
                           using System;

                           public interface IInput<T> where T : IInput<T>
                           {
                               static abstract T Parse(string text);
                               static abstract T ParseExact(string text);
                           }

                           [Fprog.FunctionalCore]
                           public static class Read
                           {
                               public static T One<T>(string text) where T : IInput<T> => T.{|FP0302:Parse|}(text);

                               public static Func<string, T> Exact<T>() where T : IInput<T> => T.{|FP0302:ParseExact|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ParseExactOfPureTypeAsMethodGroup_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static class Read
                           {
                               public static Func<string, string, IFormatProvider, DateTime> Exact() => DateTime.{|FP0302:ParseExact|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_InstanceParseAndParseOfCoreAndShellTypes_NoReport()
    {
        return VerifyAsync("""
                           namespace Domain;

                           public sealed class Shell
                           {
                               public static int Parse(string value) => value.Length;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Core
                           {
                               public int Parse(string value) => value.Length;

                               public static int ParseExact(string value) => value.Length;

                               public static int Use(Core parser, string value) => parser.Parse(value) + ParseExact(value) + Shell.Parse(value);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ConvertToFromChangeTypeAndMethodGroup_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Globalization;

                           [Fprog.FunctionalCore]
                           public static class Numbers
                           {
                               public static int Int(string text) => Convert.{|FP0302:ToInt32|}(text, CultureInfo.InvariantCulture);

                               public static byte[] Decode(string text) => Convert.{|FP0302:FromBase64String|}(text);

                               public static object Change(object value) => Convert.{|FP0302:ChangeType|}(value, typeof(decimal), CultureInfo.InvariantCulture);

                               public static Func<string, int> Parser() => Convert.{|FP0302:ToInt32|};

                               public static Func<string, byte[]> Decoder() => Convert.{|FP0302:FromHexString|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_TotalScalarConvertOverloads_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static class Numbers
                           {
                               public static int Same(int value) => Convert.ToInt32(value);
                               public static DateTime SameDate(DateTime value) => Convert.ToDateTime(value);
                               public static int FromByte(byte value) => Convert.ToInt32(value);
                               public static Func<byte, int> FromByte() => Convert.ToInt32;
                               public static long FromUnsigned(uint value) => Convert.ToInt64(value);
                               public static int FromBit(bool value) => Convert.ToInt32(value);
                               public static bool Nonzero(double value) => Convert.ToBoolean(value);
                               public static char FromByteCode(byte value) => Convert.ToChar(value);
                               public static char FromCodeUnit(ushort value) => Convert.ToChar(value);
                               public static ushort CodeUnit(char value) => Convert.ToUInt16(value);
                               public static decimal ExactInteger(ulong value) => Convert.ToDecimal(value);
                               public static double Approximate(decimal value) => Convert.ToDouble(value);
                               public static float Round(double value) => Convert.ToSingle(value);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_OverflowingAndUnsupportedScalarConvertOverloads_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static class Numbers
                           {
                               public static int Narrow(long value) => Convert.{|FP0302:ToInt32|}(value);
                               public static Func<long, int> Narrow() => Convert.{|FP0302:ToInt32|};
                               public static ulong Unsigned(long value) => Convert.{|FP0302:ToUInt64|}(value);
                               public static char FromSigned(sbyte value) => Convert.{|FP0302:ToChar|}(value);
                               public static short NarrowCode(char value) => Convert.{|FP0302:ToInt16|}(value);
                               public static decimal FromReal(double value) => Convert.{|FP0302:ToDecimal|}(value);
                               public static double FromCode(char value) => Convert.{|FP0302:ToDouble|}(value);
                               public static decimal DecimalCode(char value) => Convert.{|FP0302:ToDecimal|}(value);
                               public static bool NonzeroCode(char value) => Convert.{|FP0302:ToBoolean|}(value);
                               public static DateTime Date(int value) => Convert.{|FP0302:ToDateTime|}(value);
                               public static int FromBox(byte value) => Convert.{|FP0302:ToInt32|}((object)value);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ConvertStringProducingAndTryMethods_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Globalization;

                           [Fprog.FunctionalCore]
                           public static class Numbers
                           {
                               public static string Format(int value) => Convert.ToString(value, CultureInfo.InvariantCulture);

                               public static string Hex(byte[] value) => Convert.ToHexString(value);

                               public static string HexLower(byte[] value) => Convert.ToHexStringLower(value);

                               public static string Encode(byte[] value) => Convert.ToBase64String(value);

                               public static int EncodeTo(byte[] value, char[] output) => Convert.ToBase64CharArray(value, 0, value.Length, output, 0);

                               public static int Decode(string text, Span<byte> bytes)
                                   => Convert.TryFromBase64String(text, bytes, out var count) ? count : 0;

                               public static Func<byte[], string> Formatter() => Convert.ToHexString;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_SeedlessAggregateAndValueExtremaAndAverage_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Stats
                           {
                               public static int Combine(IEnumerable<int> xs) => xs.{|FP0302:Aggregate|}((a, b) => a + b);

                               public static int Minimum(IEnumerable<int> xs) => xs.{|FP0302:Min|}();

                               public static int Maximum(IEnumerable<int> xs) => xs.{|FP0302:Max|}();

                               public static double Mean(IEnumerable<int> xs) => xs.{|FP0302:Average|}();

                               public static int Closest(IEnumerable<int> xs) => xs.{|FP0302:MinBy|}(x => Math.Abs(x));

                               public static int Farthest(IEnumerable<int> xs) => xs.{|FP0302:MaxBy|}(x => Math.Abs(x));

                               public static Func<IEnumerable<int>, Func<int, int, int>, int> CombineGroup() => Enumerable.{|FP0302:Aggregate|};

                               public static Func<IEnumerable<int>, int> MaxGroup() => Enumerable.{|FP0302:Max|};

                               public static Func<IEnumerable<int>, double> MeanGroup() => Enumerable.{|FP0302:Average|};

                               public static Func<IEnumerable<int>, Func<int, int>, int> ClosestGroup() => Enumerable.{|FP0302:MinBy|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_SeededAggregateAndNullableOrReferenceExtremaAndAverage_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Stats
                           {
                               public static int Combine(IEnumerable<int> xs) => xs.Aggregate(0, (a, b) => a + b);

                               public static int? Maximum(IEnumerable<int?> xs) => xs.Max();

                               public static double? Mean(IEnumerable<int?> xs) => xs.Average();

                               public static string? Minimum(IEnumerable<string> names) => names.Min(StringComparer.Ordinal);

                               public static string? Shortest(IEnumerable<string> names) => names.MinBy(name => name.Length);

                               public static Func<IEnumerable<int>, int, Func<int, int, int>, int> CombineGroup() => Enumerable.Aggregate;

                               public static Func<IEnumerable<int?>, int?> MaxGroup() => Enumerable.Max;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_CollectionDequeuePeekPopAndMethodGroups_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Next(Queue<int> queue) => queue.{|FP0302:Dequeue|}();

                               public static int Read(PriorityQueue<int, int> queue) => queue.{|FP0302:Peek|}();

                               public static int Previous(Stack<int> stack) => stack.{|FP0302:Pop|}();

                               public static ImmutableStack<int> Previous(ImmutableStack<int> stack) => stack.{|FP0302:Pop|}();

                               public static Func<int> Reader(Queue<int> queue) => queue.{|FP0302:Dequeue|};

                               public static Func<int> Top(Stack<int> stack) => stack.{|FP0302:Peek|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_CollectionTryOperationsAndSimilarMethodOutsideCollections_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed class Score
                           {
                               public int Pop() => 0;

                               public static int Next(Queue<int> queue) => queue.TryDequeue(out var value) ? value : 0;

                               public static int Read(Queue<int> queue) => queue.TryPeek(out var value) ? value : 0;

                               public static int Previous(Stack<int> stack) => stack.TryPop(out var value) ? value : 0;

                               public static Func<int> Top(Score score) => score.Pop;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_KeysAndPositionsOfKeyedCollection_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.ObjectModel;

                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static string Key(KeyedCollection<string, string> values, string key)
                                   => values.Contains(key) ? {|FP0302:values[key]|} : "";

                               public static string Position(KeyedCollection<string, string> values, int index)
                                   => {|FP0302:values[index]|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ArrayStringAndSpanIndexAndRangeReads_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int One(int[] values) => {|FP0302:values[0]|};

                               public static int Matrix(int[,] values) => {|FP0302:values[0, 0]|};

                               public static int FromEnd(List<int> values, Index index) => {|FP0302:values[index]|};

                               public static int[] Slice(int[] values, Range range) => {|FP0302:values[range]|};

                               public static char Char(string text) => {|FP0302:text[^1]|};

                               public static char? Maybe(string? text) => {|FP0302:text?[0]|};

                               public static string Slice(string text) => {|FP0302:text[1..]|};

                               public static Span<int> Slice(Span<int> values) => {|FP0302:values[1..]|};

                               public static int Value(ReadOnlySpan<int> values) => {|FP0302:values[0]|};

                               public static int ReadAndWrite(int[] values)
                               {
                                   {|FP0302:values[0]|}++;
                                   return 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_PositionalWritesOnly_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Writes
                           {
                               public static int Set(int[] xs, Span<int> span, List<int> list)
                               {
                                   xs[0] = 1;
                                   (xs[1], span[0], list[0]) = (2, 3, 4);
                                   Arg(out xs[2]);
                                   return 0;

                                   static void Arg(out int value) => value = 1;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_NullableValueUnguarded_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Value(int? number) => number.{|FP0302:Value|};

                               public static int Wrong(int? number, int? other) => other.HasValue ? number.{|FP0302:Value|} : 0;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_NullableValueHasValueOrTestsAgainstNull_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int HasValue(int? x) => x.HasValue ? x.Value : 0;

                               public static int Equal(int? x) => x == null ? 0 : x.Value;

                               public static int NonNull(int? x) => x is not null ? x.Value : 0;

                               public static int Exists(int? x) => x is { } ? x.Value : 0;

                               public static int Present(int? x) => x is int ? x.Value : 0;

                               public static string MemberName(int? x) => nameof(x.Value);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_IntegerAndDecimalDivisionRemainderAndCompoundForms_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Numerics;

                           [Fprog.FunctionalCore]
                           public static class Numbers
                           {
                               public static int One(int a, int b) => a {|FP0302:/|} b;

                               public static decimal Two(decimal a, decimal b) => a {|FP0302:%|} b;

                               public static Int128 Three(Int128 a, Int128 b) => a {|FP0302:/|} b;

                               public static BigInteger Four(BigInteger a, BigInteger b) => a {|FP0302:%|} b;

                               public static int? Lifted(int? a, int? b) => a {|FP0302:/|} b;

                               public static int Update(int a, int b)
                               {
                                   a {|FP0302:/=|} b;
                                   a {|FP0302:%=|} b;
                                   return a;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_FloatingPointDivisionAndKnownNonzeroDivisors_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Numbers
                           {
                               public static double One(double a, double b) => a / b;

                               public static float Two(float a, float b) => a % b;

                               public static int WithConstant(int value)
                               {
                                   const int ten = 10;
                                   return value / ten;
                               }

                               public static decimal WithLiteral(decimal value) => value % 2.25m;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ExplicitDowncastAndUnboxingFromNamedSource_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public abstract class Root;

                           [Fprog.FunctionalCore]
                           public sealed class Leaf(int key) : Root
                           {
                               public int Key { get; } = key;
                           }

                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Down(Root value) => ({|FP0302:(Leaf)value|}).Key;

                               public static int Unbox(IFormattable number) => {|FP0302:(int)number|};

                               public static int UnboxValueType(ValueType number) => {|FP0302:(int)number|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_SafeNamedCastsAndRuntimeTypeInspectionOwnedByFP0601_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public abstract class Root;

                           [Fprog.FunctionalCore]
                           public sealed class Leaf : Root;

                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static Root Up(Leaf value) => (Root)value;

                               public static Leaf? Down(Root value) => value as Leaf;

                               public static int Unbox(object number) => (int)number;

                               public static T Generic<T>(object value) => (T)value;

                               public static int Runtime(dynamic value) => (int)value;

                               public static long Widen(int number) => (long)number;

                               public static bool IsLeaf(Root value) => value is Leaf;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_EnclosingIfAndElseGuardWithLengthAndCount_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Head(int[] values)
                               {
                                   if (values.Length > 0)
                                       return values[0];
                                   return 0;
                               }

                               public static int Head(List<int> values)
                               {
                                   if (values.Count == 0)
                                       return 0;
                                   else
                                       return values[0];
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_EnclosingWhileGuardWithIsEmpty_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Head(ReadOnlySpan<int> values)
                               {
                                   while (!values.IsEmpty)
                                       return values[0];
                                   return 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_EnclosingForGuardWithIndexComparison_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Last(int[] values)
                               {
                                   for (int index = values.Length - 1; index >= 0; index--)
                                       return values[index];
                                   return 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ConditionalIncludingListPatternAndIsDefaultOrEmpty_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Select(int[] values) => values is [_, ..] ? values[0] : 0;

                               public static int Select(ImmutableArray<int> values) => values.IsDefaultOrEmpty ? 0 : values[0];

                               public static int Select(string text) => text is { Length: > 0 } ? text[0] : 0;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_WhenGuardsInSwitchAndCatch_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Divide(int mode, int divisor) => mode switch
                               {
                                   0 when divisor is not 0 => 10 / divisor,
                                   _ => 0
                               };

                               public static int First(int mode, int[] values)
                               {
                                   switch (mode)
                                   {
                                       case 0 when values.Length > 0: return values[0];
                                       default: return 0;
                                   }
                               }

                               public static int CatchFilter(System.Func<int> read, int divisor)
                               {
                                   try
                                   {
                                       return read();
                                   }
                                   catch (System.InvalidOperationException) when (divisor != 0)
                                   {
                                       return 10 / divisor;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_AndOrLeftGuardsWithAnyAndComparison_NoReport()
    {
        return VerifyAsync("""
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static bool Head(int[] values) => values.Any() && values[0] > 0;

                               public static bool Divide(int divisor) => divisor == 0 || 100 / divisor > 5;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_PrecedingIfInSameOrEnclosingBlock_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Select(int[] values, int index, int divisor)
                               {
                                   if ((uint)index >= (uint)values.Length)
                                       return 0;

                                   {
                                       if (divisor == 0)
                                           return 0;

                                       return values[index] % divisor;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_WrongSymbolLaterConditionAndDifferentReceiver_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Values(int[] Items, int? Number);

                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Wrong(Values first, Values second)
                                   => first.Items.Length > 0 ? {|FP0302:second.Items[0]|} : 0;

                               public static int Nullable(Values first, Values second)
                                   => first.Number.HasValue ? second.Number.{|FP0302:Value|} : 0;

                               public static int Index(int[] values, int first, int second)
                                   => first > 0 ? {|FP0302:values[second]|} : 0;

                               public static bool Later(int[] values)
                                   => {|FP0302:values[0]|} > 0 && values.Length > 0;

                               public static int DifferentBlock(int[] values)
                               {
                                   {
                                       if (values.Length == 0) return 0;
                                   }

                                   return {|FP0302:values[0]|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ParseOfConfiguredPureTypesAndNamespaces_Reports()
    {
        const string source = """
                              namespace Inputs.Trusted
                              {
                                  public static class Reader
                                  {
                                      public static int Parse(string input) => input.Length;
                                  }
                              }

                              namespace Inputs.Other
                              {
                                  public static class Exact
                                  {
                                      public static int ParseExact(string input) => input.Length;
                                  }

                                  public static class Reader
                                  {
                                      public static int Parse(string input) => input.Length;
                                  }
                              }

                              [Fprog.FunctionalCore]
                              public static class Input
                              {
                                  public static int Read(string input)
                                      => Inputs.Trusted.Reader.{|FP0302:Parse|}(input);

                                  public static System.Func<string, int> Exact() => Inputs.Other.Exact.{|FP0302:ParseExact|};

                                  public static int Other(string input) => Inputs.Other.Reader.Parse(input);
                              }
                              """;
        const string config = """
                              is_global = true
                              fprog_pure_apis = Inputs.Trusted, Inputs.Other.Exact
                              """;
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0302", source, config, testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0302_InheritedIndexerWithIndexAndRange_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;

                           public sealed class Values : List<int>;

                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Last(Values values, Index index) => {|FP0302:values[index]|};
                               public static List<int> Cut(Values values, Range range) => {|FP0302:values[range]|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_FromEndRangeAndSharedCaseLabelGuards_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Last(int[] values, int fromEnd, int length)
                                   => 0 < fromEnd && fromEnd <= length ? values[^fromEnd] : 0;

                               public static int[] Slice(int[] values, int start, int length)
                                   => 0 <= start && start <= length ? values[start..] : [];

                               public static int Shared(int choice, int[] values)
                               {
                                   switch (choice)
                                   {
                                       case 0 when values.Length > 0:
                                       case 1 when values is [_, ..]:
                                           return values[0];
                                       default:
                                           return 0;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_WhenOnOnlyOneLabelOfSharedSection_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Shared(int choice, int[] values)
                               {
                                   switch (choice)
                                   {
                                       case 0 when values.Length > 0:
                                       case 1:
                                           return {|FP0302:values[0]|};
                                       default:
                                           return 0;
                                   }
                               }
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0302", source, testContext.CancellationToken);
    }
}
