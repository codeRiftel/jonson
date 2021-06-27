using System.Collections.Generic;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using option;

namespace vjp {
    public class JSONType {
        public Option<string> Str;
        public Option<string> Num;
        public Option<bool> Bool;
        public Option<Dictionary<string, JSONType>> Obj;
        public Option<List<JSONType>> Arr;
        public Option<JSONNull> Null;

        public static JSONType Make(string str) {
            if (str == null) {
                return JSONType.Make();
            }

            JSONType type = new JSONType();
            type.Str = Option<string>.Some(str);
            return type;
        }

        public static JSONType Make(bool b) {
            JSONType type = new JSONType();
            type.Bool = Option<bool>.Some(b);
            return type;
        }

        public static JSONType Make(Dictionary<string, JSONType> obj) {
            if (obj == null) {
                return JSONType.Make();
            }

            JSONType type = new JSONType();
            type.Obj = Option<Dictionary<string, JSONType>>.Some(obj);
            return type;
        }

        public static JSONType Make(List<JSONType> arr) {
            if (arr == null) {
                return JSONType.Make();
            }

            JSONType type = new JSONType();
            type.Arr = Option<List<JSONType>>.Some(arr);
            return type;
        }

        public static JSONType Make() {
            JSONType type = new JSONType();
            type.Null = Option<JSONNull>.Some(new JSONNull());
            return type;
        }
    }

    public struct JSONNull { }

    public enum JSONErrType {
        UnknownToken,
        NaN,
        HangingStr,
        HangingNum,
        HangingHex,
        HangingSurrogatePair,
        ExpObj,
        ExpKey,
        ExpColon,
        ExpValue,
        ExpComma,
        ExpArr,
        IncorrectNum,
        MultipleValues,
        MaxDepth,
        NotHex,
        UnescapedControl
    }

    public struct JSONError {
        public JSONErrType type;
        public int position;

        public static JSONError Make(JSONErrType type, int position) {
            JSONError err = new JSONError();
            err.type = type;
            err.position = position;
            return err;
        }
    }

    enum TokenType {
        Unknown,
        BeginObj,
        EndObj,
        BeginArr,
        EndArr,
        ValueSep,
        NameSep,
        String,
        Number,
        Null,
        False,
        True,
        EOF
    }

    struct Token {
        public TokenType type;
        public int start;
        public int length;
    }

    struct Lexed {
        public int advance;
        public Token token;
    }

    struct Parsed {
        public int advance;
        public JSONType type;
    }

    struct KeyVal {
        public int advance;
        public string key;
        public JSONType value;
    }

    enum NumState {
        IntMinus,
        Int,
        DecimalPoint,
        Frac,
        E,
        ExpSign,
        Exp
    }

    public static class VJP {
        private static string TRUE = "true";
        private static string FALSE = "false";
        private static string NULL = "null";

        public static Result<JSONType, JSONError> Parse(string data, int maxDepth) {
            Result<Parsed, JSONError> parseRootRes = ParseValue(data, 0, maxDepth);
            if (parseRootRes.IsErr()) {
                return Result<JSONType, JSONError>.Err(parseRootRes.AsErr());
            }

            Parsed parseRoot = parseRootRes.AsOk();

            Result<Lexed, JSONError> lexRes = Lex(data, parseRoot.advance);
            if (lexRes.IsErr()) {
                return Result<JSONType, JSONError>.Err(lexRes.AsErr());
            }

            Lexed lex = lexRes.AsOk();
            if (lex.token.type != TokenType.EOF) {
                JSONError error = JSONError.Make(JSONErrType.MultipleValues, parseRoot.advance);
                return Result<JSONType, JSONError>.Err(error);
            }

            return Result<JSONType, JSONError>.Ok(parseRoot.type);
        }

        public static string Generate(JSONType type) {
            StringBuilder builder = new StringBuilder();
            GenerateType(type, builder, false, 0);
            return builder.ToString();
        }

