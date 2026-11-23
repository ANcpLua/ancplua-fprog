using System.Linq;
using System.Threading.Tasks;
using ancplua_fprog.Rules;
using ancplua_fprog.Tests.Shared;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0802Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0802_AsyncTypeNameAtRightmostTokenInEveryPosition_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using TaskAlias = System.Threading.Tasks.Task<int>;

                           [Fprog.FunctionalCore]
                           public sealed class Work
                           {
                               public readonly {|FP0802:TaskAlias|} Pending = System.Threading.Tasks.{|FP0802:Task|}.FromResult(1);
                               public {|FP0802:TaskAlias|}? Saved { get; }
                               public System.Threading.Tasks.{|FP0802:ValueTask|}<int>? Optional => null;
                               public {|FP0802:IAsyncDisposable|} Resource({|FP0802:IAsyncDisposable|} resource) => resource;
                               public {|FP0802:IAsyncEnumerable|}<int> Stream({|FP0802:IAsyncEnumerable|}<int> stream) => stream;
                               public Func<{|FP0802:TaskAlias|}> Method() => Local;
                               private {|FP0802:TaskAlias|} Local() => Pending;
                               public System.Type Type() => typeof(System.Threading.Tasks.{|FP0802:Task|});
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_ImplicitMemberUseAndAwaitOfAnUnanchoredValue_Reports()
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0802", """
            using System.Threading.Tasks;
            using static System.Threading.Tasks.Task;

            public static class TrustedTasks
            {
                public static Task<int> Current { get; } = Task.FromResult(1);
            }

            [Fprog.FunctionalCore]
            public static class Work
            {
                public static int Blocking() => TrustedTasks.Current.{|FP0802:Result|};
                public static async {|FP0802:Task|}<int> Wait() => await {|FP0802:TrustedTasks.Current|};
                public static int Direct() => {|FP0802:Run|}(() => 1).GetAwaiter().GetResult();
            }
            """, "is_global = true\nfprog_pure_apis = TrustedTasks", testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0802_WrittenDeclarationsAnchorAwaitAndMembersOnce_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static int Blocking({|FP0802:Task|}<int> pending) => pending.Result;

                               public static async {|FP0802:Task|}<int> Wait({|FP0802:Task|}<int> input)
                               {
                                   {|FP0802:Task|}<int> pending = input;
                                   return await pending;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_AsyncLambdaAndAnonymousMethodKeyword_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using static System.Threading.Tasks.Task;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static Action Unary() => {|FP0802:async|} () => await {|FP0802:Yield|}();
                               public static Action Anonymous() => {|FP0802:async|} delegate { await {|FP0802:Yield|}(); };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_TrustListAndConstrainedParameterFollowTypeClassification_Reports()
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0802", """
            using System.Threading.Tasks;

            [Fprog.FunctionalCore]
            public static class Work
            {
                public static T Pass<T>(T value) where T : {|FP0802:Task|} => value;
            }
            """, "is_global = true\nfprog_pure_apis = System.Threading.Tasks", testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0802_CoreLookalikesShellDerivedTypeAndStaticImport_NoReport()
    {
        return VerifyAsync("""
                           using static System.Threading.Tasks.Task;

                           namespace Code
                           {
                               [Fprog.FunctionalCore]
                               public sealed record Task(int Count);

                               public sealed class WorkItem : System.Threading.Tasks.Task
                               {
                                   public WorkItem(System.Action run) : base(run) { }
                               }

                               [Fprog.FunctionalCore]
                               public static class Work
                               {
                                   public static Task Ready() => new(1);
                                   public static WorkItem Make() => new(static () => { });
                                   public static string Label => nameof(System.Threading.Tasks.Task);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_UnionMemberWithAsyncTypeAnalyzedOnce_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           [Fprog.FunctionalCore]
                           public union Result(int, string)
                           {
                               public static {|FP0802:Task|}<int> Create() => {|FP0802:Task|}.FromResult(1);
                           }
                           """);
    }

    [TestMethod]
    public async Task FP0802_FilterSpanTouchingTheName_Reports()
    {
        var compilation = TestCompilation.Create("""
            [Fprog.FunctionalCore]
            public static class Work
            {
                public static int Result() => System.Threading.Tasks.Task.FromResult(1).Result;
            }
            """);
        var method = compilation.GetMethodDeclaration("Result");
        var name = method.DescendantNodes().OfType<IdentifierNameSyntax>().Single(n => n.Identifier.ValueText == "Task").Span;
        var model = compilation.GetSemanticModel(method.SyntaxTree);
        var analyzer = new Fp0802AsyncBoundaryAnalyzer();
        var cancellationToken = testContext.CancellationToken;
        var whole = await compilation.WithAnalyzers([analyzer]).GetAnalysisResultAsync(model, null, cancellationToken);

        // A caret inside the name, a caret at its end, and the dot that follows it.
        foreach (var span in new[] { new TextSpan(name.Start + 1, 0), new TextSpan(name.End, 0), new TextSpan(name.End, 1) })
        {
            var part = await compilation.WithAnalyzers([analyzer]).GetAnalysisResultAsync(model, span, cancellationToken);
            var expected = whole.GetAllDiagnostics(analyzer).Where(d => span.IntersectsWith(d.Location.SourceSpan)).ToArray();

            Assert.HasCount(1, expected);
            CollectionAssert.AreEqual(expected, part.GetAllDiagnostics(analyzer).ToArray());
        }
    }

    [TestMethod]
    public async Task FP0802_FilterSpanUsesLocalAnchorOutsideTheSpan_Reports()
    {
        var compilation = TestCompilation.Create("""
            [Fprog.FunctionalCore]
            public static class Work
            {
                public static int Read()
                {
                    var pending = System.Threading.Tasks.Task.FromResult(1);
                    return pending.Result + System.Threading.Tasks.Task.FromResult(2).Result;
                }
            }
            """);
        var method = compilation.GetMethodDeclaration("Read");
        var span = method.DescendantNodes().OfType<ReturnStatementSyntax>().Single().Span;
        var model = compilation.GetSemanticModel(method.SyntaxTree);
        var analyzer = new Fp0802AsyncBoundaryAnalyzer();
        var cancellationToken = testContext.CancellationToken;

        var whole = await compilation.WithAnalyzers([analyzer]).GetAnalysisResultAsync(model, null, cancellationToken);
        var part = await compilation.WithAnalyzers([analyzer]).GetAnalysisResultAsync(model, span, cancellationToken);
        var expected = whole.GetAllDiagnostics(analyzer).Where(d => span.IntersectsWith(d.Location.SourceSpan)).ToArray();

        Assert.HasCount(1, expected);
        Assert.AreEqual("Task", method.SyntaxTree.GetText(cancellationToken).ToString(expected[0].Location.SourceSpan));
        CollectionAssert.AreEqual(expected, part.GetAllDiagnostics(analyzer).ToArray());
    }

    [TestMethod]
    public Task FP0802_InferredCopyOfWrittenTaskParameter_NoReport()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static async {|FP0802:Task|}<int> Wait({|FP0802:Task|}<int> input)
                               {
                                   var pending = input;
                                   return await pending + await pending;
                               }
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0802", source, testContext.CancellationToken);
    }
}
