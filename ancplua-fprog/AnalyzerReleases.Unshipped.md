; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FP0203 | Functional | Warning | Do not store mutable collections in records or readonly structs
FP0301 | Functional | Warning | Do not throw in the functional core
FP0101 | Functional | Warning | Do not return void in the functional core
FP0102 | Functional | Warning | Do not read hidden inputs in the functional core
FP0103 | Functional | Warning | Do not use ref or out parameters in the functional core
FP0104 | Functional | Warning | Do not write through a parameter in the functional core
FP0201 | Functional | Warning | Do not declare mutable fields in the functional core
FP0202 | Functional | Warning | Do not declare set accessors in the functional core
FP0302 | Functional | Warning | Do not call partial members in the functional core
FP0303 | Functional | Warning | Do not return null or default in the functional core
