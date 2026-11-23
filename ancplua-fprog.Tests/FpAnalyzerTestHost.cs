using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ancplua_fprog.Tests.Shared;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace ancplua_fprog.Tests;

/// <summary>
///     The single entry point for analyzer tests.
///     Every test compiles against exactly the .NET 11 reference assemblies from
///     Basic.Reference.Assemblies.Net110, downloads nothing at test time, parses C# 15
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

    private static readonly CSharpParseOptions CSharp15 = new(LanguageVersion.CSharp15, DocumentationMode.Diagnose);

    private static readonly CSharpParseOptions MemorySafetyPreview = CSharp15.WithLanguageVersion(LanguageVersion.Preview)
        .WithFeatures([new KeyValuePair<string, string>("updated-memory-safety-rules", "on")]);

    private static readonly Lazy<ImmutableArray<DiagnosticAnalyzer>> AllAnalyzers = new(LoadAllAnalyzers);

    /// <summary>
    ///     Runs every analyzer in the analyzer assembly and compares only diagnostics with
    ///     <paramref name="ruleId" />, so tests do not depend on analyzer class names.
    ///     Expected diagnostics usually come from markup such as <c>{|FP0301:throw|}</c>.
    /// </summary>
    public static Task VerifyRuleAsync(
        string ruleId,
        string source,
        CancellationToken cancellationToken,
        params DiagnosticResult[] expected)
    {
        return VerifyRuleAsync(ruleId, source, null, cancellationToken, expected);
    }

    public static Task VerifyRuleAsync(
        string ruleId,
        string source,
        string? analyzerConfig,
        CancellationToken cancellationToken,
        params DiagnosticResult[] expected)
    {
        return VerifyRuleCoreAsync(ruleId, source, analyzerConfig, cancellationToken, expected);
    }

    public static Task VerifyRuleWithMemorySafetyPreviewAsync(
        string ruleId, string source, CancellationToken cancellationToken)
    {
        return VerifyRuleCoreAsync(ruleId, source, null, cancellationToken, [], MemorySafetyPreview);
    }

    /// <summary>Runs a rule against a consumer of a library compiled to an in-memory DLL.</summary>
    public static Task VerifyRuleWithReferencedAssemblyAsync(
        string ruleId, string source, string librarySource, CancellationToken cancellationToken,
        params DiagnosticResult[] expected)
    {
        return VerifyRuleWithReferencedAssemblyAsync(ruleId, source, librarySource, false, cancellationToken, expected);
    }

    /// <summary>Runs a rule against a consumer of a metadata-only reference assembly.</summary>
    public static Task VerifyRuleWithReferenceAssemblyAsync(
        string ruleId, string source, string librarySource, CancellationToken cancellationToken,
        params DiagnosticResult[] expected)
    {
        return VerifyRuleWithReferencedAssemblyAsync(ruleId, source, librarySource, true, cancellationToken, expected);
    }

    private static Task VerifyRuleWithReferencedAssemblyAsync(
        string ruleId, string source, string librarySource, bool metadataOnly, CancellationToken cancellationToken,
        DiagnosticResult[] expected)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var library = TestCompilation.Create(librarySource, "ReferencedTypes");
        var reference = TestCompilation.ToImageReference(library, metadataOnly);
        return VerifyRuleCoreAsync(ruleId, source, null, cancellationToken, expected, reference: reference);
    }

    private static Task VerifyRuleCoreAsync(
        string ruleId, string source, string? analyzerConfig, CancellationToken cancellationToken,
        DiagnosticResult[] expected, CSharpParseOptions? parseOptions = null, MetadataReference? reference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        var analyzers = AllAnalyzers.Value;
        var reported = analyzers.SelectMany(SupportedIds).ToImmutableHashSet(StringComparer.Ordinal);
        if (!reported.Contains(ruleId))
            throw new InvalidOperationException($"No analyzer in {AnalyzerAssemblyName} supports {ruleId}.");

        var test = CreateTest(new RuleTest(analyzers, parseOptions), source, expected, reference);
        test.DisabledDiagnostics.AddRange(reported.Where(id => id != ruleId));
        if (analyzerConfig is not null)
        {
            var global = analyzerConfig.Split('\n')
                .Any(line => line.TrimStart().StartsWith("is_global", StringComparison.Ordinal));
            test.TestState.AnalyzerConfigFiles.Add((global ? "/.globalconfig" : "/.editorconfig", analyzerConfig));
        }

        return test.RunAsync(cancellationToken);
    }

    public static Task VerifyCompilerAsync(string source, CancellationToken cancellationToken,
        params DiagnosticResult[] expected)
    {
        var test = CreateTest(new RuleTest([]), source, expected);
        test.CompilerDiagnostics = CompilerDiagnostics.Warnings;
        return test.RunAsync(cancellationToken);
    }

    public static Task VerifyAllAsync(string source, CancellationToken cancellationToken,
        IEnumerable<KeyValuePair<string, string>>? parseFeatures = null)
    {
        var options = parseFeatures is null ? CSharp15 : CSharp15.WithFeatures(parseFeatures);
        var test = CreateTest(new RuleTest(AllAnalyzers.Value, options), source, []);
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

    private static RuleTest CreateTest(RuleTest test, string source, DiagnosticResult[] expected,
        MetadataReference? reference = null)
    {
        test.TestCode = source;
        test.ReferenceAssemblies = NoResolvedReferences;
        // Parse documentation comments without requesting CS1591 for public members.
        test.TestState.DocumentationMode = DocumentationMode.Parse;
        test.TestState.AdditionalReferences.AddRange(Net110.References.All);
        // The compiled library already supplies the core marker.
        if (reference is null)
            test.TestState.Sources.Add(("Fprog.Annotations.cs", AnnotationsSource));
        else
            test.TestState.AdditionalReferences.Add(reference);
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
    private sealed class RuleTest(ImmutableArray<DiagnosticAnalyzer> analyzers,
        CSharpParseOptions? parseOptions = null) : AnalyzerTest<DefaultVerifier>
    {
        public override string Language => LanguageNames.CSharp;

        protected override string DefaultFileExt => "cs";

        protected override CompilationOptions CreateCompilationOptions()
        {
            return new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);
        }

        protected override ParseOptions CreateParseOptions()
        {
            return parseOptions ?? CSharp15;
        }

        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
        {
            return analyzers;
        }
    }
}
