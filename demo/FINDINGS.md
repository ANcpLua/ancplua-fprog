# Known gaps

A real bug that the rules do not report goes into `KnownGaps/<Rule>_<Case>.cs`, with one entry here that says why
the rule stays silent and what would close the gap.

The entries below come from a review of commit `eb338ac` on 2026-11-28. Each case has an id (A1, B2, ...). A case
was compiled inside a type marked `[Fprog.FunctionalCore]`, got no finding, and was then run where the entry says
"ran". The run-time results are from SDK 11.0.100 on linux-x64.

Four more cases from the same review are boundaries and stand in "Boundaries" in [AGENTS.md](../AGENTS.md):
B1 `Math.Abs(int.MinValue)`, B10 `checked(a + b)` and B11 `decimal` multiplication fail only at the numeric limit;
D2 returns a sentinel value.

## Totality (FP0302)

### `Fp0302_GuardThatProvesNothing.cs` (A1 to A5)

- **Happens:** the division or the element read runs exactly for the value that makes it fail. A1 divides when the
  divisor is zero, A3 reads when the index is out of range, A4 reads the first element of an empty array. In A2
  the test has an empty body, in A5 it is on another index.
- **Silent because:** a use counts as guarded when any condition before it mentions the divisor, the index or the
  collection's size. Which branch the use is on, and what the condition says, is left to the author.
- **Closes it:** a guard counts only when no failing value satisfies the conditions on the way to the use.

### `Fp0302_PartialMathMembers.cs` (B2, B3)

- **Happens:** ran. `Math.Clamp(5, 10, 1)` throws `ArgumentException`, `Math.DivRem(1, 0)` throws
  `DivideByZeroException`.
- **Silent because:** FP0302 knows a fixed list of partial members, and neither is on it. Division is reported
  only as the `/` and `%` operators.
- **Closes it:** a table with a class for every member of a Pure type that documents an exception, in place of
  the list.

### `Fp0302_PartialTextAndRangeMembers.cs` (B4, B12, B13)

- **Happens:** ran. `"ab".Substring(5)` and `Enumerable.Range(0, -1)` throw `ArgumentOutOfRangeException`,
  `string.Format(CultureInfo.InvariantCulture, "{1}", 1)` throws `FormatException`.
- **Silent because:** none of the three is on the list of partial members.
- **Closes it:** the member table.

### `Fp0302_PartialCollectionMembers.cs` (B5, B6, B7)

- **Happens:** ran. `ToImmutableDictionary` throws `ArgumentException` on a duplicate key,
  `ImmutableArray<T>.RemoveAt` throws `ArgumentOutOfRangeException`, and `ImmutableDictionary<TKey, TValue>.Add`
  throws `ArgumentException` when the key is there with another value.
- **Silent because:** none of the three is on the list of partial members.
- **Closes it:** the member table. The reference pack documents no exception for `RemoveAt`, `SetItem` and
  `Insert` of the immutable collections, so their rows are written by hand.

### `Fp0302_ThrowingConstructors.cs` (B8, B9)

- **Happens:** ran. `new DateTime(2026, 2, 30)` throws `ArgumentOutOfRangeException`, `new Uri("not a uri")` throws
  `UriFormatException`.
- **Silent because:** the list of partial members holds methods, properties and operators, and no constructors.
- **Closes it:** the member table, which also holds constructors.

### `Fp0302_DefaultImmutableArray.cs` (B16, B17)

- **Happens:** ran. `default(ImmutableArray<decimal>)` wraps a null array: `Length` and a list pattern throw
  `NullReferenceException`.
- **Silent because:** FP0404 reports the empty value of a union, FP0303 reports `default` as a result. `default`
  as an argument for an `ImmutableArray<T>` is neither.
- **Closes it:** a rule that reports creating the default `ImmutableArray<T>` in the forms FP0404 knows.

## Purity (FP0102, FP0106, FP0801)

### `Fp0102_TimeZoneBehindConversion.cs` (C1 to C5)

- **Happens:** ran for C1, C3 and C4 with `TZ=Europe/Vienna` and with `TZ=UTC` on 2026-07-01 12:00: the offset is
  `02:00` or `00:00`, `IsDaylightSavingTime` is `True` or `False`, `ConvertTimeToUtc` gives `10:00` or `12:00`. C2 is
  the conversion of C1 in implicit form. C5 reads the machine's time-zone database.