        public static string GeneratePretty(JSONType type) {
            StringBuilder builder = new StringBuilder();
            GenerateType(type, builder, true, 0);
            return builder.ToString();
        }

        private static void AddIndent(StringBuilder builder, int count) {
            for (int i = 0; i < 4 * count; i++) {
                builder.Append(' ');
            }
        }

        private static void GenerateType(JSONType type, StringBuilder builder, bool p, int d) {
            if (type.Obj.IsSome()) {
                builder.Append('{');
                d++;

                Dictionary<string, JSONType> obj = type.Obj.Peel();

                if (p) {
                    if (obj.Count > 0) {
                        builder.Append('\n');
                    }
                    AddIndent(builder, d);
                }

                int i = 0;
                foreach (KeyValuePair<string, JSONType> pair in obj) {
                    builder.Append('"');
                    builder.Append(pair.Key);
                    builder.Append('"');
                    builder.Append(':');

                    if (p) {
                        builder.Append(' ');
                    }

                    GenerateType(pair.Value, builder, p, d);

                    if (i < obj.Count - 1) {
                        builder.Append(',');

                        if (p) {
                            builder.Append('\n');
                            AddIndent(builder, d);
                        }
                    }

                    i++;
                }

                d--;
                if (p) {
                    builder.Append('\n');
                    AddIndent(builder, d);
                }

                builder.Append('}');
            } else if (type.Arr.IsSome()) {
                builder.Append('[');
                d++;

                List<JSONType> arr = type.Arr.Peel();

                if (p) {
                    if (arr.Count > 0) {
                        builder.Append('\n');
                    }

                    AddIndent(builder, d);
                }

                int i = 0;
                foreach (JSONType element in arr) {
                    GenerateType(element, builder, p, d);

                    if (i < arr.Count - 1) {
                        builder.Append(',');

                        if (p) {
                            builder.Append('\n');
                            AddIndent(builder, d);
                        }
                    }

                    i++;
                }

                d--;
                if (p) {
                    builder.Append('\n');
                    AddIndent(builder, d);
                }

                builder.Append(']');
            } else if (type.Str.IsSome()) {
                builder.Append('"');
                Escape(type.Str.Peel(), builder);
                builder.Append('"');
            } else if (type.Num.IsSome()) {
                builder.Append(type.Num.Peel().ToString(CultureInfo.InvariantCulture));
            } else if (type.Bool.IsSome()) {
                bool value = type.Bool.Peel();
                if (value) {
                    builder.Append(TRUE);
                } else {
                    builder.Append(FALSE);
                }
            } else if (type.Null.IsSome()) {
                builder.Append(NULL);
            }
        }

