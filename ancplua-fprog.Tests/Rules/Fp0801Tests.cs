using System.Linq;
using System.Threading.Tasks;
using ancplua_fprog.Rules;
using ancplua_fprog.Tests.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0801Tests(TestContext testContext)
{
    private const string Config = """
                                  is_global = true
                                  fprog_pure_apis = Trusted
                                  """;

    private const string AcquiredValues = """

                                          namespace Effects
                                          {
                                              public delegate int Read();

                                              public sealed class Cursor : System.IDisposable
                                              {
                                                  public int Slot;
                                                  public int Value { get; }
                                                  public event System.Action Changed = () => 0.ToString();
                                                  public void Dispose() => Changed();
                                                  public int Next() => 1;
                                                  public int this[int index] => index;
                                                  public System.Collections.Generic.List<int>.Enumerator GetEnumerator()
                                                      => new System.Collections.Generic.List<int> { 1 }.GetEnumerator();

                                                  public static int operator +(Cursor value, int offset) => offset;
                                                  public static implicit operator int(Cursor value) => value.Next();
                                                  public static explicit operator string(Cursor value) => "cursor";
                                              }
                                          }

                                          namespace Trusted
                                          {
                                              public static class Source
                                              {
                                                  public static Effects.Cursor Open() => new();
                                                  public static Effects.Read Reader() => () => 1;
                                                  public static Effects.Cursor Current { get; } = new();
                                                  public static int Make(Effects.Cursor cursor) => cursor.Next();
                                                  public static int Count(Effects.Cursor[] cursors) => cursors.Length;
                                                  public static T Infer<T>(T value) => value;
                                                  public static (Effects.Cursor, int) Row => (new(), 1);
                                                  public static View? State => new();
                                              }

                                              public sealed class View { public Effects.Cursor? Current => new(); }
                                          }
                                          """;

    [TestMethod]
    public Task FP0801_WrittenTypeNamesInSignaturesCreationTypeOfAndStaticAccess_Reports()
    {
        return VerifyAsync("""
                           using Reader = System.IO.StreamReader;

                           [Fprog.FunctionalCore]
                           public sealed class Report() : System.IO.{|FP0801:StringReader|}("data")
                           {
                               private readonly {|FP0801:Reader|} _reader = new("file");
                               public {|FP0801:Reader|} Current => _reader;
                               public (int Length, System.IO.{|FP0801:FileInfo|} File) Entry
                                   => (1, new System.IO.{|FP0801:FileInfo|}("file"));
                               public System.Type Type => typeof(System.IO.{|FP0801:StreamReader|});
                               public string Read(object other) => (({|FP0801:Reader|})other).ReadToEnd();
                               public static string Load(string path) => System.IO.{|FP0801:File|}.ReadAllText(path);
                               public static string Tag => System.Environment.{|FP0801:SpecialFolder|}.Desktop.ToString();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UsesOfTypesInAttributesNameofUsingAndDocs_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;
                           using static System.Console;

                           public sealed class FactAttribute(System.Type type) : System.Attribute
                           {
                               public System.Type Type { get; } = type;
                           }

                           /// <seealso cref="FileInfo"/>
                           [Fact(typeof(FileInfo))]
                           [Fprog.FunctionalCore]
                           public static class Docs
                           {
                               public static string Name => nameof(System.IO.FileInfo);
                               public static string OtherName => nameof(System.Console.WriteLine);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UnqualifiedAndExtensionMemberNames_Reports()
    {
        return VerifyAsync("""
                           using static System.IO.File;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static string Read(string path) => {|FP0801:ReadAllText|}(path);
                               public static System.Func<string, string> ReadLater() => {|FP0801:ReadAllText|};
                               public static int Par(int[] values) => values.{|FP0801:AsParallel|}().Count();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_MembersOfExternalTypeAndReceiverChainOnce_Reports()
    {
        return VerifyAsync("""
                           public static class External
                           {
                               public static System.IO.StreamReader Open(string path) => new(path);
                           }

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static string Read(string path) =>
                                   {|FP0801:External|}.Open(path).ReadToEnd();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_InferredMemberIndexerAndDelegateUses_Reports()
    {
        return VerifyTrustedAsync("""
                                  using Trusted;

                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static int Compute()
                                      {
                                          var cursor = Source.Open();
                                          var callback = Source.Reader();
                                          var next = cursor.{|FP0801:Next|};
                                          cursor.{|FP0801:Changed|} += () => 0.ToString();
                                          return cursor.{|FP0801:Value|} + cursor.{|FP0801:Slot|}
                                              + {|FP0801:cursor[0]|} + {|FP0801:callback|}() + next();
                                      }
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_ConditionalAndInferredMemberWithoutWrittenName_Reports()
    {
        return VerifyTrustedAsync("""
                                  using Trusted;

                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static int Unwrap()
                                      {
                                          var (inferred, _) = Source.Row;
                                          return inferred?.{|FP0801:Next|}() ?? 0;
                                      }

                                      public static int Invoke()
                                      {
                                          var current = Source.Infer({|FP0801:Source.Current|});
                                          return current.{|FP0801:Next|}();
                                      }

                                      public static int Conditional() => Source.State?.Current?.{|FP0801:Next|}() ?? 0;
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_WrittenDeclarationAnchorsNamedMembersIndexerAndDelegateOnce_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;

                           public delegate int Read();
                           public sealed class Table { public int this[int index] => index; }

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static int Count({|FP0801:Read|} read, {|FP0801:Table|} table, {|FP0801:StreamReader|} input)
                                   => read() + table[0] + input.ReadToEnd().Length;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_OperatorsAndConversionsWithoutTypeName_Reports()
    {
        return VerifyTrustedAsync("""
                                  using Trusted;

                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static int Arithmetic() => Source.Open() {|FP0801:+|} 1;
                                      public static int Implicit() => {|FP0801:Source.Open()|};
                                      public static string Explicit() => {|FP0801:(|}string)Source.Open();
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_WrittenDeclarationAnchorsOperatorsAndConversionsOnce_Reports()
    {
        return VerifyTrustedAsync("""
                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static int Arithmetic(Effects.{|FP0801:Cursor|} source) => (int)source + (source + 1);
                                      public static int Implicit(Effects.{|FP0801:Cursor|} source) => source;
                                      public static string Explicit(Effects.{|FP0801:Cursor|} source) => (string)source;
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_UnanchoredValueInForeachUsingArgumentAndReturn_Reports()
    {
        return VerifyTrustedAsync("""
                                  using Trusted;

                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static object Read() => {|FP0801:Source.Current|};
                                      private static object Wrap(object value) => value;
                                      public static object Pass() => Wrap({|FP0801:Source.Open()|});

                                      public static int Sum()
                                      {
                                          var source = Source.Open();
                                          int sum = 0;
                                          foreach (var item in {|FP0801:source|})
                                              sum += item;

                                          using ({|FP0801:source|}) { }
                                          using var other = {|FP0801:Source.Open()|};
                                          return sum;
                                      }
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_WrittenDeclarationAnchorsForeachUsingArgumentAndReturnOnce_Reports()
    {
        return VerifyTrustedAsync("""
                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static object Read(Effects.{|FP0801:Cursor|} source)
                                      {
                                          foreach (var value in source) { }
                                          using (source) { }
                                          return Wrap(source);
                                      }

                                      private static object Wrap(object value) => value;
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_TargetTypedNewOfUnwrittenType_Reports()
    {
        return VerifyTrustedAsync("""
                                  using Trusted;

                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static int Compute() => Source.Make({|FP0801:new|}());
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_TargetTypedNewInCollectionExpressionAnchoredByWrittenType_Reports()
    {
        return VerifyTrustedAsync("""
                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static Effects.{|FP0801:Cursor|}[] Pair() => [new(), new()];

                                      public static int Sample()
                                      {
                                          Effects.{|FP0801:Cursor|}[][] named = [[new()], [new(), new()]];
                                          return named.Length;
                                      }
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_TargetTypedNewInCollectionExpressionOfUnwrittenType_Reports()
    {
        return VerifyTrustedAsync("""
                                  using Trusted;

                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static int Compute() => Source.Count([{|FP0801:new|}(), {|FP0801:new|}()]);
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_ArrayOfShellTypeNamesItsElementType_Reports()
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0801", """
                                                            [Fprog.FunctionalCore]
                                                            public static class Report
                                                            {
                                                                private static T Same<T>(T value) => value;

                                                                public static int First(Effects.{|FP0801:Cursor|}[] values)
                                                                    => {|#0:Same(values)[0]|}.Next();
                                                            }
                                                            """ + AcquiredValues, Config, testContext.CancellationToken,
            new DiagnosticResult("FP0801", DiagnosticSeverity.Warning).WithLocation(0).WithArguments("Cursor"));
    }

    [TestMethod]
    public Task FP0801_TargetTypedNewAnchoredByTypeWrittenInCoreOnce_Reports()
    {
        return VerifyTrustedAsync("""
                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static Effects.{|FP0801:Cursor|} P { get; } = new();
                                      public static Effects.{|FP0801:Cursor|} Make() => new();
                                      private static int Use(Effects.{|FP0801:Cursor|} value) => value.Next();

                                      public static int Sample()
                                      {
                                          Effects.{|FP0801:Cursor|} named = new();
                                          return Use(named) + Use(new());
                                      }
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_LockAndStyleOfExplicitThreadingType_Reports()
    {
        return VerifyAsync("""
                           using System.Threading;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static int Run({|FP0801:Lock|} gate)
                               {
                                   {|FP0801:lock|} (gate) { return 1; }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_PerFileTrustedApiConfigLeavesOutSiblingNamespace_Reports()
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0801", """
            namespace Helpers.Pure
            {
                public static class Maths { public static int Twice(int n) => n * 2; }
            }

            namespace Helpers.Edge
            {
                public static class Maths { public static int Twice(int n) => n * 2; }
            }

            namespace Domain
            {
                [Fprog.FunctionalCore]
                public static class Work
                {
                    public static int Value() => Helpers.Pure.Maths.Twice(2) + Helpers.Edge.{|FP0801:Maths|}.Twice(1);
                }
            }
            """, "root = true\n[*.cs]\nfprog_pure_apis = Helpers.Pure", testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0801_CoreNestedInShellType_Reports()
    {
        return VerifyAsync("""
                           public sealed class Outer
                           {
                               public static bool Exists() => System.IO.File.Exists("path");

                               [Fprog.FunctionalCore]
                               public static class Inner
                               {
                                   public static bool Exists() => System.IO.{|FP0801:File|}.Exists("path");
                               }
                           }
                           """);
    }

    [TestMethod]
    public async Task FP0801_AssemblyCoreTopLevelStatement_Reports()
    {
        var compilation = TestCompilation.CreateProgram("""
            [assembly: Fprog.FunctionalCore]
            return System.IO.File.Exists("path") ? 0 : 1;
            """);
        var analyzer = new Fp0801ShellBoundaryAnalyzer();
        var cancellationToken = testContext.CancellationToken;
        var result = await compilation.WithAnalyzers([analyzer]).GetAnalysisResultAsync(cancellationToken);
        var diagnostic = result.GetAllDiagnostics(analyzer).Single();

        Assert.AreEqual("FP0801", diagnostic.Id);
        Assert.AreEqual("File", compilation.SyntaxTrees.First().GetText(cancellationToken).ToString(diagnostic.Location.SourceSpan));
    }

    [TestMethod]
    public async Task FP0801_FilterSpanUsesLocalAnchorOutsideTheSpan_Reports()
    {
        var compilation = TestCompilation.Create("""
            [Fprog.FunctionalCore]
            public static class Work
            {
                public static (string?, bool) Read()
                {
                    var reader = new System.IO.StreamReader("path");
                    return (reader.ReadLine(), System.IO.File.Exists("path"));
                }
            }
            """);
        var method = compilation.GetMethodDeclaration("Read");
        var span = method.DescendantNodes().OfType<ReturnStatementSyntax>().Single().Span;
        var model = compilation.GetSemanticModel(method.SyntaxTree);
        var analyzer = new Fp0801ShellBoundaryAnalyzer();
        var cancellationToken = testContext.CancellationToken;

        var whole = await compilation.WithAnalyzers([analyzer]).GetAnalysisResultAsync(model, null, cancellationToken);
        var part = await compilation.WithAnalyzers([analyzer]).GetAnalysisResultAsync(model, span, cancellationToken);
        var expected = whole.GetAllDiagnostics(analyzer).Where(d => span.IntersectsWith(d.Location.SourceSpan)).ToArray();

        Assert.HasCount(1, expected);
        Assert.AreEqual("File", method.SyntaxTree.GetText(cancellationToken).ToString(expected[0].Location.SourceSpan));
        CollectionAssert.AreEqual(expected, part.GetAllDiagnostics(analyzer).ToArray());
    }

    [TestMethod]
    public Task FP0801_CorePureAndAsyncTypes_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Immutable;
                           using System.Globalization;
                           using System.Text.Unicode;
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public sealed record Data(int Value);

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static ImmutableArray<Data> Produce() => [new Data(1)];
                               public static string Format(int n) => n.ToString(CultureInfo.InvariantCulture);
                               public static Task<Data> Wrap(Data data) => Task.FromResult(data);
                               public static UnicodeRange Range() => UnicodeRange.Create('a', 'z');
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_ReferencedCoreTypeAndInterface_NoReport()
    {
        return FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0801", """
            using Domain;

            [Fprog.FunctionalCore]
            public static class Work
            {
                public static Count Run(ICounter input) => new Count(input.Get());
            }
            """, ReferencedAssemblySources.CoreTypes, testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0801_ReferencedUnmarkedSiblingType_Reports()
    {
        return FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0801", """
            [Fprog.FunctionalCore]
            public static class Work
            {
                public static int Read(Domain.{|FP0801:ExternalCount|} external) => external.Value;
            }
            """, ReferencedAssemblySources.CoreTypes, testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0801_UnionHeaderAndMemberAreAnalyzedOnce_Reports()
    {
        return VerifyAsync("""
                           using System.IO;

                           [Fprog.FunctionalCore]
                           public union Upload({|FP0801:FileInfo|}, string)
                           {
                               public static {|FP0801:FileInfo|} Read(string path) => new(path);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_OutsideCoreAndGeneratedMembers_NoReport()
    {
        return VerifyAsync("""
                           public static class Outside
                           {
                               public static string Read() => System.IO.File.ReadAllText("file");
                           }

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               [System.CodeDom.Compiler.GeneratedCode("test", "1")]
                               public static string Generated() => System.IO.File.ReadAllText("file");

                               public static string Value() => "ready";
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_RandomEnvironmentAndTimeProvider_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using static System.Environment;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static int Capacity() => {|FP0801:ProcessorCount|};
                               public static int Roll({|FP0801:Random|} random) => random.Next(1, 7);
                               public static int Seeded() => new {|FP0801:Random|}(42).Next();
                               public static bool IsLate(DateTimeOffset due, {|FP0801:TimeProvider|} clock) => clock.GetUtcNow() > due;
                               public static DateTimeOffset MachineTime() => {|FP0801:TimeProvider|}.System.GetUtcNow();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_LocalInitializedWithWrittenConstruction_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static string Read(string path)
                               {
                                   var reader = new {|FP0801:StreamReader|}(path);
                                   return reader.ReadLine() + reader.ReadLine();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_LocalCopiedFromWrittenParameter_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static string Read({|FP0801:StreamReader|} reader)
                               {
                                   var copy = reader;
                                   return copy.ReadLine() + copy.ReadLine();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_LongChainOfAnchoredLocals_NoReport()
    {
        var assignments = string.Join("\n", Enumerable.Range(1, 256)
            .Select(index => $"var reader{index} = reader{index - 1};"));
        return VerifyAsync($$"""
                             using System.IO;

                             [Fprog.FunctionalCore]
                             public static class Report
                             {
                                 public static string Read({|FP0801:StreamReader|} reader0)
                                 {
                                     {{assignments}}
                                     return reader256.ReadToEnd();
                                 }
                             }
                             """);
    }

    [TestMethod]
    public Task FP0801_AllAssignmentsAnchorLocalIncludingAliasCycle_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static string Read({|FP0801:StreamReader|} first,
                                   {|FP0801:StreamReader|} second, bool replace)
                               {
                                   var reader = first;
                                   var copy = reader;
                                   if (replace)
                                       reader = second;
                                   else
                                       reader = copy;
                                   return reader.ReadLine() + copy.ReadLine();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_ForeachVariableInheritsCollectionAnchor_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.IO;

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static int Read(IEnumerable<{|FP0801:StreamReader|}> readers)
                               {
                                   var length = 0;
                                   foreach (var reader in readers)
                                       length += reader.ReadToEnd().Length;
                                   return length;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_ForeachOverInferredArrayWithWrittenElementType_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static int Read()
                               {
                                   var readers = new {|FP0801:StreamReader|}[0];
                                   var length = 0;
                                   foreach (var reader in readers)
                                       length += reader.ReadToEnd().Length;
                                   return length;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_PatternVariableInheritsInputAnchor_NoReport()
    {
        return VerifyAsync("""
                           using System.IO;

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static string Read({|FP0801:StreamReader|}? reader)
                                   => reader is { } copy ? copy.ReadToEnd() : "";
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_DeconstructionUsesTheSelectedComponentAnchor_Reports()
    {
        return VerifyTrustedAsync("""
                                  using Trusted;

                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static int Read(Effects.{|FP0801:Cursor|} cursor)
                                      {
                                          var (first, second) = (cursor, Source.Open());
                                          return first.Next() + second.{|FP0801:Next|}();
                                      }
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_UsingDeclarationAnchorsTheInferredLocal_NoReport()
    {
        return VerifyTrustedAsync("""
                                  using Trusted;

                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static int Read()
                                      {
                                          using var cursor = {|FP0801:Source.Open()|};
                                          var copy = cursor;
                                          return copy.Next();
                                      }
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_LocalFromPureSource_Reports()
    {
        return VerifyTrustedAsync("""
                                  using Trusted;

                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static int Read()
                                      {
                                          var cursor = Source.Open();
                                          return cursor.{|FP0801:Next|}();
                                      }
                                  }
                                  """);
    }

    [TestMethod]
    public Task FP0801_OneUnanchoredReassignmentOfLocal_Reports()
    {
        return VerifyTrustedAsync("""
                                  using Trusted;

                                  [Fprog.FunctionalCore]
                                  public static class Report
                                  {
                                      public static int Read(Effects.{|FP0801:Cursor|} input, bool replace)
                                      {
                                          var cursor = input;
                                          if (replace)
                                              cursor = Source.Open();
                                          return cursor.{|FP0801:Next|}();
                                      }
                                  }
                                  """);
    }

    [TestMethod]
    [DataRow("flag ? input : Source.Open()")]
    [DataRow("input ?? Source.Open()")]
    [DataRow("flag switch { true => input, false => Source.Open() }")]
    public Task FP0801_OneUnanchoredAlternativeOfAnAssignment_Reports(string initializer)
    {
        return VerifyTrustedAsync($$"""
                                    using Trusted;

                                    [Fprog.FunctionalCore]
                                    public static class Report
                                    {
                                        public static int Read(Effects.{|FP0801:Cursor|}? input, bool flag)
                                        {
                                            var cursor = {{initializer}};
                                            return cursor.{|FP0801:Next|}();
                                        }
                                    }
                                    """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0801", source, testContext.CancellationToken);
    }

    private Task VerifyTrustedAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0801", source + AcquiredValues, Config, testContext.CancellationToken);
    }
}
