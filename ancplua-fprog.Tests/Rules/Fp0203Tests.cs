using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0203Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0203_RecordPropertyOfGenericList_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        [Fprog.FunctionalCore]
        public sealed record Order
        {
            public List<int> {|FP0203:Lines|} { get; init; } = [];
        }
        """);

    [TestMethod]
    public Task FP0203_PositionalRecordParameters_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        [Fprog.FunctionalCore]
        public sealed record Basket(string Owner, List<string> {|FP0203:Items|}, int[] {|FP0203:Counts|});
        """);

    [TestMethod]
    public Task FP0203_PositionalRecordStruct_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public readonly record struct Point(int X, int[] {|FP0203:Rest|});

        [Fprog.FunctionalCore]
        public record struct MutablePoint(int X, System.Text.StringBuilder {|FP0203:Label|});
        """);

    [TestMethod]
    public Task FP0203_ReadonlyStructFieldsReportEachDeclarator_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        [Fprog.FunctionalCore]
        public readonly struct Lookup
        {
            private readonly Dictionary<string, int> {|FP0203:byName|};
            private readonly int[] {|FP0203:first|}, {|FP0203:second|};

            public Lookup(Dictionary<string, int> byName, int[] first, int[] second)
            {
                this.byName = byName;
                this.first = first;
                this.second = second;
            }

            public int Count => byName.Count + first.Length + second.Length;
        }
        """);

    [TestMethod]
    public Task FP0203_ArrayAndStringBuilderMembers_Reports() => VerifyAsync("""
        using System.Text;

        [Fprog.FunctionalCore]
        public sealed record Document
        {
            public byte[] {|FP0203:Bytes|} { get; init; } = [];
            public int[][] {|FP0203:Jagged|} { get; init; } = [];
            public int[,] {|FP0203:Grid|} { get; init; } = new int[1, 1];
            public StringBuilder {|FP0203:Text|} { get; init; } = new();
            private readonly string[] {|FP0203:names|} = [];

            public int NameCount() => names.Length;
        }
        """);

    [TestMethod]
    public Task FP0203_ReadOnlyInterfacesAreForbiddenToo_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        [Fprog.FunctionalCore]
        public sealed record Views(
            IEnumerable<int> {|FP0203:Sequence|},
            IReadOnlyList<int> {|FP0203:List|},
            IReadOnlyCollection<int> {|FP0203:Collection|},
            IReadOnlyDictionary<string, int> {|FP0203:Map|},
            IReadOnlySet<int> {|FP0203:Set|},
            ISet<int> {|FP0203:MutableSet|});
        """);

    [TestMethod]
    public Task FP0203_ObjectModelConcurrentAndNonGenericCollections_Reports() => VerifyAsync("""
        using System.Collections;
        using System.Collections.Concurrent;
        using System.Collections.ObjectModel;

        [Fprog.FunctionalCore]
        public sealed record Bag(
            ReadOnlyCollection<int> {|FP0203:ReadOnly|},
            ObservableCollection<int> {|FP0203:Observable|},
            ConcurrentDictionary<string, int> {|FP0203:Concurrent|},
            ConcurrentBag<int> {|FP0203:Unordered|},
            ArrayList {|FP0203:Untyped|},
            IEnumerable {|FP0203:NonGeneric|},
            Hashtable {|FP0203:Table|});
        """);

    [TestMethod]
    public Task FP0203_NullableAnnotationDoesNotChangeType_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        [Fprog.FunctionalCore]
        public sealed record Optional(List<int>? {|FP0203:Maybe|}, int[]? {|FP0203:MaybeArray|})
        {
            public KeyValuePair<string, int>? {|FP0203:Pair|} { get; init; }
        }
        """);

    [TestMethod]
    public Task FP0203_DecidedForbiddenTypeAsTypeArgumentCounts_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Collections.Immutable;

        [Fprog.FunctionalCore]
        public sealed record Nested(
            ImmutableArray<List<int>> {|FP0203:Lists|},
            ImmutableArray<int[]> {|FP0203:Arrays|},
            (int Id, List<string> Tags) {|FP0203:Tuple|},
            System.Func<IEnumerable<int>> {|FP0203:Factory|});
        """);

    [TestMethod]
    public Task FP0203_DecidedOtherSystemCollectionsNamespacesCount_Reports() => VerifyAsync("""
        using System.Collections.Specialized;

        [Fprog.FunctionalCore]
        public sealed record Headers(NameValueCollection {|FP0203:Values|}, BitVector32 {|FP0203:Bits|});
        """);

    [TestMethod]
    public Task FP0203_DecidedStaticAndConstMembersCount_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        [Fprog.FunctionalCore]
        public sealed record Catalog
        {
            public static readonly IReadOnlyList<string> {|FP0203:Known|} = ["a", "b"];
            private const List<int>? {|FP0203:Nothing|} = null;
            public static int[] {|FP0203:Defaults|} { get; } = [1, 2];

            public static int Size() => Known.Count + Defaults.Length + (Nothing?.Count ?? 0);
        }
        """);

    [TestMethod]
    public Task FP0203_DecidedComputedPropertiesCount_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Linq;

        [Fprog.FunctionalCore]
        public sealed record Totals(ImmutableArray<int> Values)
        {
            public IEnumerable<int> {|FP0203:Positive|} => Values.Where(value => value > 0);

            public int[] {|FP0203:Copy|}
            {
                get { return Values.ToArray(); }
            }
        }
        """);

    [TestMethod]
    public Task FP0203_DecidedIndexerReportsAtThisKeyword_Reports() => VerifyAsync("""
        using System.Collections.Immutable;
        using System.Linq;

        [Fprog.FunctionalCore]
        public sealed record Matrix(ImmutableArray<ImmutableArray<int>> Rows)
        {
            public int[] {|FP0203:this|}[int row] => Rows[row].ToArray();
        }
        """);

    [TestMethod]
    public Task FP0203_ExplicitInterfaceProperty_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        public interface IHasTags
        {
            IReadOnlyList<string> Tags { get; }
        }

        [Fprog.FunctionalCore]
        public sealed record Tagged(string Name) : IHasTags
        {
            IReadOnlyList<string> IHasTags.{|FP0203:Tags|} => [Name];
        }
        """);

    [TestMethod]
    public Task FP0203_ReadonlyRefStructField_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public readonly ref struct Window
        {
            public readonly int[] {|FP0203:Data|};

            public Window(int[] data) => Data = data;
        }
        """);

    [TestMethod]
    public Task FP0203_TypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Model
        {
            public sealed record Line(string Sku, int[] {|FP0203:Sizes|});

            public sealed class Inner
            {
                public readonly record struct Cell(System.Collections.Generic.HashSet<int> {|FP0203:Marks|});
            }
        }
        """);

    [TestMethod]
    public Task FP0203_AssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public sealed record Anywhere(System.Collections.Generic.List<int> {|FP0203:Items|});
        """);

    [TestMethod]
    public Task FP0203_BackingFieldOfAutoPropertyReportsOnce_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public record Shape
        {
            public int[] {|FP0203:Corners|} { get; init; } = [];
        }

        [Fprog.FunctionalCore]
        public sealed record Square(int Side) : Shape;
        """);

    [TestMethod]
    public Task FP0203_OutsideCore_NoReport() => VerifyAsync("""
        using System.Collections.Generic;

        public sealed record Order(List<int> Lines, int[] Counts)
        {
            public System.Text.StringBuilder Text { get; init; } = new();
        }

        public readonly struct Lookup(Dictionary<string, int> map)
        {
            public readonly IReadOnlyList<int> Keys = [];
            public int Size => map.Count;
        }
        """);

    [TestMethod]
    public Task FP0203_PlainClassAndMutableStructInCore_NoReport() => VerifyAsync("""
        using System.Collections.Generic;

        [Fprog.FunctionalCore]
        public sealed class Service
        {
            private readonly List<int> items = [];
            public int[] Values { get; } = [];

            public int Count() => items.Count + Values.Length;
        }

        [Fprog.FunctionalCore]
        public struct Buffer
        {
            public List<int> Items { get; init; }
        }
        """);

    [TestMethod]
    public Task FP0203_ImmutableAndFrozenCollections_NoReport() => VerifyAsync("""
        using System.Collections.Frozen;
        using System.Collections.Immutable;

        [Fprog.FunctionalCore]
        public sealed record Snapshot(
            ImmutableArray<int> Values,
            ImmutableList<string> Names,
            ImmutableDictionary<string, int> Map,
            IImmutableSet<int> Set,
            FrozenSet<int> Frozen,
            FrozenDictionary<string, ImmutableArray<int>> Index,
            string Text,
            System.ReadOnlyMemory<int> Memory)
        {
            public ImmutableHashSet<int>? Optional { get; init; }
            private readonly ImmutableQueue<int> queue = ImmutableQueue<int>.Empty;

            public bool HasQueue() => !queue.IsEmpty;
        }
        """);

    [TestMethod]
    public Task FP0203_CollectionsInMethodsLocalsAndParameters_NoReport() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Text;

        [Fprog.FunctionalCore]
        public sealed record Report(ImmutableArray<string> Lines)
        {
            public string Render()
            {
                var builder = new StringBuilder();
                var seen = new HashSet<string>();
                foreach (var line in Lines)
                {
                    if (seen.Add(line))
                        builder.AppendLine(line);
                }

                return builder.ToString();
            }

            public static Report From(IEnumerable<string> lines, int[] skip) => new([.. lines]);

            public List<string> ToList() => [.. Lines];
        }
        """);

    [TestMethod]
    public Task FP0203_DecidedPrimaryConstructorParameterOfReadonlyStructIsNotAMember_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public readonly struct Sum(int[] values)
        {
            public int Total => System.Linq.Enumerable.Sum(values);
        }
        """);

    [TestMethod]
    public Task FP0203_DecidedExplicitMemberForPositionalParameterReportsAtMember_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        [Fprog.FunctionalCore]
        public sealed record Batch(List<int> Items)
        {
            public List<int> {|FP0203:Items|} { get; } = Items;
        }
        """);

    [TestMethod]
    public Task FP0203_RecordSynthesizedMembersAndInheritedMembers_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Money(decimal Amount, string Currency);

        [Fprog.FunctionalCore]
        public sealed record Derived(int Id) : Parent;

        [Fprog.FunctionalCore]
        public abstract record Parent
        {
            public System.Collections.Immutable.ImmutableArray<int> Codes { get; init; } = [];
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0203", source, testContext.CancellationToken);
}
