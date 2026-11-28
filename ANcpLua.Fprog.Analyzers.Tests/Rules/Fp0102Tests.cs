using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0102Tests(TestContext testContext)
{
    [TestMethod]
    [DataRow("DateTime.{|FP0102:Now|}")]
    [DataRow("System.DateTime.{|FP0102:UtcNow|}")]
    [DataRow("global::System.DateTime.{|FP0102:Today|}")]
    [DataRow("DateTimeOffset.{|FP0102:Now|}")]
    [DataRow("DateTimeOffset.{|FP0102:UtcNow|}")]
    [DataRow("TimeZoneInfo.{|FP0102:Local|}")]
    [DataRow("TimeZoneInfo.{|FP0102:FindSystemTimeZoneById|}(zone)")]
    [DataRow("TimeZoneInfo.{|FP0102:TryFindSystemTimeZoneById|}(zone, out _)")]
    [DataRow("TimeZoneInfo.{|FP0102:GetSystemTimeZones|}()")]
    [DataRow("date.{|FP0102:ToLocalTime|}()")]
    [DataRow("date.{|FP0102:ToUniversalTime|}()")]
    [DataRow("offset.{|FP0102:ToLocalTime|}()")]
    [DataRow("offset.{|FP0102:LocalDateTime|}")]
    [DataRow("Guid.{|FP0102:NewGuid|}()")]
    [DataRow("Guid.{|FP0102:CreateVersion7|}()")]
    [DataRow("Guid.{|FP0102:CreateVersion7|}(offset)")]
    [DataRow("CultureInfo.{|FP0102:CurrentCulture|}")]
    [DataRow("CultureInfo.{|FP0102:CurrentUICulture|}")]
    [DataRow("CultureInfo.{|FP0102:DefaultThreadCurrentCulture|}")]
    [DataRow("CultureInfo.{|FP0102:DefaultThreadCurrentUICulture|}")]
    [DataRow("CultureInfo.{|FP0102:InstalledUICulture|}")]
    [DataRow("RegionInfo.{|FP0102:CurrentRegion|}")]
    [DataRow("NumberFormatInfo.{|FP0102:CurrentInfo|}")]
    [DataRow("DateTimeFormatInfo.{|FP0102:CurrentInfo|}")]
    [DataRow("StringComparer.{|FP0102:CurrentCulture|}")]
    [DataRow("StringComparer.{|FP0102:CurrentCultureIgnoreCase|}")]
    [DataRow("StringComparison.{|FP0102:CurrentCulture|}")]
    [DataRow("StringComparison.{|FP0102:CurrentCultureIgnoreCase|}")]
    public Task FP0102_HiddenPlatformMember_Reports(string expression)
    {
        return VerifyAsync($$"""
                             using System;
                             using System.Globalization;

                             [Fprog.FunctionalCore]
                             public static class Sample
                             {
                                 public static object Test(DateTime date, DateTimeOffset offset, string zone) => {{expression}};
                             }
                             """);
    }

    [TestMethod]
    public Task FP0102_HiddenMembersThroughAliasesStaticImportsAndMethodGroups_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using Clock = System.DateTimeOffset;
                           using static System.DateTime;

                           [Fprog.FunctionalCore]
                           public static class Sample
                           {
                               public static DateTime Reading() => {|FP0102:Now|};
                               public static DateTimeOffset Other() => Clock.{|FP0102:UtcNow|};
                               public static Func<Guid> Next() => Guid.{|FP0102:NewGuid|};
                               public static Func<Guid> Version7() => Guid.{|FP0102:CreateVersion7|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0102_HashCodeMethodsInCore_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public sealed class Book(int pages)
                           {
                               public int GetHashCode(int seed) => pages + seed;
                           }

                           [Fprog.FunctionalCore]
                           public static class Shards
                           {
                               public static int Run(Book book, object key)
                               {
                                   var hash = new HashCode();
                                   hash.{|FP0102:Add|}(key);
                                   return hash.{|FP0102:ToHashCode|}() + book.{|FP0102:GetHashCode|}(1)
                                       + key.{|FP0102:GetHashCode|}() + HashCode.{|FP0102:Combine|}(key, 10);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0102_HashingInsideGetHashCodeOverride_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public sealed class Book(string title)
                           {
                               public override int GetHashCode()
                               {
                                   var hash = new HashCode();
                                   hash.Add(title.GetHashCode());
                                   return HashCode.Combine(hash.ToHashCode(), title.Length);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0102_PureMutableStaticFieldsAndProperties_Reports()
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0102", """
            using Trusted;

            namespace Trusted
            {
                public static class Global
                {
                    public static int Count;
                    public static int Last { get; set; }
                }
            }

            [Fprog.FunctionalCore]
            public static class Sample
            {
                public static int Read() => Global.{|FP0102:Count|} + Global.{|FP0102:Last|};
                public static int Update(int n)
                {
                    Global.{|FP0102:Count|} += n;
                    return Global.{|FP0102:Last|} = n;
                }
            }
            """, "is_global = true\nfprog_pure_apis = Trusted", testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0102_ConstantsReadonlyStateAndInstanceMembers_NoReport()
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0102", """
            using Trusted;

            namespace Trusted
            {
                public sealed class Value
                {
                    public const int Constant = 1;
                    public static readonly int ReadOnly = 2;
                    public static int Getter { get; } = 3;
                    public int Instance { get; set; }
                }
            }

            [Fprog.FunctionalCore]
            public static class Sample
            {
                public static int Evaluate(Value value) => Value.Constant + Value.ReadOnly + Value.Getter + value.Instance;
            }
            """, "is_global = true\nfprog_pure_apis = Trusted", testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0102_ShellMembersBelongToFP0801_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           public static class Edge
                           {
                               public static int State { get; set; }
                           }

                           [Fprog.FunctionalCore]
                           public static class Sample
                           {
                               public static int Next(Random random) => random.Next() + Environment.ProcessorCount + Edge.State;
                               public static DateTimeOffset Utc(TimeProvider provider) => provider.GetUtcNow();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0102_DeterministicLookalikeNameofAndOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Globalization;

                           public static class Outside
                           {
                               public static DateTime Read() => System.DateTime.Now;
                           }

                           [Fprog.FunctionalCore]
                           public static class Sample
                           {
                               public static string Name => nameof(System.DateTime.Now);
                               public static DateTime Deterministic(DateTime date, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(date, zone);
                               public static TimeZoneInfo Utc => TimeZoneInfo.Utc;
                               public static CultureInfo Culture => CultureInfo.InvariantCulture;
                               public static StringComparer Comparer => StringComparer.Ordinal;
                               public static Guid Id => Guid.Empty;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0102_HiddenInputsInNestedAndLocalFunctions_Reports()
    {
        return VerifyAsync("""
                           using System;
                           [assembly: Fprog.FunctionalCore]

                           public static class Outer
                           {
                               public static class Inner
                               {
                                   public static Func<DateTime> Clock()
                                   {
                                       DateTime Read() => DateTime.{|FP0102:Now|};
                                       return () => DateTime.{|FP0102:Today|} > Read() ? DateTime.MinValue : Read();
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0102_GeneratedCode_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           [Fprog.FunctionalCore]
                           public static class Sample
                           {
                               public static System.DateTime Clock() => System.DateTime.UtcNow;
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0102", source, testContext.CancellationToken);
    }
}
