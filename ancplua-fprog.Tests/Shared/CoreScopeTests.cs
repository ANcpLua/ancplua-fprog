using System.Linq;
using System.Threading;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Shared;

[TestClass]
public sealed class CoreScopeTests
{
    private const string MembersSource = """
                                         namespace Domain
                                         {
                                             [Fprog.FunctionalCore]
                                             public sealed class Pricing
                                             {
                                                 private readonly int _rate = 2;

                                                 public int Total(int price)
                                                 {
                                                     int Twice(int value) => value * 2;
                                                     System.Func<int, int> addRate = amount => amount + _rate;
                                                     return addRate(Twice(price));
                                                 }
                                             }

                                             public sealed class Shell
                                             {
                                                 private readonly int _rate = 2;

                                                 public int Total(int price)
                                                 {
                                                     int Twice(int value) => value * 2;
                                                     System.Func<int, int> addRate = amount => amount + _rate;
                                                     return addRate(Twice(price));
                                                 }
                                             }
                                         }
                                         """;

    private const string RecordsSource = """
                                         namespace Domain
                                         {
                                             public record Circle(double Radius);

                                             public record Labelled(string Label)
                                             {
                                                 public void Deconstruct(out string label, out int length)
                                                 {
                                                     label = Label;
                                                     length = Label.Length;
                                                 }

                                                 public override string ToString() => Label;
                                             }

                                             public class Plain
                                             {
                                                 public int Auto { get; set; }
                                             }

                                             public class Closed
                                             {
                                                 private Closed() { }
                                             }
                                         }
                                         """;

    [TestMethod]
    public void TypeWithTheAttribute_IsInCore()
    {
        var compilation = TestCompilation.Create("""
                                                 namespace Domain
                                                 {
                                                     [Fprog.FunctionalCore]
                                                     public sealed class Pricing { }
                                                 }
                                                 """);

        Assert.IsTrue(CoreScope.IsInCore(compilation.GetNamedType("Domain.Pricing")));
    }

    [TestMethod]
    public void TypeWithoutTheAttribute_IsNotInCore()
    {
        var compilation = TestCompilation.Create("""
                                                 namespace Domain
                                                 {
                                                     [Fprog.FunctionalCore]
                                                     public sealed class Pricing { }

                                                     public sealed class Shell
                                                     {
                                                         public sealed class Nested { }
                                                     }
                                                 }
                                                 """);

        Assert.IsFalse(CoreScope.IsInCore(compilation.GetNamedType("Domain.Shell")));
        Assert.IsFalse(CoreScope.IsInCore(compilation.GetNamedType("Domain.Shell+Nested")));
    }

    [TestMethod]
    public void TypeNestedTwoLevelsInACoreType_IsInCore()
    {
        var compilation = TestCompilation.Create("""
                                                 namespace Domain
                                                 {
                                                     [Fprog.FunctionalCore]
                                                     public sealed class Outer
                                                     {
                                                         public sealed class Middle
                                                         {
                                                             public sealed class Inner { }
                                                         }
                                                     }
                                                 }
                                                 """);

        Assert.IsTrue(CoreScope.IsInCore(compilation.GetNamedType("Domain.Outer+Middle")));
        Assert.IsTrue(CoreScope.IsInCore(compilation.GetNamedType("Domain.Outer+Middle+Inner")));
    }

    [TestMethod]
    public void TypeThatContainsACoreType_IsNotInCore()
    {
        var compilation = TestCompilation.Create("""
                                                 namespace Domain
                                                 {
                                                     public sealed class Shell
                                                     {
                                                         [Fprog.FunctionalCore]
                                                         public sealed class Pricing { }
                                                     }
                                                 }
                                                 """);

        Assert.IsFalse(CoreScope.IsInCore(compilation.GetNamedType("Domain.Shell")));
        Assert.IsTrue(CoreScope.IsInCore(compilation.GetNamedType("Domain.Shell+Pricing")));
    }

