using System.Threading.Tasks;
using ancplua_fprog.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0404Tests(TestContext testContext)
{
    private const string Pets = """

                                 public sealed record Cat(string Name);
                                 public sealed record Dog(string Name);
                                 public union Pet(Cat, Dog);
                                 """;

    private const string HandWrittenUnions = """

                                              namespace Custom
                                              {
                                                  using System.Runtime.CompilerServices;

                                                  [Union]
                                                  public readonly struct BoxedAmount : IUnion
                                                  {
                                                      public BoxedAmount(int number) => Value = number;
                                                      public BoxedAmount(string text) => Value = text;
                                                      public object? Value { get; }
                                                  }

                                                  [Union]
                                                  public readonly struct Amount : IUnion
                                                  {
                                                      private readonly int _number;
                                                      private readonly string? _text;

                                                      public Amount(int number) => _number = number;
                                                      public Amount(string text) => _text = text;
                                                      public object Value => _text is null ? (object)_number : _text;
                                                  }

                                                  [Union]
                                                  public struct FieldBackedAmount : IUnion
                                                  {
                                                      public FieldBackedAmount(int number) => Value = number;
                                                      public FieldBackedAmount(string text) => Value = text;
                                                      public object? Value { get => field ?? 0; private set; }
                                                  }
                                              }
                                              """;

    private const string AutoValueUses = """
                                         #nullable enable
                                         using Custom;

                                         public static class Amounts
                                         {
                                             public static BoxedAmount ByDefaultLiteral() => {|FP0404:default|};
                                             public static BoxedAmount ByDefaultExpression() => {|FP0404:default(BoxedAmount)|};
                                             public static BoxedAmount ByConstructor() => {|FP0404:new BoxedAmount()|};
                                             public static BoxedAmount ByTargetTypedConstructor() => {|FP0404:new()|};
                                         }
                                         """;

    private const string ComputedValueUses = """
                                              #nullable enable
                                              using Custom;

                                              public static class Amounts
                                              {
                                                  public static Amount ByDefaultLiteral() => default;
                                                  public static Amount ByDefaultExpression() => default(Amount);
                                                  public static Amount ByConstructor() => new Amount();
                                                  public static Amount ByTargetTypedConstructor() => new();

                                                  public static int Evaluate(Amount amount) => amount switch
                                                  {
                                                      int number => number,
                                                      string text => text.Length
                                                  };

                                                  public static FieldBackedAmount BackedByDefaultLiteral() => default;
                                                  public static FieldBackedAmount BackedByDefaultExpression() => default(FieldBackedAmount);
                                                  public static FieldBackedAmount BackedByConstructor() => new FieldBackedAmount();
                                                  public static FieldBackedAmount BackedByTargetTypedConstructor() => new();
                                              }
                                              """;

    [TestMethod]
    public Task FP0404_DefaultLiteral_Reports()
    {
        return VerifyAsync("public static class Shelter { public static Pet Empty => {|FP0404:default|}; }" + Pets);
    }

    [TestMethod]
    public Task FP0404_DefaultExpression_Reports()
    {
        return VerifyAsync("public static class Shelter { public static Pet Empty => {|FP0404:default(Pet)|}; }" + Pets);
    }

    [TestMethod]
    public Task FP0404_ImplicitParameterlessConstructor_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Shelter { public static Pet Empty => {|FP0404:new Pet()|}; }
                           """ + Pets);
    }

    [TestMethod]
    public Task FP0404_TargetTypedParameterlessConstructor_Reports()
    {
        return VerifyAsync("public static class Shelter { public static Pet Empty => {|FP0404:new()|}; }" + Pets);
    }

    [TestMethod]
    public Task FP0404_EmptyGenericUnion_Reports()
    {
        return VerifyAsync("""
                           public union Choice<T>(T, string);

                           public static class Choices
                           {
                               public static Choice<int> Empty => {|FP0404:new Choice<int>()|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0404_UnionBuiltFromCase_NoReport()
    {
        return VerifyAsync("""
                           public static class Shelter
                           {
                               public static Pet Adopt(Cat cat) => new Pet(cat);
                               public static Pet Found(Dog dog) => dog;
                           }
                           """ + Pets);
    }

    [TestMethod]
    public Task FP0404_NullableClosedAndOrdinaryStructs_NoReport()
    {
        return VerifyAsync("""
                           public closed class Payment;
                           public sealed class Cash : Payment;
                           public readonly record struct Money(int Cents);

                           public static class Values
                           {
                               public static Money Zero => default;
                               public static Money Empty => new Money();
                               public static Payment? MaybePayment => default;
                               public static Pet? Missing => default(Pet?);
                               public static Pet? MissingExplicitly => new Pet?();
                           }
                           """ + Pets);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0404_EmptyReferencedUnionStruct_Reports(bool referenceAssembly)
    {
        return VerifyFromMetadataAsync("""
            using Domain;

            public static class Shelter
            {
                public static Pet ByDefaultLiteral() => {|FP0404:default|};
                public static Pet ByDefaultExpression() => {|FP0404:default(Pet)|};
                public static Pet ByConstructor() => {|FP0404:new Pet()|};
                public static Pet ByTargetTypedConstructor() => {|FP0404:new()|};
            }
            """, ReferencedAssemblySources.Unions, referenceAssembly);
    }

    [TestMethod]
    public Task FP0404_HandWrittenAutoValueProperty_Reports()
    {
        return VerifyAsync(AutoValueUses + HandWrittenUnions);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0404_ReferencedHandWrittenAutoValueProperty_Reports(bool referenceAssembly)
    {
        return VerifyFromMetadataAsync(AutoValueUses, HandWrittenUnions, referenceAssembly);
    }

    [TestMethod]
    public Task FP0404_HandWrittenComputedValueProperty_NoReport()
    {
        return VerifyAsync(ComputedValueUses + HandWrittenUnions);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0404_ReferencedHandWrittenComputedValueProperty_NoReport(bool referenceAssembly)
    {
        return VerifyFromMetadataAsync(ComputedValueUses, HandWrittenUnions, referenceAssembly);
    }

    [TestMethod]
    public Task FP0404_ReferencedClosedAndFilledUnion_NoReport()
    {
        return FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0404", """
            #nullable enable
            using Domain;

            public static class Shelter
            {
                public static Pet Adopt(Cat cat) => new Pet(cat);
                public static Shape? Missing() => default(Shape);
            }
            """, ReferencedAssemblySources.Unions, testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0404_GeneratedCode_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           public static class Shelter { public static Pet Empty => default; }
                           """ + Pets);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0404", source, testContext.CancellationToken);
    }

    private Task VerifyFromMetadataAsync(string source, string librarySource, bool referenceAssembly)
    {
        return referenceAssembly
            ? FpAnalyzerTestHost.VerifyRuleWithReferenceAssemblyAsync(
                "FP0404", source, librarySource, testContext.CancellationToken)
            : FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync(
                "FP0404", source, librarySource, testContext.CancellationToken);
    }
}
