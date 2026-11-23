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

                               public static readonly int First = int.{|FP0302:Parse|}(Raw[0]);

                               public int Second => int.{|FP0302:Parse|}(Raw[1]);

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
    public Task FP0302_ParseExactIsAnotherName_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Parsing
                           {
                               public static System.DateTime When(string text)
                                   => System.DateTime.ParseExact(text, "yyyy", System.Globalization.CultureInfo.InvariantCulture);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0302_ParseOnTypeOutsideSystemNamespace_NoReport()
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

                                   public static System.Numerics.BigInteger Big(string text) => System.Numerics.BigInteger.Parse(text);
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
    public Task FP0302_TotalIndexersAndNonDictionaryIndexers_NoReport()
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

                               public static string Keyed(Keyed k) => k["a"];

                               public static int Items(List<int> xs, int[] ys, ImmutableArray<int> zs, ImmutableList<int> ws)
                                   => xs[0] + ys[0] + zs[0] + ws[0];

                               public static char Letter(string s) => s[0];

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

                               public static int Sum(ImmutableArray<int> xs) => xs.Any() ? xs.Aggregate((a, b) => a + b) : xs.Where(x => x > 0).Select(x => x).ToArray().Length;

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
    public Task FP0302_OddShapesInCore_NoReport()
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
                                   return s[0];
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

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0302", source, testContext.CancellationToken);
    }
}