    [TestMethod]
    public void AssemblyLevelAttribute_PutsEveryTypeInCore()
    {
        var compilation = TestCompilation.Create("""
                                                 [assembly: Fprog.FunctionalCore]

                                                 namespace Domain
                                                 {
                                                     public sealed class Pricing
                                                     {
                                                         public int Total(int price) => price;

                                                         public sealed class Nested { }
                                                     }

                                                     public readonly struct Money { }
                                                 }
                                                 """);

        var pricing = compilation.GetNamedType("Domain.Pricing");
        Assert.IsTrue(CoreScope.IsInCore(pricing));
        Assert.IsTrue(CoreScope.IsInCore(pricing.GetMembers("Total").Single()));
        Assert.IsTrue(CoreScope.IsInCore(compilation.GetNamedType("Domain.Pricing+Nested")));
        Assert.IsTrue(CoreScope.IsInCore(compilation.GetNamedType("Domain.Money")));
    }

    [TestMethod]
    public void AttributeWithTheSameSimpleNameButAnotherFullName_DoesNotCount()
    {
        var compilation = TestCompilation.Create("""
                                                 [assembly: Other.FunctionalCore]

                                                 namespace Other
                                                 {
                                                     public sealed class FunctionalCoreAttribute : System.Attribute { }
                                                 }

                                                 namespace Outer.Fprog
                                                 {
                                                     public sealed class FunctionalCoreAttribute : System.Attribute { }
                                                 }

                                                 namespace Fprog
                                                 {
                                                     public static class Holder
                                                     {
                                                         public sealed class FunctionalCoreAttribute : System.Attribute { }
                                                     }

                                                     public sealed class FunctionalCoreAttribute<T> : System.Attribute { }
                                                 }

                                                 namespace Domain
                                                 {
                                                     [Other.FunctionalCore]
                                                     public sealed class OtherNamespace { }

                                                     [Outer.Fprog.FunctionalCore]
                                                     public sealed class NestedNamespace { }

                                                     [Fprog.Holder.FunctionalCore]
                                                     public sealed class NestedAttributeType { }

                                                     [Fprog.FunctionalCore<int>]
                                                     public sealed class GenericAttribute { }
                                                 }
                                                 """);

        Assert.IsFalse(CoreScope.IsInCore(compilation.GetNamedType("Domain.OtherNamespace")));
        Assert.IsFalse(CoreScope.IsInCore(compilation.GetNamedType("Domain.NestedNamespace")));
        Assert.IsFalse(CoreScope.IsInCore(compilation.GetNamedType("Domain.NestedAttributeType")));
        Assert.IsFalse(CoreScope.IsInCore(compilation.GetNamedType("Domain.GenericAttribute")));
    }

    [TestMethod]
    public void MemberAndParameter_InACoreType_AreInCore()
    {
        var symbols = Symbols(TestCompilation.Create(MembersSource), "Pricing");

        Assert.IsTrue(CoreScope.IsInCore(symbols.Field));
        Assert.IsTrue(CoreScope.IsInCore(symbols.Method));
        Assert.IsTrue(CoreScope.IsInCore(symbols.Parameter));
    }

    [TestMethod]
    public void LocalFunction_InACoreType_IsInCore()
    {
        var symbols = Symbols(TestCompilation.Create(MembersSource), "Pricing");

        Assert.AreEqual(MethodKind.LocalFunction, symbols.LocalFunction.MethodKind);
        Assert.IsTrue(CoreScope.IsInCore(symbols.LocalFunction));
    }

    [TestMethod]
    public void LambdaAndItsParameter_InACoreType_AreInCore()
    {
        var symbols = Symbols(TestCompilation.Create(MembersSource), "Pricing");

        Assert.AreEqual(MethodKind.AnonymousFunction, symbols.Lambda.MethodKind);
        Assert.IsTrue(CoreScope.IsInCore(symbols.Lambda));
        Assert.IsTrue(CoreScope.IsInCore(symbols.LambdaParameter));
    }