        private static Result<Parsed, JSONError> ParseObject(string data, int start, int depth) {
            if (depth == 0) {
                return Result<Parsed, JSONError>.Err(JSONError.Make(JSONErrType.MaxDepth, start));
            }

            Parsed res = new Parsed();
            int pos = start;

            JSONType objType = new JSONType();
            Dictionary<string, JSONType> obj = new Dictionary<string, JSONType>();
            objType.Obj = Option<Dictionary<string, JSONType>>.Some(obj);
            res.type = objType;

            Result<Lexed, JSONError> openLexRes = Lex(data, pos);
            if (openLexRes.IsErr()) {
                return Result<Parsed, JSONError>.Err(openLexRes.AsErr());
            }

            Lexed openLex = openLexRes.AsOk();
            if (openLex.token.type != TokenType.BeginObj) {
                return Result<Parsed, JSONError>.Err(JSONError.Make(JSONErrType.ExpObj, pos));
            }

            pos += openLex.advance;

            Result<Lexed, JSONError> closeLexRes = Lex(data, pos);
            if (closeLexRes.IsErr()) {
                return Result<Parsed, JSONError>.Err(closeLexRes.AsErr());
            }

            Lexed closeLex = closeLexRes.AsOk();
            if (closeLex.token.type == TokenType.EndObj) {
                pos += closeLex.advance;
            } else {
                while (true) {
                    Result<KeyVal, JSONError> kvParsedRes = ParseKeyVal(data, pos, depth);
                    if (kvParsedRes.IsErr()) {
                        return Result<Parsed, JSONError>.Err(kvParsedRes.AsErr());
                    }

                    KeyVal keyvalParsed = kvParsedRes.AsOk();

                    obj[keyvalParsed.key] = keyvalParsed.value;

                    pos += keyvalParsed.advance;

                    Result<Lexed, JSONError> lexCommaRes = Lex(data, pos);
                    if (lexCommaRes.IsErr()) {
                        return Result<Parsed, JSONError>.Err(lexCommaRes.AsErr());
                    }

                    Lexed lexComma = lexCommaRes.AsOk();

                    if (lexComma.token.type == TokenType.EndObj) {
                        pos += lexComma.advance;
                        break;
                    } else if (lexComma.token.type != TokenType.ValueSep) {
                        JSONError error = JSONError.Make(JSONErrType.ExpComma, pos);
                        return Result<Parsed, JSONError>.Err(error);
                    }

                    pos += lexComma.advance;
                }
            }

            res.advance = pos - start;
            return Result<Parsed, JSONError>.Ok(res);
        }

        private static Result<KeyVal, JSONError> ParseKeyVal(string data, int start, int depth) {
            KeyVal res = new KeyVal();
            int pos = start;

            Result<Lexed, JSONError> lexKeyRes = Lex(data, pos);
            if (lexKeyRes.IsErr()) {
                return Result<KeyVal, JSONError>.Err(lexKeyRes.AsErr());
            }

            Lexed lexKey = lexKeyRes.AsOk();
            if (lexKey.token.type != TokenType.String) {
                JSONError error = JSONError.Make(JSONErrType.ExpKey, pos);
                return Result<KeyVal, JSONError>.Err(error);
            }

            pos += lexKey.advance;

            Result<Lexed, JSONError> lexColonRes = Lex(data, pos);
            if (lexColonRes.IsErr()) {
                return Result<KeyVal, JSONError>.Err(lexColonRes.AsErr());
            }

            Lexed lexColon = lexColonRes.AsOk();
            if (lexColon.token.type != TokenType.NameSep) {
                JSONError error = JSONError.Make(JSONErrType.ExpColon, pos);
                return Result<KeyVal, JSONError>.Err(error);
            }

            pos += lexColon.advance;

            Result<Parsed, JSONError> parseValRes = ParseValue(data, pos, depth);
            if (parseValRes.IsErr()) {
                return Result<KeyVal, JSONError>.Err(parseValRes.AsErr());
            }

            Parsed parseVal = parseValRes.AsOk();

            pos += parseVal.advance;

            int keyStart = lexKey.token.start + 1;
            int keyLength = lexKey.token.length - 2;

            Result<string, JSONErrType> keyRes = Unescape(data, keyStart, keyLength);
            if (keyRes.IsErr()) {
                return Result<KeyVal, JSONError>.Err(JSONError.Make(keyRes.AsErr(), pos));
            }

            res.key = keyRes.AsOk();
            res.value = parseVal.type;

            res.advance = pos - start;
            return Result<KeyVal, JSONError>.Ok(res);
        }

