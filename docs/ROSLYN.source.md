# Roslyn API map

The rule catalog, scope, and diagnostic spans are defined in [AGENTS.md](../AGENTS.md). The analyzers compile
against Roslyn 5.12.0, the version used by the pinned .NET 11.0.100 SDK and the test host. The tests use C# 15
and the .NET 11 reference assemblies. FP0107 also tests the memory-safety preview by opting into `Preview`
and `updated-memory-safety-rules` in the test host.

Roslyn 5.12.0 calls a syntax node action once per node, including nodes inside a union body. The driver
visits only the implementing declaration of a partial constructor in a syntax node action. FP0103 walks
the semantic model so it can check both declarations.

## Shared models

| Model | APIs and use |
| --- | --- |
| `CoreScope` | Walks `ISymbol.ContainingType` and checks `GetAttributes()` on the type and assembly for the `Fprog.FunctionalCoreAttribute` metadata name. `DeclaringSyntaxReferences` and `IsImplicitlyDeclared` identify authored declarations. |
| `Purity` | Classifies types in Core, Async, Pure, Shell order. It reads `AnalyzerConfigOptionsProvider.GetOptions(SyntaxTree)` and falls back to `GlobalOptions` for `fprog_pure_apis`. |
| `EntryRule` | One semantic model action per tree. `GetSymbolInfo` resolves written names and member uses. `GetTypeInfo` identifies inferred values. `GetOperation` and `GetConversion` bind arguments, operators, indexers and delegate invocations. Receiver expressions and declaration type syntax identify anchors. `LocalAliases` indexes assigned sources at the root of an operation body. The entry rule checks every such source and every result branch, including `foreach` and pattern bindings. |
| `UnionModel` | Uses `ITypeSymbol.IsUnion` and `ITypeSymbol.UnionCaseTypes` for native and hand-written unions. The compiler substitutes type arguments in the case types. The model unwraps `Nullable<T>` before reading its cases. For a closed type, `GetClosedDerivedTypeInfo(cancellationToken)` gives the direct subtypes and whether the list is complete at that construction, including for referenced assemblies. The model records the direct cases, their leaves, and completeness for each union type during the compilation. It follows `IConversionOperation.Operand` through upcasts and boxing. |
| `Mutators`, `LocalAliases`, `MutationRoots` | `IConversionOperation.GetConversion()` tells a tuple literal conversion, whose `ITupleOperation` operand shows each element's conversion, from a conversion of a tuple value, whose element conversions come from `Compilation.ClassifyConversion` on the element types. `CommonConversion.IsNullable` with equal underlying types is a wrap or unwrap. Share the traversal of assignments, increment and decrement, `ref` and `out` arguments, calls and event assignments. Alias indexing uses declarator, deconstruction, foreach and pattern operations. Reachability follows storage and receiver operations to either a parameter or a field. It records whether the path crosses a reference, ref return, span or memory view, including through a read of a value-type parameter or local. |
| `PartialUseGuards` | Walks the enclosing C# syntax and preceding `if` statements. Semantic symbol identity ties each supported condition to the collection, index, divisor or nullable operand being checked. |

## Rules

