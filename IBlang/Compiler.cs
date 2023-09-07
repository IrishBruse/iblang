namespace IBlang;

public class Compiler
{
    public static void Run(string file)
    {
        file = Path.GetFullPath(file);

        StreamReader sourceFile = File.OpenText(file);

        Console.WriteLine("-------- Lexer  --------");

        Lexer lexer = new(sourceFile, file, LexerDebug.Print);
        Tokens tokens = new(lexer.Lex());
        Parser parser = new(tokens, lexer.LineEndings, true);
        FileAst ast = parser.Parse();

        Console.WriteLine("\n-------- Parser --------");

        AstVisitor debugVisitor = new(new PrintAstDebugger());
        debugVisitor.Visit(ast);

        Console.WriteLine("\n-------- Errors --------");

        tokens.ListErrors();
    }

    public static (string expected, string output) Test(string file)
    {
        string source = File.ReadAllText(file);

        string[] lines = source.Split("\n");

        string expected = "";

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (line.StartsWith("//"))
            {
                expected += line[2..] + "\n";
            }
        }

        Run(file);

        return (expected, expected);
    }
}