        private static Result<Parsed, JSONError> ParseArray(string data, int start, int depth) {
            if (depth == 0) {
                return Result<Parsed, JSONError>.Err(JSONError.Make(JSONErrType.MaxDepth, start));
            }

            Parsed res = new Parsed();
            int pos = start;

            Result<Lexed, JSONError> lexOpenRes = Lex(data, pos);
            if (lexOpenRes.IsErr()) {
                return Result<Parsed, JSONError>.Err(lexOpenRes.AsErr());
            }

            Lexed lexOpen = lexOpenRes.AsOk();
            if (lexOpen.token.type != TokenType.BeginArr) {
                return Result<Parsed, JSONError>.Err(JSONError.Make(JSONErrType.ExpArr, pos));
            }

            pos += lexOpen.advance;

            JSONType arrayType = new JSONType();
            List<JSONType> array = new List<JSONType>();
            arrayType.Arr = Option<List<JSONType>>.Some(array);
            res.type = arrayType;

            Result<Lexed, JSONError> lexCloseRes = Lex(data, pos);
            if (lexCloseRes.IsErr()) {
                return Result<Parsed, JSONError>.Err(lexCloseRes.AsErr());
            }

            Lexed lexClose = lexCloseRes.AsOk();
            if (lexClose.token.type == TokenType.EndArr) {
                pos += lexClose.advance;
            } else {
                while (true) {
                    Result<Parsed, JSONError> parseValRes = ParseValue(data, pos, depth);
                    if (parseValRes.IsErr()) {
                        return Result<Parsed, JSONError>.Err(parseValRes.AsErr());
                    }

                    Parsed parseVal = parseValRes.AsOk();

                    pos += parseVal.advance;

                    array.Add(parseVal.type);

                    Result<Lexed, JSONError> lexCommaRes = Lex(data, pos);
                    if (lexCommaRes.IsErr()) {
                        return Result<Parsed, JSONError>.Err(lexCommaRes.AsErr());
                    }

                    Lexed lexComma = lexCommaRes.AsOk();
                    if (lexComma.token.type == TokenType.EndArr) {
                        pos += lexComma.advance;
                        break;
                    } else if (lexComma.token.type != TokenType.ValueSep) {
                        JSONError error = JSONError.Make(JSONErrType.ExpComma, pos);
                        return Result<Parsed, JSONError>.Err(error);
                    }

                    pos += lexComma.advance;
                }
            }

            res.advance = pos - start;
            return Result<Parsed, JSONError>.Ok(res);
        }

        private static Result<Parsed, JSONError> ParseValue(string data, int start, int depth) {
            Parsed res = new Parsed();
            int pos = start;

            Result<Lexed, JSONError> lexValRes = Lex(data, pos);
            if (lexValRes.IsErr()) {
                return Result<Parsed, JSONError>.Err(lexValRes.AsErr());
            }

            Lexed lexVal = lexValRes.AsOk();

            JSONType valType = new JSONType();

            Token token = lexVal.token;
            switch (token.type) {
                case TokenType.Number:
                    valType.Num = Option<string>.Some(data.Substring(token.start, token.length));
                    pos += lexVal.advance;
                    break;
                case TokenType.String:
                    int strStart = token.start + 1;
                    int strLength = token.length - 2;
                    Result<string, JSONErrType> strRes = Unescape(data, strStart, strLength);
                    if (strRes.IsErr()) {
                        return Result<Parsed, JSONError>.Err(JSONError.Make(strRes.AsErr(), pos));
                    }
                    valType.Str = Option<String>.Some(strRes.AsOk());
                    pos += lexVal.advance;
                    break;
                case TokenType.True:
                    valType.Bool = Option<bool>.Some(true);
                    pos += lexVal.advance;
                    break;
                case TokenType.False:
                    valType.Bool = Option<bool>.Some(false);
                    pos += lexVal.advance;
                    break;
                case TokenType.Null:
                    valType.Null = Option<JSONNull>.Some(new JSONNull());
                    pos += lexVal.advance;
                    break;
                case TokenType.BeginObj:
                    Result<Parsed, JSONError> parseObjRes = ParseObject(data, pos, depth - 1);
                    if (parseObjRes.IsErr()) {
                        return Result<Parsed, JSONError>.Err(parseObjRes.AsErr());
                    }

                    Parsed parseObj = parseObjRes.AsOk();
                    valType = parseObj.type;
                    pos += parseObj.advance;
                    break;
                case TokenType.BeginArr:
                    Result<Parsed, JSONError> parseArrRes = ParseArray(data, pos, depth - 1);
                    if (parseArrRes.IsErr()) {
                        return Result<Parsed, JSONError>.Err(parseArrRes.AsErr());
                    }

                    Parsed parseArr = parseArrRes.AsOk();
                    valType = parseArr.type;
                    pos += parseArr.advance;
                    break;
                default:
                    JSONError error = JSONError.Make(JSONErrType.ExpValue, pos);
                    return Result<Parsed, JSONError>.Err(error);
            }

            res.type = valType;
            res.advance = pos - start;

            return Result<Parsed, JSONError>.Ok(res);
        }

