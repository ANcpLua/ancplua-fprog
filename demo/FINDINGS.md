# Known gaps

A real bug that the rules do not report goes into `KnownGaps/<Rule>_<Case>.cs`, with one entry here that says why
the rule stays silent and what would close the gap.

## FP0801 and FP0802: a query expression over an extension of a Shell class

`KnownGaps/Fp0801_QueryOverShellExtension.cs`. `from line in input select line.Length` binds to
`ReportReading.Select`, an extension method of a class outside core, and no name of that class or method is
written. `EntryRule` finds such an extension where a `foreach`, an `await`, a deconstruction or a positional
pattern binds it. It does not look at the clauses of a query expression, so FP0801 stays silent.

To close it, `EntryRule` has to ask the semantic model for the method behind each query clause
(`GetQueryClauseInfo`, and `GetSymbolInfo` for `select`, `group` and orderings) and treat it like the other
extensions bound without their name.