- **Silent because:** FP0102 knows the members that name the local time zone. These five use it without naming it.
- **Closes it:** the member table, with a class for members whose result depends on something that is not an
  argument.

### `Fp0102_PlatformConstant.cs` (C7)

- **Happens:** `BitConverter.IsLittleEndian` depends on the processor.
- **Silent because:** it is a readonly static field of a Pure type, and not on the FP0102 list.
- **Closes it:** the member table.

### `Fp0102_HashOrder.cs` (C9, C10)

- **Happens:** ran three times in separate processes. `string.Join(",", names)` over an `ImmutableHashSet<string>`
  gave three orders, and the first key of an `ImmutableDictionary<string, int>` differed each time. A set of
  `(int, int)` tuples and a `FrozenSet` of records with a string member behave the same.
- **Silent because:** FP0102 reports calls to `GetHashCode`. Enumerating a collection that is ordered by hash codes
  calls nothing the rule knows.
- **Closes it:** a rule that reports enumerating a hash-ordered collection whose key hash changes between
  processes, unless the use gives the same result for every order.

### `Fp0106_SortedCollectionDefaultComparer.cs` (S1 to S3)

- **Happens:** ran. `ImmutableSortedSet.Create("z", "ä", "a", "B")` enumerates as `a,B,z,ä` under sv-SE and as
  `a,ä,B,z` under de-DE. With `StringComparer.Ordinal` it is `B,a,z,ä` everywhere.
- **Silent because:** FP0106 covers `OrderBy`, `Sort` and `BinarySearch` with the default comparer. Creating a
  sorted collection of strings without a comparer is not on its list.
- **Closes it:** FP0106 also reports every way of creating a sorted collection with a string key and no comparer.

### `Fp0801_SerializerAndRegex.cs` (B14, B15, C6)

- **Happens:** `JsonSerializer.Deserialize` throws `JsonException` for malformed text and works by reflection. Ran:
  `Regex.IsMatch("x", "(")` throws `RegexParseException`, and `Regex.IsMatch("I", "i", RegexOptions.IgnoreCase)` is
  `True` under en-US and `False` under tr-TR.
- **Silent because:** `System.Text` counts as Pure with every namespace below it, and that takes in
  `System.Text.Json` and `System.Text.RegularExpressions`.
- **Closes it:** both namespaces become Shell. An owner who wants them in the core names them in `fprog_pure_apis`.

## Immutability (FP0203)

### `Fp0203_MutableStaticCollection.cs` (C0, C8, E2, E3)

- **Happens:** any code outside the core can add to the list or the dictionary, and the core reads the result.
- **Silent because:** FP0203 checks records and readonly structs. The fields are `readonly`, so FP0201 and the
  static-state check of FP0102 pass them.
- **Closes it:** FP0203 applies to the fields and properties of every core type, static ones included.

### `Fp0203_MutableCollectionInClass.cs` (E1)

- **Happens:** the caller keeps the list it passed to the constructor and can change it afterwards.
- **Silent because:** FP0203 checks records and readonly structs, and `Cart` is a sealed class.
- **Closes it:** the same widening of FP0203.

## Missing results (FP0303)

### `Fp0303_FlagTuple.cs` (D1)

- **Happens:** `(bool Found, string Name)` lets a caller read `Name` without reading `Found`.
- **Silent because:** FP0103 takes `out`, FP0301 takes `throw` and FP0303 takes `T?`. A tuple with a flag is what
  is left, and no rule looks at it. Five HappyPath files return one.
- **Closes it:** `Option<T>` and `Result<T, TError>` unions delivered with the package, and a rule that reports a
  `bool` next to another element in a returned tuple.

## Rules switched off in source

The two files are named after the rule they silence. No rule owns this gap.

### `Fp0302_SilencedByPragma.cs` (D3) and `Fp0301_SilencedBySuppressMessage.cs` (D4)

- **Happens:** one `#pragma warning disable FP0302` or one `[SuppressMessage("Functional", "FP0301")]` removes the
  finding, and the division by zero or the `throw` stays.
- **Silent because:** these are the compiler's own switches, and they work for every analyzer.
- **Closes it:** a rule that reports both inside the core. Tested on Roslyn 5.12.0: a diagnostic tagged
  `NotConfigurable` stays under `#pragma`, `<NoWarn>` and `.editorconfig`; `[SuppressMessage]` on the type removes
  it unless it is reported without a source location; `[assembly: SuppressMessage]` removes it even then.
