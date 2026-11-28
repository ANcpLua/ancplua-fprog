using System.Collections.Generic;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Shared;

[TestClass]
public sealed class PurityTests
{
    private const string Source = """
                                  namespace System.Threading.Tasks.Custom
                                  {
                                      [Fprog.FunctionalCore]
                                      public sealed class Core;
                                      public sealed class Async;
                                  }

                                  namespace Helpers.Parse { public sealed class Reader; }
                                  namespace Helpers.Parse.Sub { public sealed class Reader; }
                                  namespace Helpers.Parsing { public sealed class Reader; }
                                  namespace Helpers.Edge
                                  {
                                      public sealed class Trusted;
                                      public sealed class Other;
                                  }
                                  """;

    [TestMethod]
    [DataRow("System.Threading.Tasks.Custom.Core", "Core")]
    [DataRow("System.Threading.Tasks.Custom.Async", "Async")]
    [DataRow("System.Threading.Tasks.Task`1", "Async")]
    [DataRow("System.Runtime.CompilerServices.TaskAwaiter", "Async")]
    [DataRow("System.Runtime.CompilerServices.IAsyncStateMachine", "Async")]
    [DataRow("System.IAsyncDisposable", "Async")]
    [DataRow("System.Collections.Generic.IAsyncEnumerable`1", "Async")]
    [DataRow("System.Collections.Generic.IAsyncEnumerator`1", "Async")]
    [DataRow("System.Linq.AsyncEnumerable", "Async")]
    [DataRow("System.Collections.IList", "Pure")]
    [DataRow("System.Collections.Generic.Dictionary`2", "Pure")]
    [DataRow("System.Collections.ObjectModel.ReadOnlyCollection`1", "Pure")]
    [DataRow("System.Collections.Immutable.ImmutableList`1", "Pure")]
    [DataRow("System.Collections.Frozen.FrozenDictionary`2", "Pure")]
    [DataRow("System.Numerics.BigInteger", "Pure")]
    [DataRow("System.Globalization.CultureInfo", "Pure")]
    [DataRow("System.Buffers.Binary.BinaryPrimitives", "Pure")]
    [DataRow("System.Buffers.Text.Utf8Parser", "Pure")]
    [DataRow("System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute", "Pure")]
    [DataRow("System.Text.StringBuilder", "Pure")]
    [DataRow("System.Text.Unicode.UnicodeRange", "Pure")]
    [DataRow("System.Math", "Pure")]
    [DataRow("System.Linq.Enumerable", "Pure")]
    [DataRow("System.Linq.ILookup`2", "Pure")]
    [DataRow("System.Runtime.CompilerServices.IUnion", "Pure")]
    [DataRow("System.Buffers.SearchValues`1", "Pure")]
    [DataRow("System.Collections.Concurrent.ConcurrentDictionary`2", "Shell")]
    [DataRow("System.Environment", "Shell")]
    [DataRow("System.Environment+SpecialFolder", "Shell")]
    [DataRow("System.Random", "Shell")]
    [DataRow("System.TimeProvider", "Shell")]
    [DataRow("System.WeakReference`1", "Shell")]
    [DataRow("System.Threading.ThreadPool", "Shell")]
    [DataRow("System.Linq.ParallelEnumerable", "Shell")]
    [DataRow("Helpers.Edge.Other", "Shell")]
    public void BuiltInCatalog(string metadataName, string expected)
    {
        var compilation = TestCompilation.Create(Source);
        var tree = compilation.SyntaxTrees.First();
        var options = new ConfigProvider("", tree, "System.Threading.Tasks");
        var type = compilation.GetNamedType(metadataName);
        var model = new Purity.Model(options);

        Assert.AreEqual(expected, Purity.Classify(type, options, tree).ToString());
        Assert.AreEqual(expected, model.Classify(type, model.ForTree(tree)).ToString());
    }

    [TestMethod]
    [DataRow("Helpers.Parse.Reader", "Pure", "Shell")]
    [DataRow("Helpers.Parse.Sub.Reader", "Pure", "Shell")]
    [DataRow("Helpers.Parsing.Reader", "Shell", "Shell")]
    [DataRow("Helpers.Edge.Trusted", "Pure", "Pure")]
    [DataRow("Helpers.Edge.Other", "Shell", "Pure")]
    public void ConfigUsesExactTypeAndNamespaceTreeWithLocalOverride(
        string metadataName, string globalExpected, string localExpected)
    {
        var compilation = TestCompilation.Create([Source, "public sealed class OtherFile;"]);
        var type = compilation.GetNamedType(metadataName);
        var trees = compilation.SyntaxTrees.ToArray();
        var options = new ConfigProvider(" Helpers.Parse , Helpers.Edge.Trusted ", trees[1], "Helpers.Edge");
        var model = new Purity.Model(options);

        Assert.AreEqual(globalExpected, Purity.Classify(type, options, trees[0]).ToString());
        Assert.AreEqual(localExpected, Purity.Classify(type, options, trees[1]).ToString());
        Assert.AreEqual(globalExpected, model.Classify(type, model.ForTree(trees[0])).ToString());
        Assert.AreEqual(localExpected, model.Classify(type, model.ForTree(trees[1])).ToString());
    }

    private sealed class ConfigProvider(string global, SyntaxTree localTree, string local) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Config(global);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return new Config(tree == localTree ? local : null);
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return new Config(null);
        }
    }

    private sealed class Config(string? value) : AnalyzerConfigOptions
    {
        public override IEnumerable<string> Keys => value is null ? [] : ["fprog_pure_apis"];

        public override bool TryGetValue(string key, out string found)
        {
            found = value ?? "";
            return key == "fprog_pure_apis" && value is not null;
        }
    }
}
