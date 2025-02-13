namespace Lua.CodeAnalysis.Syntax.Nodes;

public record LocalFunctionDeclarationStatementNode(ReadOnlyMemory<char> Name, IdentifierNode[] ParameterNodes, SyntaxNode[] Nodes, bool HasVariableArguments, SourcePosition Position, int LineDefined,int LastLineDefined) : FunctionDeclarationStatementNode(Name, ParameterNodes, Nodes, HasVariableArguments, Position, LineDefined, LastLineDefined)
{
    public override TResult Accept<TContext, TResult>(ISyntaxNodeVisitor<TContext, TResult> visitor, TContext context)
    {
        return visitor.VisitLocalFunctionDeclarationStatementNode(this, context);
    }
}