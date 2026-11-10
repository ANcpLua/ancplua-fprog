using System;
using System.IO;
using System.Linq;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Shared;

/// <summary>
/// Compiles test source together with <see cref="FpAnalyzerTestHost.AnnotationsSource"/> against the
/// .NET 11 reference assemblies, with C# preview syntax and nullable reference types enabled.
/// The test sources are the first syntax trees, named <c>Test0.cs</c>, <c>Test1.cs</c> and so on.
/// </summary>
internal static class TestCompilation
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    /// <summary>Compiles <paramref name="source"/> and fails the test if it does not compile.</summary>
    public static CSharpCompilation Create(string source, string assemblyName = "Tests", params MetadataReference[] references)
        => Create([source], assemblyName, references);

    /// <summary>
    /// Compiles <paramref name="sources"/>, each as its own file, into one assembly and fails the test
    /// if they do not compile.
    /// </summary>
    public static CSharpCompilation Create(string[] sources, string assemblyName = "Tests", params MetadataReference[] references)
    {
        var compilation = Compile(sources, assemblyName, OutputKind.DynamicallyLinkedLibrary, references);
        AssertNoErrors(compilation);
        return compilation;
    }

    /// <summary>Compiles <paramref name="source"/> as a console program, so it can have top-level statements.</summary>
    public static CSharpCompilation CreateProgram(string source)
    {
        var compilation = Compile([source], "Program", OutputKind.ConsoleApplication, []);
        AssertNoErrors(compilation);
        return compilation;
    }

    /// <summary>
    /// Compiles <paramref name="source"/> that does not compile, for tests of helpers on erroneous code.
    /// Fails the test if the compiler does not report <paramref name="expectedErrorId"/>.
    /// </summary>
    public static CSharpCompilation CreateWithError(string source, string expectedErrorId)
    {
        var compilation = Compile([source], "Erroneous", OutputKind.DynamicallyLinkedLibrary, []);
        var errorIds = Errors(compilation).Select(diagnostic => diagnostic.Id).ToArray();
        Assert.Contains(expectedErrorId, errorIds);
        return compilation;
    }

    /// <summary>Emits <paramref name="compilation"/> and references it as a metadata (DLL) reference.</summary>
    public static MetadataReference ToImageReference(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    /// <summary>The type with the metadata name <paramref name="metadataName"/>, for example <c>N.Outer+Inner</c>.</summary>
    public static INamedTypeSymbol GetNamedType(this Compilation compilation, string metadataName)
    {
        var type = compilation.GetTypeByMetadataName(metadataName);
        Assert.IsNotNull(type, $"Type '{metadataName}' not found.");
        return type;
    }

    /// <summary>The declaration of the only method named <paramref name="methodName"/> in the test sources.</summary>
    public static MethodDeclarationSyntax GetMethodDeclaration(this Compilation compilation, string methodName)
        => compilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Single(method => method.Identifier.Text == methodName);

    /// <summary>The operation of the only <c>switch</c> expression in the method named <paramref name="methodName"/>.</summary>
    public static ISwitchExpressionOperation GetSwitchExpression(this Compilation compilation, string methodName)
    {
        var syntax = compilation.GetMethodDeclaration(methodName).DescendantNodes().OfType<SwitchExpressionSyntax>().Single();
        var operation = compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax) as ISwitchExpressionOperation;
        Assert.IsNotNull(operation, $"No switch expression operation for '{syntax}'.");
        return operation;
    }

    private static CSharpCompilation Compile(string[] sources, string assemblyName, OutputKind outputKind, MetadataReference[] references)
        => CSharpCompilation.Create(
            assemblyName,
            [
                .. sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, ParseOptions, $"Test{index}.cs")),
                CSharpSyntaxTree.ParseText(FpAnalyzerTestHost.AnnotationsSource, ParseOptions, "Fprog.Annotations.cs")
            ],
            [.. Net110.References.All, .. references],
            new CSharpCompilationOptions(outputKind, nullableContextOptions: NullableContextOptions.Enable));

    private static void AssertNoErrors(CSharpCompilation compilation)
    {
        var errors = Errors(compilation);
        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    private static Diagnostic[] Errors(CSharpCompilation compilation)
        => compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
}
