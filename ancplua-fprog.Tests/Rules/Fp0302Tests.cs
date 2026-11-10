using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0302Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0302_SystemParseMethodsInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0302_ParseThroughIParsableTypeParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Generic
        {
            public static T Read<T>(string text) where T : System.IParsable<T> => T.{|FP0302:Parse|}(text, null);
        }
        """);

    [TestMethod]
    public Task FP0302_ParseThroughUsingStatic_Reports() => VerifyAsync("""
        using static System.Int32;

        [Fprog.FunctionalCore]
        public static class Parsing
        {
            public static int Number(string text) => {|FP0302:Parse|}(text);
        }
        """);

    [TestMethod]
    public Task FP0302_EnumerablePartialMethodsAsExtensions_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0302_EnumerablePartialMethodsCalledStatically_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0302_EnumerablePartialMethodInConditionalAccess_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Lists
        {
            public static int? Head(List<int>? xs) => xs?.{|FP0302:First|}();
        }
        """);

    [TestMethod]
    public Task FP0302_DictionaryIndexerReads_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0302_DictionaryIndexerReadAndWrite_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0302_IndexerOfClassDerivedFromDictionary_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        public sealed class Registry : Dictionary<string, int> { }

        [Fprog.FunctionalCore]
        public static class Lookup
        {
            public static int Get(Registry registry) => {|FP0302:registry["a"]|};
        }
        """);

    [TestMethod]
    public Task FP0302_ConditionalIndexerSpanIncludesReceiver_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0302_InLambdaLocalFunctionAndInitializers_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0302_InTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public static class Inner
            {
                public static int Number(string text) => int.{|FP0302:Parse|}(text);
            }
        }
        """);

    [TestMethod]
    public Task FP0302_UnderAssemblyLevelCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0302_OutsideCore_NoReport() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Linq;

        public static class Shell
        {
            public static int Number(string text) => int.Parse(text);

            public static int Head(IEnumerable<int> xs) => xs.First();

            public static int Get(IReadOnlyDictionary<string, int> d) => d["a"];
        }
        """);

    [TestMethod]
    public Task FP0302_TotalAlternativesInCore_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0302_ParseExactIsAnotherName_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Parsing
        {
            public static System.DateTime When(string text)
                => System.DateTime.ParseExact(text, "yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }
        """);

    [TestMethod]
    public Task FP0302_ParseOnTypeOutsideSystemNamespace_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0302_MethodGroupIsAReferenceNotACall_NoReport() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Groups
        {
            public static IEnumerable<int> Numbers(IEnumerable<string> xs) => xs.Select(int.Parse);

            public static System.Func<IEnumerable<int>, int> Head() => Enumerable.First;

            public static string Name() => nameof(int.Parse);
        }
        """);

    [TestMethod]
    public Task FP0302_QueryableFirstIsNotEnumerable_NoReport() => VerifyAsync("""
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Queries
        {
            public static int Head(IQueryable<int> xs) => xs.First();
        }
        """);

    [TestMethod]
    public Task FP0302_DictionaryIndexerWrites_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0302_IndexersNotDeclaredOnTheDictionaryTypes_NoReport() => VerifyAsync("""
        using System.Collections.Concurrent;
        using System.Collections.Generic;
        using System.Collections.Immutable;

        [Fprog.FunctionalCore]
        public static class Others
        {
            public static int Concurrent(ConcurrentDictionary<string, int> d) => d["a"];

            public static int Sorted(SortedDictionary<string, int> d) => d["a"];

            public static int Immutable(ImmutableDictionary<string, int> d) => d["a"];
        }
        """);

    [TestMethod]
    public Task FP0302_CompilerGeneratedRecordMembers_NoReport() => VerifyAsync("""
        using System.Collections.Generic;

        [Fprog.FunctionalCore]
        public sealed record Snapshot(IReadOnlyDictionary<string, int> Values, string Raw);
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0302", source, testContext.CancellationToken);
}
