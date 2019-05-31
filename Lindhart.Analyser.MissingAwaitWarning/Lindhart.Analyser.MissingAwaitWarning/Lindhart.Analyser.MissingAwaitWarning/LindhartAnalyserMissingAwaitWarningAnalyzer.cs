using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Lindhart.Analyser.MissingAwaitWarning
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LindhartAnalyserMissingAwaitWarningAnalyzer : DiagnosticAnalyzer
    {
        public const string StandardRuleId = "LindhartAnalyserMissingAwaitWarning";
        public const string StrictRuleId = "LindhartAnalyserMissingAwaitWarningStrict";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString StandardTitle = new LocalizableResourceString(nameof(Resources.StandardRuleTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString StrictTitle = new LocalizableResourceString(nameof(Resources.StandardRuleTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormatStrict = new LocalizableResourceString(nameof(Resources.StrictAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString AlternateDescription = new LocalizableResourceString(nameof(Resources.StrictRuleDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "UnintentionalUsage";

        private static readonly DiagnosticDescriptor StandardRule = new DiagnosticDescriptor(StandardRuleId, StandardTitle, MessageFormat, Category, DiagnosticSeverity.Warning, true, Description);
        private static readonly DiagnosticDescriptor StrictRule = new DiagnosticDescriptor(StrictRuleId, StrictTitle, MessageFormatStrict, Category, DiagnosticSeverity.Warning, true, AlternateDescription);


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(StandardRule, StrictRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyseSymbolNode, SyntaxKind.InvocationExpression);
            context.RegisterCodeBlockAction(AnalyzeBlockNode);
        }

        private void AnalyzeBlockNode(CodeBlockAnalysisContext context)
        {
            var walker = new UnawaitedTaskWalker(context);
            walker.Analyze(context.CodeBlock);

            foreach (var symbol in walker.UnawaitedTasks)
            {
                var diagnostic = Diagnostic.Create(StrictRule, symbol.Locations.First(), symbol.ToDisplayString());

                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyseSymbolNode(SyntaxNodeAnalysisContext syntaxNodeAnalysisContext)
        {
            if (syntaxNodeAnalysisContext.Node is InvocationExpressionSyntax node)
            {
                var symbolInfo = syntaxNodeAnalysisContext
                    .SemanticModel
                    .GetSymbolInfo(node.Expression, syntaxNodeAnalysisContext.CancellationToken);

                if ((symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault())
                    is IMethodSymbol methodSymbol)
                {
                    switch (node.Parent)
                    {
                        // Checks if a task is not awaited when the task itself is not assigned to a variable.
                        case ExpressionStatementSyntax _:
                            // Check the method return type against all the known awaitable types.
                            if (Common.EqualsType(methodSymbol.ReturnType, syntaxNodeAnalysisContext.SemanticModel, Common.AwaitableTypes))
                            {
                                var diagnostic = Diagnostic.Create(StandardRule, node.GetLocation(), methodSymbol.ToDisplayString());

                                syntaxNodeAnalysisContext.ReportDiagnostic(diagnostic);
                            }

                            break;
                    }
                }
            }
        }
    }
}
