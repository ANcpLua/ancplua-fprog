using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests;

[TestClass]
public sealed class CSharp15SyntaxTests(TestContext testContext)
{
    [TestMethod]
    public Task ExtensionIndexerDeclarationAndAccess_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public readonly record struct Window(int Start);

                           [Fprog.FunctionalCore]
                           public static class WindowExtensions
                           {
                               extension(Window window)
                               {
                                   public int this[int offset] => window.Start + offset;
                               }

                               public static int Second(Window window) => window[1];
                           }
                           """);
    }

    [TestMethod]
    public Task CollectionExpressionWithComparer_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public static class Tags
                           {
                               public static ImmutableHashSet<string> Unique(ImmutableArray<string> values)
                                   => [with(StringComparer.Ordinal), .. values];
                           }
                           """);
    }

    [TestMethod]
    public Task LabeledBreakAndContinue_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Search
                           {
                               public static int Score(int[] xs, int[] ys)
                               {
                                   var score = 0;
                                   outer:
                                   foreach (var x in xs)
                                   {
                                       foreach (var y in ys)
                                       {
                                           if (x == y) continue outer;
                                           if (x < y) break outer;
                                           score += x + y;
                                       }
                                   }

                                   return score;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task UnionDeclarationWithMembers_ReportsAtWrittenSites()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public union Answer(int, string)
                           {
                               public static Answer Empty() => {|FP0404:default|};

                               public int Code() => this switch
                               {
                                   int number => number,
                                   string text => text.Length,
                                   null => -1
                               };

                               public static int ShortCode(Answer value) => value switch
                               {
                                   int number => number,
                                   {|FP0501:_|} => 0
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task NestedClosedHierarchyUsesLeafCases_NoReport()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public closed record Expression;
                           public sealed record Literal(int Value) : Expression;
                           public closed record Operation : Expression;
                           public sealed record Add(Expression Left, Expression Right) : Operation;
                           public sealed record Negate(Expression Value) : Operation;

                           public static class Evaluator
                           {
                               public static int Run(Expression value) => value switch
                               {
                                   Literal literal => literal.Value,
                                   Add add => Run(add.Left) + Run(add.Right),
                                   Negate negate => -Run(negate.Value),
                               };
                           }
                           """);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task RuntimeAsyncKeepsFP0802Spans_Reports(bool runtimeAsync)
    {
        IEnumerable<KeyValuePair<string, string>> features = runtimeAsync
            ? [new KeyValuePair<string, string>("runtime-async", "on")]
            : [];
        return FpAnalyzerTestHost.VerifyAllAsync("""
                                                using System.Threading.Tasks;

                                                [Fprog.FunctionalCore]
                                                public static class Work
                                                {
                                                    public static async {|FP0802:Task|}<int> Next({|FP0802:Task|}<int> pending)
                                                        => (await pending) + 1;
                                                }
                                                """, testContext.CancellationToken, features);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyAllAsync(source, testContext.CancellationToken);
    }
}
