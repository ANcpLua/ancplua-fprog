using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0301Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0301_ThrowStatementInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowExpressionInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Names
        {
            public static string Required(string? name)
                => name ?? {|FP0301:throw|} new System.ArgumentNullException(nameof(name));
        }
        """);

    [TestMethod]
    public Task FP0301_ExpressionBodiedMemberThatOnlyThrows_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Later
        {
            public static int Pending() => {|FP0301:throw|} new System.NotImplementedException();
        }
        """);

    [TestMethod]
    public Task FP0301_RethrowInCatch_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInSwitchExpressionArm_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInLambdaAndLocalFunctionInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInPropertyAndFieldInitializerInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public static class Inner
            {
                public static void Fail() => {|FP0301:throw|} new System.InvalidOperationException();
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowUnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public static class Anywhere
        {
            public static void Fail() => {|FP0301:throw|} new System.InvalidOperationException();
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowOutsideCore_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ExceptionCreatedAndReturnedInCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Errors
        {
            public static System.Exception Describe(string message) => new System.InvalidOperationException(message);

            public static System.Exception? Find(bool failed)
                => failed ? new System.TimeoutException() : null;
        }
        """);

    [TestMethod]
    public Task FP0301_TryCatchWithoutThrowInCore_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInUnionBodyNestedInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInTopLevelUnionBodyUnderAssemblyLevelCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInClassNestedInUnionBody_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_SeveralThrowsInUnionBodyInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInUnionBodyOutsideCore_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInCoreClassNestedInUnionOutsideCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInPrimaryConstructorBaseArgumentInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Shapes
        {
            public abstract record Named(string Name);

            public sealed record Circle(string? Label) : Named(Label ?? {|FP0301:throw|} new System.ArgumentNullException(nameof(Label)));
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowStatementInConstructorAccessorAndOperatorInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInPropertyInitializerAndLambdaInFieldInitializerInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Defaults
        {
            private static int? Seed() => null;

            public static int Start { get; } = Seed() ?? {|FP0301:throw|} new System.InvalidOperationException();

            public static readonly System.Func<int> Fail = () => {|FP0301:throw|} new System.InvalidOperationException();
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInClassPrimaryConstructorBaseArgumentInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInThisAndBaseConstructorInitializerInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInTopLevelStatementsUnderAssemblyLevelCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowStatementInMethodConstructorAccessorAndOperatorInUnionBody_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInLocalFunctionAndLambdaInUnionBody_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInInitializersInUnionBody_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInPrimaryConstructorBaseArgumentInUnionBody_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInThisConstructorInitializerInUnionBody_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInUnionNestedInUnionBodyInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInInitializersAndBaseArgumentsInUnionBodyOutsideCore_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0301_ThrowInIteratorInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Sequence
        {
            public static System.Collections.Generic.IEnumerable<int> Upto(int count)
            {
                if (count < 0)
                    {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(count));

                for (var i = 0; i < count; i++)
                    yield return i;
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInConditionalGenericAndWhenClauseInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Checks
        {
            public static T Present<T>(T? value) where T : class => value ?? {|FP0301:throw|} new System.ArgumentNullException(nameof(value));

            public static int Positive(int value) => value > 0 ? value : {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(value));

            public static int Pick(object value)
            {
                switch (value)
                {
                    case int i when i > 0:
                        return i;
                    default:
                        {|FP0301:throw|} new System.ArgumentException(nameof(value));
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInNestedClosuresInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Closures
        {
            public static System.Func<int, int> Make()
                => x =>
                {
                    static int Inner(int y) => y > 0 ? y : {|FP0301:throw|} new System.InvalidOperationException();
                    System.Func<int> later = () => {|FP0301:throw|} new System.NotSupportedException();
                    return Inner(x) + later();
                };
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInExtensionBlockInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class IntExtensions
        {
            extension(int value)
            {
                public int Half() => value < 0 ? {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(value)) : value / 2;

                public int Doubled
                {
                    get
                    {
                        if (value > int.MaxValue / 2)
                            {|FP0301:throw|} new System.OverflowException();
                        return value * 2;
                    }
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInFieldKeywordAccessorInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Age
        {
            public int Years
            {
                get => field;
                init => field = value >= 0 ? value : {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(value));
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInUnannotatedPartOfPartialCoreType_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static partial class Split
        {
        }

        public static partial class Split
        {
            public static int Fail() => {|FP0301:throw|} new System.InvalidOperationException();
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowIfNullCallInCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Guards
        {
            public static int Length(string text)
            {
                System.ArgumentNullException.ThrowIfNull(text);
                System.ArgumentOutOfRangeException.ThrowIfNegative(text.Length);
                return text.Length;
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInGeneratedCodeInCore_NoReport() => VerifyAsync("""
        // <auto-generated/>
        [Fprog.FunctionalCore]
        public static class Generated
        {
            public static int Fail() => throw new System.InvalidOperationException();
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInLocalFunctionOfMethodOutsideCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Core
        {
            public static int Zero() => 0;
        }

        public static class Shell
        {
            public static int Run()
            {
                int Fail() => throw new System.InvalidOperationException();
                System.Func<int> later = () => throw new System.NotSupportedException();
                return Fail() + later();
            }
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0301", source, testContext.CancellationToken);
}
