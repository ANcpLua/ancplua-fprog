> [AGENTS.md](../AGENTS.md) is the source of truth for the analyzer catalog. This document explains its functional-programming principles.

# Textbook Specification: Functional Programming (FP) Rules for C# Static Analysis

## 1. Referential Transparency & Purity (Referenzielle Transparenz & Reinheit)
* **Keywords:** `ReferentialTransparency`, `PureFunction`, `ObservationalPurity`, `Determinism`, `NoSideEffects`
* **Textbook Definition:**
  * Ein Ausdruck ist **referenziell transparent**, wenn er an jeder Stelle im Programm durch seinen ausgewerteten Wert ersetzt werden kann, ohne das Programmverhalten zu verändern.
  * Eine Funktion $f: A \to B$ ist **rein (pure)**, wenn sie eine mathematische Abbildung darstellt:
    1. **Determinismus:** Gleiche Eingabewerte liefern immer denselben Ausgabewert ($x = y \implies f(x) = f(y)$).
    2. **Abwesenheit von Seiteneffekten:** Die Auswertung erzeugt keine außerhalb der Funktion beobachtbare Zustandsänderung (keine Mutation externer Referenzen, kein I/O).
* **Textbook Nuance (Strong vs. Observational Purity):**
  * **Starke Reinheit:** Keinerlei Mutation, auch nicht lokal.
  * **Beobachtbare Reinheit (Weak / Observational Purity):** Eine Funktion darf intern lokal begrenzten, veränderlichen Zustand nutzen (z. B. lokale Schleifenvariablen, `Span<T>` oder einen lokalen `StringBuilder`), solange dieser Zustand niemals nach außen entweicht und die Funktion nach außen streng deterministisch und referenziell transparent bleibt.
* **C# Semantic Violations (Analyzer Rules):**
  * **Kein Ergebnis ohne Eingabe / Kein Aufruf ohne Rückgabe:** Eine reine Funktion darf weder `void` noch ein untypisiertes `Task`/`ValueTask` zurückgeben (ein Aufruf ohne Rückgabewert existiert nur für seinen Seiteneffekt). Eine parameterlose Funktion ist nur dann rein, wenn sie eine unveränderliche Konstante zurückgibt.
  * **Keine impliziten Eingaben (Hidden Inputs):** Verbot von Lesezugriffen auf veränderliche statische Felder, globale Singletons, Uhren (`DateTime.Now`), Zufallsgeneratoren (`Random`, `Guid.NewGuid`) oder Umgebungsvariablen.
  * **Keine impliziten Ausgaben (Hidden Outputs):** Verbot von `ref`- und `out`-Parametern, Verbot der Mutation übergebener Referenztypen sowie Verbot jeglicher I/O-Operationen.

---

## 2. Deep Immutability & Value Semantics (Tiefe Unveränderlichkeit & Wertsemantik)
* **Keywords:** `DeepImmutability`, `ValueSemantics`, `StructuralEquality`, `PersistentDataStructures`, `CopyOnWrite`
* **Textbook Definition:**
  * In der FP sind Daten **Werte (Values)**, keine Speicherorte (Places). So wie die Zahl `5` nicht zu `6` mutiert werden kann, ändert sich ein einmal erzeugter Domänenwert nie.
  * **Wertsemantik (Structural Equality):** Zwei Werte sind genau dann gleich, wenn ihre Bestandteile gleich sind – unabhängig von ihrer Speicheradresse (Referenzidentität ist irrelevant).
  * **Transitive (tiefe) Unveränderlichkeit:** Ein Datentyp ist nur dann unveränderlich, wenn nicht nur seine eigenen Felder schreibgeschützt sind (flache Unveränderlichkeit), sondern rekursiv auch alle Typen, auf die er verweist.
