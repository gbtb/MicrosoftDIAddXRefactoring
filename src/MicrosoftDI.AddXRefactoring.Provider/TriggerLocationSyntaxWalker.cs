using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MicrosoftDI.AddXRefactoring.Provider;

/// <summary>
/// Used to detect position on which refactoring was triggered
/// </summary>
internal class TriggerLocationSyntaxWalker: CSharpSyntaxWalker
{
    private bool _isComplete;
    
    /// <summary>
    /// BaseType on which refactoring was triggered
    /// </summary>
    public BaseTypeSyntax? BaseType { get; private set; }
    
    /// <summary>
    /// Type declaration for which refactoring was triggered
    /// </summary>
    public TypeDeclarationSyntax? TypeDeclarationSyntax { get; private set; }

    public override void DefaultVisit(SyntaxNode node)
    {
        //dont trigger inside class body
        if (node is MemberDeclarationSyntax)
            return;
        
        if (!_isComplete && node.Parent != null)
            Visit(node.Parent);
    }

    public override void VisitSimpleBaseType(SimpleBaseTypeSyntax node)
    {
        BaseType = node;
        base.VisitSimpleBaseType(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        TypeDeclarationSyntax = node;
        _isComplete = true;
    }
    
    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        TypeDeclarationSyntax = node;
        _isComplete = true;
    }
    
    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        _isComplete = true;
    }
}