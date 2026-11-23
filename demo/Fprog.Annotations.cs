namespace Fprog
{
    [System.AttributeUsage(System.AttributeTargets.Assembly | System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public sealed class FunctionalCoreAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class UnionAttribute : System.Attribute { }
}
