using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0107Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0107_PointerTypesAndWritesThroughPointer_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Core
                           {
                               public static {|FP0107:unsafe|} int Write({|FP0107:int*|} target)
                               {
                                   *target = 42;
                                   target[0] = 42;
                                   return *target;
                               }

                               public static bool Empty()
                               {
                                   {|FP0107:int*|} address = null;
                                   return address == null;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0107_FunctionPointerType_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Core
                           {
                               public static {|FP0107:unsafe|} int Call({|FP0107:delegate* managed<int, int>|} callback)
                                   => callback(1);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0107_AddressOfAndUnsafeExpression_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Core
                           {
                               public static int Read(int value) => {|FP0107:unsafe|}(*{|FP0107:&|}value);

                               public static {|FP0107:unsafe|} int ReadAgain(int value) => *({|FP0107:&|}value);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0107_FixedStatementAndUnsafeBlock_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Core
                           {
                               public static int Change(int[] values)
                               {
                                   {|FP0107:unsafe|} {
                                       {|FP0107:fixed|} ({|FP0107:int*|} first = values)
                                           first[0] = 7;
                                   }

                                   return values.Length;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0107_UnsafeModifiersOnTypeAndDeclarations_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public {|FP0107:unsafe|} sealed class Core
                           {
                               private {|FP0107:unsafe|} readonly {|FP0107:int*|} pointer = null;

                               private {|FP0107:unsafe|} int Identity(int value) => value;

                               private {|FP0107:unsafe|} int Value => Identity(1);

                               private {|FP0107:unsafe|} delegate int Operation(int value);

                               public bool Read()
                               {
                                   {|FP0107:unsafe|} int Number() => Value;
                                   Operation read = Identity;
                                   return Number() == read(2) && pointer == null;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0107_UnionMemberSyntaxReportsOnce_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public union Count(int, string)
                           {
                               public static int Read(int value) => {|FP0107:unsafe|}(*{|FP0107:&|}value);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0107_BitwiseAndLogicalOperatorsAndSafeRef_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static class Core
                           {
                               public static int Read(int[] values, bool left, bool right)
                               {
                                   if (values.Length == 0) return 0;
                                   ref readonly var first = ref values[0];
                                   ReadOnlySpan<int> span = values;
                                   return (left && right ? first : span.Length) & 1;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0107_OutsideCore_NoReport()
    {
        return VerifyAsync("""
                           public static unsafe class Shell
                           {
                               public static int Read(int* value) => *value;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0107_GeneratedMember_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Core
                           {
                               [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
                               public static unsafe int Read(int* value) => *value;
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0107", source, testContext.CancellationToken);
    }
}