* **C# Semantic Violations (Analyzer Rules):**
  * **Flache Mutabilität:** Felder ohne `readonly` sowie Properties mit `set` statt `init` (oder reinem `get`).
  * **Gebrochene transitive Unveränderlichkeit:** Ein `record` oder `readonly struct`, der auf ein Array (`T[]`), eine `List<T>`, ein `Dictionary<TKey, TValue>` oder eine veränderliche Klasse verweist, verletzt sowohl die Unveränderlichkeit als auch die strukturelle Wertgleichheit (`Equals`/`GetHashCode` auf Arrays prüft in C# nur die Referenz, nicht den Inhalt).
  * **Erlaubte Typen:** Primitive Werttypen, `string`, `readonly record struct`, `sealed record` (sofern alle Felder transitiv unveränderlich sind) sowie persistente/unveränderliche Sammlungen (`ImmutableArray<T>`, `ImmutableList<T>`, `FrozenSet<T>`).

---

## 3. Total Functions vs. Partial Functions (Totale Funktionen)
* **Keywords:** `TotalFunctions`, `PartialFunctions`, `ParseDontValidate`, `SmartConstructors`, `ExplicitErrorTypes`
* **Textbook Definition:**
  * Eine **totale Funktion** $f: A \to B$ liefert für *jedes* Element ihrer Eingabemenge $A$ ein gültiges Element der Ausgabemenge $B$.
  * Eine **partielle Funktion** ist nur für eine Teilmenge von $A$ definiert und bricht für andere Eingaben ab (durch Exceptions, `null` oder Endlosschleifen) – die Typsignatur „lügt“ über das tatsächliche Verhalten.
  * Eine partielle Funktion wird im Lehrbuch auf genau zwei Wegen in eine totale Funktion überführt:
    1. **Einschränkung der Definitionsmenge (Restricting the Domain / „Parse, don't validate“):** Statt `string` oder `int` zu akzeptieren und in der Funktion Fehler zu werfen, wird der Eingabetyp auf einen validierten Value Object / Smart Constructor eingeschränkt (z. B. `ThreadId`, `NonEmptyString`, `PositiveInt`).
    2. **Erweiterung der Zielmenge (Extending the Codomain):** Wenn ein Fehlschlag fachlich möglich ist, wird der Rückgabetyp $B$ zu einem expliziten Summentyp erweitert (z. B. `Option<B>` oder `Result<B, Error>`).
* **C# Semantic Violations (Analyzer Rules):**
  * **Exceptions als Kontrollfluss:** Das Werfen von Exceptions (`throw`) oder das Aufrufen bekannter partieller Methoden (`int.Parse`, `.First()`, Dictionary-Indexer `dict[key]`) innerhalb der Domänenlogik.
  * **Null als impliziter Fehlerwert:** Rückgabe von `null` statt eines expliziten `Option<T>`- oder Union-Typs.
  * **Ungeprüfte Konstruktoren auf Value Objects:** Öffentliche Konstruktoren auf eingeschränkten Domänentypen, die das Umgehen der Smart-Constructor-Fabrikmethode (`TryCreate` / `Parse` -> `Result<T>`) erlauben.

---

## 4. Algebraic Data Types (ADTs) & Cardinality (Algebraische Datentypen)
* **Keywords:** `AlgebraicDataTypes`, `SumTypes`, `ProductTypes`, `DiscriminatedUnions`, `MakeIllegalStatesUnrepresentable`
* **Textbook Definition:**
  * Typen bilden eine Algebra aus zwei Grundoperationen:
    1. **Produkttypen (AND / $A \times B$):** Records und Tupel. Die Anzahl möglicher Zustände (Kardinalität) multipliziert sich: $|A \times B| = |A| \cdot |B|$.
    2. **Summentypen / Discriminated Unions (OR / $A + B$):** Ein Wert ist *entweder* $A$ *oder* $B$, niemals beides. Die Kardinalität addiert sich: $|A + B| = |A| + |B|$.
  * **Prinzip „Make Illegal States Unrepresentable“:** Wenn ein Zustand nur in bestimmten Phasen gültige Felder besitzt (z. B. hat eine Frage nur im Zustand `Answered` einen Antworttext, nicht im Zustand `Pending`), darf dies *nicht* als Produkttyp mit optionalen Feldern (`bool IsAnswered; string? AnswerText;`) modelliert werden, sondern muss als Summentyp (`Pending | Answered(string Text) | Dismissed`) definiert sein.
* **C# Semantic Violations (Analyzer Rules):**
  * **Offene Vererbungshierarchien für ADTs:** Ein Summentyp muss eine geschlossene Menge an Varianten bilden. Basistypen müssen `abstract` mit eingeschränkter Vererbung sein, und jede konkrete Variante (Leaf Type) muss `sealed` sein.
  * **Bool-Flags & Null-Kombinationen (Product Type Smell):** Records mit mehreren `bool`-Statusfeldern oder mehreren voneinander abhängigen Nullable-Properties, die implizite Zustandsautomaten darstellen.

---

## 5. Exhaustive Pattern Matching (Erschöpfende Fallunterscheidung)
* **Keywords:** `ExhaustiveMatching`, `StructuralDeconstruction`, `ClosedWorldAssumption`
* **Textbook Definition:**
  * Da ein Summentyp $A + B + C$ eine abgeschlossene Menge von Varianten definiert, muss jede Funktion, die diesen Typ verarbeitet, für *jede* Variante explizit angeben, wie sie abgebildet wird.
  * Wildcard-/Catch-All-Muster (`_`) hebeln die Typprüfung des Compilers bei zukünftigen Erweiterungen des Summentyps ($A + B + C + D$) aus und führen zu stillen Logikfehlern.
* **C# Semantic Violations (Analyzer Rules):**
  * **Catch-All-Patterns auf Summentypen:** Verwendung des Discard-Patterns (`_`), eines untypisierten `var x`-Patterns, des Basis-Typs selbst oder eines leeren Property-Patterns (`{ }`) in einer `switch`-Expression über einen Summentyp.
  * **Partielle Typ-Tests:** Prüfung einzelner Union-Fälle mittels `if (x is CaseA)` ohne vollständige Abdeckung aller übrigen Varianten des Summentyps.

---

## 6. Parametric Polymorphism & Parametricity (Parametrischer Polymorphismus)
* **Keywords:** `ParametricPolymorphism`, `Parametricity`, `FreeTheorems`, `TypeErasureSemantics`
* **Textbook Definition:**
  * Eine parametrisch polymorphe Funktion $\forall \alpha. f(\alpha)$ arbeitet für *jeden* Typ $\alpha$ nach exakt derselben Vorschrift, ohne die interne Struktur oder Identität von $\alpha$ zu kennen.
  * **Parametrizität (nach Reynolds & Wadler):** Weil die Funktion nichts über $\alpha$ weiß, kann sie keine Werte vom Typ $\alpha$ aus dem Nichts erfinden und ihr Verhalten nicht per Laufzeit-Weiche (`if (x is int)`) ändern. Wird der konkrete Typ zur Laufzeit abgefragt, degradiert parametrischer Polymorphismus zu Ad-hoc-Polymorphismus und zerstört die mathematischen Garantien (*Free Theorems*).
* **C# Semantic Violations (Analyzer Rules):**
  * **Laufzeit-Typprüfung auf Typparametern:** Verwendung von `is`, `as`, Typ-Patterns in `switch`-Ausdrücken oder `typeof(T)` / `.GetType()` auf einem unbeschränkten generischen Typparameter `T`.
  * **Ausnahme (Bounded Polymorphism / Type Classes):** Der Aufruf von Methoden, die explizit über eine generische Constraint (`where T : IMonoid<T>`) als Schnittstelle gefordert wurden, ist erlaubt, solange nicht auf konkrete Subtypen gecastet wird.

---

## 7. Lexical Closures over Immutable Bindings (Reine Funktionsabschlüsse)
* **Keywords:** `LexicalClosure`, `FirstClassFunctions`, `HigherOrderFunctions`, `ImmutableEnvironment`
* **Textbook Definition:**
  * Eine **Closure** entsteht, wenn eine Funktion höherer Ordnung eine innere Funktion erzeugt, die freie Variablen aus ihrem lexikalischen Umfeld (Environment) bindet.
  * Im Lambda-Kalkül werden freie Variablen an **unveränderliche Werte** gebunden. In imperativen Sprachen wie C# fängt eine Closure jedoch eine **Referenz auf die Variable (L-Value / Box)** ein.
  * Ändert sich die eingefangene Variable oder das eingefangene Objekt nach der Erzeugung der Closure, liefert dieselbe Funktion bei gleichen Argumenten zu unterschiedlichen Zeitpunkten unterschiedliche Ergebnisse – die Closure verliert ihre referenzielle Transparenz und wird zu einem versteckten, zustandsbehafteten Objekt.
* **C# Semantic Violations (Analyzer Rules):**
  * **Capture veränderlicher Bindungen:** Eine Lambda-Funktion oder lokale Funktion fängt eine lokale Variable oder einen Parameter ein, der nach seiner Initialisierung (im äußeren Scope oder innerhalb der Closure) neu zugewiesen wird.
  * **Capture veränderlicher Objekte (`this` oder Services):** Eine Closure fängt eine Referenz auf ein Objekt (inklusive `this`) ein, das nicht streng unveränderlich (gemäß Regel 2) ist oder I/O-Abhängigkeiten hält. (`static`-Lambdas sind immer sicher).

---

## 8. Effect Isolation: Functional Core, Imperative Shell (Trennung von Berechnung & Wirkung)
* **Keywords:** `FunctionalCore`, `ImperativeShell`, `EffectIsolation`, `PureDecisionEngine`, `Idempotency`
* **Textbook Definition:**
  * Reale Programme benötigen Seiteneffekte (Netzwerk, Datenbank, Prozess-I/O, Zeit), aber Seiteneffekte lassen sich nicht wie mathematische Funktionen komponieren.
  * **Architektur-Gesetz:** Das System wird strikt geteilt in:
    1. **Functional Core (Reine Domänenlogik):** Kennt keine Infrastruktur, keine Zeitgeber, keine Locks und keine Sockets. Er empfängt bereits geladene, unveränderliche Zustände und Fakten (inklusive der aktuellen Uhrzeit als Wert) und gibt reine Entscheidungen oder neue Zustände zurück: $(\text{CurrentState}, \text{Command}, \text{Now}) \to \text{Result}<\text{NewState}, \text{DomainError}>$.
    2. **Imperative Shell (Orchestrierung & I/O):** Liest Daten aus der Außenwelt, ruft den Functional Core auf und führt die vom Core berechneten Entscheidungen als Seiteneffekt in der Außenwelt aus.
* **C# Semantic Violations (Analyzer Rules):**
  * **I/O und Synchronisation im Core:** Jegliche Referenz innerhalb der Domänenschicht auf Betriebssystem-, Netzwerk-, Dateisystem-, Datenbank-, Prozess- oder Threading/Locking-Typen.
  * **Asynchrone Ansteckung ohne Grund:** Verwendung von `Task<T>` im reinen Functional Core, wenn gar kein I/O stattfindet (reine Berechnungen im Speicher sind synchron).