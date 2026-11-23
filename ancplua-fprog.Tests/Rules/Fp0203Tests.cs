using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0203Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0203_RecordPropertyOfGenericList_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Order
                           {
                               public List<int> {|FP0203:Lines|} { get; init; } = [];
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_PositionalRecordParameters_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Basket(string Owner, List<string> {|FP0203:Items|}, int[] {|FP0203:Counts|});
                           """);
    }

    [TestMethod]
    public Task FP0203_PositionalRecordStruct_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public readonly record struct Point(int X, int[] {|FP0203:Rest|});

                           [Fprog.FunctionalCore]
                           public record struct MutablePoint(int X, System.Text.StringBuilder {|FP0203:Label|});
                           """);
    }

    [TestMethod]
    public Task FP0203_ReadonlyStructFieldsReportEachDeclarator_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_ArrayAndStringBuilderMembers_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_ReadOnlyInterfacesAreForbiddenToo_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_ObjectModelAndNonGenericCollections_Reports()
    {
        return VerifyAsync("""
                           using System.Collections;
                           using System.Collections.Concurrent;
                           using System.Collections.ObjectModel;

                           [Fprog.FunctionalCore]
                           public sealed record Bag(
                               ReadOnlyCollection<int> {|FP0203:ReadOnly|},
                               ObservableCollection<int> {|FP0203:Observable|},
                               ConcurrentDictionary<string, int> Concurrent,
                               ConcurrentBag<int> Unordered,
                               ArrayList {|FP0203:Untyped|},
                               IEnumerable {|FP0203:NonGeneric|},
                               Hashtable {|FP0203:Table|});
                           """);
    }

    [TestMethod]
    public Task FP0203_NullableAnnotationDoesNotChangeType_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Optional(List<int>? {|FP0203:Maybe|}, int[]? {|FP0203:MaybeArray|})
                           {
                               public KeyValuePair<string, int>? {|FP0203:Pair|} { get; init; }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_DecidedForbiddenTypeAsTypeArgumentDoesNotCount_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public sealed record Nested(
                               ImmutableArray<List<int>> Lists,
                               ImmutableArray<int[]> Arrays,
                               (int Id, List<string> Tags) Tuple,
                               System.Func<IEnumerable<int>> Factory);
                           """);
    }

    [TestMethod]
    public Task FP0203_OtherSystemCollectionsNamespaces_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Specialized;

                           [Fprog.FunctionalCore]
                           public sealed record Headers(NameValueCollection Values, BitVector32 Bits);
                           """);
    }

    [TestMethod]
    public Task FP0203_DecidedStaticAndConstMembersCount_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_DecidedComputedPropertiesCount_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_DecidedIndexerReportsAtThisKeyword_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public sealed record Matrix(ImmutableArray<ImmutableArray<int>> Rows)
                           {
                               public int[] {|FP0203:this|}[int row] => Rows[row].ToArray();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_ExplicitInterfaceProperty_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_ReadonlyRefStructField_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public readonly ref struct Window
                           {
                               public readonly int[] {|FP0203:Data|};

                               public Window(int[] data) => Data = data;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_TypeNestedInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_AssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public sealed record Anywhere(System.Collections.Generic.List<int> {|FP0203:Items|});
                           """);
    }

    [TestMethod]
    public Task FP0203_BackingFieldOfAutoPropertyReportsOnce_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public record Shape
                           {
                               public int[] {|FP0203:Corners|} { get; init; } = [];
                           }

                           [Fprog.FunctionalCore]
                           public sealed record Square(int Side) : Shape;
                           """);
    }

    [TestMethod]
    public Task FP0203_OutsideCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_PlainClassAndMutableStructInCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_ImmutableAndFrozenCollections_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_CollectionsInMethodsLocalsAndParameters_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_DecidedPrimaryConstructorParameterOfReadonlyStructIsNotAMember_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public readonly struct Sum(int[] values)
                           {
                               public int Total => System.Linq.Enumerable.Sum(values);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_DecidedExplicitMemberForPositionalParameterReportsAtMember_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Batch(List<int> Items)
                           {
                               public List<int> {|FP0203:Items|} { get; } = Items;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_RecordSynthesizedMembersAndInheritedMembers_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0203_GenericRecordsAndRecordsInGenericTypes_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Box<T>(List<T> {|FP0203:Items|}, T Single)
                           {
                               public Dictionary<string, T>? {|FP0203:ByKey|} { get; init; }
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Outer<T>
                           {
                               public readonly record struct Inner(ISet<T> {|FP0203:Marks|}, T Value);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_SystemArrayIsAnArray_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Raw
                           {
                               public System.Array {|FP0203:Values|} { get; init; } = new int[0];
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_PointerAndFunctionPointerSignatures_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public readonly unsafe struct Native
                           {
                               public readonly KeyValuePair<int, int>* Pair;
                               public readonly delegate*<List<int>, int> Count;
                               public readonly delegate*<int, int> Plain;
                               public readonly int* Raw;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_DecidedPartialPropertyReportsBothParts_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed partial record Order
                           {
                               public partial List<int> {|FP0203:Lines|} { get; }
                           }

                           public sealed partial record Order
                           {
                               public partial List<int> {|FP0203:Lines|} => [];
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_AutoGeneratedFile_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           [Fprog.FunctionalCore]
                           public sealed record Generated(int[] Values, System.Collections.Generic.List<int> Items);
                           """);
    }

    [TestMethod]
    public Task FP0203_GeneratedCodeAttribute_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
                           public sealed record Generated(int[] Values)
                           {
                               public System.Text.StringBuilder Text { get; init; } = new();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_DecidedDerivedPositionalParameterReportsOnlyAtBase_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public record Base(int[] {|FP0203:Items|});

                           [Fprog.FunctionalCore]
                           public sealed record Derived(int[] Items, int Extra) : Base(Items);
                           """);
    }

    [TestMethod]
    public Task FP0203_PlainTypesNestedInRecord_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Container(int Id)
                           {
                               public sealed class Cache
                               {
                                   private readonly List<int> items = [];
                                   public int Count => items.Count;
                               }

                               public struct Scratch
                               {
                                   public int[] Buffer;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_AliasGlobalQualifiedCommentedAndVerbatimNames_Reports()
    {
        return VerifyAsync("""
                           using Ints = System.Collections.Generic.List<int>;
                           using Bytes = byte[];

                           [Fprog.FunctionalCore]
                           public sealed record Spans(Ints {|FP0203:Aliased|}, Bytes {|FP0203:Raw|})
                           {
                               public global::System.Collections.Generic.Dictionary<int, int> /* map */ {|FP0203:Map|} { get; init; } = [];
                               public int[]
                                   {|FP0203:@class|} { get; init; } = [];
                               public System.Collections.Generic.IList<int?>? {|FP0203:Maybe|} { get; init; }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_LargeTupleAndNestedEnumeratorTypes_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Wide(
                               (int A, int B, int C, int D, int E, int F, int G, List<int> H) Tuple,
                               List<int>.Enumerator {|FP0203:Cursor|});
                           """);
    }

    [TestMethod]
    public Task FP0203_ImmutableBuilderButNotEnumerator_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public sealed record Building(
                               ImmutableArray<int>.Builder {|FP0203:Builder|},
                               ImmutableDictionary<string, int>.Enumerator Cursor);
                           """);
    }

    [TestMethod]
    public Task FP0203_AbstractVirtualOverrideAndRequiredProperties_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public abstract record Base
                           {
                               public abstract IReadOnlyList<int> {|FP0203:Items|} { get; }
                               public virtual int[] {|FP0203:Extra|} => [];
                               public required List<string> {|FP0203:Names|} { get; init; }
                           }

                           [Fprog.FunctionalCore]
                           public sealed record Derived : Base
                           {
                               public override IReadOnlyList<int> {|FP0203:Items|} => [];
                               public new int[] {|FP0203:Extra|} => [1];
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_DecidedNonCollectionTypesFromGenericNamespaceCount_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public readonly record struct Ordering(
                               IEqualityComparer<string> {|FP0203:Equality|},
                               IComparer<int> {|FP0203:Order|},
                               IAsyncEnumerable<int> {|FP0203:Stream|});
                           """);
    }

    [TestMethod]
    public Task FP0203_DecidedEventIsNotAFieldOrProperty_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Notifier(string Name)
                           {
                               public event System.Action<int[]>? Changed;

                               public event System.Action<System.Collections.Generic.List<int>> Added
                               {
                                   add { }
                                   remove { }
                               }

                               public bool HasListeners() => Changed is not null;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_FieldKeywordPropertyReportsOnce_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Keyed
                           {
                               public List<int> {|FP0203:Items|}
                               {
                                   get => field;
                                   init => field = value;
                               } = [];
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_PositionalParamsAndDefaultParameters_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Args(
                               string Name,
                               [property: System.CLSCompliant(false)] List<int>? {|FP0203:Items|} = null,
                               params int[] {|FP0203:Values|});
                           """);
    }

    [TestMethod]
    public Task FP0203_RecordDeclaringPrintMembersWithStringBuilder_NoReport()
    {
        return VerifyAsync("""
                           using System.Text;

                           [Fprog.FunctionalCore]
                           public sealed record Named(string Name)
                           {
                               private bool PrintMembers(StringBuilder builder)
                               {
                                   builder.Append(Name);
                                   return true;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_ExtensionPropertyOnRecord_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Order(int Id);

                           [Fprog.FunctionalCore]
                           public static class OrderExtensions
                           {
                               extension(Order order)
                               {
                                   public List<int> Ids => [order.Id];
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_ForbiddenTypeArgumentOfContainingType_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public sealed class Outer<T>
                           {
                               public sealed class Inner;
                           }

                           [Fprog.FunctionalCore]
                           public sealed record Holder(Outer<List<int>>.Inner Value, Outer<int>.Inner Plain);
                           """);
    }

    [TestMethod]
    public Task FP0203_LookalikeNamespacesAndTypeNames_NoReport()
    {
        return VerifyAsync("""
                           namespace MyApp.System.Collections.Generic
                           {
                               public sealed record List<T>(T Head);
                           }

                           namespace Domain.Collections
                           {
                               public sealed record Bag(int Count);
                           }

                           namespace Domain.Text
                           {
                               public sealed record StringBuilder(string Value);
                           }

                           namespace Domain
                           {
                               [Fprog.FunctionalCore]
                               public sealed record Holder(
                                   MyApp.System.Collections.Generic.List<int> Items,
                                   Collections.Bag Bag,
                                   Text.StringBuilder Builder);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_TypicalFunctionalCoreShapes_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public abstract record Option<T>
                           {
                               private Option() { }

                               public sealed record Some(T Value) : Option<T>;

                               public sealed record None : Option<T>;
                           }

                           [Fprog.FunctionalCore]
                           public sealed record Pipeline<T>(
                               Func<T, ImmutableList<T>> Expand,
                               Lazy<ImmutableArray<T>> Cache,
                               (int Count, ImmutableHashSet<string> Keys) Summary,
                               int? Limit,
                               IComparable<T>? Order)
                           {
                               public static readonly Pipeline<T>? Empty = null;
                               public ImmutableArray<T> Seed { get; init; } = [];
                               public int Total => Seed.Length;
                           }

                           [Fprog.FunctionalCore]
                           public readonly ref struct Cursor(ReadOnlySpan<char> text, int position)
                           {
                               public readonly ReadOnlySpan<char> Text = text;
                               public int Position { get; } = position;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_ExternStaticAndIndexerShapesInReadonlyStruct_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public readonly struct Odd
                           {
                               public static extern int[] {|FP0203:External|} { get; }
                               public static List<int> {|FP0203:Shared|} = [];
                               public IReadOnlyList<int> {|FP0203:this|}[int index] => [index];
                               public readonly ImmutableHolder Holder;
                           }

                           public sealed class ImmutableHolder;
                           """);
    }

    [TestMethod]
    public Task FP0203_ForbiddenTypeOnlyAsTypeArgument_NoReport()
    {
        return VerifyAsync("""
                           #nullable enable
                           using System;
                           using System.Collections.Generic;
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public sealed record Parser<T>(Func<string, IEnumerable<(T Value, string Remainder)>> Run);

                           [Fprog.FunctionalCore]
                           public sealed record Lookup(
                               ImmutableArray<KeyValuePair<string, int>> Entries,
                               ImmutableDictionary<string, ImmutableArray<int>> ByKey,
                               Lazy<IReadOnlyList<string>> Names);

                           [Fprog.FunctionalCore]
                           public sealed record Rule<T>(string Name, Func<IEnumerable<T>, bool> Holds, Comparison<T> Order);
                           """);
    }

    [TestMethod]
    public Task FP0203_ListedOwnTypeWithAllowedTypeArguments_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public sealed record Outer(
                               List<ImmutableArray<int>> {|FP0203:Lists|},
                               IEnumerable<Func<int, string>> {|FP0203:Steps|},
                               IReadOnlyDictionary<string, ImmutableList<int>> {|FP0203:ByKey|},
                               ImmutableArray<int>[] {|FP0203:Rows|});
                           """);
    }

    [TestMethod]
    public Task FP0203_NullableOfListedValueTypeIsUnwrapped_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public readonly struct Window
                           {
                               public readonly System.Nullable<KeyValuePair<int, int>> {|FP0203:Pair|};
                               public readonly List<int>.Enumerator? {|FP0203:Cursor|};
                               public HashSet<int>.Enumerator? {|FP0203:Keys|} { get; init; }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_NullableUnwrapStopsAtOneLevel_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           using System.Text;

                           [Fprog.FunctionalCore]
                           public sealed record Wrapped(
                               ImmutableArray<int[]>? Rows,
                               (List<int> Items, int Count)? Pair,
                               Lazy<StringBuilder> Text,
                               Func<int[]> Make);
                           """);
    }

    [TestMethod]
    public Task FP0203_MemoryAndArraySegmentInRecordsAndReadonlyStructs_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public sealed record Slice(Memory<int> {|FP0203:Memory|}, ArraySegment<int> {|FP0203:Segment|})
                           {
                               public ImmutableList<int>.Builder {|FP0203:Builder|} { get; init; } = ImmutableList.CreateBuilder<int>();
                           }

                           [Fprog.FunctionalCore]
                           public readonly struct Page
                           {
                               public readonly ArraySegment<int>? {|FP0203:Segment|};
                               public Memory<int> {|FP0203:Data|} { get; init; }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0203_ReadOnlyMemoryAndReadOnlySpan_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public sealed record Slice(ReadOnlyMemory<int> Memory);

                           [Fprog.FunctionalCore]
                           public readonly ref struct Page(ReadOnlySpan<int> values)
                           {
                               public ReadOnlySpan<int> Values { get; } = values;
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0203", source, testContext.CancellationToken);
    }
}