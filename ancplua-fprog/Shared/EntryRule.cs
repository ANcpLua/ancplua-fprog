using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace ancplua_fprog.Shared;

/// <summary>Finds the first use of a Shell or Async value in a core expression.</summary>
internal static class EntryRule
{
    public static void Register(AnalysisContext context, DiagnosticDescriptor rule, PurityClass category)
    {
        context.RegisterCompilationStartAction(start =>
        {
            var purity = new Purity.Model(start.Options.AnalyzerConfigOptionsProvider);
            start.RegisterSemanticModelAction(model => new TreeAnalysis(model, rule, category, purity).Analyze());
        });
    }

    private sealed class TreeAnalysis(
        SemanticModelAnalysisContext context, DiagnosticDescriptor rule, PurityClass category, Purity.Model purity)
    {
        private readonly SemanticModel _model = context.SemanticModel;
        private readonly CancellationToken _cancellationToken = context.CancellationToken;
        private readonly TextSpan? _filterSpan = context.FilterSpan;
        private readonly Purity.ConfiguredApis _configured = purity.ForTree(context.SemanticModel.SyntaxTree);
        private readonly Dictionary<SyntaxTree, SemanticModel> _declarationModels = new();
        private readonly Dictionary<SimpleNameSyntax, (ISymbol? Symbol, INamedTypeSymbol? WrittenType)> _names = new();
        private readonly Dictionary<ISymbol, bool> _declarationAnchors = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<SyntaxNode, LocalAliases> _aliasesByBody = new();
        private readonly Dictionary<ILocalSymbol, bool> _localAnchors = new(SymbolEqualityComparer.Default);
        private readonly HashSet<ILocalSymbol> _checkingAnchors = new(SymbolEqualityComparer.Default);

        public void Analyze()
        {
            var scopes = new Stack<SyntaxNode>();
            scopes.Push(_model.SyntaxTree.GetRoot(_cancellationToken));
            while (scopes.Count > 0)
            {
                var scope = scopes.Pop();
                var topLevel = scope is GlobalStatementSyntax;
                var core = IsTypeDeclaration(scope) && purity.Scope.Contains(_model.GetDeclaredSymbol(scope, _cancellationToken));
                var descendants = _filterSpan is { } span
                    ? scope.DescendantNodes(span, Descend)
                    : scope.DescendantNodes(Descend);
                foreach (var node in descendants)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    if (IsTypeDeclaration(node) || node is GlobalStatementSyntax)
                        scopes.Push(node);
                    else if (core || topLevel && purity.Scope.Contains(_model.GetEnclosingSymbol(node.SpanStart, _cancellationToken)))
                        AnalyzeNode(node);
                }

                bool Descend(SyntaxNode node)
                {
                    if (node == scope)
                        return true;
                    if (IsTypeDeclaration(node) || node is GlobalStatementSyntax)
                        return false;

                    // An unmarked type can still contain a separately marked nested type.
                    return core || topLevel
                        ? VisitChildren(node)
                        : node is BaseNamespaceDeclarationSyntax or ExtensionBlockDeclarationSyntax;
                }
            }
        }

        private static bool IsTypeDeclaration(SyntaxNode node)
        {
            return node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax;
        }

