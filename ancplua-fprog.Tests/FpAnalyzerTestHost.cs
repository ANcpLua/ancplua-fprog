using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace ancplua_fprog.Tests;

/// <summary>
///     The single entry point for analyzer tests, shared by visible and hidden tests.
///     Every test compiles against exactly the .NET 11 reference assemblies from
///     Basic.Reference.Assemblies.Net110, downloads nothing at test time, parses C# preview
///     syntax and sees the Fprog core marker.
/// </summary>
internal static class FpAnalyzerTestHost
{
    private const string AnalyzerAssemblyName = "ancplua-fprog";

    /// <summary>The marker attributes that define the rule scope (see AGENTS.md, "Scope").</summary>
    internal const string AnnotationsSource = """
                                              namespace Fprog
                                              {
                                                  [System.AttributeUsage(System.AttributeTargets.Assembly | System.AttributeTargets.Class | System.AttributeTargets.Struct |
                                                  System.AttributeTargets.Interface | System.AttributeTargets.Enum | System.AttributeTargets.Delegate)]
                                                  public sealed class FunctionalCoreAttribute : System.Attribute { }
                                              }
                                              """;

    /// <summary>
    ///     A target framework without a reference-assembly package. The testing library resolves
    ///     nothing by itself, so <see cref="Net110.References.All" /> is the only core library set.
    ///     (<c>ReferenceAssemblies.Default</c> is <c>Net.Net80</c> on .NET 8+ and would add a second set.)
    /// </summary>
    private static readonly ReferenceAssemblies NoResolvedReferences = new("net11.0");

    private static readonly Lazy<ImmutableArray<DiagnosticAnalyzer>> AllAnalyzers = new(LoadAllAnalyzers);

    /// <summary>
    ///     Runs every analyzer in the analyzer assembly and compares only diagnostics with
    ///     <paramref name="ruleId" />. Hidden tests use this method, so analyzer class names are free.
    ///     Expected diagnostics usually come from markup such as <c>{|FP0301:throw|}</c>.
    /// </summary>
    public static Task VerifyRuleAsync(
        string ruleId,
        string source,
        CancellationToken cancellationToken,
        params DiagnosticResult[] expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        var analyzers = AllAnalyzers.Value;
        var reported = analyzers.SelectMany(SupportedIds).ToImmutableHashSet(StringComparer.Ordinal);
        if (!reported.Contains(ruleId))
            throw new InvalidOperationException($"No analyzer in {AnalyzerAssemblyName} supports {ruleId}.");

        var test = CreateTest(new RuleTest(analyzers), source, expected);
        test.DisabledDiagnostics.AddRange(reported.Where(id => id != ruleId));
        return test.RunAsync(cancellationToken);
    }

    public static Task VerifyCompilerAsync(string source, CancellationToken cancellationToken,
        params DiagnosticResult[] expected)
    {
        var test = CreateTest(new RuleTest([]), source, expected);
        test.CompilerDiagnostics = CompilerDiagnostics.Warnings;
        return test.RunAsync(cancellationToken);
    }

    /// <summary>Runs a single analyzer type. Useful while developing one rule.</summary>
    public static Task VerifyAsync<TAnalyzer>(
        string source,
        CancellationToken cancellationToken,
        params DiagnosticResult[] expected)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        return CreateTest(new RuleTest([new TAnalyzer()]), source, expected).RunAsync(cancellationToken);
    }

    private static RuleTest CreateTest(RuleTest test, string source, DiagnosticResult[] expected)
    {
        test.TestCode = source;
        test.ReferenceAssemblies = NoResolvedReferences;
        // Parse documentation comments without requesting CS1591 for public members.
        test.TestState.DocumentationMode = DocumentationMode.Parse;
        test.TestState.Sources.Add(("Fprog.Annotations.cs", AnnotationsSource));
        test.TestState.AdditionalReferences.AddRange(Net110.References.All);
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    private static IEnumerable<string> SupportedIds(DiagnosticAnalyzer analyzer)
    {
        return analyzer.SupportedDiagnostics.Select(descriptor => descriptor.Id);
    }

    private static ImmutableArray<DiagnosticAnalyzer> LoadAllAnalyzers()
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.Load(new AssemblyName(AnalyzerAssemblyName));
        }
        catch (FileNotFoundException exception)
        {
            throw new InvalidOperationException(
                $"Assembly '{AnalyzerAssemblyName}' was not found. The test project needs a ProjectReference " +
                $"to the analyzer project, and the analyzer's AssemblyName must be '{AnalyzerAssemblyName}'.",
                exception);
        }

        return
        [
            .. assembly.GetTypes()
                .Where(type => !type.IsAbstract
                               && typeof(DiagnosticAnalyzer).IsAssignableFrom(type)
                               && type.GetCustomAttributes<DiagnosticAnalyzerAttribute>()
                                   .Any(attribute => attribute.Languages.Contains(LanguageNames.CSharp)))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
        ];
    }

    /// <summary>
    ///     A C# analyzer test over a fixed set of analyzers. It derives from the language-neutral base
    ///     because <c>CSharpAnalyzerTest</c> runs exactly one analyzer type, and a placeholder analyzer
    ///     declared in this test assembly would trigger RS1036 and RS1038.
    /// </summary>
    private sealed class RuleTest(ImmutableArray<DiagnosticAnalyzer> analyzers) : AnalyzerTest<DefaultVerifier>
    {
        public override string Language => LanguageNames.CSharp;

        protected override string DefaultFileExt => "cs";

        protected override CompilationOptions CreateCompilationOptions()
        {
            return new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);
        }

        protected override ParseOptions CreateParseOptions()
        {
            return new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Diagnose);
        }

        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
        {
            return analyzers;
        }
    }
}
