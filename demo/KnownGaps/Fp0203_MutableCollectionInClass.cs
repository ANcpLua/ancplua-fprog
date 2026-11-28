using System.Collections.Generic;

namespace Demo.Fp0203_MutableCollectionInClass;

// E1: the same members in a record are reported. The caller keeps the list it passed in and can change it.
[Fprog.FunctionalCore]
public sealed class Cart(List<int> items)
{
    public List<int> Items { get; } = items;
    public IReadOnlyList<int> View => Items;
}