| Rule | Roslyn APIs |
| --- | --- |
| FP0101 | Method, local-function and operator declaration syntax together with named-type symbols, `INamedTypeSymbol.DelegateInvokeMethod`, and `IDelegateCreationOperation` distinguish written and inferred void results. |
| FP0102 | `IInvocationOperation`, `IMethodReferenceOperation`, `IPropertyReferenceOperation` and `IFieldReferenceOperation` bind the ambient members and mutable static state. `IMethodSymbol.IsOverride` identifies the hashing exemption. |
| FP0103 | `ParameterSyntax.Modifiers` and `IParameterSymbol.RefKind` distinguish `ref` and `out` from `in` and `ref readonly`. |
| FP0104 | `Mutators` and `MutationRoots` inspect writes through `IParameterReferenceOperation` and aliases. Receiver methods, destination arguments and indexer assignments share the same reachability test. Extension indexers expose the receiver through `IPropertyReferenceOperation.Instance`. |
| FP0105 | The same write model starts at fields and source properties with backing fields. `IEventAssignmentOperation` exposes subscriptions. The enclosing operation block distinguishes construction. |
| FP0106 | `IInvocationOperation.TargetMethod` and the bound parameters expose provider overloads and string key types. A comparer's declared parameter type or generic constraint identifies its role even when `Span<T>.Sort` infers a concrete comparer. `IPropertyReferenceOperation.Property` identifies an explicit `Comparer<string>.Default`. Calls introduced by `orderby` have `OrderingSyntax` whose `Expression` gives the span of each key. `InterpolatedStringExpressionSyntax` and `GetConversion` identify string interpolation. `IBinaryOperation` exposes string concatenation operands. |
| FP0107 | `PointerTypeSyntax`, `FunctionPointerTypeSyntax`, address-of syntax, `FixedStatementSyntax`, `UnsafeStatementSyntax` and declaration tokens give the classic unsafe forms and spans. `SyntaxKind.UnsafeExpression` and `UnsafeExpressionSyntax` cover `unsafe(expr)` in the memory-safety preview and require a file-local `RSEXPERIMENTAL006` suppression. |
| FP0201 | `IFieldSymbol.IsReadOnly` and `IsConst` handle ordinary fields. A named-type symbol start action collects operation blocks and finds writes to primary-constructor parameters, including through `ref` locals. |
| FP0202 | The `SetAccessorDeclaration` syntax kind provides the `set` token. |
| FP0203 | Field and property symbol actions inspect `ITypeSymbol.IsRecord`, `IsReadOnly` and the declared member type. Positional properties point to their parameter syntax. |
| FP0301 | `ThrowStatementSyntax` and `ThrowExpressionSyntax` supply the keyword. `IInvocationOperation.TargetMethod` supplies exception helper methods and their `DoesNotReturnAttribute` metadata. |
| FP0302 | Invocation and method-reference operations bind partial methods. For scalar `System.Convert`, the bound `IMethodSymbol` supplies the parameter and return types. `Compilation.ClassifyConversion` identifies numeric conversions, with the BCL's Boolean and character cases checked separately. Property, array and implicit-indexer references expose element reads and `Nullable<T>.Value`. Binary and compound operations expose division and remainder. `IConversionOperation` identifies explicit reference downcasts and unboxing. |
| FP0303 | `IReturnOperation` covers ordinary and yielded values. `NullableTypeSyntax.QuestionToken` identifies written nullable results, and the `SuppressNullableWarningExpression` syntax kind identifies the null-forgiving operator. |
| FP0401 | A named-type symbol action reads `TypeKind`, `IsSealed`, `IsStatic`, `IsClosed` and the base type. |
| FP0403 | A named-type symbol action checks each direct subtype of a closed class for `IsSealed` or `IsClosed`. The compiler reports CS9382 for derivation from a closed base in a different assembly. |
| FP0404 | `IDefaultValueOperation` and the implicit parameterless `IObjectCreationOperation.Constructor` identify zero initialization. The `Value` getter and its backing field distinguish auto-properties from computed properties. Metadata retains the generated field and getter but leaves the field's `IFieldSymbol.AssociatedSymbol` unset, and it marks the getter of an auto-property `[CompilerGenerated]` unless the property is partial. With an `IObjectCreationOperation.Initializer` that assigns `Value`, the author states what the union holds. |
| FP0501 | `ISwitchOperation` and `ISwitchExpressionOperation` expose labels and arms. `IPatternOperation.InputType` and each pattern's child operations let the rule check nested union positions. `IDefaultCaseClauseOperation` supplies the default label. |
| FP0502 | `ISwitchOperation` exposes statement clauses. `IPatternCaseClauseOperation.Guard` and the pattern's matched type, subpatterns and alternatives determine coverage of each leaf case. Compiler warnings CS8509 and CS8655 cover switch expressions, including over type parameters constrained to closed classes. Coverage comes from the cases because `ISwitchExpressionOperation.IsExhaustive` can be false even when every case is named. |
| FP0503 | `IIsTypeOperation`, `IIsPatternOperation` and `IConversionOperation.IsTryCast` identify type selection on union operands. The written right-hand syntax supplies the span even when a native union pattern operation is implicit. |
| FP0601 | Type-test, conversion, pattern, typeof and invocation operations expose the static operand and tested type. `IDynamicTypeSymbol` identifies the written keyword. Override links and `FindImplementationForInterfaceMember` identify external contracts. |
| FP0701 | Operation block actions inspect closure bodies, reference operations and writes. Extension method and property operations expose the receiver through `Instance`. The member's `ContainingType.ExtensionParameter.RefKind` identifies writable extension receivers, and `INameOfOperation` marks an unevaluated operand. A named-type symbol start action also collects captures and writes across members of that type. |
| FP0801 | `EntryRule` uses the Shell classification and `LockStatementSyntax.LockKeyword`. |
| FP0802 | `EntryRule` uses the Async classification and the `AsyncKeyword` of an anonymous function. Runtime async changes lowering, so the same syntax and symbol checks apply under either parse feature setting. |
| FP0803 | A named-type symbol action walks authored `BaseListSyntax` entries and binds each written base type through its semantic model. The rightmost name token is reported when the target is core and the declaring type is outside core. |

When using an API in a new rule, inspect its signature from the matching package with `dotnet tool run ref assembly`.
The SDK observations and experimental API diagnostic are recorded in [AGENTS.md](../AGENTS.md).
