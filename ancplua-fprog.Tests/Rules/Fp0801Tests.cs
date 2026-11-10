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
    public Task FP0801_NestedTypeOfLockReportsOnlyLockToken_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Scopes
        {
            public static System.Threading.{|FP0801:Lock|}.Scope Enter(System.Threading.{|FP0801:Lock|} gate)
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
    public Task FP0801_UsingStaticMethodAndEnumConstant_NoReport() => VerifyAsync("""
        using static System.Console;
        using static System.IO.FileMode;

        [Fprog.FunctionalCore]
        public static class Quiet
        {
            public static int Say(string text)
            {
                WriteLine(text);
                return (int)Open;
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

    [TestMethod]
    public Task FP0801_FieldWithSeveralDeclaratorsReportsSharedTypeOnce_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Pair
        {
            private readonly System.IO.{|FP0801:Stream|} _first = System.IO.{|FP0801:Stream|}.Null, _second = System.IO.{|FP0801:Stream|}.Null;

            public bool Same() => ReferenceEquals(_first, _second);
        }
        """);

    [TestMethod]
    public Task FP0801_PatternsForeachAndQuery_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.IO;
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Shapes
        {
            public static int Arm(object value) => value switch
            {
                {|FP0801:FileInfo|} => 1,
                {|FP0801:Stream|} { CanRead: true } s => (int)s.Length,
                not {|FP0801:TextReader|} => 2,
                _ => 0
            };

            public static int Case(object value)
            {
                switch (value)
                {
                    case {|FP0801:Stream|} s when s.CanSeek:
                        return 1;
                    case {|FP0801:FileInfo|}:
                        return 2;
                    default:
                        return 0;
                }
            }

            public static int Loop(IEnumerable<object> items)
            {
                var count = 0;
                foreach ({|FP0801:FileInfo|} file in items)
                    count += file.Name.Length;
                return count;
            }

            public static IEnumerable<string> Query(IEnumerable<object> items)
                => from {|FP0801:FileInfo|} file in items
                   where file.Exists
                   select file.Name;

            public static bool Declared(object value) => value is {|FP0801:Stream|} stream && stream.CanRead;
        }
        """);

    [TestMethod]
    public Task FP0801_ConstructorsAccessorsOperatorsAndInitializers_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Members
        {
            private readonly int _separator = System.IO.{|FP0801:Path|}.DirectorySeparatorChar;

            public Members() : this(System.IO.{|FP0801:Path|}.PathSeparator)
            {
            }

            private Members(char separator) => _separator = separator + System.IO.{|FP0801:Path|}.AltDirectorySeparatorChar;

            ~Members() => System.{|FP0801:Console|}.Beep();

            public bool Exists { get; } = System.IO.{|FP0801:File|}.Exists("a");

            public string Name
            {
                get { return System.IO.{|FP0801:Path|}.GetFileName("a/b"); }
                init { _ = System.IO.{|FP0801:Path|}.GetFileName(value); }
            }

            public char this[int index] => System.IO.{|FP0801:Path|}.GetInvalidPathChars()[index];

            public static Members operator +(Members left, Members right) => System.IO.{|FP0801:File|}.Exists("b") ? left : right;

            public static explicit operator int(Members value) => value._separator + System.{|FP0801:Console|}.Read();

            public event System.Action? Changed
            {
                add => System.{|FP0801:Console|}.WriteLine(value);
                remove => System.{|FP0801:Console|}.WriteLine(value);
            }
        }
        """);

    [TestMethod]
    public Task FP0801_DefaultValuesAndAttributeArguments_Reports() => VerifyAsync("""
        namespace Shell
        {
            [System.AttributeUsage(System.AttributeTargets.All)]
            public sealed class TagAttribute(System.Type type) : System.Attribute
            {
                public System.Type Type { get; } = type;
            }
        }

        namespace Core
        {
            [Fprog.FunctionalCore]
            [Shell.Tag(typeof(System.IO.{|FP0801:Stream|}))]
            public static class Defaults
            {
                public static int Mode(int mode = (int)System.IO.{|FP0801:FileMode|}.Open) => mode;

                [Shell.Tag(typeof(System.IO.{|FP0801:File|}))]
                [return: Shell.Tag(typeof(System.{|FP0801:Console|}))]
                public static int Tagged([Shell.Tag(typeof(System.Threading.{|FP0801:Thread|}))] int value) => value;

                [System.Obsolete(nameof(System.{|FP0801:Console|}))]
                public static int Old() => 0;

                public static System.Func<int, int> Lambda()
                    => ([Shell.Tag(typeof(System.Threading.{|FP0801:Monitor|}))] int x) => x;

                public static int Local()
                {
                    return Inner();

                    [Shell.Tag(typeof(System.Threading.{|FP0801:Mutex|}))]
                    static int Inner(int value = (int)System.IO.{|FP0801:SeekOrigin|}.End) => value;
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0801_SpanIsNameTokenInOddSyntax_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        public sealed class Box
        {
            public int Value { get; init; }
        }

        [Fprog.FunctionalCore]
        public static class Spans
        {
            public static string Interpolated() => $"{System.IO.{|FP0801:Path|}.PathSeparator}";

            public static List<char> Collection() => new() { System.IO.{|FP0801:Path|}.PathSeparator };

            public static Box Initialized() => new() { Value = System.IO.{|FP0801:Path|}.PathSeparator };

            public static (System.IO.{|FP0801:Stream|}? Source, int Count) Tuple() => (null, 0);

            public static string? Conditional() => System.{|FP0801:Console|}.In?.ReadLine();

            public static object Parenthesized() => (System.{|FP0801:Console|}.Out);

            public static object? Cast(object value) => (System.IO.{|FP0801:Stream|}?)value;

            public static int Verbatim() => System.{|FP0801:@Console|}.Read();

            public static bool Commented() => System.IO. /* the shell */ {|FP0801:File|}.Exists("a");

            public static bool Broken() => System
                .IO
                .{|FP0801:File|}
                .Exists("a");

            public static System.IO.{|FP0801:Stream|}?[] Array() => [];

            public static System.IO.Enumeration.{|FP0801:FileSystemEnumerable|}<int>? Generic() => null;

            public static System.Type Unbound() => typeof(System.IO.Enumeration.{|FP0801:FileSystemEnumerable|}<>);
        }
        """);

    [TestMethod]
    public Task FP0801_RecordStructInterfaceGenericEnumAndDelegate_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public readonly record struct Handle(int Id)
        {
            public bool Exists => System.IO.{|FP0801:File|}.Exists(Id.ToString());
        }

        [Fprog.FunctionalCore]
        public static class Kinds
        {
            public interface IOpener
            {
                System.IO.{|FP0801:Stream|} Open();

                bool Ready() => System.IO.{|FP0801:File|}.Exists("ready");
            }

            public sealed class Holder<T> where T : System.IO.{|FP0801:Stream|}
            {
                public System.Collections.Generic.IReadOnlyList<System.IO.{|FP0801:Stream|}> Items { get; init; } = [];
            }

            public enum Mode
            {
                Open = (int)System.IO.{|FP0801:FileMode|}.Open
            }

            public delegate System.IO.{|FP0801:Stream|} Factory(System.Threading.{|FP0801:Timer|} timer);
        }
        """);

    [TestMethod]
    public Task FP0801_PrimaryConstructorAndPartialPartWithoutAttribute_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed partial class Reader(System.IO.{|FP0801:TextReader|} source)
        {
            public string? Line() => source.ReadLine();
        }

        public sealed partial class Reader
        {
            public static string Read() => System.{|FP0801:Console|}.ReadLine() ?? "";
        }
        """);

    [TestMethod]
    public Task FP0801_ExtensionBlock_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class StreamExtensions
        {
            extension(System.IO.{|FP0801:Stream|} stream)
            {
                public bool IsEmpty => stream.Length == 0;

                public static System.IO.{|FP0801:Stream|} Empty => System.IO.{|FP0801:Stream|}.Null;
            }
        }
        """);

    [TestMethod]
    public Task FP0801_AsyncIteratorUsingAndNestedLocks_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public sealed class Effects
        {
            private readonly object _gate = new();

            private readonly System.Func<int, int> _locked;

            public Effects() => _locked = value =>
            {
                {|FP0801:lock|} (_gate)
                {
                    return value;
                }
            };

            public async Task<string> ReadAsync(string path) => await System.IO.{|FP0801:File|}.ReadAllTextAsync(path);

            public static IEnumerable<int> Keys()
            {
                yield return System.{|FP0801:Console|}.Read();
            }

            public static string? First(string text)
            {
                using (var reader = new System.IO.{|FP0801:StringReader|}(text))
                {
                    return reader.ReadLine();
                }
            }

            public int Count
            {
                get
                {
                    {|FP0801:lock|} (_gate)
                    {
                        return Inner();
                    }

                    int Inner()
                    {
                        {|FP0801:lock|} (this)
                        {
                            return _locked(1);
                        }
                    }
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0801_NamespaceAndTypeAliases_Reports() => VerifyAsync("""
        using IO = System.IO;
        using L = System.Threading.Lock;

        [Fprog.FunctionalCore]
        public static class Aliases
        {
            public static bool Dotted() => IO.{|FP0801:File|}.Exists("a");

            public static bool Qualified() => IO::{|FP0801:File|}.Exists("a");

            public static {|FP0801:L|}.Scope Enter({|FP0801:L|} gate) => gate.EnterScope();
        }
        """);

    [TestMethod]
    public Task FP0801_UsingStaticMethodGroupNameofAndDynamicCall_NoReport() => VerifyAsync("""
        using static System.Console;

        [Fprog.FunctionalCore]
        public static class Imported
        {
            public static System.Action<string> Group() => WriteLine;

            public static string Name() => nameof(WriteLine);

            public static int Dynamic(dynamic value)
            {
                WriteLine(value);
                return 0;
            }
        }
        """);

    [TestMethod]
    public Task FP0801_NameofAndDynamicCallOfOwnStaticMembers_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Own
        {
            public static int Twice(int value) => value * 2;

            public static int Twice(long value) => (int)value * 2;

            public static string Name() => nameof(Twice);

            public static int Dynamic(dynamic value) => Twice(value);
        }
        """);

    [TestMethod]
    public Task FP0801_StaticMemberInheritedFromForbiddenBaseUnqualifiedReportsOnlyTypeTokens_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public abstract class Wrapped : System.IO.{|FP0801:TextWriter|}
        {
            public static System.IO.{|FP0801:TextWriter|} Quiet() => Null;
        }
        """);

    [TestMethod]
    public Task FP0801_CoreNestedInShellReportsOnlyInner_Reports() => VerifyAsync("""
        public static class Shell
        {
            public static int Read() => System.Console.Read();

            [Fprog.FunctionalCore]
            public static class Core
            {
                public static int Read() => System.{|FP0801:Console|}.Read();
            }
        }
        """);

    [TestMethod]
    public Task FP0801_AbstractExternAndUnsafe_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public abstract unsafe class Odd
        {
            public abstract System.IO.{|FP0801:Stream|} Open();

            [System.Runtime.InteropServices.DllImport("native")]
            public static extern int Close(System.IntPtr handle, System.IO.{|FP0801:FileAccess|} access);

            public static int Call(delegate*<System.IO.{|FP0801:FileMode|}, int> callback)
                => callback(System.IO.{|FP0801:FileMode|}.Open);

            public static int Size() => sizeof(System.IO.{|FP0801:FileMode|});
        }
        """);

    [TestMethod]
    public Task FP0801_FunctionalCoreWithShellLikeMemberNames_NoReport() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Linq;

        namespace Domain
        {
            [Fprog.FunctionalCore]
            public sealed record Job(string Process, int Timer, string Console);

            [Fprog.FunctionalCore]
            public static class Pure
            {
                public static int Lock(int value) => value;

                public static string Path(string a, string b) => a + "/" + b;

                public static ImmutableArray<Job> Due(ImmutableArray<Job> jobs, int now)
                    => [.. jobs.Where(job => job.Timer <= now).OrderBy(job => job.Process)];

                public static int Total(IEnumerable<Job> jobs)
                {
                    var total = 0;
                    foreach (var job in jobs)
                    {
                        total += job switch
                        {
                            { Timer: > 10, Console: "x" } => Lock(job.Timer),
                            { Process.Length: 0 } => 0,
                            _ => 1
                        };
                    }

                    return total;
                }

                public static string Describe(Job job) => $"{job.Process}:{job.Console}:{Path("a", "b")}";

                public static bool Present([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? text)
                {
                    System.Diagnostics.Debug.Assert(text is null || text.Length >= 0);
                    return text is not null;
                }

                public static System.Lazy<int> Later() => new(() => 1, System.Threading.LazyThreadSafetyMode.None);

                public static System.Threading.CancellationTokenSource? Source() => null;

                public static System.Threading.SpinLock? Spin() => null;

                public static System.Threading.Tasks.ValueTask<int> Ready() => new(1);
            }
        }
        """);

    [TestMethod]
    public Task FP0801_GeneratedCodeAttribute_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
        public static class Emitted
        {
            public static int Read() => System.Console.Read();
        }
        """);

    [TestMethod]
    public Task FP0801_ShellTypeDerivedFromForbiddenTypeNoForbiddenToken_NoReport() => VerifyAsync("""
        public sealed class ShellBuffer : System.IO.MemoryStream
        {
        }

        [Fprog.FunctionalCore]
        public static class UsesBuffer
        {
            public static long Size(ShellBuffer buffer) => buffer.Length;

            public static ShellBuffer Fresh() => new();
        }
        """);

    [TestMethod]
    public Task FP0801_UsingStaticMemberHasNoTypeNameToken_NoReport() => VerifyAsync("""
        using static System.Console;
        using static System.Math;

        [Fprog.FunctionalCore]
        public static class Report
        {
            public static int Twice(int value)
            {
                WriteLine(value);
                return Abs(value) * 2;
            }
        }
        """);

    [TestMethod]
    public Task FP0801_UsingStaticStillReportsWrittenTypeTokens_Reports() => VerifyAsync("""
        using static System.Console;
        using static System.IO.Enumeration.FileSystemEnumerable<int>;
        using C = System.Console;

        [Fprog.FunctionalCore]
        public static class Report
        {
            public static int Qualified(int value)
            {
                System.{|FP0801:Console|}.WriteLine(value);
                {|FP0801:C|}.WriteLine(value);
                return value;
            }

            public static {|FP0801:FindPredicate|}? Imported() => null;
        }
        """);

    [TestMethod]
    public Task FP0801_UsingStaticPropertyFieldEventAndNameof_NoReport() => VerifyAsync("""
        using static System.Console;
        using static System.IO.Path;

        [Fprog.FunctionalCore]
        public static class Report
        {
            public static object Writer() => Out;

            public static char Separator() => DirectorySeparatorChar;

            public static System.ConsoleCancelEventHandler? Handler(System.ConsoleCancelEventHandler handler)
            {
                CancelKeyPress += handler;
                return null;
            }

            public static string Name() => nameof(Out);
        }
        """);

    [TestMethod]
    public Task FP0801_NestedScopeOfLockIsNotAListedType_Reports() => VerifyAsync("""
        using System.Threading;

        [Fprog.FunctionalCore]
        public static class Scopes
        {
            public static int Run({|FP0801:Lock|} gate)
            {
                using ({|FP0801:Lock|}.Scope scope = gate.EnterScope())
                {
                    return 1;
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0801_NestedTypesInListedNamespacesStillReport_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Nested
        {
            public static System.IO.Enumeration.{|FP0801:FileSystemEnumerable|}<int>.{|FP0801:FindPredicate|}? Predicate() => null;

            public static System.IO.Enumeration.{|FP0801:FileSystemEnumerable|}<int>.{|FP0801:FindTransform|}? Transform() => null;
        }
        """);

    [TestMethod]
    public Task FP0801_NestedTypeNamedLikeListedType_NoReport() => VerifyAsync("""
        namespace System.Threading
        {
            public static class Holder
            {
                public sealed class Timer { }

                public sealed class Lock { }
            }
        }

        namespace System
        {
            public static class Shelf
            {
                public sealed class Console { }
            }
        }

        [Fprog.FunctionalCore]
        public static class Uses
        {
            public static System.Threading.Holder.Timer? Timer() => null;

            public static System.Threading.Holder.Lock? Lock() => null;

            public static System.Shelf.Console? Console() => null;
        }
        """);

    [TestMethod]
    public Task FP0801_InUnionBodyNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Pets
        {
            public record Cat;

            public record Dog;

            public union Pet(Cat, Dog)
            {
                public static int Read() => System.{|FP0801:Console|}.Read();

                public static int Locked(object gate)
                {
                    {|FP0801:lock|} (gate)
                    {
                        return 1;
                    }
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0801_InTopLevelUnionUnderAssemblyCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public record Cat;

        public record Dog;

        public union Pet(Cat, Dog)
        {
            public static string Read(string path) => System.IO.{|FP0801:File|}.ReadAllText(path);

            public static System.IO.{|FP0801:Stream|}? Open() => null;
        }
        """);

    [TestMethod]
    public Task FP0801_InClassNestedInUnionBody_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Pets
        {
            public record Cat;

            public record Dog;

            public union Pet(Cat, Dog)
            {
                public abstract class Keeper : System.IO.{|FP0801:TextWriter|}
                {
                    public static int Read() => System.{|FP0801:Console|}.Read();
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0801_DynamicCallAndCreationOnWrittenType_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Late
        {
            public static object Combine(dynamic value) => System.IO.{|FP0801:Path|}.Combine(value, "a");

            public static object Create(dynamic value) => new System.IO.{|FP0801:StringReader|}(value);
        }
        """);

    [TestMethod]
    public Task FP0801_UnionCaseTypeAndMemberTypeInUnion_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Sources
        {
            public union Source(System.IO.{|FP0801:Stream|}, string)
            {
                public static System.Threading.{|FP0801:Timer|}? Tick => null;
            }
        }
        """);

    [TestMethod]
    public Task FP0801_InUnionBodyOutsideCore_NoReport() => VerifyAsync("""
        public record Cat;

        public record Dog;

        public union Pet(Cat, Dog)
        {
            public static int Read() => System.Console.Read();

            public abstract class Keeper : System.IO.TextWriter
            {
            }
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0801", source, testContext.CancellationToken);
}
