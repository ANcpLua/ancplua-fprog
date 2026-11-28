using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0701Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0701_LocalAssignedAfterLambda_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static int Run()
                               {
                                   var count = 1;
                                   System.Func<int> read = () => {|FP0701:count|} + count;
                                   count = 2;
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_LocalAssignedBeforeLambda_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static int Run()
                               {
                                   var count = 1;
                                   count = 2;
                                   System.Func<int> read = () => {|FP0701:count|};
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_IncrementInsideLambda_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static System.Func<int> Next()
                               {
                                   var count = 0;
                                   return () => ++{|FP0701:count|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_PostfixDecrementOutside_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static int Run()
                               {
                                   var count = 5;
                                   System.Func<int> read = () => {|FP0701:count|};
                                   count--;
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_CompoundAssignmentAndCoalesceAssignment_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static int Run(string? name)
                               {
                                   var total = 1;
                                   System.Func<int> read = () => {|FP0701:total|} + {|FP0701:name|}!.Length;
                                   total += 3;
                                   name ??= "x";
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_RefAndOutArguments_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static int Run(string text)
                               {
                                   var a = 1;
                                   var b = 2;
                                   System.Func<int> read = () => {|FP0701:a|} + {|FP0701:b|};
                                   Bump(ref a);
                                   int.TryParse(text, out b);
                                   return read();
                               }

                               private static int Bump(ref int value) => value + 1;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_DeconstructionIntoExistingLocals_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pairs
                           {
                               public static int Run()
                               {
                                   var a = 1;
                                   var b = 2;
                                   var c = 3;
                                   System.Func<int> read = () => {|FP0701:a|} + {|FP0701:b|} + c;
                                   (a, (b, _)) = (4, (5, 6));
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_ParameterAssigned_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Clamp
                           {
                               public static System.Func<int> Of(int value)
                               {
                                   if (value < 0)
                                       value = 0;
                                   return () => {|FP0701:value|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_LocalFunctionCapturesMutatedLocal_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   count = 3;
                                   return Twice();

                                   int Twice() => {|FP0701:count|} * 2 + count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_AnonymousMethodDelegate_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   System.Func<int> read = delegate { return {|FP0701:count|}; };
                                   count = 1;
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_ForLoopVariableCapturedInBody_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Loops
                           {
                               public static int Run()
                               {
                                   var sum = 0;
                                   for (var i = 0; i < 3; i++)
                                   {
                                       System.Func<int> read = () => {|FP0701:i|};
                                       sum = sum + read();
                                   }

                                   return sum;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_ForeachVariableNeverAssigned_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Loops
                           {
                               public static System.Collections.Immutable.ImmutableArray<System.Func<int>> Run(System.Collections.Immutable.ImmutableArray<int> items)
                               {
                                   var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<System.Func<int>>();
                                   foreach (var item in items)
                                       builder.Add(() => item);
                                   return builder.ToImmutable();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_LocalWithoutInitializerAssignedOnce_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Late
                           {
                               public static int Run(bool flag)
                               {
                                   int value;
                                   value = flag ? 1 : 2;
                                   System.Func<int> read = () => {|FP0701:value|};
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_NestedClosureReportsOuterAndInnerOnceEach_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Nested
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   System.Func<int> outer = () =>
                                   {
                                       var first = {|FP0701:count|};
                                       System.Func<int> inner = () => {|FP0701:count|} + count;
                                       return first + inner();
                                   };
                                   count = 1;
                                   return outer();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_NestedClosureSameFirstUseReportedOnce_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Nested
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   System.Func<System.Func<int>> outer = () => () => {|FP0701:count|};
                                   count = 1;
                                   return outer()();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_InnerClosureCapturesLocalOfOuterClosure_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Nested
                           {
                               public static System.Func<int> Run() => () =>
                               {
                                   var local = 0;
                                   System.Func<int> inner = () => {|FP0701:local|};
                                   local = 2;
                                   return inner();
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_StructFieldAssignedThroughLocal_Reports()
    {
        return VerifyAsync("""
                           public struct Point
                           {
                               public int X;
                           }

                           [Fprog.FunctionalCore]
                           public static class Points
                           {
                               public static int Run()
                               {
                                   var point = new Point();
                                   System.Func<int> read = () => {|FP0701:point|}.X;
                                   point.X = 3;
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_ClosureInTypeNestedInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public static class Inner
                               {
                                   public static int Run()
                                   {
                                       var count = 0;
                                       count++;
                                       System.Func<int> read = () => {|FP0701:count|};
                                       return read();
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_ClosureUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public static class Anywhere
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   count++;
                                   System.Func<int> read = () => {|FP0701:count|};
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_QueryClauseCapturesMutatedLocal_Reports()
    {
        return VerifyAsync("""
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Queries
                           {
                               public static int Run(int[] items)
                               {
                                   var limit = 1;
                                   limit = 2;
                                   return (from item in items where item > {|FP0701:limit|} select item).Count();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_SameCodeOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           public static class Shell
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   System.Func<int> read = () => count++;
                                   count = 4;
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_CapturedButNeverReassigned_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pure
                           {
                               public static System.Func<int, int> AddTo(int offset)
                               {
                                   var scale = 2;
                                   (int a, int b) = (1, 2);
                                   int.TryParse("3", out var parsed);
                                   return x => (x + offset + a + b + parsed) * scale;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_ClosureMutatesOnlyItsOwnLocalsAndParameters_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pure
                           {
                               public static int Run()
                               {
                                   System.Func<int, int> sum = n =>
                                   {
                                       var total = 0;
                                       for (var i = 0; i < n; i++)
                                           total += i;
                                       n = 0;
                                       return total + n;
                                   };

                                   return Local(3);

                                   int Local(int x)
                                   {
                                       x++;
                                       return sum(x);
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_MutatedLocalNotCaptured_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pure
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   count++;
                                   System.Func<int> read = () => nameof(count).Length;
                                   return read() + count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_StaticLambdaAndStaticLocalFunction_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pure
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   count++;
                                   System.Func<int, int> twice = static x => x * 2;
                                   return twice(count) + Square(count);

                                   static int Square(int x) => x * x;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_CapturedThis_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Holder
                           {
                               private readonly int _value = 1;

                               public System.Func<int> Reader() => () => _value + this._value;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_GeneratedCodeAndRecordMembers_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public sealed record Pair(int Left, int Right);

                               [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
                               public static class Generated
                               {
                                   public static int Run()
                                   {
                                       var count = 0;
                                       count++;
                                       System.Func<int> read = () => count;
                                       return read();
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_ConstructorParameterAssignedInBody_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Box
                           {
                               public Box(int seed)
                               {
                                   seed++;
                                   Read = () => {|FP0701:seed|};
                               }

                               public System.Func<int> Read { get; }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_GetterIndexerAndOperatorInRecordStruct_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public readonly record struct Money(int Cents)
                           {
                               public int Doubled
                               {
                                   get
                                   {
                                       var total = Cents;
                                       total += Cents;
                                       System.Func<int> read = () => {|FP0701:total|};
                                       return read();
                                   }
                               }

                               public int this[int index]
                               {
                                   get
                                   {
                                       index--;
                                       System.Func<int> read = () => {|FP0701:index|};
                                       return read();
                                   }
                               }

                               public static Money operator +(Money left, Money right)
                               {
                                   var sum = left.Cents;
                                   sum = sum + right.Cents;
                                   return new Money(Local());

                                   int Local() => {|FP0701:sum|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_FieldInitializerLambdaInGenericType_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Holder<T>
                           {
                               private static readonly System.Func<T?, int> Count = item =>
                               {
                                   var seen = 0;
                                   System.Func<int> read = () => {|FP0701:seen|};
                                   if (item is not null)
                                       seen++;
                                   return read();
                               };

                               public static T Pick<TOther>(T first, T second, TOther other)
                               {
                                   first = second;
                                   return Local();

                                   T Local() => other is null ? {|FP0701:first|} : first;
                               }

                               public int Run(T? item) => Count(item);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_InterfaceDefaultMethodAsyncLambdaAndIterator_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public interface IScaler
                               {
                                   int Factor { get; }

                                   System.Func<System.Threading.Tasks.Task<int>> Scaled(int value)
                                   {
                                       value *= Factor;
                                       return async () =>
                                       {
                                           await System.Threading.Tasks.Task.Yield();
                                           return {|FP0701:value|};
                                       };
                                   }
                               }

                               public static System.Collections.Generic.IEnumerable<int> Numbers(int start)
                               {
                                   var current = start;
                                   while (current < 10)
                                   {
                                       yield return Next();
                                       current += 3;
                                   }

                                   int Next() => {|FP0701:current|} * 2;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_PartialTypeAndPartialMethodImplementation_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static partial class Split
                           {
                               public static partial int Run(int value);
                           }

                           public static partial class Split
                           {
                               public static partial int Run(int value)
                               {
                                   value <<= 1;
                                   System.Func<int> read = () => {|FP0701:value|};
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_ExtensionBlockReceiverAssigned_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class IntExtensions
                           {
                               extension(int value)
                               {
                                   public System.Func<int> Clamped()
                                   {
                                       if (value < 0)
                                           value = 0;
                                       return () => {|FP0701:value|};
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_PrimaryConstructorParameterAssignedInOtherMember_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Counter(int start)
                           {
                               public int Reset() => start = 0;

                               public System.Func<int> Reader() => () => {|FP0701:start|} + start;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_PrimaryConstructorParameterInBaseInitializerLambda_Reports()
    {
        return VerifyAsync("""
                           public class Base(System.Func<int> read)
                           {
                               public int Value => read();
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Derived(int seed) : Base(() => {|FP0701:seed|})
                           {
                               public int Next { get; } = seed++;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_PrimaryConstructorParameterNeverAssigned_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Scale(int factor)
                           {
                               public System.Func<int, int> By() => x => x * factor;

                               public int Factor { get; } = factor;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_WritableRefLocalAliasCountsAsWrite_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Aliases
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   var other = 1;
                                   var third = 2;
                                   ref var alias = ref count;
                                   alias = 5;
                                   ref var rebound = ref alias;
                                   rebound = ref third;
                                   ref readonly var view = ref other;
                                   System.Func<int> read = () => {|FP0701:count|} + other + {|FP0701:third|};
                                   return read() + view;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_RefConditionalAsTargetAndArgument_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Choice
                           {
                               public static int Run(bool flag)
                               {
                                   var a = 1;
                                   var b = 2;
                                   var c = 3;
                                   var d = 4;
                                   (flag ? ref a : ref b) = 3;
                                   Bump(ref flag ? ref c : ref d);
                                   System.Func<int> read = () => {|FP0701:a|} + {|FP0701:b|} + {|FP0701:c|} + {|FP0701:d|};
                                   return read();
                               }

                               private static int Bump(ref int value) => value + 1;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_MixedDeconstructionDeclaresOneAndWritesOther_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pairs
                           {
                               public static int Run()
                               {
                                   var left = 0;
                                   (var fresh, left) = (1, 2);
                                   System.Func<int> read = () => {|FP0701:left|} + fresh;
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_RefThisExtensionMethodReceiver_Reports()
    {
        return VerifyAsync("""
                           public static class Bumps
                           {
                               public static int Bump(this ref int value) => ++value;
                           }

                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   count.Bump();
                                   System.Func<int> read = () => {|FP0701:count|};
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    [DataRow("count.Advance();")]
    [DataRow("_ = count.Next;")]
    [DataRow("count.Next = 3;")]
    [DataRow("_ = count[1];")]
    [DataRow("count[1] = 3;")]
    public Task FP0701_RefExtensionDeclarationReceiver_Reports(string use)
    {
        return FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0701", $$"""
                [Fprog.FunctionalCore]
                public static class Counter
                {
                    public static int Run(int count)
                    {
                        System.Func<int> read = () => {|FP0701:count|};
                        {{use}}
                        return read();
                    }
                }
                """, """
                public static class CounterExtensions
                {
                    extension(ref int count)
                    {
                        public int Advance() => ++count;
                        public int Next { get => ++count; set => count = value; }
                        public int this[int step] { get => count += step; set => count = step + value; }
                    }
                }
                """, testContext.CancellationToken);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("in ")]
    [DataRow("ref readonly ")]
    public Task FP0701_ExtensionDeclarationWithoutWritableReceiver_NoReport(string modifier)
    {
        return VerifyAsync($$"""
                             public static class WindowExtensions
                             {
                                 extension({{modifier}}System.ArraySegment<int> window)
                                 {
                                     public int Size() => window.Count;
                                     public int First { set => window[0] = value; }
                                     public int this[bool last] { set => window[last ? window.Count - 1 : 0] = value; }
                                 }
                             }

                             [Fprog.FunctionalCore]
                             public static class Windows
                             {
                                 public static int Fill(System.ArraySegment<int> window)
                                 {
                                     System.Func<int> size = () => window.Count;
                                     if (window.Size() > 0)
                                     {
                                         window.First = 1;
                                         window[true] = 2;
                                     }
                                     return size();
                                 }
                             }
                             """);
    }

    [TestMethod]
    public Task FP0701_RefExtensionDeclarationGetterInNameof_NoReport()
    {
        return VerifyAsync("""
                           public static class CounterExtensions
                           {
                               extension(ref int count)
                               {
                                   public int Next => ++count;
                               }
                           }

                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static System.Func<int> Watch(int count)
                               {
                                   _ = nameof(count.Next.CompareTo);
                                   return () => count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_NestedStructMemberTupleElementAndOutField_Reports()
    {
        return VerifyAsync("""
                           public struct Inner
                           {
                               public int X;

                               public int Y { get; set; }
                           }

                           public struct Wrapper
                           {
                               public Inner Inner;
                           }

                           [Fprog.FunctionalCore]
                           public static class Structs
                           {
                               public static int Run()
                               {
                                   var wrapper = new Wrapper();
                                   var pair = (1, 2);
                                   var other = new Wrapper();
                                   var cell = new Inner();
                                   wrapper.Inner.X = 1;
                                   pair.Item1 = 3;
                                   int.TryParse("4", out other.Inner.X);
                                   cell.Y++;
                                   System.Func<int> read = () => {|FP0701:wrapper|}.Inner.X + {|FP0701:pair|}.Item2 + {|FP0701:other|}.Inner.X + {|FP0701:cell|}.Y;
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_InlineArrayElementWrite_Reports()
    {
        return VerifyAsync("""
                           [System.Runtime.CompilerServices.InlineArray(2)]
                           public struct Buffer
                           {
                               private int _element;
                           }

                           [Fprog.FunctionalCore]
                           public static class Buffers
                           {
                               public static int Run()
                               {
                                   var buffer = new Buffer();
                                   buffer[0] = 1;
                                   System.Func<int> read = () => {|FP0701:buffer|}[1];
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_WriteInSiblingClosure_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Counter
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   System.Func<int> reset = () => {|FP0701:count|} = 0;
                                   System.Func<int> read = () => {|FP0701:count|};
                                   return reset() + read() + Bump();

                                   int Bump() => {|FP0701:count|} += 2;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_VerbatimIdentifierWithCommentsAndLineBreaks_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Spans
                           {
                               public static int Run()
                               {
                                   var @count = 1;
                                   @count++;
                                   System.Func<int> read = () =>
                                       /* leading */
                                       {|FP0701:@count|} // trailing
                                       +
                                       count;
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_FirstUseInsideCastConditionalAccessPatternAndInterpolation_Reports()
    {
        return VerifyAsync("""
                           public sealed record Point(int X);

                           [Fprog.FunctionalCore]
                           public static class Spans
                           {
                               public static string Run(string? text)
                               {
                                   var count = 1;
                                   var label = "a";
                                   var limit = 2;
                                   var named = 3;
                                   text = text?.Trim();
                                   count++;
                                   label += "b";
                                   limit--;
                                   named *= 2;
                                   System.Func<string> read = () =>
                                       $"{{|FP0701:label|}}{(long){|FP0701:count|}}{{|FP0701:text|}?.Length}{{|FP0701:limit|} is > 0}"
                                       + new { {|FP0701:named|} }.named
                                       + new Point(named) { X = limit }
                                       + new[] { count, limit };
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_NameofBeforeRealUse_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Names
                           {
                               public static string Run()
                               {
                                   var count = 0;
                                   count++;
                                   System.Func<string> read = () => nameof(count) + {|FP0701:count|};
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_QueryCaptureVersusJoinInnerSequence_Reports()
    {
        return VerifyAsync("""
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Queries
                           {
                               public static int Run(int[] items)
                               {
                                   var others = new[] { 1 };
                                   others = [2];
                                   var key = 0;
                                   key = 1;
                                   var joined = from a in items join b in others on a equals b + {|FP0701:key|} select a;
                                   var crossed = from a in items from b in {|FP0701:others|} select a + b;
                                   return joined.Count() + crossed.Count();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_LoopBodyDeclarationsAndDeconstructedForeach_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public static class Loops
                           {
                               public static ImmutableArray<Func<int>> Run(ImmutableArray<(int Left, int Right)> pairs)
                               {
                                   var builder = ImmutableArray.CreateBuilder<Func<int>>();
                                   foreach (var (left, right) in pairs)
                                   {
                                       var sum = left + right;
                                       builder.Add(() => sum + left + right);
                                   }

                                   var index = 0;
                                   while (index < pairs.Length)
                                   {
                                       var current = pairs[index];
                                       builder.Add(() => current.Left);
                                       index++;
                                   }

                                   return builder.ToImmutable();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_InArgumentsAndRefReadonlyParameter_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Reads
                           {
                               public static int Run()
                               {
                                   var count = 1;
                                   var other = 2;
                                   var total = Read(in count) + Peek(ref other) + Peek(in other);
                                   System.Func<int> read = () => count + other + total;
                                   return read();
                               }

                               private static int Read(in int value) => value;

                               private static int Peek(ref readonly int value) => value;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_LambdaParameterShadowsMutatedLocal_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Shadow
                           {
                               public static int Run()
                               {
                                   var x = 0;
                                   x++;
                                   System.Func<int, int> f = x => x + 1;
                                   return f(x) + Local(2);

                                   static int Local(int x) => x * 2;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_ReadonlyStructIndexerSetterDoesNotWriteVariable_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Segments
                           {
                               public static int Run()
                               {
                                   var segment = new System.ArraySegment<int>(new int[2]);
                                   segment[0] = 1;
                                   System.Func<int> read = () => segment.Count;
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_FunctionalPipelineWithLocalAccumulator_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Orders
                           {
                               public sealed record Line(string Sku, int Quantity, decimal Price);

                               public static decimal Total(ImmutableArray<Line> lines, decimal discount)
                               {
                                   var subtotal = 0m;
                                   foreach (var line in lines)
                                       subtotal += line.Quantity * line.Price;

                                   var factor = 1 - discount;
                                   var expensive = lines
                                       .Where(line => line.Price * factor > 10m)
                                       .Select(line => line with { Price = line.Price * factor })
                                       .ToImmutableArray();
                                   return expensive.Aggregate(subtotal, (sum, line) => sum switch
                                   {
                                       > 1000m => sum,
                                       var other when line.Quantity > 0 => other + line.Price,
                                       _ => sum
                                   });
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_UnusualMemberShapesDoNotCrash_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public abstract unsafe partial class Odd
                           {
                               protected Odd()
                               {
                               }

                               ~Odd()
                               {
                               }

                               public abstract int Abstract();

                               [System.Runtime.InteropServices.DllImport("native")]
                               public static extern int Extern();

                               partial void Hook();

                               public static int Pointer(int value)
                               {
                                   var p = &value;
                                   *p = 2;
                                   return value;
                               }

                               public static System.Func<int, System.Func<int, System.Func<int, int>>> Deep(int a)
                                   => b => c => d => a + b + c + d;

                               public static System.Func<int> NameOnly()
                               {
                                   var count = 0;
                                   count++;
                                   return static () => nameof(count).Length;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_PrimaryConstructorInNestedPartialAndGenericTypes_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed partial class Outer<T>(T seed)
                           {
                               public System.Func<T> Reader() => () => {|FP0701:seed|};

                               public sealed class Inner(int start)
                               {
                                   public System.Func<int> Next()
                                   {
                                       start++;
                                       return () => {|FP0701:start|};
                                   }
                               }
                           }

                           public sealed partial class Outer<T>
                           {
                               public T Replace(T value) => seed = value;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_RecordParameterCapturedInInitializerLambda_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Settings(int Limit)
                           {
                               public int Doubled { get; } = Limit *= 2;

                               public System.Func<int> Reader { get; } = () => {|FP0701:Limit|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_PrimaryConstructorOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           public sealed class Shell(int start)
                           {
                               public int Reset() => start = 0;

                               public System.Func<int> Reader() => () => start;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Core(int start)
                           {
                               public System.Func<int> Reader() => () => start;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_AutoGeneratedFile_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           [Fprog.FunctionalCore]
                           public static class Generated
                           {
                               public static int Run()
                               {
                                   var count = 0;
                                   count++;
                                   System.Func<int> read = () => count;
                                   return read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0701_VeryLongExpressionInClosure_Reports()
    {
        return VerifyAsync($$"""
                             [Fprog.FunctionalCore]
                             public static class Long
                             {
                                 public static int Run()
                                 {
                                     var count = 0;
                                     count++;
                                     System.Func<int> read = () => {|FP0701:count|}{{string.Concat(Enumerable.Repeat(" + count", 3000))}};
                                     return read();
                                 }
                             }
                             """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0701", source, testContext.CancellationToken);
    }
}