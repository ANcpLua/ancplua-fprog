namespace ancplua_fprog.Tests.Shared;

internal static class ReferencedAssemblySources
{
    public const string Unions = """
                                 namespace Domain;

                                 public interface IAnimal;
                                 public sealed record Cat : IAnimal;
                                 public sealed record Dog : IAnimal;
                                 public union Pet(Cat, Dog);

                                 public closed record Shape;
                                 public sealed record Circle : Shape;
                                 public closed record Polygon : Shape;
                                 public sealed record Square : Polygon;
                                 public sealed record Triangle : Polygon;
                                 """;

    public const string CoreTypes = """
                                    namespace Domain;

                                    [Fprog.FunctionalCore]
                                    public interface ICounter { int Get(); }

                                    [Fprog.FunctionalCore]
                                    public sealed record Count(int Value);

                                    public sealed record ExternalCount(int Value);
                                    """;
}
