using Lua.CodeAnalysis.Syntax;
using Lua.CodeAnalysis.Syntax.Nodes;

namespace Lua.Tests
{
    // TODO: add more tests

    public class ParserTests
    {
        [Test]
        public void Test_If_ElseIf_Else_Empty()
        {
            var source =
@"if true then
elseif true then
else
end";
            var actual = LuaSyntaxTree.Parse(source).Nodes[0];
            var expected = new IfStatementNode(
                new() { ConditionNode = new BooleanLiteralNode(true, new(1, 3)), ThenNodes = [] },
                [new() { ConditionNode = new BooleanLiteralNode(true, new(2, 7)), ThenNodes = [] }],
                [],
                new(1, 0));

            Assert.That(actual, Is.TypeOf<IfStatementNode>());
            Assert.That(actual.ToString(), Is.EqualTo(expected.ToString()));
        }
        
        [Test]
        public void Test_MultiLine_ShortString()
        {
            var source = 
"""
print "Hello,\
World!"
""";
            var actual = LuaSyntaxTree.Parse(source).Nodes[0]; 
             Assert.That(actual, Is.TypeOf<CallFunctionStatementNode>());
             var literal =((StringLiteralNode)((CallFunctionStatementNode)actual).Expression.ArgumentNodes[0]).Text.ToString();
             Assert.That(literal,  Is.EqualTo(
                 """
                 Hello,\
                 World!
                 """
                 ));
        }
    }
}