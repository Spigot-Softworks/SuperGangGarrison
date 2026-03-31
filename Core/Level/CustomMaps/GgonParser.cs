using System.Collections.ObjectModel;
using System.Text;

namespace OpenGarrison.Core;

internal abstract record GgonValue
{
    public sealed record Scalar(string Value) : GgonValue;

    public sealed record Map(IReadOnlyDictionary<string, GgonValue> Entries) : GgonValue;

    public sealed record List(IReadOnlyList<GgonValue> Items) : GgonValue;
}

internal static class GgonParser
{
    public static bool TryParse(string text, out GgonValue value)
    {
        try
        {
            var parser = new Parser(text);
            value = parser.Parse();
            return true;
        }
        catch (FormatException)
        {
            value = new GgonValue.Scalar(string.Empty);
            return false;
        }
    }

    private sealed class Parser
    {
        private readonly string _text;
        private int _index;

        public Parser(string text)
        {
            _text = text;
        }

        public GgonValue Parse()
        {
            SkipWhitespace();
            var value = ParseValue();
            SkipWhitespace();
            if (!IsAtEnd)
            {
                throw new FormatException("Unexpected trailing GGON content.");
            }

            return value;
        }

        private GgonValue ParseValue()
        {
            SkipWhitespace();
            if (IsAtEnd)
            {
                throw new FormatException("Unexpected end of GGON input.");
            }

            return Peek() switch
            {
                '{' => ParseMap(),
                '[' => ParseList(),
                '\'' => new GgonValue.Scalar(ParseQuotedString()),
                _ => new GgonValue.Scalar(ParseUnquotedString()),
            };
        }

        private GgonValue ParseMap()
        {
            Expect('{');
            SkipWhitespace();

            var entries = new Dictionary<string, GgonValue>(StringComparer.Ordinal);
            if (TryConsume('}'))
            {
                return new GgonValue.Map(new ReadOnlyDictionary<string, GgonValue>(entries));
            }

            while (true)
            {
                var key = ParseStringToken();
                SkipWhitespace();
                Expect(':');
                SkipWhitespace();
                entries[key] = ParseValue();
                SkipWhitespace();

                if (TryConsume('}'))
                {
                    return new GgonValue.Map(new ReadOnlyDictionary<string, GgonValue>(entries));
                }

                Expect(',');
                SkipWhitespace();
            }
        }

        private GgonValue ParseList()
        {
            Expect('[');
            SkipWhitespace();

            var items = new List<GgonValue>();
            if (TryConsume(']'))
            {
                return new GgonValue.List(items.AsReadOnly());
            }

            while (true)
            {
                items.Add(ParseValue());
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return new GgonValue.List(items.AsReadOnly());
                }

                Expect(',');
                SkipWhitespace();
            }
        }

        private string ParseStringToken()
        {
            SkipWhitespace();
            if (IsAtEnd)
            {
                throw new FormatException("Unexpected end of GGON input.");
            }

            return Peek() == '\''
                ? ParseQuotedString()
                : ParseUnquotedString();
        }

        private string ParseQuotedString()
        {
            Expect('\'');
            var builder = new StringBuilder();
            while (!IsAtEnd)
            {
                var character = Read();
                if (character == '\'')
                {
                    return builder.ToString();
                }

                if (character != '\\')
                {
                    builder.Append(character);
                    continue;
                }

                if (IsAtEnd)
                {
                    throw new FormatException("Unterminated GGON escape sequence.");
                }

                builder.Append(Read() switch
                {
                    '\\' => '\\',
                    '\'' => '\'',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '0' => '\0',
                    var invalid => throw new FormatException($"Unsupported GGON escape sequence '\\{invalid}'."),
                });
            }

            throw new FormatException("Unterminated GGON string.");
        }

        private string ParseUnquotedString()
        {
            var start = _index;
            while (!IsAtEnd && IsUnquotedCharacter(Peek()))
            {
                _index += 1;
            }

            if (_index == start)
            {
                throw new FormatException($"Unexpected GGON token '{Peek()}'.");
            }

            return _text[start.._index];
        }

        private static bool IsUnquotedCharacter(char character)
        {
            return char.IsAsciiLetterOrDigit(character)
                || character is '_' or '.' or '-' or '+';
        }

        private bool TryConsume(char expected)
        {
            if (!IsAtEnd && Peek() == expected)
            {
                _index += 1;
                return true;
            }

            return false;
        }

        private void Expect(char expected)
        {
            if (IsAtEnd || Read() != expected)
            {
                throw new FormatException($"Expected '{expected}'.");
            }
        }

        private void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(Peek()))
            {
                _index += 1;
            }
        }

        private char Peek()
        {
            return _text[_index];
        }

        private char Read()
        {
            return _text[_index++];
        }

        private bool IsAtEnd => _index >= _text.Length;
    }
}
