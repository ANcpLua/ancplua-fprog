using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Net10;

[TestClass]
public sealed class SampleAnalyzerTests(TestContext testContext)
{
    [TestMethod]
    public async Task ClassWithMyCompanyTitle_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<SampleSyntaxAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            TestCode = """
                public class {|#0:MyCompanyClass|}
                {
                }
                """
        };

        test.ExpectedDiagnostics.Add(
            CSharpAnalyzerVerifier<SampleSyntaxAnalyzer, DefaultVerifier>.Diagnostic()
                .WithLocation(0)
                .WithArguments("MyCompanyClass"));

        await test.RunAsync(testContext.CancellationToken);
    }

    [TestMethod]
    public async Task ClassWithoutCompanyTitle_ReportsNoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<SampleSyntaxAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            TestCode = """
                public class CommonClass
                {
                }
                """
        };

        await test.RunAsync(testContext.CancellationToken);
    }

    [TestMethod]
    public async Task SetSpeedAboveSpeedOfLight_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<SampleSemanticAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            TestCode = """
                public class Program
                {
                    public void Main()
                    {
                        var spaceship = new Spaceship();
                        spaceship.SetSpeed({|#0:300000000|});
                    }
                }

                public class Spaceship
                {
                    public void SetSpeed(long speed) { }
                }
                """
        };

        test.ExpectedDiagnostics.Add(
            CSharpAnalyzerVerifier<SampleSemanticAnalyzer, DefaultVerifier>.Diagnostic()
                .WithLocation(0)
                .WithArguments("300000000"));

        await test.RunAsync(testContext.CancellationToken);
    }

    [TestMethod]
    public async Task ClassWithMyCompanyTitle_CodeFixRenamesClass()
    {
        var test = new CSharpCodeFixTest<SampleSyntaxAnalyzer, SampleCodeFixProvider, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            TestCode = """
                public class {|#0:MyCompanyClass|}
                {
                }
                """,
            FixedCode = """
                public class CommonClass
                {
                }
                """
        };

        test.ExpectedDiagnostics.Add(
            CSharpCodeFixVerifier<SampleSyntaxAnalyzer, SampleCodeFixProvider, DefaultVerifier>.Diagnostic()
                .WithLocation(0)
                .WithArguments("MyCompanyClass"));

        await test.RunAsync(testContext.CancellationToken);
    }
}
