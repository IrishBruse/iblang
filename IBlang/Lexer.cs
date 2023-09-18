namespace IBlang;

using System;
using System.Globalization;
using System.IO;
using System.Text;

using IBlang.Data;

public class Lexer : IDisposable
{
    public SortedList<int, int> LineEndings { get; private set; } = new();

    public static readonly Dictionary<string, TokenType> Keywords = new()
    {
        { "func", TokenType.Keyword_Func },
        { "true", TokenType.Keyword_True },
        { "false", TokenType.Keyword_False },
    };

    private static readonly Dictionary<string, TokenType> ControlflowKeywords = new()
    {
        { "if", TokenType.Keyword_If },
        { "else", TokenType.Keyword_Else },
        { "return", TokenType.Keyword_Return },
    };

    private const ConsoleColor CommentColor = ConsoleColor.DarkGray;
    private const ConsoleColor WhitespaceColor = ConsoleColor.DarkGray;
    private const ConsoleColor KeywordColor = ConsoleColor.Blue;
    private const ConsoleColor BracketsColor = ConsoleColor.DarkGreen;
    private const ConsoleColor ErrorColor = ConsoleColor.DarkRed;
    private const ConsoleColor OperatorColor = ConsoleColor.Red;
    private const ConsoleColor NumberColor = ConsoleColor.Cyan;
    private const ConsoleColor StringColor = ConsoleColor.Yellow;
    private const ConsoleColor IdentifierColor = ConsoleColor.Gray;
    private const ConsoleColor ControlflowColor = ConsoleColor.Magenta;

    private StreamReader sourceFile;
    private readonly string file;

    private readonly LexerDebug flags;

    private int endIndex;
    private int startIndex;
    private int line;

    public Lexer(StreamReader sourceFile, string file, LexerDebug flags = LexerDebug.None)
    {
        this.sourceFile = sourceFile;
        this.file = file;

        this.flags = flags;
    }

    public Lexer(string sourceText, LexerDebug flags = LexerDebug.None)
    {
        file = "__NOFILE__.ib";
        MemoryStream stream = new();
        StreamWriter writer = new(stream);
        writer.Write(sourceText);
        writer.Flush();
        stream.Position = 0;

        sourceFile = new StreamReader(stream);

        this.flags = flags;
    }

    static Lexer()
    {
        foreach ((string key, TokenType value) in ControlflowKeywords)
        {
            Keywords.Add(key, value);
        }
    }

    public IEnumerator<Token> Lex()
    {
        while (!sourceFile.EndOfStream)
        {
            char c = Peek();

            startIndex = endIndex;

            if (char.IsWhiteSpace(c))
            {
                EatWhitespace(c);
            }
            else if (char.IsLetter(c))
            {
                yield return LexIdentifier();
            }
            else if (char.IsNumber(c))
            {
                yield return LexNumber();
            }
            else
            {
                yield return c switch
                {
                    '"' => LexString(),

                    '[' => LexBracket(TokenType.OpenBracket),
                    ']' => LexBracket(TokenType.CloseBracket),

                    '(' => LexBracket(TokenType.OpenParenthesis),
                    ')' => LexBracket(TokenType.CloseParenthesis),

                    '{' => LexBracket(TokenType.OpenScope),
                    '}' => LexBracket(TokenType.CloseScope),

                    '<' => LexOperator(TokenType.LessThan),
                    '>' => LexOperator(TokenType.GreaterThan),
                    '+' => LexOperator(TokenType.Addition),
                    '-' => LexOperator(TokenType.Subtraction),
                    '*' => LexOperator(TokenType.Multiplication),
                    '/' => LexOperator(TokenType.Division),
                    '&' => LexOperator(TokenType.BitwiseAnd),
                    '|' => LexOperator(TokenType.BitwiseOr),
                    '%' => LexOperator(TokenType.Modulo),
                    '!' => LexOperator(TokenType.LogicalNot),
                    '=' => LexOperator(TokenType.Assignment),

                    _ => new Token(c.ToString(), TokenType.Garbage, new(file, startIndex, endIndex))
                };
            }
        }

        yield return new Token(string.Empty, TokenType.Eof, new(file, startIndex, endIndex));
    }

    private void EatWhitespace(char c)
    {
        if (c == '\r')
        {
            Next(display: flags.HasFlag(LexerDebug.Whitespace) ? "\\r" : "", foreground: WhitespaceColor);
        }
        else if (c == '\n')
        {
            line++;
            LineEndings.Add(endIndex, line);
            Next(display: flags.HasFlag(LexerDebug.Whitespace) ? "\\n\n" : "\n", foreground: WhitespaceColor);
        }
        else if (c == '\t')
        {
            Next(display: flags.HasFlag(LexerDebug.Whitespace) ? "»   " : "    ", foreground: WhitespaceColor);
        }
        else
        {
            // Eat all other whitespace
            Next(display: flags.HasFlag(LexerDebug.Whitespace) ? "·" : " ", foreground: WhitespaceColor);
        }
    }

