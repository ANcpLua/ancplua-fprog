using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0106Tests(TestContext testContext)
{
    [TestMethod]
    [DataRow("amount.{|FP0106:ToString|}()")]
    [DataRow("amount.{|FP0106:ToString|}(\"C\")")]
    [DataRow("int.{|FP0106:TryParse|}(text, out _)")]
    [DataRow("string.{|FP0106:Format|}(\"{0}\", amount)")]
    [DataRow("text.{|FP0106:ToUpper|}()")]
    [DataRow("text.{|FP0106:ToLower|}()")]
    [DataRow("Convert.{|FP0106:ToDouble|}(text)")]
    [DataRow("date.{|FP0106:ToString|}(\"yyyy/MM/dd\")")]
    public Task FP0106_OverloadWithExtraCultureProvider_Reports(string expression)
    {
        return VerifyAsync($$"""
                             using System;
                             [Fprog.FunctionalCore]
                             public static class Sample
                             {
                                 public static object Format(decimal amount, string text, DateTime date) => {{expression}};
                             }
                             """);
    }

    [TestMethod]
    [DataRow("string.{|FP0106:Compare|}(text, \"a\")")]
    [DataRow("text.{|FP0106:CompareTo|}(\"a\")")]
    [DataRow("text.{|FP0106:StartsWith|}(\"a\")")]
    [DataRow("text.{|FP0106:EndsWith|}(\"a\")")]
    [DataRow("text.{|FP0106:IndexOf|}(\"a\", 0)")]
    [DataRow("text.{|FP0106:LastIndexOf|}(\"a\")")]
    public Task FP0106_StringSearchAndComparisonWithoutComparisonKind_Reports(string expression)
    {
        return VerifyAsync($$"""
                             [Fprog.FunctionalCore]
                             public static class Sample
                             {
                                 public static object Test(string text) => {{expression}};
                             }
                             """);
    }

    [TestMethod]
    [DataRow("values.{|FP0106:OrderBy|}(s => s)")]
    [DataRow("values.{|FP0106:OrderByDescending|}(s => s)")]
    [DataRow("ordered.{|FP0106:ThenBy|}(s => s)")]
    [DataRow("ordered.{|FP0106:ThenByDescending|}(s => s)")]
    [DataRow("values.{|FP0106:Order|}()")]
    [DataRow("values.{|FP0106:OrderDescending|}()")]
    [DataRow("values.{|FP0106:Min|}()")]
    [DataRow("values.{|FP0106:Max|}(s => s)")]
    [DataRow("values.{|FP0106:MinBy|}(s => s)")]
    [DataRow("values.{|FP0106:MaxBy|}(s => s)")]
    [DataRow("Array.{|FP0106:Sort|}(values)")]
    [DataRow("Array.{|FP0106:BinarySearch|}(values, \"a\")")]
    [DataRow("list.{|FP0106:Sort|}()")]
    [DataRow("list.{|FP0106:BinarySearch|}(\"a\")")]
    [DataRow("values.{|FP0106:OrderBy|}(s => s, comparer: null)")]
    [DataRow("values.{|FP0106:OrderBy|}(s => s, Comparer<string>.Default)")]
    [DataRow("ordered.{|FP0106:ThenByDescending|}(s => s, comparer: (IComparer<string>)Comparer<string>.Default)")]
    [DataRow("Array.{|FP0106:Sort|}(values, Comparer<string>.Default)")]
    [DataRow("list.{|FP0106:BinarySearch|}(\"a\", Comparer<string>.Default)")]
    [DataRow("values.AsSpan().{|FP0106:Sort|}()")]
    [DataRow("values.AsSpan().{|FP0106:Sort|}(Comparer<string>.Default)")]
    [DataRow("values.AsSpan().{|FP0106:Sort|}(new IComparer<string>[values.Length].AsSpan())")]
    public Task FP0106_StringKeysOrElementsUseDefaultComparer_Reports(string statement)
    {
        return VerifyAsync($$"""
                             using System;
                             using System.Collections.Generic;
                             using System.Linq;
                             [Fprog.FunctionalCore]
                             public static class Sample
                             {
                                 public static int Test(string[] values, List<string> list)
                                 {
                                     var ordered = values.OrderBy(s => s.Length);
                                     {{statement}};
                                     return 0;
                                 }
                             }
                             """);
    }

    [TestMethod]
    [DataRow("{|FP0106:entry.City|}, entry.Rank, {|FP0106:entry.Name|} descending")]
    [DataRow("{|FP0106:entry.City|} descending, {|FP0106:entry.Name|} ascending")]
    public Task FP0106_QueryOrdersByStringKeys_Reports(string orderings)
    {
        return VerifyAsync($$"""
                             using System.Collections.Generic;
                             using System.Linq;

                             [Fprog.FunctionalCore]
                             public static class Directory
                             {
                                 public static IEnumerable<string> Sort((string City, string Name, int Rank)[] entries)
                                     => from entry in entries orderby {{orderings}} select entry.Name;
                             }
                             """);
    }

    [TestMethod]
    public Task FP0106_QueryOrdersByNumericKeyOrRunsOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Core
                           {
                               public static IEnumerable<string> ByLength(string[] names)
                                   => from name in names orderby name.Length descending select name;
                           }

                           public static class Shell
                           {
                               public static IEnumerable<string> ByName(string[] names)
                                   => from name in names orderby name select name;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0106_QueryBindsToCustomOrdinalOrdering_NoReport()
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0106", """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public sealed class OrdinalNames(string[] names)
            {
                public IEnumerable<string> OrderBy(Func<string, string> key)
                    => names.OrderBy(key, StringComparer.Ordinal);
            }

            [Fprog.FunctionalCore]
            public static class Directory
            {
                public static IEnumerable<string> Sort(OrdinalNames names)
                    => from name in names orderby name select name;
            }
            """, "is_global = true\nfprog_pure_apis = OrdinalNames", testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0106_InterpolationAndConcatenationReportAtTheValue_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static class Sample
                           {
                               public static string Interpolate(decimal amount, DateTime date, int? maybe)
                                   => $"Value: {{|FP0106:amount|}:C} at {{|FP0106:date|}:d}. Code: {{|FP0106:maybe|}}";

                               public static string Generic<T>(T value) where T : IFormattable => "Formatted: " + {|FP0106:value|};
                               public static string Concat(decimal amount, DateTime date) => {|FP0106:amount|} + " on " + {|FP0106:date|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0106_ExplicitProvidersAndCultureFreeInterpolation_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Globalization;
                           using System.Text;

                           [Fprog.FunctionalCore]
                           public static class Sample
                           {
                               public enum Phase { New, Last }

                               public static string CultureFree(char code, bool ok, Guid id, Rune rune,
                                   Version version, Uri uri, Phase phase, string text)
                                   => $"{code}{ok}{id}{rune}{version}{uri}{phase.ToString()}{text.ToString()}";

                               public static string WithHandler(decimal amount)
                                   => string.Create(CultureInfo.InvariantCulture, $"Cost: {amount:C}");

                               public static string WithFormattable(decimal amount)
                                   => FormattableString.Invariant($"Cost: {amount:C}");

                               public static IFormattable ToFormat(decimal amount) => $"Cost: {amount:C}";

                               public static string Explicit(decimal amount) => amount.ToString(CultureInfo.InvariantCulture);
                               public static bool Parse(string value) => int.TryParse(value, CultureInfo.InvariantCulture, out _);
                               public static bool Search(string value) => value.StartsWith('a') || value.EndsWith("a", StringComparison.Ordinal);
                               public static int Compare(string a, string b) => string.Compare(a, b, StringComparison.Ordinal);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0106_ExplicitComparerAndStringPayloadWithNumericKey_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Sample
                           {
                               private static IComparer<string> Default => StringComparer.Ordinal;

                               public static int Test(string[] values, List<string> list, IComparer<string> comparer)
                               {
                                   Array.Sort(values, StringComparer.Ordinal);
                                   Array.Sort(new[] { 1, 2 }, values);
                                   list.Sort(StringComparer.Ordinal);
                                   list.Sort(string.CompareOrdinal);
                                   values.OrderBy(s => s, StringComparer.Ordinal);
                                   values.OrderBy(s => s, comparer);
                                   values.OrderBy(s => s, Default);
                                   values.AsSpan().Sort(StringComparer.Ordinal);
                                   values.AsSpan().Sort(comparer);
                                   values.AsSpan().Sort(new int[values.Length].AsSpan(), StringComparer.Ordinal);
                                   new int[values.Length].AsSpan().Sort(values.AsSpan());
                                   values.MinBy(s => s.Length);
                                   values.OrderBy(s => s.Length);
                                   return Array.BinarySearch(values, "a", StringComparer.Ordinal);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0106_ShellCultureOverloadAndGeneratedOrShellBody_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Globalization;

                           public static class Shell
                           {
                               public static string Format(decimal amount) => amount.ToString();
                               public static string Format(decimal amount, IFormatProvider provider) => amount.ToString(provider);
                           }

                           [Fprog.FunctionalCore]
                           public static class Sample
                           {
                               [System.CodeDom.Compiler.GeneratedCode("test", "1")]
                               public static string Generated(decimal amount) => amount.ToString("C");
                               public static string ShellFormats(decimal amount) => Shell.Format(amount);
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0106", source, testContext.CancellationToken);
    }
}