    [TestMethod]
    public void SameMembers_InATypeOutsideCore_AreNotInCore()
    {
        var symbols = Symbols(TestCompilation.Create(MembersSource), "Shell");

        Assert.IsFalse(CoreScope.IsInCore(symbols.Field));
        Assert.IsFalse(CoreScope.IsInCore(symbols.Method));
        Assert.IsFalse(CoreScope.IsInCore(symbols.Parameter));
        Assert.IsFalse(CoreScope.IsInCore(symbols.LocalFunction));
        Assert.IsFalse(CoreScope.IsInCore(symbols.Lambda));
        Assert.IsFalse(CoreScope.IsInCore(symbols.LambdaParameter));
    }

    [TestMethod]
    public void SymbolOutsideAnyType_IsNotInCore()
    {
        var compilation = TestCompilation.Create("""
                                                 [assembly: Fprog.FunctionalCore]

                                                 namespace Domain
                                                 {
                                                     public sealed class Pricing { }
                                                 }
                                                 """);

        Assert.IsFalse(CoreScope.IsInCore(compilation.GetNamedType("Domain.Pricing").ContainingNamespace));
        Assert.IsFalse(CoreScope.IsInCore(null));
    }

    [TestMethod]
    public void PartialType_WithTheAttributeOnAPartInAnotherFile_IsInCore()
    {
        var compilation = TestCompilation.Create(
        [
            """
            namespace Domain
            {
                public sealed partial class Pricing
                {
                    public int Total(int price) => price;
                }
            }
            """,
            """
            namespace Domain
            {
                [Fprog.FunctionalCore]
                public sealed partial class Pricing
                {
                    public int Half(int price) => price / 2;
                }
            }
            """
        ]);
        var pricing = compilation.GetNamedType("Domain.Pricing");

        Assert.IsTrue(CoreScope.IsInCore(pricing));
        Assert.IsTrue(CoreScope.IsInCore(pricing.GetMembers("Total").Single()));
        Assert.IsTrue(CoreScope.IsInCore(pricing.GetMembers("Half").Single()));
    }

    [TestMethod]
    public void ConstructedGenericCoreType_AndATypeNestedInIt_AreInCore()
    {
        var compilation = TestCompilation.Create("""
                                                 namespace Domain
                                                 {
                                                     [Fprog.FunctionalCore]
                                                     public sealed class Box<T>
                                                     {
                                                         public T Unwrap(T value) => value;

                                                         public sealed class Lid { }
                                                     }

                                                     public sealed class Crate<T>
                                                     {
                                                         public sealed class Lid { }
                                                     }

                                                     public static class Uses
                                                     {
                                                         public static Box<int> Box => new();

                                                         public static Box<int>.Lid BoxLid => new();

                                                         public static Crate<int>.Lid CrateLid => new();
                                                     }
                                                 }
                                                 """);
        var uses = compilation.GetNamedType("Domain.Uses");
        var boxOfInt = PropertyType(uses, "Box");

        Assert.IsFalse(boxOfInt.IsDefinition);
        Assert.IsTrue(CoreScope.IsInCore(boxOfInt));
        Assert.IsTrue(CoreScope.IsInCore(boxOfInt.GetMembers("Unwrap").Single()));
        Assert.IsTrue(CoreScope.IsInCore(PropertyType(uses, "BoxLid")));
        Assert.IsFalse(CoreScope.IsInCore(PropertyType(uses, "CrateLid")));
    }

    [TestMethod]
    public void TypeParameters_OfACoreTypeAndOfItsMethods_AreInCore()
    {
        var compilation = TestCompilation.Create("""
                                                 namespace Domain
                                                 {
                                                     [Fprog.FunctionalCore]
                                                     public sealed class Box<T>
                                                     {
                                                         public TResult Map<TResult>(TResult value) => value;
                                                     }

                                                     public sealed class Crate<T>
                                                     {
                                                         public TResult Map<TResult>(TResult value) => value;
                                                     }
                                                 }
                                                 """);
        var box = compilation.GetNamedType("Domain.Box`1");
        var crate = compilation.GetNamedType("Domain.Crate`1");

        Assert.IsTrue(CoreScope.IsInCore(box.TypeParameters.Single()));
        Assert.IsTrue(
            CoreScope.IsInCore(box.GetMembers("Map").OfType<IMethodSymbol>().Single().TypeParameters.Single()));
        Assert.IsFalse(CoreScope.IsInCore(crate.TypeParameters.Single()));
        Assert.IsFalse(CoreScope.IsInCore(crate.GetMembers("Map").OfType<IMethodSymbol>().Single().TypeParameters
            .Single()));
    }

