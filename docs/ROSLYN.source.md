> **Partly verified source.** Written by Gemini as a map from the FP rules to Roslyn APIs.
> Every API name in it was checked against the public metadata of `Microsoft.CodeAnalysis` 5.9.0
> (NuGet) on 2026-11-04. The rules, IDs and scope in this file are **not** binding: AGENTS.md wins.
>
> **Errata.** Apply these when you use the text below:
>
> 1. `OperationAnalysisContext` has no `SemanticModel` property. Use `context.Operation.SemanticModel`, which can be `null`.
> 2. `[DoesNotReturnIf]` targets parameters, not methods, and `ArgumentNullException.ThrowIfNull` has no `[DoesNotReturn]`:
>    it returns normally for a non-null argument. Checking method attributes does not find throw helpers.
> 3. `System.Activator` is a type in namespace `System`, not a namespace.
> 4. Section 6: `IIsPatternOperation` on a value of type `T` also matches `x is null`, which is not a type test.
>    Look at the kind of pattern (FP0601).
> 5. Section 4: the catch-all list misses a type pattern without designation (`Shape =>`, `ITypePatternOperation`),
>    `not null` (`INegatedPatternOperation` over a null constant) and type patterns of a base type such as `object`.
>    AGENTS.md FP0501 has the complete list.
> 6. Section 2 allows `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, `IReadOnlyDictionary<TKey, TValue>`, `IEnumerable<T>`
>    and `ReadOnlyCollection<T>`. These are read-only views, possibly over mutable collections, so they are neither
>    deeply immutable nor structurally equal. AGENTS.md FP0203 forbids them.
> 7. Section 1 flags only non-generic `Task` and `ValueTask`; FP0802 flags all four task types. Section 5 forbids `try`;
>    AGENTS.md has no such rule. Rules that AGENTS.md does not list are not scored.
> 8. Use the rule IDs from AGENTS.md and the `[Fprog.FunctionalCore]` marker for core scope.

# Specification: Roslyn API Mapping for FP Static Analyzers

## Global Analyzer Setup
- **Base Class:** `Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer`
- **Recommended Execution:**
  * `context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);`
  * `context.EnableConcurrentExecution();`
- **Preferred Analysis Layer:** `IOperation` (sprachunabhängige semantische Repräsentation) kombiniert mit `ISymbol` und `DataFlowAnalysis`, statt reinem Syntax-Matching.

---

## 1. Pure Functions & Referential Transparency (Observational Purity)
- **FP Principle:** Eine Funktion $f: A \to B$ ist referenziell transparent, wenn sie ihren Rückgabewert deterministisch ausschließlich aus ihren expliziten Parametern berechnet und außerhalb ihres Gültigkeitsbereichs keinen Zustand verändert. Lokale Variablen und Schleifen im Methodenrumpf sind erlaubt, solange sie nicht nach außen entweichen (*Observational Purity*).
- **Registration:**
  * `context.RegisterSymbolAction(AnalyzeMethodSymbol, SymbolKind.Method)`
  * `context.RegisterOperationAction(AnalyzePurityOperations, OperationKind.FieldReference, OperationKind.PropertyReference, OperationKind.Invocation)`
- **Symbol Checks (`IMethodSymbol`):**
  * **No-Op Return Check:** Melde Verstoß, wenn `method.MethodKind == MethodKind.Ordinary` und:
    * `method.ReturnsVoid == true`
    * oder `method.ReturnType is INamedTypeSymbol { IsGenericType: false }` vom Typ `System.Threading.Tasks.Task` bzw. `System.Threading.Tasks.ValueTask` ist.
  * **Mutable Ref/Out Parameters:** Melde Verstoß für jedes `param` in `method.Parameters`, wenn `param.RefKind is RefKind.Ref or RefKind.Out`. (`RefKind.In` und `RefKind.RefReadOnlyParameter` sind erlaubt).
- **Operation Checks (`IOperation`):**
  * **Global/Static State Access:**
    * `IFieldReferenceOperation op`: Melde Verstoß, wenn `op.Field.IsStatic && !op.Field.IsConst && (!op.Field.IsReadOnly || !IsDeeplyImmutable(op.Field.Type))`.
    * `IPropertyReferenceOperation op`: Melde Verstoß, wenn `op.Property.IsStatic` und:
      * das Property ein nicht-deterministischer System-Getter ist (`DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`, `Environment.TickCount`, `Environment.TickCount64`, `Environment.CurrentManagedThreadId`),
      * oder `!IsDeeplyImmutable(op.Property.Type)` gilt (erlaubt reine Singletons wie `ImmutableArray<T>.Empty` oder `StringComparer.Ordinal`).
  * **Non-Deterministic Invocations:**
    * `IInvocationOperation op`: Prüfe `op.TargetMethod.ContainingType` und `op.TargetMethod.Name` gegen bekannte Entropie-Quellen:
      * `System.Guid.NewGuid`
      * `System.Random` (alle Methoden)
      * `System.Security.Cryptography.RandomNumberGenerator` (alle Methoden)

---

## 2. Deep Immutability & Value Semantics
- **FP Principle:** Domänendaten sind unveränderliche Werte mit struktureller Gleichheit. Unveränderlichkeit muss transitiv (tief) gelten: Ein Datentyp ist nur unveränderlich, wenn auch alle von ihm referenzierten Typen und Sammlungen unveränderlich sind.
- **Registration:**
  * `context.RegisterSymbolAction(AnalyzeTypeImmutability, SymbolKind.NamedType, SymbolKind.Field, SymbolKind.Property)`
  * `context.RegisterOperationAction(AnalyzeMutationOperations, OperationKind.SimpleAssignment, OperationKind.CompoundAssignment, OperationKind.Increment, OperationKind.Decrement)`
- **Symbol Checks (`INamedTypeSymbol`, `IFieldSymbol`, `IPropertySymbol`):**
  * **Writable Fields:** `IFieldSymbol field`: Melde Verstoß, wenn `!field.IsImplicitlyDeclared && !field.IsConst && !field.IsReadOnly`.
  * **Writable Properties:** `IPropertySymbol prop`: Melde Verstoß, wenn `prop.SetMethod is not null && !prop.SetMethod.IsInitOnly`.
  * **Mutable Collection Exposure:** Melde Verstoß, wenn `prop.Type` oder `field.Type`:
    * ein `IArrayTypeSymbol` (`TypeKind == TypeKind.Array`) ist (Arrays brechen Unveränderlichkeit und strukturelle `record`-Gleichheit),
    * oder ein konkreter Typ aus `System.Collections.Generic` bzw. `System.Collections.Concurrent` ist (z. B. `List<T>`, `Dictionary<TKey, TValue>`, `HashSet<T>`, `Queue<T>`, `Stack<T>`, `ConcurrentBag<T>`).
    * *Erlaubte Sammlungen:* Typen aus `System.Collections.Immutable`, `System.Collections.Frozen`, `System.Collections.ObjectModel.ReadOnlyCollection<T>` sowie reine Lese-Interfaces (`IReadOnlyList<T>`, `IReadOnlyCollection<T>`, `IReadOnlySet<T>`, `IReadOnlyDictionary<TKey, TValue>`, `IEnumerable<T>`).
- **Operation Checks (`IAssignmentOperation`, `IIncrementOrDecrementOperation`):**
  * Prüfe `operation.Target` (ignoriere `ILocalReferenceOperation`, sofern die lokale Variable nicht in einer Closure eingefangen wird):
    * `IArrayElementReferenceOperation` (`OperationKind.ArrayElementReference`): Melde Verstoß, wenn das Ziel-Array ein Feld, Property oder Parameter ist.
    * `IFieldReferenceOperation` oder `IPropertyReferenceOperation`: Melde Verstoß, wenn das umschließende `IMethodSymbol` weder `MethodKind.Constructor`, `MethodKind.StaticConstructor` noch ein `init`-Accessor (`MethodKind.PropertySet` mit `IsInitOnly == true`) ist und der Knoten nicht Teil einer `IObjectOrCollectionInitializerOperation` ist.

---

## 3. Side-Effect Isolation: Functional Core vs. Imperative Shell
- **FP Principle:** Reine Geschäfts- und Domänenlogik (*Functional Core*) ist strikt von Infrastruktur, I/O, Betriebssystemprozessen, Zeitgebern und Locking-Mechanismen (*Imperative Shell*) isoliert.
- **Registration:**
  * `context.RegisterOperationAction(AnalyzeCoreIsolation, OperationKind.Invocation, OperationKind.ObjectCreation, OperationKind.FieldReference, OperationKind.PropertyReference, OperationKind.Lock)`
- **Scope Filter:**
  * Ermittle den aktuellen Namespace über `context.ContainingSymbol.ContainingNamespace.ToDisplayString()`. Führe die Regel nur aus, wenn der Namespace zum definierten *Functional Core* gehört (z. B. `*.Domain*`).
- **Operation & Symbol Checks:**
  * Extrahiere das Zielsymbol:
    * `IInvocationOperation` -> `TargetMethod.ContainingType`
    * `IObjectCreationOperation` -> `Type`
    * `IFieldReferenceOperation` -> `Field.ContainingType`
    * `IPropertyReferenceOperation` -> `Property.ContainingType`
  * **Forbidden Namespaces:** Melde Verstoß, wenn `ContainingNamespace` mit einem der folgenden Präfixe beginnt:
    * `System.IO`
    * `System.Net` (inkl. `Sockets`, `Http`, `WebSockets`)
    * `System.Data`
    * `Microsoft.Data` (inkl. `Microsoft.Data.Sqlite`)
  * **Forbidden Boundary Types:** Melde Verstoß bei Referenz auf:
    * `System.Console`, `System.Environment`
    * `System.Diagnostics.Process`
    * `System.Threading.SemaphoreSlim`, `System.Threading.Mutex`, `System.Threading.Monitor`, `System.Threading.Interlocked`, `System.Threading.Thread`, `System.Threading.ThreadPool`
    * `System.Threading.Tasks.Task` (insbesondere `Task.Run`, `Task.Delay`)
    * `System.TimeProvider`, `System.Threading.CancellationToken`
  * **Forbidden Operations:** Melde jeden `ILockOperation`-Knoten (`OperationKind.Lock`).

---

## 4. Algebraic Data Types (ADTs) & Exhaustive Pattern Matching
- **FP Principle:** Summentypen (Discriminated Unions) bilden eine geschlossene Menge von Varianten (*Make Illegal States Unrepresentable*). Jede Fallunterscheidung über einen Summentyp muss alle Varianten explizit behandeln, ohne neue Fälle durch Catch-All-Muster zu verschlucken.
- **Registration:**
  * `context.RegisterSymbolAction(AnalyzeAdtHierarchy, SymbolKind.NamedType)`
  * `context.RegisterOperationAction(AnalyzeSwitchExhaustiveness, OperationKind.SwitchExpression, OperationKind.Switch)`
- **Union model:** `ITypeSymbol.IsUnion` identifies native union structs. Public one-parameter constructors
  supply their case types. `ITypeSymbol.IsClosed` identifies closed classes and records.
  `GetClosedDerivedTypeInfo(cancellationToken).ClosedDerivedTypes` supplies their direct cases.
- **Declaration rules:** FP0401 checks `IsSealed`, `IsStatic` and `IsClosed` on classes in core. FP0403
  checks `IsSealed` and `IsClosed` on the direct children of closed classes.
- **Empty union values:** FP0404 checks `IDefaultValueOperation` and `IObjectCreationOperation` with
  the implicit parameterless constructor of a native union struct.
- **Operation Checks (`ISwitchExpressionOperation`, `ISwitchOperation`):**
  * Prüfe `switchOp.Value.Type`: Wenn der Typ eine Domain-Union oder ein `abstract` ADT-Basistyp ist:
    * Bei `ISwitchExpressionOperation`: Iteriere über `switchOp.Arms` und prüfe `arm.Pattern`.
    * Bei `ISwitchOperation`: Iteriere über `switchOp.Cases.SelectMany(c => c.Clauses)`. Melde Verstoß bei `IDefaultCaseClauseOperation` (`default:`) und prüfe bei `IPatternCaseClauseOperation` das `clause.Pattern`.
  * **Forbidden Catch-All Patterns auf ADTs:**
    * `IDiscardPatternOperation` (`OperationKind.DiscardPattern`, `_`).
    * `IDeclarationPatternOperation` (`OperationKind.DeclarationPattern`), bei der `MatchesNull == true` (`var x`) oder `MatchedType` gleich dem ADT-Basistyp selbst ist.
    * `IRecursivePatternOperation` (`OperationKind.RecursivePattern`, `{ }`), bei der `MatchedType` `null` oder gleich dem ADT-Basistyp ist und keine einschränkenden Subpatterns vorliegen.

---

## 5. Total Functions & Explicit Error Values (Railway Oriented Programming)
- **FP Principle:** Totale Funktionen liefern für jede Eingabe ihrer Typsignatur einen gültigen Rückgabewert (z. B. `Result<T, TError>` oder `Option<T>`), statt den Kontrollfluss durch Exceptions abzubrechen.
- **Registration:**
  * `context.RegisterOperationAction(AnalyzeTotalFunctions, OperationKind.Throw, OperationKind.Try, OperationKind.Invocation)`
- **Scope Filter:** Aktiv innerhalb von *Functional Core* Namespaces.
- **Operation Checks:**
  * **No Throws in Core:** Jeder `IThrowOperation`-Knoten (`OperationKind.Throw`, deckt `ThrowStatementSyntax` und `ThrowExpressionSyntax` ab) löst einen Verstoß aus (Ausnahme: `System.Diagnostics.UnreachableException`).
  * **No Try/Catch Control Flow in Core:** Jeder `ITryOperation`-Knoten (`OperationKind.Try`) innerhalb einer Domänenmethode löst einen Verstoß aus.
  * **No Throw-Helpers or Partial BCL Parsers:** Melde `IInvocationOperation`, wenn `op.TargetMethod`:
    * mit `[DoesNotReturn]` oder `[DoesNotReturnIf]` annotiert ist (z. B. `ArgumentNullException.ThrowIfNull`),
    * oder eine bekannte partielle Methode ist (`int.Parse`, `Guid.Parse`, `Enumerable.First`, `Enumerable.Single`, `DomainBoundary.RequireSuccess`).

---

## 6. Parametric Polymorphism & Type Opacity ("Theorems for Free")
- **FP Principle:** Parametrisch polymorphe Funktionen behandeln generische Typparameter `<T>` als opake Black Box. Laufzeit-Typprüfungen auf `<T>` brechen die Parametrizität.
- **Registration:**
  * `context.RegisterOperationAction(AnalyzeTypeOpacity, OperationKind.IsType, OperationKind.IsPattern, OperationKind.Conversion, OperationKind.TypeOf, OperationKind.Invocation, OperationKind.SwitchExpression, OperationKind.Switch)`
- **Operation Checks:**
  * **Runtime Type Inspection on `<T>`:**
    * `IIsTypeOperation op`: Melde Verstoß, wenn `op.ValueOperand.Type?.TypeKind == TypeKind.TypeParameter`.
    * `IIsPatternOperation op`: Melde Verstoß, wenn `op.Value.Type?.TypeKind == TypeKind.TypeParameter`.
    * `ISwitchExpressionOperation` / `ISwitchOperation`: Melde Verstoß, wenn `Value.Type?.TypeKind == TypeKind.TypeParameter` und ein Arm/Case ein Typ-Pattern prüft.
    * `IConversionOperation op`: Melde Verstoß, wenn `op.Operand.Type is ITypeParameterSymbol tp` und entweder `op.IsTryCast == true` (`as`) gilt oder `op.Type` kein durch `tp.ConstraintTypes` garantiertes Interface/Basistyp ist (erlaubt gültige Aufrufe auf generischen Constraints wie `where T : IEquatable<T>`).
  * **Reflection on `<T>`:**
    * `ITypeOfOperation op`: Melde Verstoß, wenn `op.TypeOperand.TypeKind == TypeKind.TypeParameter` (`typeof(T)`).
    * `IInvocationOperation op`: Melde Verstoß, wenn `op.TargetMethod.Name == "GetType"` auf einer Instanz vom Typ `TypeKind.TypeParameter` aufgerufen wird, oder `op.TargetMethod.ContainingType` im Namespace `System.Reflection` bzw. `System.Activator` liegt.

---

## 7. Pure Lexical Closures (Immutable Environment Capture)
- **FP Principle:** Eine Closure darf freie Variablen aus ihrem lexikalischen Umfeld nur dann einfangen, wenn diese Bindungen und die referenzierten Werte unveränderlich sind.
- **Registration:**
  * `context.RegisterOperationAction(AnalyzeClosures, OperationKind.AnonymousFunction, OperationKind.LocalFunction)`
- **Operation & DataFlow Checks:**
  * **Fast Path (`static` Modifier):**
    * Extrahiere das `IMethodSymbol` über `((IAnonymousFunctionOperation)op).Symbol` bzw. `((ILocalFunctionOperation)op).Symbol`. Wenn `methodSymbol.IsStatic == true`, brich die Prüfung sofort als gültig ab.
  * **Capture Analysis (`DataFlowAnalysis`):**
    * Rufe `var flow = context.SemanticModel.AnalyzeDataFlow(op.Syntax)` auf.
    * Wenn `!flow.Succeeded` oder `flow.CapturedInside.IsEmpty`, ist die Closure rein.
    * Prüfe jedes `ISymbol captured` in `flow.CapturedInside`:
      1. **Reassigned Variable:** Melde Verstoß, wenn `flow.WrittenInside.Contains(captured)` (Mutation innerhalb der Closure) ODER wenn `captured` im umschließenden Methoden-Operation-Baum *nach* seiner Initialisierung erneut beschrieben wird (d. h. als `Target` einer `IAssignmentOperation` / `IIncrementOrDecrementOperation` oder als `ref`/`out` `IArgumentOperation` auftritt). *(Hinweis: Nicht pauschal `outerFlow.WrittenInside` nutzen, da schon die Variablen-Initialisierung `var x = 1;` dort als Schreibzugriff zählt).*
      2. **Captured `this` Reference:** Wenn `captured is IParameterSymbol { IsThis: true }`, melde Verstoß, sofern `captured.Type` kein tief unveränderlicher Typ (gemäß Regel 2) ist.
      3. **Captured Mutable Reference:** Melde Verstoß, wenn `captured.Type` veränderliche Felder/Properties, veränderliche Sammlungen oder I/O-Handles (gemäß Regel 2 und 3) besitzt.

---

## Recommended Diagnostic IDs Summary
- `FP0001`: Impure operation or non-deterministic call in pure context.
- `FP0002`: Mutable field, property, or collection exposed on value model.
- `FP0003`: Imperative shell / I/O dependency referenced inside Functional Core.
- `FP0004`: Non-exhaustive ADT switch or unsealed/open ADT hierarchy.
- `FP0005`: Exception control flow (`throw`, `try-catch`, or partial function) used inside Functional Core.
- `FP0006`: Parametricity violation (runtime type inspection on generic parameter `<T>`).
- `FP0007`: Impure closure capturing reassigned variable or stateful reference.