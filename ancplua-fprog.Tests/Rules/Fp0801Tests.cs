using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0801Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0801_StaticMemberAccessOnConsole_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_QualifiedNameReportsTypeTokenOnly_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Files
                           {
                               public static string Read(string path) => System.IO.{|FP0801:File|}.ReadAllText(path);

                               public static string ReadGlobal(string path) => global::System.IO.{|FP0801:File|}.ReadAllText(path);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_DeclarationTypes_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_ObjectCreation_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_GenericArgumentTypeofCastAndPattern_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_BaseListAndConstraint_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_AttributeFromForbiddenNamespace_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_EnumMemberAccess_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Modes
                           {
                               public static int Open() => (int)System.IO.{|FP0801:FileMode|}.Open;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_CatchClauseException_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_NetAndDataNamespacesAndBelow_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Infra
                           {
                               public static System.Net.{|FP0801:IPAddress|}? Address() => null;

                               public static System.Net.Http.{|FP0801:HttpClient|}? Client() => null;

                               public static System.Data.{|FP0801:DataTable|}? Table() => null;

                               public static System.Data.Common.{|FP0801:DbConnection|}? Connection() => null;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_SubNamespaceOfSystemIO_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Zip
                           {
                               public static System.IO.Compression.{|FP0801:CompressionLevel|} Level()
                                   => System.IO.Compression.{|FP0801:CompressionLevel|}.Optimal;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_Process_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Processes
                           {
                               public static int Id() => System.Diagnostics.{|FP0801:Process|}.GetCurrentProcess().Id;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_NamedThreadingTypes_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_NestedTypeOfLockReportsOnlyLockToken_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Scopes
                           {
                               public static System.Threading.{|FP0801:Lock|}.Scope Enter(System.Threading.{|FP0801:Lock|} gate)
                                   => gate.EnterScope();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_LockStatement_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_LockStatementOnLockType_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_UsingStaticMethodAndEnumConstant_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_AliasNameToken_Reports()
    {
        return VerifyAsync("""
                           using F = System.IO.File;

                           [Fprog.FunctionalCore]
                           public static class Aliased
                           {
                               public static string Read(string path) => {|FP0801:F|}.ReadAllText(path);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_NameofType_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Names
                           {
                               public static string Of() => nameof(System.{|FP0801:Console|});

                               public static string OfMember() => nameof(System.{|FP0801:Console|}.WriteLine);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_MethodGroupAndLambdaAndLocalFunction_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_PositionalRecordParameterReportedOnce_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Entry(System.IO.{|FP0801:FileInfo|} File);
                           """);
    }

    [TestMethod]
    public Task FP0801_InTypeNestedInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public static class Inner
                               {
                                   public static int Read() => System.{|FP0801:Console|}.Read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_OutsideCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_UsingDirectivesOnly_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_VarWithForbiddenInferredTypeFromShellReportsMember_Reports()
    {
        return VerifyAsync("""
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
                                   return stream.{|FP0801:Length|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_AllowedLookalikes_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_NamespaceSegmentLookalikes_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_DocumentationCref_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Documented
                           {
                               /// <summary>Unlike <see cref="System.Console.WriteLine()"/>, this is pure.</summary>
                               public static int Twice(int value) => value * 2;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_FieldLikeEventSynthesizedInterlocked_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Notifier
                           {
                               public event System.Action? Changed;

                               public bool HasListeners() => Changed is not null;
                           }

                           [Fprog.FunctionalCore]
                           public sealed record Point(int X, int Y);
                           """);
    }

    [TestMethod]
    public Task FP0801_GeneratedCode_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_FieldWithSeveralDeclaratorsReportsSharedTypeOnce_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Pair
                           {
                               private readonly System.IO.{|FP0801:Stream|} _first = System.IO.{|FP0801:Stream|}.Null, _second = System.IO.{|FP0801:Stream|}.Null;

                               public bool Same() => ReferenceEquals(_first, _second);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_PatternsForeachAndQuery_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_ConstructorsAccessorsOperatorsAndInitializers_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_DefaultValuesAndAttributeArguments_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_SpanIsNameTokenInOddSyntax_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_RecordStructInterfaceGenericEnumAndDelegate_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_PrimaryConstructorAndPartialPartWithoutAttribute_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_ExtensionBlock_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_AsyncIteratorUsingAndNestedLocks_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_NamespaceAndTypeAliases_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_UsingStaticMethodGroupAndDynamicCall_Reports()
    {
        return VerifyAsync("""
                           using static System.Console;

                           [Fprog.FunctionalCore]
                           public static class Imported
                           {
                               public static System.Action<string> Group() => {|FP0801:WriteLine|};

                               public static int Dynamic(dynamic value)
                               {
                                   {|FP0801:WriteLine|}(value);
                                   return 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UsingStaticMethodNameof_NoReport()
    {
        return VerifyAsync("""
                           using static System.Console;

                           [Fprog.FunctionalCore]
                           public static class Imported
                           {
                               public static string Name() => nameof(WriteLine);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UsingStaticDynamicCallWithCandidatesOutsideForbiddenType_NoReport()
    {
        return VerifyAsync("""
                           using static System.Console;
                           using static Tools;

                           public static class Tools
                           {
                               public static int WriteLine(int value, int width) => value + width;
                           }

                           [Fprog.FunctionalCore]
                           public static class Imported
                           {
                               public static object Dynamic(dynamic value) => WriteLine(value, 2);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_NameofAndDynamicCallOfOwnStaticMembers_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Own
                           {
                               public static int Twice(int value) => value * 2;

                               public static int Twice(long value) => (int)value * 2;

                               public static string Name() => nameof(Twice);

                               public static int Dynamic(dynamic value) => Twice(value);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_StaticMemberInheritedFromForbiddenBaseUnqualifiedReportsOnlyTypeTokens_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public abstract class Wrapped : System.IO.{|FP0801:TextWriter|}
                           {
                               public static System.IO.{|FP0801:TextWriter|} Quiet() => Null;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_CoreNestedInShellReportsOnlyInner_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_AbstractExternAndUnsafe_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_FunctionalCoreWithShellLikeMemberNames_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_GeneratedCodeAttribute_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
                           public static class Emitted
                           {
                               public static int Read() => System.Console.Read();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_ShellTypeDerivedFromForbiddenTypeNoForbiddenToken_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_UsingStaticMemberOfForbiddenTypeWithoutTypeToken_Reports()
    {
        return VerifyAsync("""
                           using static System.Console;
                           using static System.Math;

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static int Twice(int value)
                               {
                                   {|FP0801:WriteLine|}(value);
                                   return value * 2;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UsingStaticMemberOfAllowedType_NoReport()
    {
        return VerifyAsync("""
                           using static System.Console;
                           using static System.Math;

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static int Twice(int value) => Abs(value) * 2;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UsingStaticStillReportsWrittenTypeTokens_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_UsingStaticPropertyFieldAndEvent_Reports()
    {
        return VerifyAsync("""
                           using static System.Console;
                           using static System.IO.Path;

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static object Writer() => {|FP0801:Out|};

                               public static char Separator() => {|FP0801:DirectorySeparatorChar|};

                               public static System.ConsoleCancelEventHandler? Handler(System.ConsoleCancelEventHandler handler)
                               {
                                   {|FP0801:CancelKeyPress|} += handler;
                                   return null;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UsingStaticPropertyNameof_NoReport()
    {
        return VerifyAsync("""
                           using static System.Console;
                           using static System.IO.Path;

                           [Fprog.FunctionalCore]
                           public static class Report
                           {
                               public static string Name() => nameof(Out);

                               public static string Separator() => nameof(DirectorySeparatorChar) + nameof(Out.Write);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_NestedScopeOfLockIsNotAListedType_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_NestedTypesInListedNamespacesStillReport_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Nested
                           {
                               public static System.IO.Enumeration.{|FP0801:FileSystemEnumerable|}<int>.{|FP0801:FindPredicate|}? Predicate() => null;

                               public static System.IO.Enumeration.{|FP0801:FileSystemEnumerable|}<int>.{|FP0801:FindTransform|}? Transform() => null;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_NestedTypeNamedLikeListedType_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_InUnionBodyNestedInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_InTopLevelUnionUnderAssemblyCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public record Cat;

                           public record Dog;

                           public union Pet(Cat, Dog)
                           {
                               public static string Read(string path) => System.IO.{|FP0801:File|}.ReadAllText(path);

                               public static System.IO.{|FP0801:Stream|}? Open() => null;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_InClassNestedInUnionBody_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_DynamicCallAndCreationOnWrittenType_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Late
                           {
                               public static object Combine(dynamic value) => System.IO.{|FP0801:Path|}.Combine(value, "a");

                               public static object Create(dynamic value) => new System.IO.{|FP0801:StringReader|}(value);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UnionCaseTypeAndMemberTypeInUnion_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Sources
                           {
                               public union Source(System.IO.{|FP0801:Stream|}, string)
                               {
                                   public static System.Threading.{|FP0801:Timer|}? Tick => null;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_InUnionBodyOutsideCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0801_EveryPositionOutsideUnion_Reports()
    {
        return VerifyAsync("""
                           namespace Shell
                           {
                               [System.AttributeUsage(System.AttributeTargets.All)]
                               public sealed class TagAttribute(System.Type type) : System.Attribute
                               {
                                   public System.Type Type { get; } = type;
                               }

                               public class Base(char separator)
                               {
                                   public char Separator { get; } = separator;
                               }
                           }

                           namespace Core
                           {
                               [Fprog.FunctionalCore]
                               public static class Plain
                               {
                                   public abstract class Wrapper : System.IO.{|FP0801:Stream|}
                                   {
                                   }

                                   public sealed class Holder<T> where T : System.IO.{|FP0801:Stream|}
                                   {
                                   }

                                   [Shell.Tag(typeof(System.IO.{|FP0801:Stream|}))]
                                   public sealed class Tagged
                                   {
                                   }

                                   public sealed class Keeper(System.IO.{|FP0801:Stream|} source) : Shell.Base(System.IO.{|FP0801:Path|}.PathSeparator)
                                   {
                                       private readonly System.IO.{|FP0801:Stream|} _source = source;

                                       public System.IO.{|FP0801:Stream|} Source => _source;
                                   }

                                   private static readonly System.IO.{|FP0801:Stream|} Shared = System.IO.{|FP0801:Stream|}.Null;

                                   public static System.IO.{|FP0801:Stream|} Empty { get; } = Shared;

                                   public static System.IO.{|FP0801:Stream|} Same(System.IO.{|FP0801:Stream|} stream) => stream;

                                   public static T Constrained<T>(T value) where T : System.IO.{|FP0801:Stream|} => value;

                                   public static int Body(object value, object gate)
                                   {
                                       System.IO.{|FP0801:Stream|} local = System.IO.{|FP0801:Stream|}.Null;
                                       var reader = new System.IO.{|FP0801:StringReader|}("a");
                                       var exists = System.IO.{|FP0801:File|}.Exists("p");
                                       System.Collections.Generic.List<System.IO.{|FP0801:Stream|}> list = [];
                                       var cast = (System.IO.{|FP0801:Stream|})value;
                                       var matches = value is System.IO.{|FP0801:Stream|} { CanRead: true };
                                       var name = nameof(System.IO.{|FP0801:Stream|});
                                       var type = typeof(System.IO.{|FP0801:Stream|});
                                       {|FP0801:lock|} (gate)
                                       {
                                           return local.CanRead && exists && matches && list.Count == 0 && cast.CanRead && reader.Peek() > 0
                                               ? name.Length + type.Name.Length
                                               : 0;
                                       }
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_EveryPositionInUnionBodyNestedInCore_Reports()
    {
        return VerifyAsync("""
                           namespace Shell
                           {
                               [System.AttributeUsage(System.AttributeTargets.All)]
                               public sealed class TagAttribute(System.Type type) : System.Attribute
                               {
                                   public System.Type Type { get; } = type;
                               }

                               public class Base(char separator)
                               {
                                   public char Separator { get; } = separator;
                               }
                           }

                           namespace Core
                           {
                               [Fprog.FunctionalCore]
                               public static class Pets
                               {
                                   public record Cat;

                                   public record Dog;

                                   [Shell.Tag(typeof(System.IO.{|FP0801:File|}))]
                                   public union Pet(Cat, Dog)
                                   {
                                       public abstract class Wrapper : System.IO.{|FP0801:Stream|}
                                       {
                                       }

                                       public sealed class Holder<T> where T : System.IO.{|FP0801:Stream|}
                                       {
                                       }

                                       [Shell.Tag(typeof(System.IO.{|FP0801:Stream|}))]
                                       public sealed class Tagged
                                       {
                                       }

                                       public sealed class Keeper(System.IO.{|FP0801:Stream|} source) : Shell.Base(System.IO.{|FP0801:Path|}.PathSeparator)
                                       {
                                           private readonly System.IO.{|FP0801:Stream|} _source = source;

                                           public System.IO.{|FP0801:Stream|} Source => _source;
                                       }

                                       private static readonly System.IO.{|FP0801:Stream|} Shared = System.IO.{|FP0801:Stream|}.Null;

                                       public static System.IO.{|FP0801:Stream|} Empty { get; } = Shared;

                                       public static System.IO.{|FP0801:Stream|} Same(System.IO.{|FP0801:Stream|} stream) => stream;

                                       public static T Constrained<T>(T value) where T : System.IO.{|FP0801:Stream|} => value;

                                       public static int Body(object value, object gate)
                                       {
                                           System.IO.{|FP0801:Stream|} local = System.IO.{|FP0801:Stream|}.Null;
                                           var reader = new System.IO.{|FP0801:StringReader|}("a");
                                           var exists = System.IO.{|FP0801:File|}.Exists("p");
                                           System.Collections.Generic.List<System.IO.{|FP0801:Stream|}> list = [];
                                           var cast = (System.IO.{|FP0801:Stream|})value;
                                           var matches = value is System.IO.{|FP0801:Stream|} { CanRead: true };
                                           var name = nameof(System.IO.{|FP0801:Stream|});
                                           var type = typeof(System.IO.{|FP0801:Stream|});
                                           {|FP0801:lock|} (gate)
                                           {
                                               return local.CanRead && exists && matches && list.Count == 0 && cast.CanRead && reader.Peek() > 0
                                                   ? name.Length + type.Name.Length
                                                   : 0;
                                           }
                                       }
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_EveryPositionInUnionBodyInNamespaceUnderAssemblyCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           namespace Shell
                           {
                               [System.AttributeUsage(System.AttributeTargets.All)]
                               public sealed class TagAttribute(System.Type type) : System.Attribute
                               {
                                   public System.Type Type { get; } = type;
                               }

                               public class Base(char separator)
                               {
                                   public char Separator { get; } = separator;
                               }
                           }

                           namespace Zoo
                           {
                               public record Cat;

                               public record Dog;

                               [Shell.Tag(typeof(System.IO.{|FP0801:File|}))]
                               public union Pet(Cat, Dog)
                               {
                                   public abstract class Wrapper : System.IO.{|FP0801:Stream|}
                                   {
                                   }

                                   public sealed class Holder<T> where T : System.IO.{|FP0801:Stream|}
                                   {
                                   }

                                   [Shell.Tag(typeof(System.IO.{|FP0801:Stream|}))]
                                   public sealed class Tagged
                                   {
                                   }

                                   public sealed class Keeper(System.IO.{|FP0801:Stream|} source) : Shell.Base(System.IO.{|FP0801:Path|}.PathSeparator)
                                   {
                                       private readonly System.IO.{|FP0801:Stream|} _source = source;

                                       public System.IO.{|FP0801:Stream|} Source => _source;
                                   }

                                   private static readonly System.IO.{|FP0801:Stream|} Shared = System.IO.{|FP0801:Stream|}.Null;

                                   public static System.IO.{|FP0801:Stream|} Empty { get; } = Shared;

                                   public static System.IO.{|FP0801:Stream|} Same(System.IO.{|FP0801:Stream|} stream) => stream;

                                   public static T Constrained<T>(T value) where T : System.IO.{|FP0801:Stream|} => value;

                                   public static int Body(object value, object gate)
                                   {
                                       System.IO.{|FP0801:Stream|} local = System.IO.{|FP0801:Stream|}.Null;
                                       var reader = new System.IO.{|FP0801:StringReader|}("a");
                                       var exists = System.IO.{|FP0801:File|}.Exists("p");
                                       System.Collections.Generic.List<System.IO.{|FP0801:Stream|}> list = [];
                                       var cast = (System.IO.{|FP0801:Stream|})value;
                                       var matches = value is System.IO.{|FP0801:Stream|} { CanRead: true };
                                       var name = nameof(System.IO.{|FP0801:Stream|});
                                       var type = typeof(System.IO.{|FP0801:Stream|});
                                       {|FP0801:lock|} (gate)
                                       {
                                           return local.CanRead && exists && matches && list.Count == 0 && cast.CanRead && reader.Peek() > 0
                                               ? name.Length + type.Name.Length
                                               : 0;
                                       }
                                   }
                               }

                               public union Source(System.Collections.Generic.List<System.IO.{|FP0801:Stream|}>, string);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_TypeLevelPositionsInAndOutsideUnionBody_Reports()
    {
        return VerifyAsync("""
                           namespace Shell
                           {
                               [System.AttributeUsage(System.AttributeTargets.All)]
                               public sealed class TagAttribute(System.Type type) : System.Attribute
                               {
                                   public System.Type Type { get; } = type;
                               }

                               public interface IMark<T>
                               {
                               }

                               public class Box<T>
                               {
                               }

                               public record Parent(char Separator);
                           }

                           namespace Core
                           {
                               [Fprog.FunctionalCore]
                               public static class Kinds
                               {
                                   public sealed class Marked<[Shell.Tag(typeof(System.IO.{|FP0801:Stream|}))] T>
                                   {
                                   }

                                   public sealed class Boxed : Shell.Box<System.IO.{|FP0801:Stream|}>, Shell.IMark<System.IO.{|FP0801:FileInfo|}>
                                   {
                                   }

                                   public readonly struct Amount : Shell.IMark<System.IO.{|FP0801:Stream|}>
                                   {
                                   }

                                   public interface IShape : Shell.IMark<System.IO.{|FP0801:Stream|}>
                                   {
                                   }

                                   [Shell.Tag(typeof(System.IO.{|FP0801:Stream|}))]
                                   public enum Mode
                                   {
                                       [Shell.Tag(typeof(System.IO.{|FP0801:File|}))] Open
                                   }

                                   [method: Shell.Tag(typeof(System.IO.{|FP0801:File|}))]
                                   public sealed record Child(int Size) : Shell.Parent(System.IO.{|FP0801:Path|}.PathSeparator);

                                   [Shell.Tag(typeof(System.{|FP0801:Console|}))]
                                   public delegate int Reader();

                                   public record Cat;

                                   public record Dog;

                                   public union Pet(Cat, Dog)
                                   {
                                       public sealed class Marked<[Shell.Tag(typeof(System.IO.{|FP0801:Stream|}))] T>
                                       {
                                       }

                                       public sealed class Boxed : Shell.Box<System.IO.{|FP0801:Stream|}>, Shell.IMark<System.IO.{|FP0801:FileInfo|}>
                                       {
                                       }

                                       public readonly struct Amount : Shell.IMark<System.IO.{|FP0801:Stream|}>
                                       {
                                       }

                                       public interface IShape : Shell.IMark<System.IO.{|FP0801:Stream|}>
                                       {
                                       }

                                       [Shell.Tag(typeof(System.IO.{|FP0801:Stream|}))]
                                       public enum Mode
                                       {
                                           [Shell.Tag(typeof(System.IO.{|FP0801:File|}))] Open
                                       }

                                       [method: Shell.Tag(typeof(System.IO.{|FP0801:File|}))]
                                       public sealed record Child(int Size) : Shell.Parent(System.IO.{|FP0801:Path|}.PathSeparator);

                                       [Shell.Tag(typeof(System.{|FP0801:Console|}))]
                                       public delegate int Reader();
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_CoreClassInShellUnionBodyReportsOnlyInner_Reports()
    {
        return VerifyAsync("""
                           public record Cat;

                           public record Dog;

                           public union Pet(Cat, Dog)
                           {
                               public static int Read() => System.Console.Read();

                               [Fprog.FunctionalCore]
                               public abstract class Keeper : System.IO.{|FP0801:TextWriter|}
                               {
                                   public static int Read() => System.{|FP0801:Console|}.Read();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UnionHeaderAndTypeLevelPositionsOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           namespace Shell
                           {
                               [System.AttributeUsage(System.AttributeTargets.All)]
                               public sealed class TagAttribute(System.Type type) : System.Attribute
                               {
                                   public System.Type Type { get; } = type;
                               }

                               public static class Outer
                               {
                                   public record Cat;

                                   public record Dog;

                                   [Tag(typeof(System.IO.File))]
                                   public union Pet(Cat, Dog)
                                   {
                                       [Tag(typeof(System.IO.Stream))]
                                       public sealed class Holder<T> where T : System.IO.Stream
                                       {
                                       }

                                       public static int Locked(object gate)
                                       {
                                           lock (gate)
                                           {
                                               return 1;
                                           }
                                       }
                                   }

                                   public union Source(System.IO.Stream, string);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UsingStaticInEveryMemberPosition_Reports()
    {
        return VerifyAsync("""
                           using static System.IO.File;
                           using static System.Console;
                           using static System.IO.FileMode;
                           using static System.Net.Mime.MediaTypeNames.Text;

                           public class Base(string text)
                           {
                               public string Text { get; } = text;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class PrimaryBaseArgument(string path) : Base({|FP0801:ReadAllText|}(path))
                           {
                               public int Unrelated(System.IO.{|FP0801:Stream|} stream) => 1;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class PrimaryBaseArgumentAlone(string path) : Base({|FP0801:ReadAllText|}(path));

                           [Fprog.FunctionalCore]
                           public sealed class Accessors
                           {
                               public string Value
                               {
                                   get { System.{|FP0801:Console|}.Write("get"); return ""; }
                                   init { {|FP0801:WriteLine|}(value); }
                               }

                               public string this[string path] => {|FP0801:ReadAllText|}(path);

                               public static readonly string Initialized = {|FP0801:ReadAllText|}("x");

                               public static string Mime => {|FP0801:Plain|};

                               public static string Lambda(string path)
                               {
                                   System.Func<string, string> read = static p => {|FP0801:ReadAllText|}(p);
                                   return read(path);
                               }
                           }

                           [Fprog.FunctionalCore]
                           public static class Enums
                           {
                               public enum Sizes
                               {
                                   Small = (int)System.IO.{|FP0801:FileMode|}.Open,
                                   Large = (int){|FP0801:Truncate|},
                                   Same = Large
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UsingStaticUseReportedWhateverElseTheMemberContains_Reports()
    {
        return VerifyAsync("""
                           using static System.IO.File;

                           [Fprog.FunctionalCore]
                           public static class Guarded
                           {
                               public static string Before(string path)
                               {
                                   System.{|FP0801:Console|}.WriteLine(path);
                                   return {|FP0801:ReadAllText|}(path);
                               }

                               public static string After(string path) => {|FP0801:ReadAllText|}(path);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_GlobalUsingStaticAndGenericMethod_Reports()
    {
        return VerifyAsync("""
                           global using static System.IO.Path;
                           global using static System.Threading.Interlocked;

                           [Fprog.FunctionalCore]
                           public static class Names
                           {
                               public static string Ext(string name) => {|FP0801:GetExtension|}(name);

                               public static string Swap(ref string target) => {|FP0801:Exchange|}<string>(ref target, "x");
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UsingStaticOwnAndOuterStaticMembers_NoReport()
    {
        return VerifyAsync("""
                           using static System.Math;

                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public const int Limit = 3;

                               public static int Twice(int value) => value * 2;

                               public static class Inner
                               {
                                   public static int Use(int value) => Twice(Max(value, Limit));
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_InstanceMemberOnShellObjectNeverNamedInCore_Reports()
    {
        return VerifyAsync("""
                           using System.Linq;

                           public static class Shell
                           {
                               public static System.IO.StreamReader Open(string path) => new(path);

                               public static System.Net.Http.HttpClient Client { get; } = new();

                               public static System.Collections.Generic.IEnumerable<System.IO.StreamReader> Readers => [];

                               public static System.Lazy<System.IO.StreamReader> Later { get; } = new(() => Open("a"));

                               public static System.Threading.Tasks.Task<System.IO.StreamReader> OpenAsync(string path)
                                   => System.Threading.Tasks.Task.FromResult(Open(path));

                               public static bool TryOpen(out System.IO.StreamReader reader)
                               {
                                   reader = Open("a");
                                   return true;
                               }

                               public static (System.IO.StreamReader Reader, int Size) Pair() => (Open("a"), 1);
                           }

                           [Fprog.FunctionalCore]
                           public static class Importer
                           {
                               public static string FirstLine(string path)
                               {
                                   var reader = Shell.Open(path);
                                   return reader.{|FP0801:ReadLine|}() ?? "";
                               }

                               public static string Everything(string path) => Shell.Open(path).{|FP0801:ReadToEnd|}();

                               public static string Download(string url) => Shell.Client.{|FP0801:GetStringAsync|}(url).Result;

                               public static string? Maybe(string path) => Shell.Open(path)?.{|FP0801:ReadLine|}();

                               public static long OnlyFirstMemberOfChain(string path) => Shell.Open(path).{|FP0801:BaseStream|}.Length;

                               public static System.Func<string> Group(string path) => Shell.Open(path).{|FP0801:ReadToEnd|};

                               public static string Lazy() => Shell.Later.Value.{|FP0801:ReadToEnd|}();

                               public static async System.Threading.Tasks.ValueTask<string> Awaited(string path)
                                   => (await Shell.OpenAsync(path)).{|FP0801:ReadToEnd|}();

                               public static int Each()
                               {
                                   var count = 0;
                                   foreach (var reader in Shell.Readers)
                                       count += reader.{|FP0801:Peek|}();

                                   return count;
                               }

                               public static string[] Lines() => [.. Shell.Readers.Select(reader => reader.{|FP0801:ReadToEnd|}())];

                               public static string Out() => Shell.TryOpen(out var reader) ? reader.{|FP0801:ReadToEnd|}() : "";

                               public static string Deconstructed()
                               {
                                   var (reader, _) = Shell.Pair();
                                   return reader.{|FP0801:ReadToEnd|}();
                               }

                               public static int Disposed(string path)
                               {
                                   using var reader = Shell.Open(path);
                                   var again = reader;
                                   return again.{|FP0801:Peek|}();
                               }

                               public static string Matched(string path)
                                   => Shell.Open(path) is var reader ? reader.{|FP0801:ReadToEnd|}() : "";
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_InstanceMemberDeclaredOnForbiddenBaseOfShellType_Reports()
    {
        return VerifyAsync("""
                           public sealed class ShellBuffer : System.IO.MemoryStream
                           {
                           }

                           public static class Shell
                           {
                               public static ShellBuffer Buffer() => new();
                           }

                           public abstract class ShellBase
                           {
                               public System.IO.TextReader Reader { get; } = System.IO.TextReader.Null;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class UsesShell : ShellBase
                           {
                               public static long Size()
                               {
                                   var buffer = Shell.Buffer();
                                   return buffer.{|FP0801:Length|};
                               }

                               public string Line() => Reader.{|FP0801:ReadToEnd|}();

                               public string ThisLine() => this.Reader.{|FP0801:ReadToEnd|}();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_InstanceMemberOnReceiverThatNamesTheType_Reports()
    {
        return VerifyAsync("""
                           using static System.Console;
                           using static System.IO.File;
                           using System.Collections.Generic;
                           using System.Linq;

                           public static class Shell
                           {
                               public static T Pick<T>(T value) => value;

                               public static System.IO.Stream Wrap(System.IO.Stream stream) => stream;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Covered(System.IO.{|FP0801:TextReader|} source)
                           {
                               private readonly System.IO.{|FP0801:TextReader|} _field = System.IO.{|FP0801:TextReader|}.Null;

                               public string Primary() => source.ReadToEnd();

                               public string Field() => _field.ReadToEnd();

                               public static string Parameter(System.IO.{|FP0801:StreamReader|} reader) => reader.ReadToEnd();

                               public static string Created(string path)
                               {
                                   var reader = new System.IO.{|FP0801:StreamReader|}(path);
                                   return reader.ReadToEnd();
                               }

                               public static string Opened(string path) => System.IO.{|FP0801:File|}.OpenText(path).ReadToEnd();

                               public static string Imported(string path) => {|FP0801:OpenText|}(path).ReadToEnd();

                               public static int Print(string text)
                               {
                                   System.{|FP0801:Console|}.Out.Write(text);
                                   {|FP0801:Out|}.Write(text);
                                   return 0;
                               }

                               public static long Cast(object value) => ((System.IO.{|FP0801:Stream|})value).Length;

                               public static long Wrapped(object value) => Shell.Wrap((System.IO.{|FP0801:Stream|})value).Length;

                               public static string Generic(System.IO.{|FP0801:StreamReader|} reader) => Shell.Pick(reader).ReadToEnd();

                               public static string First(List<System.IO.{|FP0801:StreamReader|}> readers)
                                   => Enumerable.First(readers).ReadToEnd() + readers[0].ReadToEnd();

                               public static string Later(System.Lazy<System.IO.{|FP0801:StreamReader|}> later) => later.Value.ReadToEnd();

                               public static string[] Each(IEnumerable<System.IO.{|FP0801:StreamReader|}> readers)
                               {
                                   foreach (var reader in readers)
                                       _ = reader.Peek();

                                   return [.. readers.Select(reader => reader.ReadToEnd())];
                               }

                               public static string Typed(object value) => value is System.IO.{|FP0801:StreamReader|} reader ? reader.ReadToEnd() : "";

                               public static string Delegate(System.Func<System.IO.{|FP0801:StreamReader|}> open) => open().ReadToEnd();
                           }

                           [Fprog.FunctionalCore]
                           public abstract class Derived : System.IO.{|FP0801:TextWriter|}
                           {
                               public int Twice(string text)
                               {
                                   Write(text);
                                   this.Write(text);
                                   base.Write(text);
                                   return 2;
                               }
                           }

                           [Fprog.FunctionalCore]
                           public static class StreamExtensions
                           {
                               extension(System.IO.{|FP0801:Stream|} stream)
                               {
                                   public bool IsEmpty => stream.Length == 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_ShellValueWithoutShellMemberOrOnTypedCoreReceiver_NoReport()
    {
        return VerifyAsync("""
                           public sealed class ShellBuffer : System.IO.MemoryStream
                           {
                           }

                           public sealed record Reply(System.Net.HttpStatusCode Status, System.Version Protocol);

                           [Fprog.FunctionalCore]
                           public static class Pure
                           {
                               public static bool Ok(Reply reply) => (int)reply.Status == 200;

                               public static int Major(Reply reply) => reply.Protocol.Major;

                               public static long Size(ShellBuffer buffer)
                               {
                                   var same = buffer;
                                   return same.Length + buffer.Position;
                               }

                               public static int Words(string text)
                               {
                                   var parts = text.Split(' ');
                                   return parts.Length;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_ShellMemberReachedThroughMemberDeclaredOutsideCore_Reports()
    {
        return VerifyAsync("""
                           public sealed record Contact(string Name, System.Net.Mail.MailAddress Address);

                           public sealed record Reply(System.Net.HttpStatusCode Status, System.Version Protocol);

                           public sealed class Repo
                           {
                               public System.Data.Common.DbConnection Connection { get; init; } = null!;
                           }

                           public sealed class Holder
                           {
                               public System.Lazy<System.IO.StreamReader> Later { get; } = new(() => System.IO.StreamReader.Null);

                               public System.IO.FileInfo[] Files { get; } = [];
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Pure(Repo store)
                           {
                               public static bool Internal(Contact contact) => contact.Address.{|FP0801:Host|} == "example.org";

                               public static string Kind(Reply reply) => reply.Status.{|FP0801:ToString|}();

                               public static int Open(Repo repo)
                               {
                                   repo.Connection.{|FP0801:Open|}();
                                   return 1;
                               }

                               public static int Length(Repo repo) => repo.Connection.{|FP0801:ConnectionString|}.Length;

                               public static string? Maybe(Repo? repo) => repo?.Connection.{|FP0801:Database|};

                               public string Primary() => store.Connection.{|FP0801:Database|};

                               public static string Later(Holder holder) => holder.Later.Value.{|FP0801:ReadToEnd|}();

                               public static long Size(Holder holder) => holder.Files[0].{|FP0801:Length|};

                               public static int Major(Reply reply) => reply.Protocol.Major + reply.Status.{|FP0801:GetHashCode|}();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_ShellMemberOnWrittenShellTypeReportsOnlyTheTypeToken_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Written
                           {
                               public static long Size(System.IO.{|FP0801:StreamReader|} reader) => reader.BaseStream.Length;

                               public static int Name(System.IO.{|FP0801:FileInfo|} file) => file.Directory!.Name.Length + file.Name.Length;

                               public static long Position(System.IO.{|FP0801:StreamReader|}? reader) => reader?.BaseStream.Position ?? 0;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_TopLevelStatementsVarFromCreation_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           {|CS8805:var reader = new System.IO.{|FP0801:StreamReader|}("a.txt");|}
                           var first = reader.ReadLine();
                           var rest = reader.ReadToEnd();
                           return first is null || rest.Length == 0 ? 1 : 0;
                           """);
    }

    [TestMethod]
    public Task FP0801_TargetTypedNewForShellParameter_Reports()
    {
        return VerifyAsync("""
                           public static class ShellApi
                           {
                               public static string Read(System.IO.StreamReader reader) => reader.ReadToEnd();
                           }

                           public sealed class ShellJob(System.IO.FileInfo file)
                           {
                               public System.IO.FileInfo File { get; } = file;
                           }

                           [Fprog.FunctionalCore]
                           public static class Core
                           {
                               public static string Read(string path) => ShellApi.Read({|FP0801:new|}(path));

                               public static object Job(string path) => new ShellJob({|FP0801:new|}(path));
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_TargetTypedNewWhereCoreWritesTheType_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public static class Shell
                           {
                               public static T Pick<T>(T value) => value;
                           }

                           [Fprog.FunctionalCore]
                           public static class Core
                           {
                               public static string Read(System.IO.{|FP0801:StreamReader|} reader) => "";

                               public static string Use(string path) => Read(new(path));

                               public static int Add(List<System.IO.{|FP0801:StreamReader|}> readers, string path)
                               {
                                   readers.Add(new(path));
                                   return readers.Count;
                               }

                               public static object Picked(string path) => Shell.Pick<System.IO.{|FP0801:StreamReader|}>(new(path));
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_TargetTypedNewOfAllowedTypeForShellParameter_NoReport()
    {
        return VerifyAsync("""
                           public static class ShellApi
                           {
                               public static int Count(System.Collections.Generic.List<int> values) => values.Count;
                           }

                           [Fprog.FunctionalCore]
                           public static class Core
                           {
                               public static int Count() => ShellApi.Count(new());
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UncoveredUsesInUnionBodyNestedInCore_Reports()
    {
        return VerifyAsync("""
                           using static System.Console;

                           public static class Shell
                           {
                               public static System.IO.StreamReader Open(string path) => new(path);

                               public static string Read(System.IO.StreamReader reader) => reader.ReadToEnd();
                           }

                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public record Cat;

                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public static int Say(string text)
                                   {
                                       {|FP0801:WriteLine|}(text);
                                       return Shell.Open(text).{|FP0801:Peek|}();
                                   }

                                   public static string Read(string path) => Shell.Read({|FP0801:new|}(path));
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0801_UncoveredUsesOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           using static System.Console;

                           public static class Shell
                           {
                               public static System.IO.StreamReader Open(string path) => new(path);

                               public static string Read(System.IO.StreamReader reader) => reader.ReadToEnd();
                           }

                           public static class NotCore
                           {
                               public static int Say(string text)
                               {
                                   WriteLine(text);
                                   var reader = Shell.Open(text);
                                   return reader.Peek() + Shell.Read(new(text)).Length;
                               }
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0801", source, testContext.CancellationToken);
    }
}