    [TestMethod]
    public void LocalAndProperty_InACoreType_AreInCore()
    {
        var compilation = TestCompilation.Create("""
                                                 namespace Domain
                                                 {
                                                     [Fprog.FunctionalCore]
                                                     public sealed class Pricing
                                                     {
                                                         public int Rate { get; init; }

                                                         public int Total(int price)
                                                         {
                                                             var doubled = price * 2;
                                                             return doubled + Rate;
                                                         }
                                                     }

                                                     public sealed class Shell
                                                     {
                                                         public int Rate { get; init; }

                                                         public int Total(int price)
                                                         {
                                                             var doubled = price * 2;
                                                             return doubled + Rate;
                                                         }
                                                     }
                                                 }
                                                 """);

        foreach (var (typeName, inCore) in new[] { ("Pricing", true), ("Shell", false) })
        {
            var rate = compilation.GetNamedType($"Domain.{typeName}").GetMembers("Rate").OfType<IPropertySymbol>()
                .Single();
            var local = Locals(compilation, typeName).Single();

            Assert.AreEqual(SymbolKind.Local, local.Kind, typeName);
            Assert.AreEqual(inCore, CoreScope.IsInCore(local), typeName);
            Assert.AreEqual(inCore, CoreScope.IsInCore(rate), typeName);
            Assert.AreEqual(inCore, CoreScope.IsInCore(rate.GetMethod), typeName);
            Assert.AreEqual(inCore, CoreScope.IsInCore(rate.SetMethod), typeName);
        }
    }

    [TestMethod]
    public void AssemblyLevelAttribute_InAReferencedAssembly_PutsItsTypesInCore()
    {
        var library = TestCompilation.Create("""
                                             [assembly: Fprog.FunctionalCore]

                                             namespace Lib
                                             {
                                                 public sealed class Pricing
                                                 {
                                                     public int Total(int price) => price;
                                                 }
                                             }
                                             """, "Library");
        var compilation = TestCompilation.Create("""
                                                 namespace App
                                                 {
                                                     public sealed class Shell
                                                     {
                                                         public int Total(Lib.Pricing pricing) => pricing.Total(1);
                                                     }
                                                 }
                                                 """, "App", TestCompilation.ToImageReference(library));
        var pricing = compilation.GetNamedType("Lib.Pricing");

        Assert.IsTrue(CoreScope.IsInCore(pricing));
        Assert.IsTrue(CoreScope.IsInCore(pricing.GetMembers("Total").Single()));
        Assert.IsFalse(CoreScope.IsInCore(compilation.GetNamedType("App.Shell")));
    }