        private static Result<Lexed, JSONError> Lex(string data, int start) {
            Lexed res = new Lexed();
            Token token = new Token();

            int pos = start;

            while (pos < data.Length && IsWhiteSpace(data[pos])) {
                pos++;
            }

            if (pos >= data.Length) {
                token.type = TokenType.EOF;
            } else {
                int first;
                switch (data[pos]) {
                    case '{':
                        token.type = TokenType.BeginObj;
                        token.start = pos;
                        token.length = 1;
                        pos++;
                        break;
                    case '}':
                        token.type = TokenType.EndObj;
                        token.start = pos;
                        token.length = 1;
                        pos++;
                        break;
                    case '[':
                        token.type = TokenType.BeginArr;
                        token.start = pos;
                        token.length = 1;
                        pos++;
                        break;
                    case ']':
                        token.type = TokenType.EndArr;
                        token.start = pos;
                        token.length = 1;
                        pos++;
                        break;
                    case ',':
                        token.type = TokenType.ValueSep;
                        token.start = pos;
                        token.length = 1;
                        pos++;
                        break;
                    case ':':
                        token.type = TokenType.NameSep;
                        token.start = pos;
                        token.length = 1;
                        pos++;
                        break;
                    case '"':
                        first = pos;
                        pos++;
                        while (pos < data.Length) {
                            if (data[pos] == '"') {
                                token.type = TokenType.String;
                                token.start = first;
                                token.length = pos - first + 1;
                                pos++;
                                break;
                            }

                            if (data[pos] == '\\') {
                                if (pos + 2 > data.Length) {
                                    pos = data.Length;
                                } else {
                                    pos += 2;
                                }
                            } else {
                                pos++;
                            }

                            if (pos == data.Length) {
                                JSONError error = JSONError.Make(JSONErrType.HangingStr, pos);
                                return Result<Lexed, JSONError>.Err(error);
                            }
                        }
                        break;
                    case '-':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        NumState numState = NumState.IntMinus;
                        if (data[pos] != '-') {
                            numState = NumState.Int;
                        }

                        first = pos;
                        pos++;

                        JSONError nanErr = JSONError.Make(JSONErrType.NaN, pos);

                        while (pos < data.Length) {
                            NumState next = numState;

                            bool isDigit = data[pos] >= '0' && data[pos] <= '9';
                            bool isPoint = data[pos] == '.';
                            bool isExp = data[pos] == 'e' || data[pos] == 'E';
                            bool isSign = data[pos] == '-' || data[pos] == '+';

                            if (!(isDigit || isPoint || isExp || isSign)) {
                                if (numState == NumState.IntMinus) {
                                    return Result<Lexed, JSONError>.Err(nanErr);
                                }

                                if (numState == NumState.DecimalPoint) {
                                    return Result<Lexed, JSONError>.Err(nanErr);
                                }

                                if (numState == NumState.E) {
                                    return Result<Lexed, JSONError>.Err(nanErr);
                                }

                                if (numState == NumState.ExpSign) {
                                    return Result<Lexed, JSONError>.Err(nanErr);
                                }

                                token.type = TokenType.Number;
                                token.start = first;
                                token.length = pos - first;
                                break;
                            }

                            switch (numState) {
                                case NumState.IntMinus:
                                    if (isDigit) {
                                        next = NumState.Int;
                                    } else {
                                        return Result<Lexed, JSONError>.Err(nanErr);
                                    }
                                    break;
                                case NumState.Int:
                                    if (isDigit) {
                                        next = NumState.Int;
                                    } else if (isPoint) {
                                        next = NumState.DecimalPoint;
                                    } else if (isExp) {
                                        next = NumState.E;
                                    } else {
                                        return Result<Lexed, JSONError>.Err(nanErr);
                                    }
                                    break;
                                case NumState.DecimalPoint:
                                    if (isDigit) {
                                        next = NumState.Frac;
                                    } else {
                                        return Result<Lexed, JSONError>.Err(nanErr);
                                    }
                                    break;
                                case NumState.Frac:
                                    if (isDigit) {
                                        next = NumState.Frac;
                                    } else if (isExp) {
                                        next = NumState.E;
                                    } else {
                                        return Result<Lexed, JSONError>.Err(nanErr);
                                    }
                                    break;
                                case NumState.E:
                                    if (isSign) {
                                        next = NumState.ExpSign;
                                    } else if (isDigit) {
                                        next = NumState.Exp;
                                    } else {
                                        return Result<Lexed, JSONError>.Err(nanErr);
                                    }
                                    break;
                                case NumState.ExpSign:
                                    if (isDigit) {
                                        next = NumState.Exp;
                                    } else {
                                        return Result<Lexed, JSONError>.Err(nanErr);
                                    }
                                    break;
                                case NumState.Exp:
                                    if (isDigit) {
                                        next = NumState.Exp;
                                    } else {
                                        return Result<Lexed, JSONError>.Err(nanErr);
                                    }
                                    break;
                            }

                            numState = next;

                            pos++;
                            if (pos == data.Length) {
                                JSONError error = JSONError.Make(JSONErrType.HangingNum, pos);
                                return Result<Lexed, JSONError>.Err(error);
                            }
                        }
                        break;
                    case 'n':
                        if (data.Length - pos < NULL.Length) {
                            JSONError error = JSONError.Make(JSONErrType.UnknownToken, pos);
                            return Result<Lexed, JSONError>.Err(error);
                        }

                        for (int i = 1; i < NULL.Length; i++) {
                            if (data[pos + i] != NULL[i]) {
                                JSONError error = JSONError.Make(JSONErrType.UnknownToken, pos);
                                return Result<Lexed, JSONError>.Err(error);
                            }
                        }

                        token.type = TokenType.Null;
                        token.start = pos;
                        token.length = NULL.Length;
                        pos += NULL.Length;
                        break;
                    case 't':
                        if (data.Length - pos < TRUE.Length) {
                            JSONError error = JSONError.Make(JSONErrType.UnknownToken, pos);
                            return Result<Lexed, JSONError>.Err(error);
                        }

                        for (int i = 1; i < TRUE.Length; i++) {
                            if (data[pos + i] != TRUE[i]) {
                                JSONError error = JSONError.Make(JSONErrType.UnknownToken, pos);
                                return Result<Lexed, JSONError>.Err(error);
                            }
                        }

                        token.type = TokenType.True;
                        token.start = pos;
                        token.length = TRUE.Length;
                        pos += TRUE.Length;
                        break;
                    case 'f':
                        if (data.Length - pos < FALSE.Length) {
                            JSONError error = JSONError.Make(JSONErrType.UnknownToken, pos);
                            return Result<Lexed, JSONError>.Err(error);
                        }

                        for (int i = 1; i < FALSE.Length; i++) {
                            if (data[pos + i] != FALSE[i]) {
                                JSONError error = JSONError.Make(JSONErrType.UnknownToken, pos);
                                return Result<Lexed, JSONError>.Err(error);
                            }
                        }

                        token.type = TokenType.False;
                        token.start = pos;
                        token.length = FALSE.Length;
                        pos += FALSE.Length;
                        break;
                    default:
                        break;
                }
            }

            res.advance = pos - start;
            res.token = token;

            return Result<Lexed, JSONError>.Ok(res);
        }

