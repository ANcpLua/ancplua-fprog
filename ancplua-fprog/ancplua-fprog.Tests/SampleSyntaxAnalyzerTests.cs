using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    ancplua_fprog.SampleSyntaxAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace ancplua_fprog.Tests;

[TestClass]
public sealed class SampleSyntaxAnalyzerTests
{
    [TestMethod]
    public async Task ClassWithMyCompanyTitle_AlertDiagnostic()
    {
        const string text = @"
public class MyCompanyClass
{
}
";

        var expected = Verifier.Diagnostic()
            .WithLocation(2, 14)
            .WithArguments("MyCompanyClass");
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }
}
