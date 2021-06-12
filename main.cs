using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using vjp;
using option;

class Init {
    private static int Main(string[] args) {
        string line;
        StringBuilder inputBuilder = new StringBuilder();
        while ((line = Console.ReadLine()) != null) {
            inputBuilder.Append(line);
            inputBuilder.Append('\n');
        }

        string input = inputBuilder.ToString();

        Result<JSONType, JSONError> parseResp = VJP.Parse(input, 1024);
        if (parseResp.IsErr()) {
            JSONError err = parseResp.AsErr();
            int lineCount = 1;
            int lastLinePos = 0;
            for (int i = 0; i < err.position; i++) {
                if (input[i] == '\n') {
                    lastLinePos = i;
                    lineCount++;
                }
            }
            int pos = err.position - lastLinePos;
            Console.WriteLine(err.type + ": " + lineCount + ":" + pos);
            return -1;
        } else {
            JSONType type = parseResp.AsOk();
            Console.WriteLine(VJP.Generate(type));
            return 0;
        }
    }
}
