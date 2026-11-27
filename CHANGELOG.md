# Changelog

What each release changes for a project that references the package. The exact definition of every rule is the
rule catalog in [AGENTS.md](AGENTS.md).

## 0.7.0 (unreleased)

* FP0502 checks switches against the most derived class constraint of a type parameter. When the bounds are
  `Shape` and its closed subclass `Polygon`, only polygon cases need labels, in either constraint order.
  If the most derived class is open, sealed or has an incomplete case list, FP0601 applies its ordinary
  type-parameter checks in the functional core.

## 0.6.0

* FP0106 reports each string key in a LINQ to Objects `orderby` that uses the default string comparer. Use `OrderBy` or
  `ThenBy` and pass `StringComparer.Ordinal` for culture-independent ordering.
* FP0106 also identifies `Comparer<string>.Default` when passed explicitly to a checked sorting or search method.
  Sorting a span with `StringComparer.Ordinal` or a supplied comparer is accepted.
* FP0302 accepts one-argument scalar `System.Convert` overloads that succeed for every input of the declared
  type, in both calls and method groups. `ToInt32(byte)` is accepted. `ToInt32(long)` is reported because it
  can overflow.
* FP0701 counts a captured variable passed as the writable `ref` receiver of an extension method, property or
  indexer. A setter that reassigns a by-value struct receiver changes its local copy.

## 0.5.0

* FP0801 and FP0802 report an extension method of a Shell or Async class that a clause of a query expression
  binds, as in `from line in input select line.Length` with a `Select` extension outside the core.

## 0.4.0

* A type parameter constrained to a closed class that is also a hand-written union is matched by the subtypes of
  that class. FP0502 asks for those subtypes, and where the compiler cannot list them, FP0601 applies in place of
  FP0501, FP0502 and FP0503.
* FP0502 reports a `switch` statement on a `union` value behind a cast, as in `switch ((object)choice)`: there
  the labels test the union itself and no case is handled.
* FP0801 and FP0802 report an extension method of a Shell or Async class that a `foreach`, an `await`, a
  deconstruction or a positional pattern binds without naming it.
* FP0501 allows a pattern that names the only case of a union also when that case is `object` or an interface.
* FP0104 and FP0105 follow each element of a tuple local on its own around a cycle of assignments. An element
  that is refilled with a fresh value is no longer reported.

## 0.3.0

* FP0104 and FP0105 follow a value through a tuple conversion and through wrapping in or unwrapping from
  `Nullable<T>`. A write such as `chosen.Values[0] = 0` after
  `(IList<int> Values, int Kind) chosen = useFixed ? (fixedScores, 0) : (openScores, 1);` is now reported.

## 0.2.0

* Faster analysis of functional-core boundaries, mutation through aliases and union cases.
* FP0501, FP0502 and FP0503 leave a constructed `closed` class or record to the compiler when it cannot list all
  cases. FP0601 treats a type parameter constrained to one like other type parameters.
* FP0404 allows default construction of hand-written union structs whose `Value` property is computed, and
  construction with an object initializer that assigns `Value`.
* FP0104 and FP0105 follow a value through a target-typed `?:` or `switch` expression, as in
  `Shape shape = pick ? square : triangle;`. A write through such a local is now reported.
* FP0104 and FP0105 no longer report a write to a loop variable or deconstruction target that a user-defined
  conversion created.
* FP0502 reports an unhandled case also when a label has a catch-all on a property, as in
  `case Paid { Payment: var payment }:`, and accepts an `and` pattern whose sides both cover the case.

## 0.1.0

First release.

* 24 rules for C# 15 on .NET 11, all with category `Functional` and severity `Warning`. Every message says what to
  write instead.
* Inside types marked `[Fprog.FunctionalCore]`, and inside every type of an assembly marked
  `[assembly: Fprog.FunctionalCore]`:
  * purity: FP0101 (no `void` results), FP0102 (no hidden inputs or mutable static state), FP0103 (no `ref` or `out`
    parameters), FP0104 (no writes into memory the caller owns), FP0105 (no state change after construction), FP0106
    (no dependence on the current culture), FP0107 (no pointers or `unsafe`);
  * immutability: FP0201 (fields), FP0202 (set accessors), FP0203 (mutable collections in records and readonly
    structs);
  * totality: FP0301 (no `throw`), FP0302 (no partial operations), FP0303 (no `null`, `default` or `!` for a missing
    result);
  * shape: FP0401 (classes and records are `sealed`, `static` or `closed`), FP0601 (no runtime type tests, no
    `dynamic`), FP0701 (closures capture variables that are assigned once);
  * effects: FP0801 (shell types and `lock` stay outside), FP0802 (asynchronous types and `async` stay outside).
* On every `union` type and `closed` class of the compilation: FP0403 (cases are `sealed` or `closed`), FP0404 (no
  empty union value), FP0501 (no catch-all), FP0502 (a `switch` statement handles every case), FP0503 (a case is
  picked in a `switch`).
* Outside the core: FP0803 (core classes and interfaces are implemented inside the core).
* The package compiles `Fprog.FunctionalCoreAttribute` into the referencing project as an internal type.
  `FPROG_NO_ATTRIBUTES` leaves it out.
* `fprog_pure_apis` in `.editorconfig` or `.globalconfig` names libraries you trust to be pure.
* Requires the .NET 11 SDK 11.0.100 or later; the analyzers are compiled against Roslyn 5.12.0.
