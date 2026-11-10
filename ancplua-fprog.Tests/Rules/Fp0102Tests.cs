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
    public Task FP0102_TargetTypedNewRandomReportsAtNewKeyword_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Dice
        {
            public static Random Create() => {|FP0102:new|}(7);
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

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0102", source, testContext.CancellationToken);
}