    [TestMethod]
    public void PositionalRecord_OnlyItsPropertyAndPrimaryConstructorAreDeclaredInSource()
    {
        var circle = TestCompilation.Create(RecordsSource).GetNamedType("Domain.Circle");
        var declared = circle.GetMembers().Where(CoreScope.IsDeclaredInSource)
            .Select(member => member.ToDisplayString()).ToArray();
        var primaryConstructor =
            circle.InstanceConstructors.Single(constructor => constructor.Parameters.Single().Name == "Radius");
        var radius = circle.GetMembers("Radius").Single();

        Assert.AreSequenceEqual(["Domain.Circle.Circle(double)", "Domain.Circle.Radius"], declared);
        Assert.IsTrue(CoreScope.IsDeclaredInSource(primaryConstructor.Parameters.Single()));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(circle));
        // FP0203 reports a positional property at the parameter name: the parameter is its only syntax.
        Assert.IsInstanceOfType<ParameterSyntax>(Assert.ContainsSingle(radius.DeclaringSyntaxReferences).GetSyntax());
    }

    [TestMethod]
    [DataRow("Deconstruct")]
    [DataRow("EqualityContract")]
    [DataRow("ToString")]
    [DataRow("PrintMembers")]
    [DataRow("Equals")]
    [DataRow("GetHashCode")]
    [DataRow("op_Equality")]
    [DataRow("op_Inequality")]
    [DataRow("<Clone>$")]
    [DataRow("<Radius>k__BackingField")]
    [DataRow("get_Radius")]
    [DataRow("set_Radius")]
    public void PositionalRecord_SynthesizedMember_IsNotDeclaredInSource(string memberName)
    {
        var members = TestCompilation.Create(RecordsSource).GetNamedType("Domain.Circle").GetMembers(memberName);

        Assert.IsNotEmpty(members);
        foreach (var member in members)
            Assert.IsFalse(CoreScope.IsDeclaredInSource(member), member.ToDisplayString());
    }

    [TestMethod]
    public void PositionalRecord_CopyConstructor_IsNotDeclaredInSource()
    {
        var circle = TestCompilation.Create(RecordsSource).GetNamedType("Domain.Circle");
        var copyConstructor =
            circle.InstanceConstructors.Single(constructor => constructor.Parameters.Single().Name != "Radius");

        Assert.AreEqual(circle, copyConstructor.Parameters.Single().Type, SymbolEqualityComparer.Default);
        Assert.IsFalse(CoreScope.IsDeclaredInSource(copyConstructor));
        Assert.IsFalse(CoreScope.IsDeclaredInSource(copyConstructor.Parameters.Single()));
    }

    [TestMethod]
    public void SynthesizedDeconstruct_ItsOutParameterIsNotDeclaredInSource()
    {
        var circle = TestCompilation.Create(RecordsSource).GetNamedType("Domain.Circle");
        var parameter = circle.GetMembers("Deconstruct").OfType<IMethodSymbol>().Single().Parameters.Single();

        // Roslyn calls the parameter of the synthesized method explicitly declared, but it has no syntax.
        Assert.AreEqual(RefKind.Out, parameter.RefKind);
        Assert.IsFalse(parameter.IsImplicitlyDeclared);
        Assert.IsFalse(CoreScope.IsDeclaredInSource(parameter));
    }

    [TestMethod]
    public void RecordMembersWrittenInSource_AreDeclaredInSource()
    {
        var labelled = TestCompilation.Create(RecordsSource).GetNamedType("Domain.Labelled");
        var deconstructs = labelled.GetMembers("Deconstruct").OfType<IMethodSymbol>().ToArray();
        var written = deconstructs.Single(method => method.Parameters.Length == 2);
        var synthesized = deconstructs.Single(method => method.Parameters.Length == 1);

        Assert.IsTrue(CoreScope.IsDeclaredInSource(written));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(written.Parameters[0]));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(labelled.GetMembers("ToString").Single()));
        Assert.IsFalse(CoreScope.IsDeclaredInSource(synthesized));
    }

    [TestMethod]
    public void ImplicitDefaultConstructor_IsNotDeclaredInSource()
    {
        var compilation = TestCompilation.Create(RecordsSource);

        Assert.IsFalse(
            CoreScope.IsDeclaredInSource(compilation.GetNamedType("Domain.Plain").InstanceConstructors.Single()));
        Assert.IsTrue(
            CoreScope.IsDeclaredInSource(compilation.GetNamedType("Domain.Closed").InstanceConstructors.Single()));
    }

    [TestMethod]
    public void AutoProperty_IsDeclaredInSourceButItsBackingFieldIsNot()
    {
        var plain = TestCompilation.Create(RecordsSource).GetNamedType("Domain.Plain");
        var auto = plain.GetMembers("Auto").OfType<IPropertySymbol>().Single();
        var backingField = plain.GetMembers().OfType<IFieldSymbol>().Single();

        Assert.AreEqual(auto, backingField.AssociatedSymbol, SymbolEqualityComparer.Default);
        Assert.IsFalse(CoreScope.IsDeclaredInSource(backingField));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(auto));
        Assert.IsNotNull(auto.GetMethod);
        Assert.IsNotNull(auto.SetMethod);
        Assert.IsTrue(CoreScope.IsDeclaredInSource(auto.GetMethod));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(auto.SetMethod));
    }

    [TestMethod]
    public void SymbolsWrittenInAMethodBody_AreDeclaredInSource()
    {
        var symbols = Symbols(TestCompilation.Create(MembersSource), "Pricing");

        Assert.IsTrue(CoreScope.IsDeclaredInSource(symbols.Field));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(symbols.Method));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(symbols.Parameter));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(symbols.LocalFunction));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(symbols.Lambda));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(symbols.LambdaParameter));
    }

    [TestMethod]
    public void TopLevelStatements_TheirEntryPointIsNotDeclaredInSource()
    {
        var compilation = TestCompilation.CreateProgram("""
                                                        int Twice(int value) => value * 2;

                                                        System.Console.WriteLine(Twice(1));
                                                        """);
        var entryPoint = compilation.GetEntryPoint(CancellationToken.None);
        var tree = compilation.SyntaxTrees.First();
        var localFunction = compilation.GetSemanticModel(tree)
            .GetDeclaredSymbol(tree.GetRoot().DescendantNodes().OfType<LocalFunctionStatementSyntax>().Single());

        // Roslyn calls the synthesized method explicitly declared; its syntax is the whole file.
        Assert.IsNotNull(entryPoint);
        Assert.AreEqual(WellKnownMemberNames.TopLevelStatementsEntryPointMethodName, entryPoint.Name);
        Assert.IsFalse(entryPoint.IsImplicitlyDeclared);
        Assert.IsFalse(CoreScope.IsDeclaredInSource(entryPoint));
        Assert.IsNotNull(localFunction);
        Assert.IsTrue(CoreScope.IsDeclaredInSource(localFunction));
    }

    [TestMethod]
    public void TopLevelStatements_TheirClassIsDeclaredInSourceOnlyWhenWrittenOut()
    {
        var synthesized = TestCompilation.CreateProgram("System.Console.WriteLine(1);").GetNamedType("Program");
        var writtenOut = TestCompilation.CreateProgram("""
                                                       System.Console.WriteLine(Program.Answer);

                                                       public partial class Program
                                                       {
                                                           public const int Answer = 42;
                                                       }
                                                       """).GetNamedType("Program");

        // Roslyn calls the synthesized class explicitly declared; its syntax is the whole file.
        Assert.IsFalse(synthesized.IsImplicitlyDeclared);
        Assert.IsFalse(CoreScope.IsDeclaredInSource(synthesized));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(writtenOut));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(writtenOut.GetMembers("Answer").Single()));
    }

    [TestMethod]
    public void SetterValueAndIndexerParameterInAnAccessor_AreNotDeclaredInSource()
    {
        var compilation = TestCompilation.Create("""
                                                 namespace Domain
                                                 {
                                                     public sealed class Table
                                                     {
                                                         private int _last;

                                                         public int Last
                                                         {
                                                             get => _last;
                                                             set => _last = value;
                                                         }

                                                         public int this[int index] => index + _last;
                                                     }
                                                 }
                                                 """);
        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);
        var table = compilation.GetNamedType("Domain.Table");
        var indexer = table.GetMembers().OfType<IPropertySymbol>().Single(property => property.IsIndexer);
        var used = tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(name => name.Identifier.Text is "value" or "index")
            .Select(name => Expect<IParameterSymbol>(model.GetSymbolInfo(name).Symbol, name))
            .ToArray();

        Assert.HasCount(2, used);
        foreach (var parameter in used)
            Assert.IsFalse(CoreScope.IsDeclaredInSource(parameter), parameter.ToDisplayString());

        Assert.IsTrue(CoreScope.IsDeclaredInSource(indexer.Parameters.Single()));
    }

    [TestMethod]
    public void SymbolFromAReferencedProject_IsDeclaredInSource()
    {
        var library = TestCompilation.Create("""
                                             namespace Lib
                                             {
                                                 public sealed class Pricing
                                                 {
                                                     public int Total(int price) => price;
                                                 }
                                             }
                                             """, "Library");
        var compilation = TestCompilation.Create("""
                                                 namespace App
                                                 {
                                                     public sealed class Shell { }
                                                 }
                                                 """, "App", library.ToMetadataReference());
        var pricing = compilation.GetNamedType("Lib.Pricing");

        Assert.IsTrue(CoreScope.IsDeclaredInSource(pricing));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(pricing.GetMembers("Total").Single()));
        // A rule that means this compilation only compares the assembly as well.
        Assert.AreNotEqual(compilation.Assembly, pricing.ContainingAssembly, SymbolEqualityComparer.Default);
    }

    [TestMethod]
    public void SymbolFromACompiledAssembly_IsNotDeclaredInSource()
    {
        var library = TestCompilation.Create("""
                                             namespace Lib
                                             {
                                                 public sealed class Pricing
                                                 {
                                                     public int Total(int price) => price;
                                                 }
                                             }
                                             """, "Library");
        var compilation = TestCompilation.Create("""
                                                 namespace App
                                                 {
                                                     public sealed class Shell { }
                                                 }
                                                 """, "App", TestCompilation.ToImageReference(library));
        var pricing = compilation.GetNamedType("Lib.Pricing");
        var total = pricing.GetMembers("Total").Single();

        Assert.IsFalse(total.IsImplicitlyDeclared);
        Assert.IsFalse(CoreScope.IsDeclaredInSource(total));
        Assert.IsFalse(CoreScope.IsDeclaredInSource(pricing));
        Assert.IsTrue(CoreScope.IsDeclaredInSource(compilation.GetNamedType("App.Shell")));
    }

    private static INamedTypeSymbol PropertyType(INamedTypeSymbol type, string propertyName)
    {
        var propertyType = type.GetMembers(propertyName).OfType<IPropertySymbol>().Single().Type as INamedTypeSymbol;
        Assert.IsNotNull(propertyType, $"'{propertyName}' has no named type.");
        return propertyType;
    }

    private static ISymbol[] Locals(Compilation compilation, string typeName)
    {
        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);
        return tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(node => node.Identifier.Text == typeName)
            .DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Select(declarator => Expect<ISymbol>(model.GetDeclaredSymbol(declarator), declarator))
            .ToArray();
    }

    private static MemberSymbols Symbols(Compilation compilation, string typeName)
    {
        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);
        var type = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(node => node.Identifier.Text == typeName);
        var field = type.Members.OfType<FieldDeclarationSyntax>().Single().Declaration.Variables.Single();
        var method = type.Members.OfType<MethodDeclarationSyntax>().Single();
        var lambda = method.DescendantNodes().OfType<SimpleLambdaExpressionSyntax>().Single();

        return new MemberSymbols(
            Expect<IFieldSymbol>(model.GetDeclaredSymbol(field), field),
            Expect<IMethodSymbol>(model.GetDeclaredSymbol(method), method),
            Expect<IParameterSymbol>(model.GetDeclaredSymbol(method.ParameterList.Parameters.Single()), method),
            Expect<IMethodSymbol>(
                model.GetDeclaredSymbol(method.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Single()),
                method),
            Expect<IMethodSymbol>(model.GetSymbolInfo(lambda).Symbol, lambda),
            Expect<IParameterSymbol>(model.GetDeclaredSymbol(lambda.Parameter), lambda));
    }

    private static TSymbol Expect<TSymbol>(ISymbol? symbol, SyntaxNode node)
        where TSymbol : class, ISymbol
    {
        var expected = symbol as TSymbol;
        Assert.IsNotNull(expected, $"No {typeof(TSymbol).Name} found in '{node}'.");
        return expected;
    }

    private sealed record MemberSymbols(
        IFieldSymbol Field,
        IMethodSymbol Method,
        IParameterSymbol Parameter,
        IMethodSymbol LocalFunction,
        IMethodSymbol Lambda,
        IParameterSymbol LambdaParameter);
}