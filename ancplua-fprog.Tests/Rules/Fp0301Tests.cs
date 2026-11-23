using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0301Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0301_ThrowStatementInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pricing
                           {
                               public static int Half(int value)
                               {
                                   if (value < 0)
                                       {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(value));

                                   return value / 2;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowExpressionInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Names
                           {
                               public static string Required(string? name)
                                   => name ?? {|FP0301:throw|} new System.ArgumentNullException(nameof(name));
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ExpressionBodiedMemberThatOnlyThrows_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Later
                           {
                               public static int Pending() => {|FP0301:throw|} new System.NotImplementedException();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_RethrowInCatch_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Retry
                           {
                               public static int Run(System.Func<int> work)
                               {
                                   try
                                   {
                                       return work();
                                   }
                                   catch (System.InvalidOperationException)
                                   {
                                       {|FP0301:throw|};
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInSwitchExpressionArm_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Days
                           {
                               public static string Name(int day) => day switch
                               {
                                   0 => "Sunday",
                                   1 => "Monday",
                                   _ => {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(day))
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInLambdaAndLocalFunctionInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Closures
                           {
                               public static System.Func<int> Lambda() => () => {|FP0301:throw|} new System.InvalidOperationException();

                               public static int Local()
                               {
                                   return Fail();

                                   static int Fail() => {|FP0301:throw|} new System.InvalidOperationException();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInPropertyAndFieldInitializerInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Settings
                           {
                               private static readonly string? Raw = System.Environment.GetEnvironmentVariable("FP");

                               public static readonly string Value = Raw ?? {|FP0301:throw|} new System.InvalidOperationException();

                               public string Name
                               {
                                   get => {|FP0301:throw|} new System.NotSupportedException();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInTypeNestedInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public static class Inner
                               {
                                   public static void Fail() => {|FP0301:throw|} new System.InvalidOperationException();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public static class Anywhere
                           {
                               public static void Fail() => {|FP0301:throw|} new System.InvalidOperationException();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           public static class Shell
                           {
                               public static void Fail() => throw new System.InvalidOperationException();

                               public static int Half(int value)
                               {
                                   if (value < 0)
                                       throw new System.ArgumentOutOfRangeException(nameof(value));

                                   return value / 2;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ExceptionCreatedAndReturnedInCore_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Errors
                           {
                               public static System.Exception Describe(string message) => new System.InvalidOperationException(message);

                               public static System.Exception? Find(bool failed)
                                   => failed ? new System.TimeoutException() : null;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_TryCatchWithoutThrowInCore_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Safe
                           {
                               public static int ParseOrZero(string text)
                               {
                                   try
                                   {
                                       return int.Parse(text);
                                   }
                                   catch (System.FormatException)
                                   {
                                       return 0;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInUnionBodyNestedInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public static Pet Fail() => {|FP0301:throw|} new System.InvalidOperationException();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInTopLevelUnionBodyUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           namespace Zoo
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public static Pet Fail() => {|FP0301:throw|} new System.InvalidOperationException();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInClassNestedInUnionBody_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public record Cat;
                           public record Dog;

                           public union Pet(Cat, Dog)
                           {
                               public static class Helpers
                               {
                                   public static int Fail() => {|FP0301:throw|} new System.InvalidOperationException();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_SeveralThrowsInUnionBodyInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public static Pet Fail() => {|FP0301:throw|} new System.InvalidOperationException();

                                   public static string Name(string? name) => name ?? {|FP0301:throw|} new System.ArgumentNullException(nameof(name));

                                   public static int Retry(System.Func<int> work)
                                   {
                                       try
                                       {
                                           return work();
                                       }
                                       catch (System.InvalidOperationException)
                                       {
                                           {|FP0301:throw|};
                                       }
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInUnionBodyOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           namespace Zoo
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public static Pet Fail() => throw new System.InvalidOperationException();
                               }

                               public static class Shell
                               {
                                   public record Fish;
                                   public record Bird;

                                   public union Animal(Fish, Bird)
                                   {
                                       public static Animal Fail() => throw new System.InvalidOperationException();
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInCoreClassNestedInUnionOutsideCore_Reports()
    {
        return VerifyAsync("""
                           public record Cat;
                           public record Dog;

                           public union Pet(Cat, Dog)
                           {
                               public static Pet Loud() => throw new System.InvalidOperationException();

                               [Fprog.FunctionalCore]
                               public static class Rules
                               {
                                   public static int Fail() => {|FP0301:throw|} new System.InvalidOperationException();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInPrimaryConstructorBaseArgumentInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Shapes
                           {
                               public abstract record Named(string Name);

                               public sealed record Circle(string? Label) : Named(Label ?? {|FP0301:throw|} new System.ArgumentNullException(nameof(Label)));
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowStatementInConstructorAccessorAndOperatorInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Money
                           {
                               public Money(int cents)
                               {
                                   if (cents < 0)
                                       {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(cents));

                                   Cents = cents;
                               }

                               public int Cents { get; }

                               public static int Rate
                               {
                                   get { {|FP0301:throw|} new System.NotSupportedException(); }
                               }

                               public static Money operator +(Money left, Money right)
                               {
                                   {|FP0301:throw|} new System.NotSupportedException();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInPropertyInitializerAndLambdaInFieldInitializerInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Defaults
                           {
                               private static int? Seed() => null;

                               public static int Start { get; } = Seed() ?? {|FP0301:throw|} new System.InvalidOperationException();

                               public static readonly System.Func<int> Fail = () => {|FP0301:throw|} new System.InvalidOperationException();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInClassPrimaryConstructorBaseArgumentInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Shapes
                           {
                               public class B(int x)
                               {
                                   public int X => x;
                               }

                               public sealed class D(int x) : B(x > 0 ? x : {|FP0301:throw|} new System.InvalidOperationException());
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInThisAndBaseConstructorInitializerInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Shapes
                           {
                               public class B(int x)
                               {
                                   public int X => x;
                               }

                               public sealed class E : B
                               {
                                   public E(int x) : base(x > 0 ? x : {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(x))) { }

                                   public E(string text) : this(text.Length > 0 ? text.Length : {|FP0301:throw|} new System.ArgumentException(nameof(text))) { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInTopLevelStatementsUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           {|CS8805:int? seed = null;|}
                           if (seed is null)
                               {|FP0301:throw|} new System.InvalidOperationException();
                           System.Func<int> fail = () => {|FP0301:throw|} new System.InvalidOperationException();
                           static int Local() => {|FP0301:throw|} new System.InvalidOperationException();

                           public record Cat;
                           public record Dog;

                           public union Pet(Cat, Dog)
                           {
                               public static Pet Fail() => {|FP0301:throw|} new System.InvalidOperationException();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowStatementInMethodConstructorAccessorAndOperatorInUnionBody_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   private Pet(int lives) : this(new Cat())
                                   {
                                       if (lives < 0)
                                           {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(lives));
                                   }

                                   public static Pet Pick(int index)
                                   {
                                       if (index < 0)
                                           {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(index));

                                       return new Pet(new Cat());
                                   }

                                   public static int Count
                                   {
                                       get { {|FP0301:throw|} new System.NotSupportedException(); }
                                   }

                                   public static Pet operator +(Pet left, Pet right)
                                   {
                                       {|FP0301:throw|} new System.NotSupportedException();
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInLocalFunctionAndLambdaInUnionBody_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public static System.Func<Pet> Lambda() => () => {|FP0301:throw|} new System.InvalidOperationException();

                                   public static Pet Local()
                                   {
                                       return Fail();

                                       static Pet Fail()
                                       {
                                           {|FP0301:throw|} new System.InvalidOperationException();
                                       }
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInInitializersInUnionBody_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   private static int? Seed() => null;

                                   public static readonly int Lives = Seed() ?? {|FP0301:throw|} new System.InvalidOperationException();

                                   public static int Legs { get; } = Seed() ?? {|FP0301:throw|} new System.InvalidOperationException();

                                   public static readonly System.Func<int> Fail = () => {|FP0301:throw|} new System.InvalidOperationException();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInPrimaryConstructorBaseArgumentInUnionBody_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public class B(int x)
                                   {
                                       public int X => x;
                                   }

                                   public sealed class D(int x) : B(x > 0 ? x : {|FP0301:throw|} new System.InvalidOperationException());

                                   public abstract record Named(string Name);

                                   public sealed record Tag(string? Label) : Named(Label ?? {|FP0301:throw|} new System.ArgumentNullException(nameof(Label)));
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInThisConstructorInitializerInUnionBody_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   private Pet(string name) : this(name.Length > 0 ? new Cat() : {|FP0301:throw|} new System.ArgumentException(nameof(name))) { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInUnionNestedInUnionBodyInCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           namespace Zoo
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public union Inner(Cat, Dog)
                                   {
                                       public static Inner Fail() => {|FP0301:throw|} new System.InvalidOperationException();
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowInInitializersAndBaseArgumentsInUnionBodyOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           namespace Zoo
                           {
                               [Fprog.FunctionalCore]
                               public static class Rules
                               {
                                   public static int Zero() => 0;
                               }

                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   private static int? Seed() => null;

                                   public static readonly int Lives = Seed() ?? throw new System.InvalidOperationException();

                                   public static int Legs { get; } = Seed() ?? throw new System.InvalidOperationException();

                                   private Pet(string name) : this(name.Length > 0 ? new Cat() : throw new System.ArgumentException(nameof(name))) { }

                                   public class B(int x)
                                   {
                                       public int X => x;
                                   }

                                   public sealed class D(int x) : B(x > 0 ? x : throw new System.InvalidOperationException());
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_FrameworkThrowHelpersInCore_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static class Guard
                           {
                               public static int Positive(int value)
                               {
                                   ArgumentOutOfRangeException.{|FP0301:ThrowIfNegativeOrZero|}(value);
                                   ArgumentOutOfRangeException.{|FP0301:ThrowIfGreaterThan|}<int>(value, 10);
                                   System.ArgumentOutOfRangeException.{|FP0301:ThrowIfEqual|}(value, 3);
                                   return value;
                               }

                               public static string Named(string? name)
                               {
                                   ArgumentNullException.{|FP0301:ThrowIfNull|}(name);
                                   ArgumentException.{|FP0301:ThrowIfNullOrWhiteSpace|}(name);
                                   global::System.ArgumentException.{|FP0301:ThrowIfNullOrEmpty|}(name);
                                   return name;
                               }

                               public static int Disposed(bool gone, object owner)
                               {
                                   ObjectDisposedException.{|FP0301:ThrowIf|}(gone, owner);
                                   return 1;
                               }

                               public static int Inherited(string text)
                               {
                                   ArgumentNullException.{|FP0301:ThrowIfNullOrEmpty|}(text);
                                   return text.Length;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowHelperThroughUsingStaticLambdaAndLocalFunction_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using static System.ArgumentNullException;

                           [Fprog.FunctionalCore]
                           public static class Guard
                           {
                               public static string Named(string? name)
                               {
                                   {|FP0301:ThrowIfNull|}(name);
                                   return name;
                               }

                               public static Func<int, int> Check() => n =>
                               {
                                   ArgumentOutOfRangeException.{|FP0301:ThrowIfNegative|}(n);
                                   return Local(n);

                                   int Local(int m)
                                   {
                                       ArgumentOutOfRangeException.{|FP0301:ThrowIfZero|}(m);
                                       return m;
                                   }
                               };

                               public static int Initialized { get; } = Make(1);

                               private static int Make(int n)
                               {
                                   ArgumentOutOfRangeException.{|FP0301:ThrowIfNegative|}(n);
                                   return n;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_StaticThrowHelperOnUserExceptionType_Reports()
    {
        return VerifyAsync("""
                           using System;

                           public sealed class NotFoundException(string message) : InvalidOperationException(message)
                           {
                               public static void Throw(string what) => throw new NotFoundException(what);

                               public static void ThrowIfMissing(object? value, string what)
                               {
                                   if (value is null)
                                       throw new NotFoundException(what);
                               }
                           }

                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static object Find(object? value)
                               {
                                   NotFoundException.{|FP0301:ThrowIfMissing|}(value, "value");
                                   if (value is string)
                                       NotFoundException.{|FP0301:Throw|}("text");
                                   return value!;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowHelperInUnionBodyInCoreReportedOnce_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public static Pet Make(Cat? cat)
                                   {
                                       System.ArgumentNullException.{|FP0301:ThrowIfNull|}(cat);
                                       return cat;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowHelpersOutOfScope_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;

                           public static class ShellGuard
                           {
                               public static T Fail<T>(string message) => throw new InvalidOperationException(message);

                               public static void ThrowIfNegative(int value)
                               {
                                   if (value < 0)
                                       throw new ArgumentOutOfRangeException(nameof(value));
                               }
                           }

                           public sealed class RichException(string message) : Exception(message)
                           {
                               public void ThrowAgain() => throw this;
                           }

                           [Fprog.FunctionalCore]
                           public static class Uses
                           {
                               public static int Cancel(CancellationToken ct)
                               {
                                   ct.ThrowIfCancellationRequested();
                                   return 1;
                               }

                               public static int Helper(int v)
                               {
                                   ShellGuard.ThrowIfNegative(v);
                                   return v < 0 ? ShellGuard.Fail<int>("negative") : v;
                               }

                               public static int Instance(RichException e)
                               {
                                   e.ThrowAgain();
                                   return 1;
                               }

                               public static int Marshalled(int hr)
                               {
                                   System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(hr);
                                   return hr;
                               }

                               public static Action<object?, string?> Group() => ArgumentNullException.ThrowIfNull;

                               public static string Name() => nameof(ArgumentNullException.ThrowIfNull);

                               public static Exception Make() => new ArgumentNullException("x");
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_ThrowHelperOutsideCoreAndInGeneratedCode_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           public static class Shell
                           {
                               public static string Named(string? name)
                               {
                                   System.ArgumentNullException.ThrowIfNull(name);
                                   return name;
                               }
                           }

                           [Fprog.FunctionalCore]
                           public static class Generated
                           {
                               public static int Positive(int value)
                               {
                                   System.ArgumentOutOfRangeException.ThrowIfNegative(value);
                                   return value;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_DoesNotReturnAnnotationOnStaticInstanceAndLocalMethods_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Diagnostics.CodeAnalysis;
                           using System.Runtime.ExceptionServices;

                           public sealed class Validator
                           {
                               [DoesNotReturn]
                               public static void Fail() => throw new InvalidOperationException();

                               [DoesNotReturn]
                               public void Reject<T>() => throw new InvalidOperationException();
                           }

                           [Fprog.FunctionalCore]
                           public static class Domain
                           {
                               public static int Validate(Validator? validator, Exception error, int choice)
                               {
                                   switch (choice)
                                   {
                                       case 0: Validator.{|FP0301:Fail|}(); break;
                                       case 1: validator?.{|FP0301:Reject|}<int>(); break;
                                       case 2: ExceptionDispatchInfo.{|FP0301:Throw|}(error); break;
                                       case 3: ExceptionDispatchInfo.Capture(error).{|FP0301:Throw|}(); break;
                                       case 4: System.Diagnostics.Debug.{|FP0301:Fail|}("x"); break;
                                       default: {|FP0301:Abort|}(); break;
                                   }

                                   return choice;

                                   [DoesNotReturn]
                                   static void Abort() => {|FP0301:throw|} new InvalidOperationException();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0301_DoesNotReturnMethodGroupLookalikeAndOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           using System.Diagnostics.CodeAnalysis;

                           namespace Local
                           {
                               public sealed class DoesNotReturnAttribute : Attribute;
                           }

                           public static class Helpers
                           {
                               [DoesNotReturn]
                               public static void Fail() => throw new Exception();

                               [Local.DoesNotReturn]
                               public static int Finish() => 1;

                               public static int Outside()
                               {
                                   Fail();
                                   return 0;
                               }
                           }

                           [Fprog.FunctionalCore]
                           public static class Domain
                           {
                               public static Action Failure() => Helpers.Fail;

                               public static int Finish() => Helpers.Finish();
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0301", source, testContext.CancellationToken);
    }
}