        private void AnalyzeNode(SyntaxNode node)
        {
            switch (node)
            {
                case SimpleNameSyntax name:
                    AnalyzeName(name);
                    break;
                case LockStatementSyntax statement when category == PurityClass.Shell:
                    Report(statement.LockKeyword.GetLocation(), "lock");
                    break;
                case AnonymousFunctionExpressionSyntax lambda:
                    if (category == PurityClass.Async && lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword))
                        Report(lambda.AsyncKeyword.GetLocation(), "async");
                    if (lambda is LambdaExpressionSyntax { Body: ExpressionSyntax body })
                        ReportValueUse(body);
                    break;
                case ImplicitObjectCreationExpressionSyntax creation:
                    if (BoundaryType(creation) is { } type && !TargetIsAnchored(creation))
                        Report(creation.NewKeyword.GetLocation(), DisplayName(type));
                    break;
                case ElementAccessExpressionSyntax or ElementBindingExpressionSyntax:
                    var access = (ExpressionSyntax)node;
                    if (IndexerType(access) is { } indexerType && !IsAnchored(IndexerReceiver(access)))
                        Report(access.GetLocation(), DisplayName(indexerType));
                    break;
                case ArgumentSyntax argument:
                    if (ValueUseType(argument.Expression) is { } argumentType
                        && _model.GetOperation(argument, _cancellationToken) is IArgumentOperation
                        && !IsAnchored(argument.Expression))
                        Report(argument.Expression.GetLocation(), DisplayName(argumentType));
                    break;
                case ReturnStatementSyntax { Expression: { } returned }:
                    ReportValueUse(returned);
                    break;
                case YieldStatementSyntax { Expression: { } yielded }:
                    ReportValueUse(yielded);
                    break;
                case ArrowExpressionClauseSyntax arrow:
                    ReportValueUse(arrow.Expression);
                    break;
                case CommonForEachStatementSyntax each:
                    ReportValueUse(each.Expression);
                    break;
                case UsingStatementSyntax usingStatement:
                    if (usingStatement.Expression is { } resource)
                        ReportValueUse(resource);
                    ReportResources(usingStatement.Declaration);
                    break;
                case LocalDeclarationStatementSyntax { UsingKeyword.RawKind: (int)SyntaxKind.UsingKeyword } local:
                    ReportResources(local.Declaration);
                    break;
                case AwaitExpressionSyntax awaitExpression:
                    ReportValueUse(awaitExpression.Expression);
                    break;
            }

            if (node is not ExpressionSyntax expression)
                return;

            if (expression is InvocationExpressionSyntax invocation
                && (InvocationName(invocation.Expression) is not { } target
                    || BindName(target, _model).Symbol is not IMethodSymbol { MethodKind: not MethodKind.DelegateInvoke })
                && _model.GetOperation(invocation, _cancellationToken) is
                    IInvocationOperation { TargetMethod.MethodKind: MethodKind.DelegateInvoke })
                ReportValueUse(invocation.Expression);

