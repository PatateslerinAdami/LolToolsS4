using System.Collections.Generic;
using System.Text;

namespace LolFormats
{
    public class LuaObjFile
    {
        public LuaChunk MainChunk { get; set; }
    }

    public class LuaChunk
    {
        public string SourceName { get; set; }
        public int LineDefined { get; set; }
        public int LastLineDefined { get; set; }
        public byte UpvaluesCount { get; set; }
        public byte ParamsCount { get; set; }
        public byte IsVararg { get; set; }
        public byte MaxStackSize { get; set; }

        public List<uint> Instructions { get; set; } = new List<uint>();
        public List<object> Constants { get; set; } = new List<object>();
        public List<LuaChunk> Prototypes { get; set; } = new List<LuaChunk>();

        public List<int> SourceLines { get; set; } = new List<int>();
        public List<string> Locals { get; set; } = new List<string>();
        public List<string> Upvalues { get; set; } = new List<string>();

        public override string ToString()
        {
            return "<Compiled Lua Function>";
        }

        public string Disassemble()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"-- Function: {(string.IsNullOrEmpty(SourceName) ? "Anonymous" : SourceName)}");
            sb.AppendLine($"-- {Instructions.Count} Instructions, {Constants.Count} Constants");
            sb.AppendLine();

            for (int pc = 0; pc < Instructions.Count; pc++)
            {
                uint inst = Instructions[pc];

                LuaOpcode op = (LuaOpcode)(inst & 0x3F);
                int a = (int)((inst >> 6) & 0xFF);
                int c = (int)((inst >> 14) & 0x1FF);
                int b = (int)((inst >> 23) & 0x1FF);
                int bx = (int)((inst >> 14) & 0x3FFFF);
                int sbx = bx - 131071; // Lua 5.1 signed Bx bias

                sb.Append($"[{pc + 1:D3}] {op,-10} A:{a,-3}");

                if (op == LuaOpcode.LOADK || op == LuaOpcode.GETGLOBAL || op == LuaOpcode.SETGLOBAL)
                {
                    string constVal = bx < Constants.Count ? Constants[bx]?.ToString() : "?";
                    sb.Append($" Bx:{bx,-3} ; {constVal}");
                }
                else if (op == LuaOpcode.JMP || op == LuaOpcode.FORLOOP || op == LuaOpcode.FORPREP)
                {
                    sb.Append($" sBx:{sbx,-3} ; goto {pc + 1 + sbx + 1}");
                }
                else if (op == LuaOpcode.CLOSURE)
                {
                    sb.Append($" Bx:{bx,-3} ; (Sub-function)");
                }
                else
                {
                    sb.Append($" B:{b,-3} C:{c,-3}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}