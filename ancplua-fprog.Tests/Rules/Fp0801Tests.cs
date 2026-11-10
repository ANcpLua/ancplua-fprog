using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0801Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0801_StaticMemberAccessOnConsole_Reports() => VerifyAsync("""
        using System;

        [Fprog.FunctionalCore]
        public static class Greeter
        {
            public static string Greet(string name)
            {
                {|FP0801:Console|}.WriteLine(name);
                return name;
            }
        }
        """);

    [TestMethod]
    public Task FP0801_QualifiedNameReportsTypeTokenOnly_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Files
        {
            public static string Read(string path) => System.IO.{|FP0801:File|}.ReadAllText(path);

            public static string ReadGlobal(string path) => global::System.IO.{|FP0801:File|}.ReadAllText(path);
        }
        """);

    [TestMethod]
    public Task FP0801_DeclarationTypes_Reports() => VerifyAsync("""
        using System.IO;

        [Fprog.FunctionalCore]
        public sealed class Holder
        {
            private readonly {|FP0801:Stream|} _stream;

            public Holder({|FP0801:Stream|} stream) => _stream = stream;

            public {|FP0801:TextReader|}? Reader { get; init; }

            public {|FP0801:FileInfo|}[] Files() => [];

            public int Length()
            {
                {|FP0801:Stream|} local = _stream;
                return (int)local.Length;
            }
        }
        """);

    [TestMethod]
    public Task FP0801_ObjectCreation_Reports() => VerifyAsync("""
        using System.IO;

        [Fprog.FunctionalCore]
        public static class Readers
        {
            public static string First(string text)
            {
                var reader = new {|FP0801:StringReader|}(text);
                return reader.ReadLine() ?? "";
            }
        }
        """);

    [TestMethod]
    public Task FP0801_GenericArgumentTypeofCastAndPattern_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.IO;

        [Fprog.FunctionalCore]
        public static class Mixed
        {
            public static System.Type Kind() => typeof({|FP0801:FileInfo|});

            public static IReadOnlyList<{|FP0801:FileInfo|}> None() => [];

            public static object Cast(object value) => ({|FP0801:FileInfo|})value;

            public static object? As(object value) => value as {|FP0801:FileInfo|};

            public static bool Is(object value) => value is {|FP0801:FileInfo|} { Exists: true };

            public static T Default<T>() => default!;

            public static {|FP0801:Stream|} Call() => Default<{|FP0801:Stream|}>();
        }
        """);

    [TestMethod]
    public Task FP0801_BaseListAndConstraint_Reports() => VerifyAsync("""
        using System.IO;

        [Fprog.FunctionalCore]
        public abstract class Wrapped : {|FP0801:TextWriter|}
        {
        }

        [Fprog.FunctionalCore]
        public static class Constrained
        {
            public static T Same<T>(T value) where T : {|FP0801:Stream|} => value;
        }
        """);

    [TestMethod]
    public Task FP0801_AttributeFromForbiddenNamespace_Reports() => VerifyAsync("""
        namespace System.IO.Testing
        {
            public sealed class MarkerAttribute : System.Attribute { }
        }

        [Fprog.FunctionalCore]
        [System.IO.Testing.{|FP0801:Marker|}]
        public sealed class Marked
        {
            [System.IO.Testing.{|FP0801:MarkerAttribute|}]
            public int Value { get; init; }
        }
        """);

    [TestMethod]
    public Task FP0801_EnumMemberAccess_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Modes
        {
            public static int Open() => (int)System.IO.{|FP0801:FileMode|}.Open;
        }
        """);

    [TestMethod]
    public Task FP0801_CatchClauseException_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Guarded
        {
            public static int Run(System.Func<int> work)
            {
                try
                {
                    return work();
                }
                catch (System.IO.{|FP0801:IOException|})
                {
                    return 0;
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0801_NetAndDataNamespacesAndBelow_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Infra
        {
            public static System.Net.{|FP0801:IPAddress|}? Address() => null;

            public static System.Net.Http.{|FP0801:HttpClient|}? Client() => null;

            public static System.Data.{|FP0801:DataTable|}? Table() => null;

            public static System.Data.Common.{|FP0801:DbConnection|}? Connection() => null;
        }
        """);

    [TestMethod]
    public Task FP0801_SubNamespaceOfSystemIO_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Zip
        {
            public static System.IO.Compression.{|FP0801:CompressionLevel|} Level()
                => System.IO.Compression.{|FP0801:CompressionLevel|}.Optimal;
        }
        """);

    [TestMethod]
    public Task FP0801_Process_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Processes
        {
            public static int Id() => System.Diagnostics.{|FP0801:Process|}.GetCurrentProcess().Id;
        }
        """);

    [TestMethod]
    public Task FP0801_NamedThreadingTypes_Reports() => VerifyAsync("""
        using System.Threading;

        [Fprog.FunctionalCore]
        public static class Threads
        {
            public static {|FP0801:Thread|}? T() => null;
            public static bool M(object gate) => {|FP0801:Monitor|}.IsEntered(gate);
            public static {|FP0801:Lock|}? L() => null;
            public static {|FP0801:Mutex|}? Mx() => null;
            public static {|FP0801:Semaphore|}? S() => null;
            public static {|FP0801:SemaphoreSlim|}? Ss() => null;
            public static {|FP0801:Timer|}? Tm() => null;

            public static int Next(int value)
            {
                var copy = value;
                return {|FP0801:Interlocked|}.Increment(ref copy);
            }
        }
        """);

    [TestMethod]
    public Task FP0801_NestedTypeOfLockReportsBothTokens_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Scopes
        {
            public static System.Threading.{|FP0801:Lock|}.{|FP0801:Scope|} Enter(System.Threading.{|FP0801:Lock|} gate)
                => gate.EnterScope();
        }
        """);

    [TestMethod]
    public Task FP0801_LockStatement_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Counter
        {
            private readonly object _gate = new();

            public int Read(int value)
            {
                {|FP0801:lock|} (_gate)
                {
                    return value;
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0801_LockStatementOnLockType_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Gate
        {
            private readonly System.Threading.{|FP0801:Lock|} _gate = new();

            public int Read(int value)
            {
                {|FP0801:lock|} (_gate)
                {
                    return value;
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0801_UsingStaticMemberReportsMemberToken_Reports() => VerifyAsync("""
        using static System.Console;
        using static System.IO.FileMode;

        [Fprog.FunctionalCore]
        public static class Quiet
        {
            public static int Say(string text)
            {
                {|FP0801:WriteLine|}(text);
                return (int){|FP0801:Open|};
            }
        }
        """);

    [TestMethod]
    public Task FP0801_AliasNameToken_Reports() => VerifyAsync("""
        using F = System.IO.File;

        [Fprog.FunctionalCore]
        public static class Aliased
        {
            public static string Read(string path) => {|FP0801:F|}.ReadAllText(path);
        }
        """);

    [TestMethod]
    public Task FP0801_NameofType_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Names
        {
            public static string Of() => nameof(System.{|FP0801:Console|});

            public static string OfMember() => nameof(System.{|FP0801:Console|}.WriteLine);
        }
        """);

    [TestMethod]
    public Task FP0801_MethodGroupAndLambdaAndLocalFunction_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Closures
        {
            public static System.Action<string> Group() => System.{|FP0801:Console|}.WriteLine;

            public static System.Func<string> Lambda() => () => System.{|FP0801:Console|}.ReadLine() ?? "";

            public static string Local()
            {
                return Read();

                static string Read() => System.{|FP0801:Console|}.ReadLine() ?? "";
            }
        }
        """);

    [TestMethod]
    public Task FP0801_PositionalRecordParameterReportedOnce_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Entry(System.IO.{|FP0801:FileInfo|} File);
        """);

    [TestMethod]
    public Task FP0801_InTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public static class Inner
            {
                public static int Read() => System.{|FP0801:Console|}.Read();
            }
        }
        """);

    [TestMethod]
    public Task FP0801_UnderAssemblyLevelCore_Reports() => VerifyAsync("""
        using System.IO;

        [assembly: Fprog.FunctionalCore]

        public static class Anywhere
        {
            public static string Read(string path) => {|FP0801:File|}.ReadAllText(path);

            public static int Locked(object gate, int value)
            {
                {|FP0801:lock|} (gate)
                {
                    return value;
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0801_OutsideCore_NoReport() => VerifyAsync("""
        using System;
        using System.IO;
        using System.Threading;

        public static class Shell
        {
            private static readonly object Gate = new();

            public static string Read(string path)
            {
                lock (Gate)
                {
                    Console.WriteLine(path);
                    var copy = 0;
                    Interlocked.Increment(ref copy);
                    return File.ReadAllText(path);
                }
            }

            public static Thread? Worker() => null;
        }
        """);

    [TestMethod]
    public Task FP0801_UsingDirectivesOnly_NoReport() => VerifyAsync("""
        using System;
        using System.IO;
        using System.Net.Http;
        using System.Threading;
        using static System.Console;
        using F = System.IO.File;

        [Fprog.FunctionalCore]
        public static class Pure
        {
            public static int Twice(int value) => value * 2;
        }
        """);

    [TestMethod]
    public Task FP0801_VarWithForbiddenInferredTypeNoTypeToken_NoReport() => VerifyAsync("""
        public static class Shell
        {
            public static System.IO.Stream Open() => System.IO.Stream.Null;
        }

        [Fprog.FunctionalCore]
        public static class Core
        {
            public static long Length()
            {
                var stream = Shell.Open();
                return stream.Length;
            }
        }
        """);

    [TestMethod]
    public Task FP0801_AllowedLookalikes_NoReport() => VerifyAsync("""
        using System.Threading;
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Allowed
        {
            public static string Build(string a, string b)
            {
                var builder = new System.Text.StringBuilder();
                builder.Append(a).Append(b);
                return builder.ToString();
            }

            public static CancellationToken None() => CancellationToken.None;

            public static Task<int> Done() => Task.FromResult(1);

            public static System.ConsoleColor Color() => System.ConsoleColor.Red;

            public static System.Diagnostics.Stopwatch? Watch() => null;

            public static System.Timers.Timer? OtherTimer() => null;

            public static ThreadLocal<int>? PerThread() => null;
        }
        """);

    [TestMethod]
    public Task FP0801_NamespaceSegmentLookalikes_NoReport() => VerifyAsync("""
        namespace System.Networking
        {
            public sealed class Peer { }
        }

        namespace System.Database
        {
            public sealed class Row { }
        }

        namespace MyApp.System.IO
        {
            public sealed class Disk { }
        }

        namespace MyApp
        {
            public sealed class Console { }

            public sealed class Thread { }

            [Fprog.FunctionalCore]
            public static class Uses
            {
                public static global::System.Networking.Peer? Peer() => null;

                public static global::System.Database.Row? Row() => null;

                public static global::MyApp.System.IO.Disk? Disk() => null;

                public static Console? Own() => null;

                public static Thread? OwnThread() => null;
            }
        }
        """);

    [TestMethod]
    public Task FP0801_DocumentationCref_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Documented
        {
            /// <summary>Unlike <see cref="System.Console.WriteLine()"/>, this is pure.</summary>
            public static int Twice(int value) => value * 2;
        }
        """);

    [TestMethod]
    public Task FP0801_FieldLikeEventSynthesizedInterlocked_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Notifier
        {
            public event System.Action? Changed;

            public bool HasListeners() => Changed is not null;
        }

        [Fprog.FunctionalCore]
        public sealed record Point(int X, int Y);
        """);

    [TestMethod]
    public Task FP0801_GeneratedCode_NoReport() => VerifyAsync("""
        // <auto-generated/>
        [Fprog.FunctionalCore]
        public static class Generated
        {
            public static string Read(string path)
            {
                lock (typeof(Generated))
                {
                    return System.IO.File.ReadAllText(path);
                }
            }
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0801", source, testContext.CancellationToken);
}
