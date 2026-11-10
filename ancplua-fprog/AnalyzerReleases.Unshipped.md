; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FP0101 | Functional | Warning | Do not return void in the functional core
FP0102 | Functional | Warning | Do not read hidden inputs in the functional core
FP0103 | Functional | Warning | Do not use ref or out parameters in the functional core
FP0104 | Functional | Warning | Do not write through a parameter in the functional core
FP0201 | Functional | Warning | Do not declare mutable fields in the functional core
FP0202 | Functional | Warning | Do not declare set accessors in the functional core
FP0203 | Functional | Warning | Do not store mutable collections in records or readonly structs
FP0301 | Functional | Warning | Do not throw in the functional core
FP0302 | Functional | Warning | Do not call partial members in the functional core
FP0303 | Functional | Warning | Do not return null or default in the functional core
FP0401 | Functional | Warning | A union must be abstract
FP0402 | Functional | Warning | Union constructors must be private
FP0403 | Functional | Warning | A union case must be sealed and nested inside its union
FP0501 | Functional | Warning | Do not use a catch-all in a switch over a union
FP0601 | Functional | Warning | Do not inspect the runtime type of an unconstrained type parameter
FP0701 | Functional | Warning | Do not capture a variable that is reassigned
FP0801 | Functional | Warning | Do not reach the imperative shell from the functional core
FP0802 | Functional | Warning | Do not return a task from the functional core
