using System.Collections.Generic;

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
            return string.IsNullOrEmpty(SourceName) ? "Chunk" : SourceName;
        }
    }
}