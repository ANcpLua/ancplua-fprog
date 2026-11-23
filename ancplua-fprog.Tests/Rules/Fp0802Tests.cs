using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0802Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0802_AllFourTaskTypesInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_QualifiedReturnTypeReportsWholeName_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static {|FP0802:System.Threading.Tasks.Task|} Plain() => System.Threading.Tasks.Task.CompletedTask;

                               public static {|FP0802:global::System.Threading.Tasks.ValueTask<int>|} Value() => new(1);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_AliasedTaskTypeComparedBySymbol_Reports()
    {
        return VerifyAsync("""
                           using Job = System.Threading.Tasks.Task<int>;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static {|FP0802:Job|} Run() => System.Threading.Tasks.Task.FromResult(1);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_AsyncMethodInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_LocalFunctionInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_LocalFunctionInsideLambdaInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_InterfaceAndAbstractMethodsInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_OverrideAndExplicitImplementationInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_PartialMethodReportedOnBothDeclarations_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_NullableTaskReportsWholeNullableType_Reports()
    {
        return VerifyAsync("""
                           #nullable enable
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static {|FP0802:Task?|} Maybe() => null;

                               public static {|FP0802:ValueTask<int>?|} MaybeValue() => null;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_RefReturnReportsTypeAfterRef_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public sealed class Work
                           {
                               private readonly Task _pending = Task.CompletedTask;

                               public ref readonly {|FP0802:Task|} Pending() => ref _pending;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_OperatorReturningTaskInCore_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public sealed class Job
                           {
                               public static {|FP0802:Task|} operator +(Job left, Job right) => Task.CompletedTask;

                               public static implicit operator {|FP0802:Task|}(Job job) => Task.CompletedTask;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_ExtensionBlockMethodInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_TaskInTypeNestedInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_TaskUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           [assembly: Fprog.FunctionalCore]

                           public static class Anywhere
                           {
                               public static {|FP0802:ValueTask<int>|} Run() => new(1);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_TaskOutsideCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_LambdasDelegatesAndPropertiesOfTaskTypeInCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_SimilarNonTaskReturnTypesInCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_UserTypeNamedTaskInCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0802_CompilerGeneratedMembersOfRecordWithTaskInCore_NoReport()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public sealed record Job(Task Work, ValueTask<int> Result);

                           [Fprog.FunctionalCore]
                           public readonly record struct Pending(Task<int> Value);
                           """);
    }

    [TestMethod]
    public Task FP0802_TypeDerivedFromTaskInCore_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           public class Job() : Task(static () => { });

                           public sealed class Typed() : Task<int>(static () => 1);

                           public sealed class Special() : Job;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static {|FP0802:Job|} Plain() => new();

                               public static {|FP0802:Typed|} Generic() => new();

                               public static {|FP0802:Special|} Deep() => new();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_TypeParameterConstrainedToTaskInCore_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static {|FP0802:T|} Pick<T>(T first) where T : Task => first;

                               public static {|FP0802:T|} Chain<T, TBase>(T first) where T : TBase where TBase : Task<int> => first;

                               public static int Run()
                               {
                                   _ = Local(Task.CompletedTask);
                                   return 1;

                                   static {|FP0802:TLocal|} Local<TLocal>(TLocal value) where TLocal : Task => value;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_UnconstrainedOrNonTaskTypeParameterInCore_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static T Identity<T>(T value) => value;

                               public static T Some<T>(T value) where T : class, IDisposable => value;

                               public static T Make<T>() where T : struct => default;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_LocalFunctionsInOtherMemberKindsInCore_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public sealed class Work
                           {
                               private static readonly Func<int> Field = () =>
                               {
                                   _ = InField();
                                   return 1;

                                   static {|FP0802:Task|} InField() => Task.CompletedTask;
                               };

                               public Func<int> Property { get; } = () =>
                               {
                                   _ = InProperty();
                                   return 1;

                                   static {|FP0802:ValueTask|} InProperty() => default;
                               };

                               static Work()
                               {
                                   _ = InStaticConstructor();

                                   static {|FP0802:Task|} InStaticConstructor() => Task.CompletedTask;
                               }

                               public Work()
                               {
                                   _ = InConstructor();

                                   static {|FP0802:Task<int>|} InConstructor() => Task.FromResult(1);
                               }

                               ~Work()
                               {
                                   _ = InFinalizer();

                                   static {|FP0802:Task|} InFinalizer() => Task.CompletedTask;
                               }

                               public int Getter
                               {
                                   get
                                   {
                                       _ = InGetter();
                                       return 1;

                                       static {|FP0802:Task|} InGetter() => Task.CompletedTask;
                                   }
                               }

                               public int this[int index]
                               {
                                   get
                                   {
                                       _ = InIndexer();
                                       return index;

                                       static {|FP0802:ValueTask<int>|} InIndexer() => new(1);
                                   }
                               }

                               public event Action Changed
                               {
                                   add
                                   {
                                       _ = InAdd();

                                       static {|FP0802:Task|} InAdd() => Task.CompletedTask;
                                   }
                                   remove { }
                               }

                               public static int operator -(Work left, Work right)
                               {
                                   _ = InOperator();
                                   return 0;

                                   static {|FP0802:Task|} InOperator() => Task.CompletedTask;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_MethodsInCoreRecordsStructsAndGenericTypes_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public sealed record Order(int Id)
                           {
                               public {|FP0802:Task<int>|} Total() => Task.FromResult(Id);
                           }

                           [Fprog.FunctionalCore]
                           public readonly struct Money(decimal amount)
                           {
                               public {|FP0802:ValueTask<decimal>|} Load() => new(amount);
                           }

                           [Fprog.FunctionalCore]
                           public readonly record struct Point(int X)
                           {
                               public static {|FP0802:Task|} Draw() => Task.CompletedTask;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Box<T>(T value)
                           {
                               public {|FP0802:Task<T>|} Get() => Task.FromResult(value);

                               public static {|FP0802:ValueTask<TOut>|} Map<TOut>(TOut output) => new(output);

                               public sealed record Nested
                               {
                                   public {|FP0802:Task<T[]>|} Many() => Task.FromResult(System.Array.Empty<T>());
                               }
                           }

                           [Fprog.FunctionalCore]
                           file sealed class Hidden
                           {
                               public static {|FP0802:Task|} Run() => Task.CompletedTask;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_StaticAbstractInterfaceMembersInCore_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public static class Ports
                           {
                               public interface IFactory<TSelf> where TSelf : IFactory<TSelf>
                               {
                                   static abstract {|FP0802:Task<TSelf>|} Create();

                                   static virtual {|FP0802:ValueTask|} Warm() => default;

                                   static abstract {|FP0802:Task|} operator +(TSelf left, TSelf right);

                                   static abstract implicit operator {|FP0802:ValueTask<int>|}(TSelf value);
                               }

                               public sealed class Impl : IFactory<Impl>
                               {
                                   public static {|FP0802:Task<Impl>|} Create() => Task.FromResult(new Impl());

                                   public static {|FP0802:Task|} operator +(Impl left, Impl right) => Task.CompletedTask;

                                   public static implicit operator {|FP0802:ValueTask<int>|}(Impl value) => new(1);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_ExplicitConversionAndCheckedOperatorsInCore_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public sealed class Job
                           {
                               public static explicit operator {|FP0802:Task<int>|}(Job job) => Task.FromResult(1);

                               public static {|FP0802:Task|} operator checked -(Job left, Job right) => Task.CompletedTask;

                               public static {|FP0802:Task|} operator -(Job left, Job right) => Task.CompletedTask;

                               public static {|FP0802:ValueTask|} operator !(Job job) => default;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_SpanExcludesAttributesModifiersAndTrivia_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public abstract class Work
                           {
                               /// <summary>Documented.</summary>
                               [Obsolete("old")]
                               [return: CLSCompliant(false)]
                               public static /* lead */ {|FP0802:Task|} /* trail */ Documented() => Task.CompletedTask;

                               public static {|FP0802:System.Threading.Tasks
                                   .Task<
                                       /* inner */ int>|} Split() => Task.FromResult(1);

                               public static unsafe {|FP0802:Task<int>|} Unsafe(int* pointer) => Task.FromResult(*pointer);

                               public static extern {|FP0802:ValueTask|} Imported();

                               protected internal abstract {|FP0802:Task<string>|} Text();

                               public new static {|FP0802:Task|} Equals(object left, object right) => Task.CompletedTask;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_NamespaceAliasAndNestedGenericReturnTypes_Reports()
    {
        return VerifyAsync("""
                           using Tasks = System.Threading.Tasks;
                           using MaybeJob = System.Threading.Tasks.ValueTask?;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static {|FP0802:Tasks::Task<int>|} Qualified() => Tasks.Task.FromResult(1);

                               public static {|FP0802:Tasks.ValueTask|} Dotted() => default;

                               public static {|FP0802:MaybeJob|} Maybe() => null;

                               public static {|FP0802:Tasks::Task<Tasks::Task<int>>|} Nested()
                                   => Tasks.Task.FromResult(Tasks.Task.FromResult(1));

                               public static {|FP0802:Tasks::Task<(int Count, string Name)>|} Tuple() => Tasks.Task.FromResult((1, "x"));
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_AwaitablesAndContainersOfTasksInCore_NoReport()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public static unsafe class Work
                           {
                               public static TaskAwaiter<int> Awaiter(Task<int> task) => task.GetAwaiter();

                               public static ConfiguredTaskAwaitable Configured(Task task) => task.ConfigureAwait(false);

                               public static ValueTaskAwaiter<int> ValueAwaiter(ValueTask<int> task) => task.GetAwaiter();

                               public static TaskCompletionSource<int> Source() => new();

                               public static TaskFactory Factory() => Task.Factory;

                               public static (Task First, int Second) Pair(Task first) => (first, 1);

                               public static delegate*<Task> Pointer() => null;

                               public static ref int Slot(int[] values) => ref values[0];
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_DelegatesIndexersAndEventsDeclaredInCore_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public sealed class Work
                           {
                               public delegate Task Job();

                               public delegate ValueTask<int> Query(int id);

                               public Task this[int index] => Task.CompletedTask;

                               public ValueTask<int> Value
                               {
                                   get { return new(1); }
                               }

                               public event Func<Task> Changed = static () => Task.CompletedTask;

                               public int Count() => Changed is null ? 0 : 1;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_OnlyTypesInCoreInMixedNesting_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           public static class Shell
                           {
                               public static Task Run() => Task.CompletedTask;

                               [Fprog.FunctionalCore]
                               public static class Core
                               {
                                   public static {|FP0802:Task|} Run() => Task.CompletedTask;
                               }

                               public static class Sibling
                               {
                                   public static Task<int> Run() => Task.FromResult(1);
                               }
                           }

                           [Fprog.FunctionalCore]
                           public class CoreBase
                           {
                               public virtual int Value() => 1;
                           }

                           public sealed class ShellDerived : CoreBase
                           {
                               public Task Load() => Task.CompletedTask;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_GeneratedCodeInCore_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public static class Generated
                           {
                               public static Task Run() => Task.CompletedTask;

                               public static int Local()
                               {
                                   _ = Later();
                                   return 1;

                                   static ValueTask Later() => default;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_PureCoreCodeAroundTasks_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           using System.Linq;
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public sealed record Step(string Name, int Cost);

                               public static bool Done(Task task) => task.IsCompleted;

                               public static int Total(ImmutableArray<Step> steps)
                               {
                                   var total = 0;
                                   foreach (var step in steps)
                                       total += step.Cost;

                                   return Twice(total);

                                   static int Twice(int value) => value * 2;
                               }

                               public static ImmutableArray<string> Names(ImmutableArray<Step> steps)
                                   => [.. from step in steps where step.Cost > 0 select step.Name];

                               public static string Describe(Step step) => step switch
                               {
                                   { Cost: 0 } => nameof(Task),
                                   _ => $"{step.Name}: {step.Cost}"
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_DeeplyNestedAndGenericLocalFunctionsInCore_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           public static class Work
                           {
                               public static int Run()
                               {
                                   return Outer();

                                   int Outer()
                                   {
                                       Func<Func<int>> make = () => () =>
                                       {
                                           _ = Wrap(1);
                                           return 1;

                                           [Obsolete]
                                           static {|FP0802:Task<T>|} Wrap<T>(T value)
                                           {
                                               return Inner();

                                               {|FP0802:ValueTask<T>|} Never() => new(value);
                                               {|FP0802:Task<T>|} Inner() => Never().AsTask();
                                           }
                                       };

                                       return make()();
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_ExtensionBlockStaticMembersInCore_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           public sealed class Money;

                           [Fprog.FunctionalCore]
                           public static class Extensions
                           {
                               extension(Money)
                               {
                                   public static {|FP0802:Task<Money>|} Zero() => Task.FromResult(new Money());

                                   public static {|FP0802:Task|} operator +(Money left, Money right) => Task.CompletedTask;
                               }

                               extension<T>(T[] items)
                               {
                                   public {|FP0802:ValueTask<int>|} Count() => new(items.Length);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_GeneratedCodeAttributeOnCoreType_NoReport()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           [Fprog.FunctionalCore]
                           [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
                           public static class Generated
                           {
                               public static Task Run() => Task.CompletedTask;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_UnionBodyNestedInCoreClass_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           public record Cat;
                           public record Dog;

                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public union Pet(Cat, Dog)
                               {
                                   public static {|FP0802:Task|} Feed() => Task.CompletedTask;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_TopLevelUnionUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           [assembly: Fprog.FunctionalCore]

                           public record Cat;
                           public record Dog;

                           public union Pet(Cat, Dog)
                           {
                               public {|FP0802:Task<int>|} Count() => Task.FromResult(1);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_ClassNestedInUnionBodyInCore_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           public record Cat;
                           public record Dog;

                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public union Pet(Cat, Dog)
                               {
                                   public static class Helper
                                   {
                                       public static {|FP0802:ValueTask|} Run() => default;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_LocalFunctionsFieldAndOperatorInUnionBodyInCore_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;

                           namespace Zoo
                           {
                               public record Cat;
                               public record Dog;

                               [Fprog.FunctionalCore]
                               public static class Pets
                               {
                                   public union Pet(Cat, Dog)
                                   {
                                       private static readonly Func<int> Make = static () =>
                                       {
                                           _ = InField();
                                           return 1;

                                           static {|FP0802:Task|} InField() => Task.CompletedTask;
                                       };

                                       public static int Describe()
                                       {
                                           _ = Later();
                                           return Make();

                                           static {|FP0802:Task<int>|} Later() => Task.FromResult(1);
                                       }

                                       public static {|FP0802:Task|} operator +(Pet left, Pet right) => Task.CompletedTask;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_CoreClassNestedInShellUnion_Reports()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           public record Cat;
                           public record Dog;

                           public union Shelter(Cat, Dog)
                           {
                               public static Task Open() => Task.CompletedTask;

                               [Fprog.FunctionalCore]
                               public static class Rules
                               {
                                   public static {|FP0802:Task|} Check() => Task.CompletedTask;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_ShellUnionAndNonTaskMembersOfCoreUnion_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;

                           public record Cat;
                           public record Dog;

                           public union Pet(Cat, Dog)
                           {
                               public static Task Feed() => Task.CompletedTask;

                               public static ValueTask<int> Count()
                               {
                                   return Local();

                                   static ValueTask<int> Local() => new(1);
                               }
                           }

                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public union Animal(Cat, Dog)
                               {
                                   public static int Legs() => Local();

                                   public static Func<Task> Factory() => static () => Task.CompletedTask;

                                   private static int Local() => 4;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0802_LocalFunctionInPrimaryConstructorBaseArgumentInCore_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;

                           public class Base(Func<int> make)
                           {
                               public int Value { get; } = make();
                           }

                           public record BaseRecord(Func<int> Make);

                           [Fprog.FunctionalCore]
                           public sealed class Derived() : Base(static () =>
                           {
                               _ = Later();
                               return 1;

                               static {|FP0802:Task|} Later() => Task.CompletedTask;
                           });

                           [Fprog.FunctionalCore]
                           public sealed record DerivedRecord(int Id) : BaseRecord(static () =>
                           {
                               _ = Later();
                               return 1;

                               static {|FP0802:ValueTask<int>|} Later() => new(1);
                           });
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0802", source, testContext.CancellationToken);
    }
}