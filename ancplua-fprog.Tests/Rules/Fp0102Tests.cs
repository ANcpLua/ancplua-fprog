using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0102Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0102_DateTimeNowUtcNowToday_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Clock
        {
            public static DateTime Local() => DateTime.{|FP0102:Now|};

            public static DateTime Utc() => System.DateTime.{|FP0102:UtcNow|};

            public static DateTime Day() => global::System.DateTime.{|FP0102:Today|};
        }
        """);

    [TestMethod]
    public Task FP0102_DateTimeOffsetNowUtcNow_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Clock
        {
            public static DateTimeOffset Local() => DateTimeOffset.{|FP0102:Now|};

            public static DateTimeOffset Utc() => DateTimeOffset.{|FP0102:UtcNow|};
        }
        """);

    [TestMethod]
    public Task FP0102_GuidNewGuidCallAndMethodGroup_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Ids
        {
            public static Guid Next() => Guid.{|FP0102:NewGuid|}();

            public static Func<Guid> Factory() => Guid.{|FP0102:NewGuid|};
        }
        """);

    [TestMethod]
    public Task FP0102_TimeProviderSystem_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Clock
        {
            public static TimeProvider Default() => TimeProvider.{|FP0102:System|};
        }
        """);

    [TestMethod]
    public Task FP0102_TimeProviderOtherMembersOnParameter_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Clock
        {
            public static DateTimeOffset Now(TimeProvider provider) => provider.GetUtcNow();

            public static TimeZoneInfo Zone(TimeProvider provider) => provider.LocalTimeZone;
        }
        """);

    [TestMethod]
    public Task FP0102_EnvironmentPropertiesAndMethods_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Host
        {
            public static string Line() => Environment.{|FP0102:NewLine|};

            public static int Cores() => System.Environment.{|FP0102:ProcessorCount|};

            public static string? Variable() => Environment.{|FP0102:GetEnvironmentVariable|}("PATH");

            public static Func<string[]> Args() => Environment.{|FP0102:GetCommandLineArgs|};
        }
        """);

    [TestMethod]
    public Task FP0102_EnvironmentMemberThroughUsingStatic_Reports() => VerifyAsync("""
        using static System.Environment;

        [Fprog.FunctionalCore]
        public static class Host
        {
            public static string Line() => {|FP0102:NewLine|};
        }
        """);

    [TestMethod]
    public Task FP0102_NameOfEnvironmentAndRandomMembers_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Names
        {
            public static string Line() => nameof(Environment.NewLine);

            public static string Clock() => nameof(DateTime.Now);

            public static string Next() => nameof(Random.Next);
        }
        """);

    [TestMethod]
    public Task FP0102_RandomInstanceMembers_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Dice
        {
            public static int Roll(Random random) => random.{|FP0102:Next|}(1, 7);

            public static double Unit(Random? random) => random?.{|FP0102:NextDouble|}() ?? 0;

            public static Func<int> Roller(Random random) => random.{|FP0102:Next|};
        }
        """);

    [TestMethod]
    public Task FP0102_RandomSharedNextIsTwoMembersOfRandom_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Dice
        {
            public static int Roll() => Random.{|FP0102:Shared|}.{|FP0102:Next|}();
        }
        """);

    [TestMethod]
    public Task FP0102_NewRandom_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Dice
        {
            public static object Seeded() => new {|FP0102:Random|}(42);

            public static object Unseeded() => new System.{|FP0102:Random|}();
        }
        """);

    [TestMethod]
    public Task FP0102_TargetTypedNewRandom_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Dice
        {
            public static Random Create() => new(7);
        }
        """);

    [TestMethod]
    public Task FP0102_TargetTypedNewRandomHasNoCatalogSpan_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Dice
        {
            private static readonly Random Rng = new();

            public static int Roll() => Rng.{|FP0102:Next|}(1, 7);
        }
        """);

    [TestMethod]
    public Task FP0102_TargetTypedNewRandomInLocalsArgumentsAndDynamicSeed_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Dice
        {
            public static Random Seeded(dynamic seed) => new(seed);

            public static Random[] Pair() => [new(1), new()];

            public static Random Pass(Random random) => random;

            public static Random Argument() => Pass(new());

            public static Random Local()
            {
                Random random = new(3);
                return random;
            }
        }
        """);

    [TestMethod]
    public Task FP0102_ExplicitNewRandomInStaticFieldAndLocal_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Dice
        {
            private static readonly Random Rng = new {|FP0102:Random|}();

            private static readonly Random Seeded = new System.{|FP0102:Random|}(7);

            public static Random Local()
            {
                Random random = new {|FP0102:Random|}(3);
                return random;
            }
        }
        """);

    [TestMethod]
    public Task FP0102_OverrideOfRandomMemberCalledOnSubclass_Reports() => VerifyAsync("""
        using System;

        public sealed class FixedRandom : Random
        {
            public override int Next() => 4;
        }

        [Fprog.FunctionalCore]
        public static class Dice
        {
            public static int Roll(FixedRandom random) => random.{|FP0102:Next|}();
        }
        """);

    [TestMethod]
    public Task FP0102_RandomAsTypeOnlyAndObjectMembers_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Dice
        {
            public static Type Kind() => typeof(Random);

            public static bool Same(Random left, Random right) => ReferenceEquals(left, right);

            public static string Text(Random random) => random.ToString() ?? "";
        }
        """);

    [TestMethod]
    public Task FP0102_EnvironmentNestedEnumConstantIsNotAMemberRead_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Folders
        {
            public static Environment.SpecialFolder Desktop() => Environment.SpecialFolder.Desktop;
        }
        """);

    [TestMethod]
    public Task FP0102_MutableStaticFieldRead_Reports() => VerifyAsync("""
        public static class Settings
        {
            public static int Limit = 10;
        }

        [Fprog.FunctionalCore]
        public static class Counter
        {
            private static int count;

            public static int Current() => {|FP0102:count|};

            public static int OtherTypeOutsideCore() => Settings.{|FP0102:Limit|};
        }
        """);

    [TestMethod]
    public Task FP0102_WriteToMutableStaticFieldIsReported_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            private static int count;

            public static int Reset()
            {
                {|FP0102:count|} = 0;
                return 0;
            }
        }
        """);

    [TestMethod]
    public Task FP0102_CompoundAssignmentAndIncrementOfMutableStaticField_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            private static int count;

            public static int Add(int value)
            {
                {|FP0102:count|} += value;
                {|FP0102:count|}++;
                return value;
            }
        }
        """);

    [TestMethod]
    public Task FP0102_MutableStaticFieldPassedByRefOrOutIsReported_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            private static int count;

            public static int Run()
            {
                Bump(ref {|FP0102:count|});
                Set(out {|FP0102:count|});
                return 0;
            }

            private static int Bump(ref int value) => value + 1;

            private static int Set(out int value) => value = 1;
        }
        """);

    [TestMethod]
    public Task FP0102_StaticReadonlyConstAndInstanceFields_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public sealed class Limits
        {
            private const int Max = 10;
            private static readonly int Min = 1;
            private readonly int value;
            private int other;

            public Limits(int value)
            {
                this.value = value;
                other = value;
            }

            public int Clamp() => Math.Clamp(value + other, Min, Max);

            public static string Empty() => string.Empty;

            public static Guid NoId() => Guid.Empty;
        }
        """);

    [TestMethod]
    public Task FP0102_DeterministicDateAndGuidMembers_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Dates
        {
            public static int Year(DateTime date) => date.Year;

            public static DateTime Epoch() => DateTime.UnixEpoch;

            public static DateTimeOffset Shift(DateTimeOffset date) => date.AddDays(1);

            public static DateTime Min() => DateTime.MinValue;

            public static Guid Make(byte[] bytes) => new Guid(bytes);
        }
        """);

    [TestMethod]
    public Task FP0102_StaticFieldInitializerInCore_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Startup
        {
            public static readonly DateTime Started = DateTime.{|FP0102:UtcNow|};
        }
        """);

    [TestMethod]
    public Task FP0102_LambdaAndLocalFunctionInCore_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Closures
        {
            public static Func<DateTime> Lambda() => () => DateTime.{|FP0102:Now|};

            public static Guid Local()
            {
                return Make();

                static Guid Make() => Guid.{|FP0102:NewGuid|}();
            }
        }
        """);

    [TestMethod]
    public Task FP0102_TypeNestedInCore_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Outer
        {
            public static class Inner
            {
                public static DateTime Now() => DateTime.{|FP0102:Now|};
            }
        }
        """);

    [TestMethod]
    public Task FP0102_AssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public static class Anywhere
        {
            public static string Line() => System.Environment.{|FP0102:NewLine|};
        }
        """);

    [TestMethod]
    public Task FP0102_SameCodeOutsideCore_NoReport() => VerifyAsync("""
        using System;

        public static class Shell
        {
            private static int count;

            public static DateTime Now() => DateTime.Now;

            public static Guid Id() => Guid.NewGuid();

            public static string Line() => Environment.NewLine;

            public static int Roll() => new Random(1).Next() + Random.Shared.Next() + count++;

            public static TimeProvider Clock() => TimeProvider.System;
        }
        """);

    [TestMethod]
    public Task FP0102_CompilerSynthesizedBackingFieldAndRecordMembers_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Point(int X, int Y)
        {
            public static int Scale { get => field; set => field = value; }

            public int Scaled() => X * Scale;
        }
        """);

    [TestMethod]
    public Task FP0102_GeneratedCodeInCore_NoReport() => VerifyAsync("""
        // <auto-generated/>
        [Fprog.FunctionalCore]
        public static class Generated
        {
            public static System.DateTime Now() => System.DateTime.Now;
        }
        """);

    [TestMethod]
    public Task FP0102_FunctionPointerToHiddenStaticMethods_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static unsafe class Pointers
        {
            public static Guid Next()
            {
                delegate*<Guid> make = &Guid.{|FP0102:NewGuid|};
                return make();
            }

            public static string? Path()
            {
                delegate*<string, string?> read = &global::System.Environment.{|FP0102:GetEnvironmentVariable|};
                return read("PATH");
            }
        }
        """);

    [TestMethod]
    public Task FP0102_ParenthesizedMethodGroupAndInvocationTarget_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Groups
        {
            public static Func<Guid> Factory() => (Guid.{|FP0102:NewGuid|});

            public static int Roll(Random random) => ((random).{|FP0102:Next|})();

            public static Func<string[]> Args() => ((Environment.{|FP0102:GetCommandLineArgs|}));
        }
        """);

    [TestMethod]
    public Task FP0102_DynamicArgumentsToRandomAndEnvironmentMembers_Reports() => VerifyAsync("""
        using System;
        using static System.Environment;

        [Fprog.FunctionalCore]
        public static class Late
        {
            public static int Roll(Random random, dynamic max) => random.{|FP0102:Next|}(max);

            public static object? Read(dynamic name) => Environment.{|FP0102:GetEnvironmentVariable|}(name);

            public static int Loaded<T>(T random, dynamic max) where T : Random => random.{|FP0102:Next|}(max);

            public static object Seeded(dynamic seed) => new {|FP0102:Random|}(seed);

            public static object Other(dynamic value) => value.Next() + Math.Abs(value);

            public static int Own(Custom custom, dynamic max) => custom.Pick(max);
        }

        [Fprog.FunctionalCore]
        public static class Imported
        {
            public static object? Read(dynamic name) => {|FP0102:GetEnvironmentVariable|}(name);
        }

        public sealed class Custom : Random
        {
            public int Pick(int max) => max;
        }
        """);

    [TestMethod]
    public Task FP0102_AliasesUsingStaticAndDerivedTypeQualifier_Reports() => VerifyAsync("""
        using System;
        using static System.Random;
        using Dice = System.Random;
        using Env = System.Environment;

        public sealed class FakeClock : TimeProvider;

        [Fprog.FunctionalCore]
        public static class Aliases
        {
            public static Dice Create() => new {|FP0102:Dice|}(3);

            public static Dice Global() => new global::System.{|FP0102:Random|}();

            public static string Line() => Env.{|FP0102:NewLine|};

            public static int Roll() => global::System.Random.{|FP0102:Shared|}.{|FP0102:Next|}() + {|FP0102:Shared|}.{|FP0102:Next|}();

            public static TimeProvider Clock() => FakeClock.{|FP0102:System|};
        }
        """);

    [TestMethod]
    public Task FP0102_GenericNameTriviaCastAndNullForgivingReceivers_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Shapes
        {
            public static int[] Pick(Random random, int[] items)
                => random
                    . /* pick */ {|FP0102:GetItems|}<int>(items, 2);

            public static int Cast(object value) => ((Random)value).{|FP0102:Next|}();

            public static int Forgiving(Random? random) => random!.{|FP0102:Next|}();

            public static int Shared() => Random.{|FP0102:Shared|}?.{|FP0102:Next|}() ?? 0;

            public static DateTime Wrapped() => (DateTime
                .{|FP0102:Now|});
        }
        """);

    [TestMethod]
    public Task FP0102_ConstrainedTypeParameterAndCoreRandomSubclass_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Generic
        {
            public static int Roll<T>(T random) where T : Random => random.{|FP0102:Next|}();
        }

        [Fprog.FunctionalCore]
        public sealed class Loaded : Random
        {
            public override int Next() => base.{|FP0102:Next|}() + (int)({|FP0102:Sample|}() * 0);

            public override double NextDouble() => 0.5;
        }
        """);

    [TestMethod]
    public Task FP0102_BaseConstructorCallOfRandomSubclassIsNotNewRandom_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Subclasses
        {
            public sealed class Seeded() : Random(42);

            public sealed class Fixed : Random
            {
                public Fixed() : base(7)
                {
                }
            }

            public static object Make() => new Seeded();
        }
        """);

    [TestMethod]
    public Task FP0102_ConstructorsAccessorsIndexersAndOperators_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public sealed class Ledger
        {
            private static int opened;
            private readonly DateTime created;

            static Ledger() => {|FP0102:opened|} = 1;

            public Ledger() => created = DateTime.{|FP0102:Now|};

            public DateTime Created => created;

            public string this[int index] => Environment.{|FP0102:NewLine|} + index;

            public int Count
            {
                get => {|FP0102:opened|};
                init => _ = Guid.{|FP0102:NewGuid|}();
            }

            public static Ledger operator +(Ledger left, Ledger right) => {|FP0102:opened|} > 0 ? left : right;

            public static explicit operator DateTime(Ledger ledger) => DateTimeOffset.{|FP0102:UtcNow|}.DateTime;
        }
        """);

    [TestMethod]
    public Task FP0102_PropertyInitializerAndPrimaryConstructorBaseArgument_Reports() => VerifyAsync("""
        using System;

        public abstract class Stamped(DateTime at)
        {
            public DateTime At { get; } = at;
        }

        [Fprog.FunctionalCore]
        public sealed class Order() : Stamped(DateTime.{|FP0102:UtcNow|})
        {
            public string Id { get; } = Guid.{|FP0102:NewGuid|}().ToString();
        }

        [Fprog.FunctionalCore]
        public readonly record struct Seed(int Value)
        {
            public int Machine { get; init; } = Environment.{|FP0102:ProcessorCount|};
        }
        """);

    [TestMethod]
    public Task FP0102_QueryInterpolationInitializersAndSwitchGuard_Reports() => VerifyAsync("""
        using System;
        using System.Collections.Generic;
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Reports
        {
            public static IEnumerable<DateTime> Due(IEnumerable<DateTime> dates)
                => from date in dates
                   let today = DateTime.{|FP0102:Today|}
                   where date < today
                   select date;

            public static string Banner(string name) => $"{name}{Environment.{|FP0102:NewLine|}}";

            public static List<Guid> Ids() => new() { Guid.{|FP0102:NewGuid|}() };

            public static Dictionary<string, int> Map() => new() { [Environment.{|FP0102:MachineName|}] = 1 };

            public static int[] Rolls(Random random) => [random.{|FP0102:Next|}()];

            public static string Kind(int value) => value switch
            {
                > 0 when DateTime.{|FP0102:UtcNow|}.Year > 2000 => "late",
                _ => "early"
            };
        }
        """);

    [TestMethod]
    public Task FP0102_AsyncIteratorExpressionTreeAndNestedLambdas_Reports() => VerifyAsync("""
        using System;
        using System.Collections.Generic;
        using System.Linq.Expressions;
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Flows
        {
            public static async Task<int> Later(Random random)
            {
                await Task.Yield();
                return random.{|FP0102:Next|}();
            }

            public static IEnumerable<Guid> Stream()
            {
                yield return Guid.{|FP0102:NewGuid|}();
            }

            public static Expression<Func<DateTime>> Tree() => () => DateTime.{|FP0102:Now|};

            public static Func<Func<Func<string>>> Deep() => () => () => () => Environment.{|FP0102:UserName|};
        }
        """);

    [TestMethod]
    public Task FP0102_ExtensionBlockMembersInCore_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class RandomExtensions
        {
            extension(Random random)
            {
                public int Die => random.{|FP0102:Next|}(1, 7);

                public int Coin() => random.{|FP0102:Next|}(2);
            }

            extension(DateTime)
            {
                public static DateTime Stamp => DateTime.{|FP0102:UtcNow|};
            }
        }
        """);

    [TestMethod]
    public Task FP0102_PartialTypePartWithoutAttribute_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public partial class Split
        {
            public partial DateTime When();
        }

        public partial class Split
        {
            public partial DateTime When() => DateTime.{|FP0102:Now|};

            public static Guid Id() => Guid.{|FP0102:NewGuid|}();
        }
        """);

    [TestMethod]
    public Task FP0102_NestedInterfaceDefaultMemberAndGenericStaticField_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Kinds
        {
            public interface IClock
            {
                DateTime Now => DateTime.{|FP0102:Now|};
            }

            public static class Cache<T>
            {
                public static T? Last;

                public static T? Peek() => {|FP0102:Last|};
            }

            public static int? Read() => Cache<int?>.{|FP0102:Last|};
        }
        """);

    [TestMethod]
    public Task FP0102_MutableStaticFieldRefLocalAddressDeconstructionAndInitializer_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static unsafe class Counters
        {
            private static int count = 1;
            private static readonly int Copy = {|FP0102:count|};

            public static int Shapes()
            {
                ref int alias = ref {|FP0102:count|};
                fixed (int* pointer = &{|FP0102:count|})
                {
                    alias += *pointer;
                }

                ({|FP0102:count|}, var other) = (Copy, 2);
                return alias + other;
            }
        }
        """);

    [TestMethod]
    public Task FP0102_ThreadStaticVolatileAndStructStaticFields_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public readonly struct Ambient
        {
            [ThreadStatic]
            private static int depth;

            private static volatile bool ready;

            public static int Depth() => {|FP0102:ready|} ? {|FP0102:depth|} : 0;
        }
        """);

    [TestMethod]
    public Task FP0102_CoreTypeNestedInShellTypeOnlyInnerReported_Reports() => VerifyAsync("""
        using System;

        public static class Shell
        {
            public static DateTime Outer() => DateTime.Now;

            [Fprog.FunctionalCore]
            public static class Core
            {
                public static DateTime Inner() => DateTime.{|FP0102:Now|};
            }
        }
        """);

    [TestMethod]
    public Task FP0102_NameOfMutableStaticFieldAndAttributeNameOf_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Names
        {
            private static int count;

            [Obsolete(nameof(Environment.NewLine))]
            public static string Field() => nameof(count);

            public static string Qualified() => nameof(Names.count) + nameof(Random.Shared) + nameof(Guid.NewGuid);
        }
        """);

    [TestMethod]
    public Task FP0102_UserTypesNamedLikeSystemTypes_NoReport() => VerifyAsync("""
        namespace Domain
        {
            public static class Environment
            {
                public static string NewLine => "line";
            }

            public sealed class Random
            {
                public int Next() => 4;
            }

            public static class DateTime
            {
                public static int Now => 0;
            }

            [Fprog.FunctionalCore]
            public static class Uses
            {
                public static string Line() => Environment.NewLine;

                public static int Roll() => new Random().Next() + DateTime.Now + System.Guid.NewGuid();
            }
        }

        namespace Domain.System
        {
            public static class Guid
            {
                public static int NewGuid() => 1;
            }
        }
        """);

    [TestMethod]
    public Task FP0102_RealisticFunctionalCore_NoReport() => VerifyAsync("""
        using System;
        using System.Collections.Immutable;
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Pricing
        {
            private static readonly ImmutableArray<decimal> Tiers = [10m, 20m, 50m];
            private const decimal Rate = 0.2m;

            public sealed record Line(string Sku, int Quantity, decimal Price);

            public static decimal Total(ImmutableArray<Line> lines, DateTime now)
            {
                var sum = 0m;
                foreach (var line in lines)
                    sum += line.Quantity * line.Price;

                var discount = Tiers.Count(tier => sum > tier) * Rate;
                return now.DayOfWeek is DayOfWeek.Sunday ? sum - discount : sum;
            }

            public static string Describe(Line line) => line switch
            {
                { Quantity: 0 } => "empty",
                { Price: > 100m } => $"premium {line.Sku}",
                _ => line.Sku
            };

            public static DateTimeOffset Expiry(TimeProvider clock, TimeSpan ttl) => clock.GetUtcNow() + ttl;

            public static int Roll(int seed) => (seed * 1103515245 + 12345) & int.MaxValue;
        }
        """);

    [TestMethod]
    public Task FP0102_RandomTypeOnlyUsagesArrayIsAsDefaultCref_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Types
        {
            /// <summary>Unlike <see cref="Random.Next()"/> and <see cref="DateTime.Now"/>, reads nothing.</summary>
            public static Random?[] Slots() => new Random?[2];

            public static bool IsDice(object value) => value is Random;

            public static Random? AsDice(object value) => value as Random ?? default(Random);

            public static T Pass<T>(T random) where T : Random => random;

            public static Func<int, Random> Factory(Func<int, Random> make) => make;
        }
        """);

    [TestMethod]
    public Task FP0102_StaticAutoPropertyAndFieldLikeEventBackingFields_NoReport() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Ambient
        {
            public static int Level { get; set; }

            public static event Action? Changed;

            public static int Read()
            {
                Changed?.Invoke();
                return Level;
            }
        }
        """);

    [TestMethod]
    public Task FP0102_UnusualShapesExternAbstractPartialUnsafe_NoReport() => VerifyAsync("""
        using System;
        using System.Runtime.InteropServices;

        [Fprog.FunctionalCore]
        public abstract unsafe partial class Odd
        {
            [DllImport("native")]
            private static extern int Native(int* value);

            public abstract int Abstract();

            partial void Hook();

            public int Empty()
            {
                Hook();
                return 0;
            }

            public static int Pointer(int* value) => *value + sizeof(int) + Native(value);

            public static Func<Func<Func<int>>> Nested() => () => () => () => 1;

            public static T Make<T>() where T : struct, IEquatable<T> => default;

            ~Odd()
            {
            }
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0102", source, testContext.CancellationToken);
}
