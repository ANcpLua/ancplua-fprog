# Known gaps

A real bug that the rules do not report goes into `KnownGaps/<Rule>_<Case>.cs`, with one entry here that says why
the rule stays silent and what would close the gap.

## FP0104 and FP0105: a branch result converted as a tuple or as a nullable value

`KnownGaps/Fp0104_ConvertedBranches.cs`. In `(IList<int> Values, int Kind) chosen = useFixed ? (fixedScores, 0) :
(openScores, 1);` the compiler converts each tuple literal to the target tuple type. `MutationRoots` follows identity
and reference conversions, conversions to a built-in view, and the conversion around a target-typed `?:` or `switch`.
It stops at a tuple conversion, and at the conversion from `T` to `T?` for a struct that carries a reference. So
`chosen.Values[0] = 0` is not traced back to the parameters, and FP0104 stays silent. The same holds for FP0105 with
fields.

To close it, `MutationRoots.IsPreservingConversion` has to follow a tuple conversion into the element that the write
selects, and a nullable conversion into its operand.
