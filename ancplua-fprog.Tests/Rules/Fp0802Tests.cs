using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0802Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0802_AllFourTaskTypesInCore_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Work
        {
            public static {|FP0802:Task|} Plain() => Task.CompletedTask;

            public static {|FP0802:Task<int>|} Generic() => Task.FromResult(1);

            public static {|FP0802:ValueTask|} PlainValue() => default;

            public static {|FP0802:ValueTask<string>|} GenericValue() => new("x");
        }
        """);

    [TestMethod]
    public Task FP0802_QualifiedReturnTypeReportsWholeName_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Work
        {
            public static {|FP0802:System.Threading.Tasks.Task|} Plain() => System.Threading.Tasks.Task.CompletedTask;

            public static {|FP0802:global::System.Threading.Tasks.ValueTask<int>|} Value() => new(1);
        }
        """);

    [TestMethod]
    public Task FP0802_AliasedTaskTypeComparedBySymbol_Reports() => VerifyAsync("""
        using Job = System.Threading.Tasks.Task<int>;

        [Fprog.FunctionalCore]
        public static class Work
        {
            public static {|FP0802:Job|} Run() => System.Threading.Tasks.Task.FromResult(1);
        }
        """);

    [TestMethod]
    public Task FP0802_AsyncMethodInCore_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public sealed class Work
        {
            public async {|FP0802:Task<int>|} RunAsync()
            {
                await Task.Yield();
                return 1;
            }
        }
        """);

    [TestMethod]
    public Task FP0802_LocalFunctionInCore_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Work
        {
            public static int Run()
            {
                _ = Later();
                _ = Value();
                return 1;

                static {|FP0802:Task|} Later() => Task.CompletedTask;
                async {|FP0802:ValueTask<int>|} Value()
                {
                    await Task.Yield();
                    return 2;
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0802_LocalFunctionInsideLambdaInCore_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Work
        {
            public static System.Func<int> Make() => () =>
            {
                _ = Inner();
                return 1;

                static {|FP0802:Task<int>|} Inner() => Task.FromResult(1);
            };
        }
        """);

    [TestMethod]
    public Task FP0802_InterfaceAndAbstractMethodsInCore_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Ports
        {
            public interface IStore
            {
                {|FP0802:Task<int>|} Load();

                static {|FP0802:Task|} Empty() => Task.CompletedTask;
            }
        }

        [Fprog.FunctionalCore]
        public abstract class Store
        {
            public abstract {|FP0802:ValueTask|} Save();

            protected virtual {|FP0802:Task|} Flush() => Task.CompletedTask;
        }
        """);

    [TestMethod]
    public Task FP0802_OverrideAndExplicitImplementationInCore_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        public interface ILoader
        {
            Task<int> Load();
        }

        public abstract class Saver
        {
            public abstract Task Save();
        }

        [Fprog.FunctionalCore]
        public sealed class Store : Saver, ILoader
        {
            {|FP0802:Task<int>|} ILoader.Load() => Task.FromResult(1);

            public override {|FP0802:Task|} Save() => Task.CompletedTask;
        }
        """);

    [TestMethod]
    public Task FP0802_PartialMethodReportedOnBothDeclarations_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public sealed partial class Work
        {
            public partial {|FP0802:Task<int>|} Run();
        }

        public sealed partial class Work
        {
            public partial {|FP0802:Task<int>|} Run() => Task.FromResult(1);
        }
        """);

    [TestMethod]
    public Task FP0802_NullableTaskReportsWholeNullableType_Reports() => VerifyAsync("""
        #nullable enable
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Work
        {
            public static {|FP0802:Task?|} Maybe() => null;

            public static {|FP0802:ValueTask<int>?|} MaybeValue() => null;
        }
        """);

    [TestMethod]
    public Task FP0802_RefReturnReportsTypeAfterRef_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public sealed class Work
        {
            private readonly Task _pending = Task.CompletedTask;

            public ref readonly {|FP0802:Task|} Pending() => ref _pending;
        }
        """);

    [TestMethod]
    public Task FP0802_OperatorReturningTaskInCore_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public sealed class Job
        {
            public static {|FP0802:Task|} operator +(Job left, Job right) => Task.CompletedTask;

            public static implicit operator {|FP0802:Task|}(Job job) => Task.CompletedTask;
        }
        """);

    [TestMethod]
    public Task FP0802_ExtensionBlockMethodInCore_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Extensions
        {
            extension(int value)
            {
                public {|FP0802:Task<int>|} Later() => Task.FromResult(value);
            }

            public static {|FP0802:Task<int>|} Classic(this string text) => Task.FromResult(text.Length);
        }
        """);

    [TestMethod]
    public Task FP0802_TaskInTypeNestedInCore_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Outer
        {
            public static class Inner
            {
                public static {|FP0802:Task|} Run() => Task.CompletedTask;
            }
        }
        """);

    [TestMethod]
    public Task FP0802_TaskUnderAssemblyLevelCore_Reports() => VerifyAsync("""
        using System.Threading.Tasks;

        [assembly: Fprog.FunctionalCore]

        public static class Anywhere
        {
            public static {|FP0802:ValueTask<int>|} Run() => new(1);
        }
        """);

    [TestMethod]
    public Task FP0802_TaskOutsideCore_NoReport() => VerifyAsync("""
        using System.Threading.Tasks;

        public static class Shell
        {
            public static Task Plain() => Task.CompletedTask;

            public static async Task<int> RunAsync()
            {
                await Task.Yield();
                return Local();

                static int Local() => 1;
            }

            public static ValueTask Value()
            {
                return Later();

                static ValueTask Later() => default;
            }
        }
        """);

    [TestMethod]
    public Task FP0802_LambdasDelegatesAndPropertiesOfTaskTypeInCore_NoReport() => VerifyAsync("""
        using System;
        using System.Threading.Tasks;

        public delegate Task Job();

        [Fprog.FunctionalCore]
        public sealed class Work
        {
            public Task Pending { get; } = Task.CompletedTask;

            public ValueTask<int> Computed => new(1);

            public Func<Task> Lambda() => () => Task.CompletedTask;

            public Func<Task<int>> AsyncLambda() => async () =>
            {
                await Task.Yield();
                return 1;
            };

            public Job Delegate() => static () => Task.CompletedTask;
        }
        """);

    [TestMethod]
    public Task FP0802_SimilarNonTaskReturnTypesInCore_NoReport() => VerifyAsync("""
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Work
        {
            public static Task[] Many() => [];

            public static IEnumerable<Task<int>> Sequence() => [];

            public static Func<Task> Factory() => static () => Task.CompletedTask;

            public static TaskStatus Status() => TaskStatus.Created;

            public static Lazy<ValueTask> Deferred() => new(static () => default(ValueTask));

            public static IAsyncEnumerable<int> Stream() => Empty();

            public static int Pure(Task ignored) => 1;

            private static async IAsyncEnumerable<int> Empty()
            {
                await Task.Yield();
                yield break;
            }
        }
        """);

    [TestMethod]
    public Task FP0802_UserTypeNamedTaskInCore_NoReport() => VerifyAsync("""
        namespace Domain
        {
            public sealed record Task(string Title);

            public readonly record struct ValueTask(int Id);

            [Fprog.FunctionalCore]
            public static class Planner
            {
                public static Task Plan(string title) => new(title);

                public static ValueTask Next(int id) => new(id + 1);
            }
        }
        """);

    [TestMethod]
    public Task FP0802_CompilerGeneratedMembersOfRecordWithTaskInCore_NoReport() => VerifyAsync("""
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public sealed record Job(Task Work, ValueTask<int> Result);

        [Fprog.FunctionalCore]
        public readonly record struct Pending(Task<int> Value);
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0802", source, testContext.CancellationToken);
}