            AnalyzeOperator(expression);
            if (ImplicitConversionType(expression) is { } conversionType
                && !IsAnchored(expression, includeConversion: false))
                Report(expression.GetLocation(), DisplayName(conversionType));
        }

        private bool VisitChildren(SyntaxNode node)
        {
            return node is not (AttributeListSyntax or UsingDirectiveSyntax)
                   && (node is not InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } }
                       || _model.GetOperation(node, _cancellationToken) is not INameOfOperation);
        }

        private void AnalyzeName(SimpleNameSyntax name)
        {
            var (symbol, written) = BindName(name, _model);
            if (written is not null)
            {
                Report(name.Identifier.GetLocation(), written.Name);
                return;
            }

            var receiver = MemberReceiver(name);
            if (MemberType(symbol, receiver) is { } memberType
                && !IsAnchored(receiver)
                && !DeclarationAnchors(symbol)
                && !NameHasWrittenTypeArguments(name)
                && !InheritedFromWrittenBase(name, symbol, receiver))
                Report(name.Identifier.GetLocation(), memberType.Name);
        }

        private (ISymbol? Symbol, INamedTypeSymbol? WrittenType) BindName(SimpleNameSyntax name, SemanticModel model)
        {
            if (_names.TryGetValue(name, out var binding))
                return binding;

            var symbol = model.GetSymbolInfo(name, _cancellationToken).Symbol;
            var written = !name.IsVar && symbol is INamedTypeSymbol type && Matches(type.OriginalDefinition, model.SyntaxTree)
                   && !QualifiesTypeName(name, model)
                ? type
                : null;
            binding = (symbol, written);
            _names.Add(name, binding);
            return binding;
        }

        private bool QualifiesTypeName(SimpleNameSyntax name, SemanticModel model)
        {
            SyntaxNode node = name;
            while (node.Parent is QualifiedNameSyntax qualified && qualified.Right == node
                   || node.Parent is AliasQualifiedNameSyntax aliased && aliased.Name == node
                   || node.Parent is MemberAccessExpressionSyntax access && access.Name == node)
                node = node.Parent;

            return node.Parent is QualifiedNameSyntax qualifier && qualifier.Left == node
                   || node.Parent is MemberAccessExpressionSyntax member && member.Expression == node
                   && model.GetSymbolInfo(member, _cancellationToken).Symbol is INamedTypeSymbol;
        }

        private bool HasWrittenTypeName(SyntaxNode? syntax, SemanticModel model)
        {
            return syntax is not null && syntax.DescendantNodesAndSelf().OfType<SimpleNameSyntax>()
                .Any(name => BindName(name, model).WrittenType is not null);
        }

        private bool NameHasWrittenTypeArguments(SimpleNameSyntax name)
        {
            return name is GenericNameSyntax generic && HasWrittenTypeName(generic.TypeArgumentList, _model);
        }

        private ITypeSymbol? MemberType(ISymbol? symbol, ExpressionSyntax? receiver)
        {
            if (symbol is not (IMethodSymbol or IFieldSymbol or IPropertySymbol or IEventSymbol)
                || symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction or MethodKind.DelegateInvoke })
                return null;

            var declaringType = symbol is IMethodSymbol { ReducedFrom: { } extension }
                ? extension.ContainingType
                : symbol.ContainingType;
            if (declaringType is { IsExtension: true })
                declaringType = declaringType.ContainingType;

            if (Matches(declaringType))
                return declaringType;

            return !symbol.IsStatic && receiver is not null ? BoundaryType(receiver) : null;
        }

        private bool InheritedFromWrittenBase(SimpleNameSyntax name, ISymbol? member, ExpressionSyntax? receiver)
        {
            if (receiver is not null || member?.ContainingType is not { } declaring)
                return false;

            for (var type = _model.GetEnclosingSymbol(name.SpanStart, _cancellationToken)?.ContainingType;
                 type is not null; type = type.ContainingType)
            {
                if (type.BaseType is not { } baseType || !Matches(baseType))
                    continue;

                for (var ancestor = baseType; ancestor is not null; ancestor = ancestor.BaseType)
                    if (SymbolEqualityComparer.Default.Equals(ancestor.OriginalDefinition, declaring.OriginalDefinition))
                        return true;
            }

            return false;
        }

        private bool DeclarationAnchors(ISymbol? symbol, bool returnType = false)
        {
            if (symbol is IParameterSymbol { IsImplicitlyDeclared: true, ContainingSymbol: IMethodSymbol accessor })
                symbol = accessor.AssociatedSymbol;
            if (symbol is not (IParameterSymbol or ILocalSymbol or IFieldSymbol or IPropertySymbol)
                && !(returnType && symbol is IMethodSymbol))
                return false;

            if (!_declarationAnchors.TryGetValue(symbol, out var anchored))
            {
                anchored = purity.Scope.Contains(symbol) && HasDeclarationAnchor(symbol);
                _declarationAnchors.Add(symbol, anchored);
            }

            return anchored;
        }

        private bool HasDeclarationAnchor(ISymbol symbol)
        {
            // A boundary value in a using declaration enters at its initializer.
            if (symbol is ILocalSymbol { IsUsing: true } resource && Matches(resource.Type))
                return true;

            foreach (var reference in symbol.DeclaringSyntaxReferences)
            {
                if (!_model.Compilation.ContainsSyntaxTree(reference.SyntaxTree))
                    continue;

                var declaration = reference.GetSyntax(_cancellationToken);
                var type = declaration switch
                {
                    ParameterSyntax parameter => parameter.Type,
                    VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax variable } => variable.Type,
                    BasePropertyDeclarationSyntax property => property.Type,
                    ForEachStatementSyntax each => each.Type,
                    SingleVariableDesignationSyntax { Parent: DeclarationExpressionSyntax expression } => expression.Type,
                    SingleVariableDesignationSyntax { Parent: DeclarationPatternSyntax pattern } => pattern.Type,
                    SingleVariableDesignationSyntax { Parent: RecursivePatternSyntax pattern } => pattern.Type,
                    MethodDeclarationSyntax method => method.ReturnType,
                    LocalFunctionStatementSyntax function => function.ReturnType,
                    OperatorDeclarationSyntax @operator => @operator.ReturnType,
                    ConversionOperatorDeclarationSyntax conversion => conversion.Type,
                    _ => null
                };

                if (type is not null && HasWrittenTypeName(type, DeclarationModel(reference.SyntaxTree)))
                    return true;
            }

            return false;
        }

        private SemanticModel DeclarationModel(SyntaxTree tree)
        {
            if (tree == _model.SyntaxTree)
                return _model;

            if (!_declarationModels.TryGetValue(tree, out var model))
            {
                model = _model.Compilation.GetSemanticModel(tree);
                _declarationModels.Add(tree, model);
            }

            return model;
        }

        // The written type that a target-typed expression takes its type from. A collection expression hands that
        // type on to its elements.
        private bool TargetIsAnchored(ExpressionSyntax targetTyped)
        {
            SyntaxNode value = targetTyped;
            while (value.Parent is ParenthesizedExpressionSyntax or TupleExpressionSyntax or ExpressionElementSyntax
                   || value is ExpressionElementSyntax && value.Parent is CollectionExpressionSyntax
                   || value.Parent is ConditionalExpressionSyntax conditional && conditional.Condition != value
                   || value.Parent is SwitchExpressionArmSyntax arm && arm.Expression == value
                   || value.Parent is SwitchExpressionSyntax expression && expression.GoverningExpression != value
                   || value.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax })
                value = value.Parent;

            return value.Parent switch
            {
                EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax variable } }
                    => HasWrittenTypeName(variable.Type, _model),
                EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property }
                    => HasWrittenTypeName(property.Type, _model),
                ArgumentSyntax argument => ArgumentTypeIsAnchored(argument),
                AssignmentExpressionSyntax assignment when assignment.Right == value => IsAnchored(assignment.Left),
                ReturnStatementSyntax or ArrowExpressionClauseSyntax
                    => DeclarationAnchors(ReturnTarget(value), returnType: true),
                _ => false
            };
        }

        private ISymbol? ReturnTarget(SyntaxNode value)
        {
            var enclosing = _model.GetEnclosingSymbol(value.SpanStart, _cancellationToken);
            return enclosing is IMethodSymbol { AssociatedSymbol: { } property } ? property : enclosing;
        }

        private bool ArgumentTypeIsAnchored(ArgumentSyntax argument)
        {
            if (_model.GetOperation(argument, _cancellationToken) is IArgumentOperation op
                && DeclarationAnchors(op.Parameter))
                return true;

            return argument.Parent?.Parent is InvocationExpressionSyntax invocation
                   && InvocationName(invocation.Expression) is { } name
                   && NameHasWrittenTypeArguments(name);
        }

        private static SimpleNameSyntax? InvocationName(ExpressionSyntax expression)
        {
            return expression switch
            {
                SimpleNameSyntax name => name,
                MemberAccessExpressionSyntax access => access.Name,
                MemberBindingExpressionSyntax binding => binding.Name,
                _ => null
            };
        }

        private void ReportValueUse(ExpressionSyntax expression)
        {
            if (ValueUseType(expression) is { } type && !IsAnchored(expression))
                Report(expression.GetLocation(), DisplayName(type));
        }

        private ITypeSymbol? ValueUseType(ExpressionSyntax expression)
        {
            return expression.IsKind(SyntaxKind.NullLiteralExpression) ? null : BoundaryType(expression);
        }

        private void ReportResources(VariableDeclarationSyntax? declaration)
        {
            if (declaration is null || HasWrittenTypeName(declaration.Type, _model))
                return;

            foreach (var variable in declaration.Variables)
                if (variable.Initializer is { } initializer)
                    ReportValueUse(initializer.Value);
        }

        private void AnalyzeOperator(ExpressionSyntax expression)
        {
            if (OperatorType(expression) is not { } type)
                return;

            var (token, left, right, typeName) = expression switch
            {
                BinaryExpressionSyntax binary => (binary.OperatorToken, binary.Left, binary.Right, (TypeSyntax?)null),
                AssignmentExpressionSyntax assignment => (assignment.OperatorToken, assignment.Left, assignment.Right, null),
                PrefixUnaryExpressionSyntax prefix => (prefix.OperatorToken, prefix.Operand, null, null),
                PostfixUnaryExpressionSyntax postfix => (postfix.OperatorToken, postfix.Operand, null, null),
                CastExpressionSyntax cast => (cast.OpenParenToken, cast.Expression, null, cast.Type),
                _ => default
            };

            if (token.RawKind != 0 && !IsAnchored(left) && !IsAnchored(right) && !HasWrittenTypeName(typeName, _model))
                Report(token.GetLocation(), DisplayName(type));
        }

        private INamedTypeSymbol? OperatorType(ExpressionSyntax expression)
        {
            if (expression is not (BinaryExpressionSyntax or AssignmentExpressionSyntax
                or PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax or CastExpressionSyntax))
                return null;

            // These forms cannot bind to a user-defined operator. Resolve the other forms
            // through the operation tree only when binding found a possible operator method.
            if (expression.Kind() is SyntaxKind.SimpleAssignmentExpression or SyntaxKind.CoalesceAssignmentExpression
                or SyntaxKind.CoalesceExpression or SyntaxKind.IsExpression or SyntaxKind.AsExpression
                or SyntaxKind.SuppressNullableWarningExpression)
                return null;

            var binding = _model.GetSymbolInfo(expression, _cancellationToken);
            if (binding.Symbol is IMethodSymbol { MethodKind: MethodKind.BuiltinOperator }
                || binding.Symbol is null && binding.CandidateSymbols.IsEmpty)
                return null;

            var method = _model.GetOperation(expression, _cancellationToken) switch
            {
                IBinaryOperation binary => binary.OperatorMethod,
                IUnaryOperation unary => unary.OperatorMethod,
                IIncrementOrDecrementOperation mutation => mutation.OperatorMethod,
                ICompoundAssignmentOperation assignment => assignment.OperatorMethod,
                IConversionOperation { IsImplicit: false } conversion => conversion.OperatorMethod,
                _ => null
            };

            return method?.ContainingType is { } declaring && Matches(declaring) ? declaring : null;
        }

        private INamedTypeSymbol? ImplicitConversionType(ExpressionSyntax expression)
        {
            if (SyntaxFacts.IsInTypeOnlyContext(expression))
                return null;

            var conversion = _model.GetConversion(expression, _cancellationToken);
            return conversion.IsImplicit && conversion.IsUserDefined
                   && conversion.MethodSymbol?.ContainingType is { } declaring && Matches(declaring)
                ? declaring
                : null;
        }

        private ITypeSymbol? IndexerType(ExpressionSyntax access)
        {
            var (declaring, receiver) = _model.GetOperation(access, _cancellationToken) switch
            {
                IPropertyReferenceOperation { Property.IsIndexer: true } reference
                    => (reference.Property.ContainingType, reference.Instance?.Type),
                IImplicitIndexerReferenceOperation implicitIndexer
                    => (implicitIndexer.IndexerSymbol.ContainingType, implicitIndexer.Instance.Type),
                IArrayElementReferenceOperation array => (null, array.ArrayReference.Type),
                _ => default
            };

            return Matches(declaring) ? declaring : Matches(receiver) ? receiver : null;
        }

        private bool ElementIsAnchored(CollectionElementSyntax element)
        {
            return element switch
            {
                ExpressionElementSyntax expression => IsAnchored(expression.Expression),
                SpreadElementSyntax spread => IsAnchored(spread.Expression),
                _ => false
            };
        }

        // An array has no name of its own; the message names the type of its elements.
        private static string DisplayName(ITypeSymbol type)
        {
            while (type is IArrayTypeSymbol array)
                type = array.ElementType;
            return type.Name;
        }

        private ITypeSymbol? BoundaryType(ExpressionSyntax value)
        {
            var info = _model.GetTypeInfo(value, _cancellationToken);
            var type = info.Type ?? info.ConvertedType;
            return Matches(type) ? type : null;
        }

        private bool Matches(ITypeSymbol? type, SyntaxTree? tree = null)
        {
            return type is not null
                   && purity.Classify(type, tree is null || tree == _model.SyntaxTree ? _configured : purity.ForTree(tree)) == category;
        }

        // A candidate at the receiver anchors its use even if its own report is subsumed by an earlier entry.
        private bool IsAnchored(ExpressionSyntax? expression, bool includeConversion = true)
        {
            if (expression is null)
                return false;
            if (OperatorType(expression) is not null || includeConversion && ImplicitConversionType(expression) is not null)
                return true;

            return expression switch
            {
                SimpleNameSyntax name => NameAnchors(name, null),
                MemberAccessExpressionSyntax access => NameAnchors(access.Name, access.Expression),
                MemberBindingExpressionSyntax binding => NameAnchors(binding.Name, ConditionalReceiver(binding)),
                InvocationExpressionSyntax invocation => IsAnchored(invocation.Expression),
                ElementAccessExpressionSyntax or ElementBindingExpressionSyntax
                    => IndexerType(expression) is not null || IsAnchored(IndexerReceiver(expression)),
                ConditionalAccessExpressionSyntax conditional
                    => IsAnchored(conditional.Expression) || IsAnchored(conditional.WhenNotNull),
                ParenthesizedExpressionSyntax parenthesized => IsAnchored(parenthesized.Expression),
                PostfixUnaryExpressionSyntax suppressed when suppressed.IsKind(SyntaxKind.SuppressNullableWarningExpression)
                    => IsAnchored(suppressed.Operand),
                CastExpressionSyntax cast => HasWrittenTypeName(cast.Type, _model) || IsAnchored(cast.Expression),
                ObjectCreationExpressionSyntax creation => HasWrittenTypeName(creation.Type, _model),
                ArrayCreationExpressionSyntax creation => HasWrittenTypeName(creation.Type.ElementType, _model),
                ImplicitObjectCreationExpressionSyntax creation => BoundaryType(creation) is not null,
                CollectionExpressionSyntax collection
                    => TargetIsAnchored(collection) || collection.Elements.Any(ElementIsAnchored),
                DefaultExpressionSyntax @default => HasWrittenTypeName(@default.Type, _model),
                ThisExpressionSyntax or BaseExpressionSyntax => true,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.CoalesceExpression)
                    => IsAnchored(binary.Left) || IsAnchored(binary.Right),
                ConditionalExpressionSyntax conditional
                    => IsAnchored(conditional.WhenTrue) || IsAnchored(conditional.WhenFalse),
                AssignmentExpressionSyntax assignment => IsAnchored(assignment.Left) || IsAnchored(assignment.Right),
                AwaitExpressionSyntax awaited => IsAnchored(awaited.Expression),
                SwitchExpressionSyntax switched => switched.Arms.Any(arm => IsAnchored(arm.Expression)),
                _ => false
            };
        }

        private bool NameAnchors(SimpleNameSyntax name, ExpressionSyntax? receiver)
        {
            var (symbol, written) = BindName(name, _model);
            return written is not null || MemberType(symbol, receiver) is not null
                   || DeclarationAnchors(symbol) || NameHasWrittenTypeArguments(name) || IsAnchored(receiver)
                   || (symbol is ILocalSymbol local && LocalAssignmentsAnchor(local, name));
        }

        private bool LocalAssignmentsAnchor(ILocalSymbol local, SimpleNameSyntax use)
        {
            if (_localAnchors.TryGetValue(local, out var anchored))
                return anchored;
            if (_checkingAnchors.Contains(local))
                return true;
            if (_model.GetOperation(use, _cancellationToken) is not { } body)
                return false;

            while (body.Parent is { } parent)
                body = parent;
            if (!_aliasesByBody.TryGetValue(body.Syntax, out var aliases))
            {
                aliases = new LocalAliases([body], _cancellationToken);
                _aliasesByBody.Add(body.Syntax, aliases);
            }

            var outermost = _checkingAnchors.Count == 0;
            var visited = new List<ILocalSymbol>();
            try
            {
                anchored = AllAssignmentsAnchor(local, aliases, visited);
            }
            finally
            {
                foreach (var dependency in visited)
                    _checkingAnchors.Remove(dependency);
            }

            // Only cache proofs that stand on their own. Revisiting an active local assumes its
            // assignments preserve the anchor, which the outer search still has to verify.
            if (outermost)
            {
                _localAnchors[local] = anchored;
                if (anchored)
                    foreach (var dependency in visited)
                        _localAnchors[dependency] = true;
            }

            return anchored;
        }

        private bool AllAssignmentsAnchor(ILocalSymbol local, LocalAliases aliases, List<ILocalSymbol> visited)
        {
            var pending = new Stack<IOperation>();
            if (!AddAssignments(local))
                return false;

            while (pending.Count > 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var value = pending.Pop();
                switch (value)
                {
                    case IParenthesizedOperation parenthesized:
                        pending.Push(parenthesized.Operand);
                        break;
                    case IConversionOperation { IsImplicit: true, OperatorMethod: null } conversion:
                        pending.Push(conversion.Operand);
                        break;
                    case IConditionalOperation { WhenFalse: { } alternative } conditional:
                        pending.Push(conditional.WhenTrue);
                        pending.Push(alternative);
                        break;
                    case ICoalesceOperation coalesce:
                        pending.Push(coalesce.Value);
                        pending.Push(coalesce.WhenNull);
                        break;
                    case ISwitchExpressionOperation switched:
                        foreach (var arm in switched.Arms)
                            pending.Push(arm.Value);
                        break;
                    case ILocalReferenceOperation reference:
                        if (!DeclarationAnchors(reference.Local) && !AddAssignments(reference.Local))
                            return false;
                        break;
                    default:
                        if (value.Syntax is not ExpressionSyntax expression || !IsAnchored(expression))
                            return false;
                        break;
                }
            }

            return true;

            bool AddAssignments(ILocalSymbol candidate)
            {
                if (_localAnchors.TryGetValue(candidate, out var known))
                    return known;
                if (!_checkingAnchors.Add(candidate))
                    return true;

                visited.Add(candidate);
                var assignments = aliases.Assignments(candidate);
                if (assignments.Count == 0)
                    return false;

                foreach (var assignment in assignments)
                {
                    if (AssignedValue(assignment) is not { } value)
                        return false;
                    pending.Push(value);
                }

                return true;
            }
        }

        private static IOperation? AssignedValue(LocalAliases.Source source)
        {
            var value = source.Value;
            if (source.Reads.IsDefaultOrEmpty)
                return value;

            foreach (var read in source.Reads)
            {
                while (value is IConversionOperation { IsImplicit: true, OperatorMethod: null } conversion)
                    value = conversion.Operand;
                if (read.TupleIndex < 0 || value is not ITupleOperation tuple || read.TupleIndex >= tuple.Elements.Length)
                    break;
                value = tuple.Elements[read.TupleIndex];
            }

            return value;
        }

        private static ExpressionSyntax? MemberReceiver(SimpleNameSyntax name)
        {
            return name.Parent switch
            {
                MemberAccessExpressionSyntax access when access.Name == name => access.Expression,
                MemberBindingExpressionSyntax binding => ConditionalReceiver(binding),
                _ => null
            };
        }

        private static ExpressionSyntax? IndexerReceiver(ExpressionSyntax expression)
        {
            return expression is ElementAccessExpressionSyntax access
                ? access.Expression
                : ConditionalReceiver(expression);
        }

        private static ExpressionSyntax? ConditionalReceiver(SyntaxNode node)
        {
            for (var current = node; current.Parent is { } parent; current = parent)
                if (parent is ConditionalAccessExpressionSyntax conditional && conditional.WhenNotNull == current)
                    return conditional.Expression;

            return null;
        }

        private void Report(Location location, string subject)
        {
            if (_filterSpan is not { } span || span.IntersectsWith(location.SourceSpan))
                context.ReportDiagnostic(Diagnostic.Create(rule, location, subject));
        }
    }
}