    private Token LexOperator(TokenType type)
    {
        char c = Next(OperatorColor);
        char p = Peek();

        string op = c.ToString();

        if (c == '<' && p == '=')
        {
            type = TokenType.LessThanEqual;
            op += Next(OperatorColor);
        }
        else if (c == '>' && p == '=')
        {
            type = TokenType.GreaterThanEqual;
            op += Next(OperatorColor);
        }
        else if (c == '=' && p == '=')
        {
            type = TokenType.EqualEqual;
            op += Next(OperatorColor);
        }
        else if (c == '!' && p == '=')
        {
            type = TokenType.NotEqual;
            op += Next(OperatorColor);
        }
        else if (c == '&' && p == '&')
        {
            type = TokenType.LogicalAnd;
            op += Next(OperatorColor);
        }
        else if (c == '|' && p == '|')
        {
            type = TokenType.LogicalOr;
            op += Next(OperatorColor);
        }
        else if (c == '+' && p == '=')
        {
            type = TokenType.AdditionAssignment;
            op += Next(OperatorColor);
        }
        else if (c == '-' && p == '=')
        {
            type = TokenType.SubtractionAssignment;
            op += Next(OperatorColor);
        }
        else if (c == '*' && p == '=')
        {
            type = TokenType.MultiplicationAssignment;
            op += Next(OperatorColor);
        }
        else if (c == '/' && p == '=')
        {
            type = TokenType.DivisionAssignment;
            op += Next(OperatorColor);
        }
        else if (c == '%' && p == '=')
        {
            type = TokenType.ModuloAssignment;
            op += Next(OperatorColor);
        }
        else if (c == '<' && p == '<')
        {
            type = TokenType.BitwiseShiftLeft;
            op += Next(OperatorColor);
        }
        else if (c == '>' && p == '>')
        {
            type = TokenType.BitwiseShiftRight;
            op += Next(OperatorColor);
        }
        else if (c == '/' && p == '/') // Single line comment
        {
            return LexSingleLineComment();
        }

        return new Token(op, type, new(file, startIndex, endIndex));
    }

    private Token LexSingleLineComment()
    {
        if (flags.HasFlag(LexerDebug.Print))
        {
            Console.CursorLeft--;
            Print('/', foreground: CommentColor);
        }

        StringBuilder comment = new("/");

        while (!IsLineBreak(Peek()) && !sourceFile.EndOfStream)
        {
            comment.Append(Next(CommentColor));
        }

        return new Token(comment.ToString(), TokenType.Comment, new(file, startIndex, endIndex));
    }

    private Token LexBracket(TokenType type)
    {
        char c = Next(BracketsColor);

        return new Token(c.ToString(), type, new(file, startIndex, endIndex));
    }

    private Token LexString()
    {
        StringBuilder literal = new();

        // Eat first "
        char c = Next(StringColor);
        literal.Append(c);

        do
        {
            c = Next(StringColor);
            literal.Append(c);
        }
        while (c != '"' && !IsLineBreak(c));

        return new Token(literal.ToString(), TokenType.StringLiteral, new(file, startIndex, endIndex));
    }

    private Token LexIdentifier()
    {
        StringBuilder identifierBuilder = new();
        char c;

        do
        {
            c = Next(IdentifierColor);
            identifierBuilder.Append(c);
        }
        while (char.IsLetterOrDigit(Peek()) && !IsLineBreak(c));

        string identifier = identifierBuilder.ToString().ToLower(CultureInfo.CurrentCulture);

        if (Keywords.TryGetValue(identifier, out TokenType keyword))
        {
            if (flags.HasFlag(LexerDebug.Print))
            {
                // Recolor keywords
                if (!Console.IsOutputRedirected)
                {
                    Console.CursorLeft -= identifier.Length;
                }

                ConsoleColor color = ControlflowKeywords.TryGetValue(identifier, out _) ? ControlflowColor : KeywordColor;
                Print(identifier, foreground: color);
            }

            return new Token(identifierBuilder.ToString(), keyword, new(file, startIndex, endIndex));
        }
        else
        {
            return new Token(identifierBuilder.ToString(), TokenType.Identifier, new(file, startIndex, endIndex));
        }
    }

    private char Peek()
    {
        return (char)sourceFile.Peek();
    }

    private Token LexNumber()
    {
        StringBuilder number = new();

        char c;

        do
        {
            c = Next(NumberColor);
            number.Append(c);
        }
        while (char.IsDigit(Peek()));

        return new Token(number.ToString(), TokenType.IntegerLiteral, new(file, startIndex, endIndex));
    }

    private bool IsLineBreak(char c)
    {
        return c == '\n' || c == '\r';
    }

    private char Next(ConsoleColor foreground = ConsoleColor.White, ConsoleColor background = ConsoleColor.Black, string? display = null)
    {
        char c = (char)sourceFile.Read();

        endIndex++;

        if (flags.HasFlag(LexerDebug.Print))
        {
            if (display != null)
            {
                Print(display, background, foreground);
            }
            else
            {
                Print(c, background, foreground);
            }
        }

        return c;
    }

    private static void Print(char c, ConsoleColor background = ConsoleColor.Black, ConsoleColor foreground = ConsoleColor.White)
    {
        Console.BackgroundColor = background;
        Console.ForegroundColor = foreground;
        Console.Write(c);
        Console.ResetColor();
    }

    private static void Print(string str, ConsoleColor background = ConsoleColor.Black, ConsoleColor foreground = ConsoleColor.White)
    {
        Console.BackgroundColor = background;
        Console.ForegroundColor = foreground;
        Console.Write(str);
        Console.ResetColor();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        sourceFile.Dispose();
    }
}
