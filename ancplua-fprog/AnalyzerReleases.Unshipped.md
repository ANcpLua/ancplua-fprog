; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

 Rule ID | Category   | Severity | Notes
---------|------------|----------|--------------------------------------------------------------------
 FP0101  | Functional | Warning  | Do not use void results in the functional core
 FP0102  | Functional | Warning  | Do not read hidden inputs in the functional core
 FP0103  | Functional | Warning  | Do not use ref or out parameters in the functional core
 FP0104  | Functional | Warning  | Do not write through a parameter in the functional core
 FP0105  | Functional | Warning  | Do not change state held in a field of the functional core
 FP0106  | Functional | Warning  | Pass the culture or comparer explicitly
 FP0107  | Functional | Warning  | Do not use pointers or unsafe code in the functional core
 FP0201  | Functional | Warning  | Do not declare mutable fields in the functional core
 FP0202  | Functional | Warning  | Do not declare set accessors in the functional core
 FP0203  | Functional | Warning  | Do not store mutable collections or memory in records or readonly structs
 FP0301  | Functional | Warning  | Do not throw in the functional core
 FP0302  | Functional | Warning  | Use total operations in the functional core
 FP0303  | Functional | Warning  | Represent missing results explicitly in the functional core
 FP0401  | Functional | Warning  | Declare a core class sealed, static or closed
 FP0403  | Functional | Warning  | Declare a closed class case sealed or closed
 FP0404  | Functional | Warning  | Construct a union from a case
 FP0501  | Functional | Warning  | Do not use a catch-all in a switch over a union
 FP0502  | Functional | Warning  | Handle every case of a union in a switch statement
 FP0503  | Functional | Warning  | Match union cases in a switch
 FP0601  | Functional | Warning  | Do not inspect a runtime type hidden by the static type
 FP0701  | Functional | Warning  | Do not capture a variable that is reassigned
 FP0801  | Functional | Warning  | Keep Shell types and locks outside the functional core
 FP0802  | Functional | Warning  | Keep asynchronous work outside the functional core
 FP0803  | Functional | Warning  | Keep implementations of core types in the functional core