        private static bool IsWhiteSpace(char c) {
            return c == 0x20 || c == 0x09 || c == 0x0A || c == 0x0D;
        }

        private static void Escape(string data, StringBuilder builder) {
            for (int i = 0; i < data.Length; i++) {
                char c = data[i];
                if (c == '"' || c == '\\' || (c >= 0 && c <= 0x1f)) {
                    builder.Append('\\');
                }
                builder.Append(c);
            }
        }

        private static Result<string, JSONErrType> Unescape(string data, int start, int len) {
            StringBuilder builder = new StringBuilder();
            for (int i = start; i < start + len; i++) {
                char c = data[i];
                if (c == '\\') {
                    char n = data[i + 1];
                    char last;
                    switch (n) {
                        case 'b':
                            last = Convert.ToChar(0x08);
                            break;
                        case 'f':
                            last = Convert.ToChar(0x0C);
                            break;
                        case 'n':
                            last = Convert.ToChar(0x0A);
                            break;
                        case 'r':
                            last = Convert.ToChar(0x0D);
                            break;
                        case 't':
                            last = Convert.ToChar(0x09);
                            break;
                        case 'u':
                            Result<int, JSONErrType> codeRes = Parse4Hex(data, i + 2);
                            if (codeRes.IsErr()) {
                                return Result<string, JSONErrType>.Err(codeRes.AsErr());
                            }
                            int code = codeRes.AsOk();
                            if (code >= 0xd800 && code <= 0xdfff) {
                                if (i + 11 >= data.Length) {
                                    JSONErrType errType = JSONErrType.HangingSurrogatePair;
                                    return Result<string, JSONErrType>.Err(errType);
                                }

                                if (data[i + 6] == '\\' && data[i + 7] == 'u') {
                                    Result<int, JSONErrType> lowCodeRes = Parse4Hex(data, i + 8);
                                    if (lowCodeRes.IsErr()) {
                                        JSONErrType errType = lowCodeRes.AsErr();
                                        return Result<string, JSONErrType>.Err(errType);
                                    }

                                    int lowCode = lowCodeRes.AsOk();
                                    builder.Append((char)code);
                                    code = lowCode;

                                    i += 6;
                                }
                            }
                            last = (char)(code);
                            i += 4;
                            break;
                        default:
                            last = n;
                            break;
                    }

                    builder.Append(last);
                    i++;
                } else {
                    if (c >= 0 && c <= 0x1f) {
                        return Result<string, JSONErrType>.Err(JSONErrType.UnescapedControl);
                    }
                    builder.Append(c);
                }
            }

            return Result<string, JSONErrType>.Ok(builder.ToString());
        }

        private static Result<int, JSONErrType> Parse4Hex(string data, int start) {
            int pos = start;
            if (pos + 3 >= data.Length) {
                return Result<int, JSONErrType>.Err(JSONErrType.HangingHex);
            }

            int num = 0;
            for (int i = pos; i < pos + 4; i++) {
                char c = data[i];
                bool isDigit = c >= '0' && c <= '9';
                bool isAF = c >= 'A' && c <= 'F';
                bool isaf = c >= 'a' && c <= 'f';
                if (isDigit || isAF || isaf) {
                    int n;
                    if (isDigit) {
                        n = c - '0';
                    } else if (isAF) {
                        n = c - 'A' + 10;
                    } else {
                        n = c - 'a' + 10;
                    }

                    int power = 3 - (i - pos);
                    int b = 1;
                    while (power > 0) {
                        b *= 16;
                        power--;
                    }

                    num += b * n;
                } else {
                    return Result<int, JSONErrType>.Err(JSONErrType.NotHex);
                }
            }

            return Result<int, JSONErrType>.Ok(num);
        }
    }
}
