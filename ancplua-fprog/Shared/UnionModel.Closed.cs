using Microsoft.CodeAnalysis;

namespace ancplua_fprog.Shared;

internal sealed partial class UnionModel
{
    /// <summary>
    ///     True when no value of <paramref name="union" /> (a union definition) can exist other than a value of
    ///     one of its cases, as far as the symbol shows: the union is abstract, so <c>new Shape()</c> is
    ///     impossible, and it has no visible instance constructor that is not private, so no type outside the
    ///     union can derive from it. An implicit constructor counts as declared (protected for an abstract
    ///     class). This also works for a union from a referenced assembly: with default import options a private
    ///     constructor of a metadata type is not imported at all, so only the non-private ones are seen.
    ///     <para>
    ///         The copy constructor of a record (one parameter of the record's own type) does not count. A record
    ///         cannot make it private (CS8878), so counting it would make every record union open. That leaves a
    ///         known hole: another assembly can write
    ///         <c>record Extra : Shape { public Extra(Shape original) : base(original) { } }</c> and create a value no
    ///         arm handles. FP0403 reports such a type in its own project.
    ///     </para>
    /// </summary>
    public static bool IsClosed(INamedTypeSymbol union)
    {
        var definition = union.OriginalDefinition;
        if (!definition.IsAbstract)
            return false;

        foreach (var constructor in definition.InstanceConstructors)
            if (constructor.DeclaredAccessibility != Accessibility.Private &&
                !IsRecordCopyConstructor(definition, constructor))
                return false;

        return true;
    }

    private static bool IsRecordCopyConstructor(INamedTypeSymbol record, IMethodSymbol constructor)
    {
        return record.IsRecord
               && constructor.Parameters.Length == 1
               && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type.OriginalDefinition, record)
               && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, record);
    }
}