using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LolFormats
{
    public class LuaObjReader
    {
        private static readonly byte[] LuaSignature = { 0x1B, 0x4C, 0x75, 0x61 };

        public LuaObjFile Read(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                return Read(br);
            }
        }

        public LuaObjFile Read(BinaryReader br)
        {
            byte[] sig = br.ReadBytes(4);
            if (sig[0] != LuaSignature[0] || sig[1] != LuaSignature[1] ||
                sig[2] != LuaSignature[2] || sig[3] != LuaSignature[3])
            {
                throw new Exception("Invalid Lua signature. This is not a .luaobj file.");
            }

            byte version = br.ReadByte(); 
            if (version != 0x51) throw new Exception($"Unsupported Lua version: {version:X}");

            byte format = br.ReadByte();
            byte endianness = br.ReadByte(); // 1 = Little Endian
            byte sizeInt = br.ReadByte();    // 4
            byte sizeSizeT = br.ReadByte();  // 4
            byte sizeInstr = br.ReadByte();  // 4
            byte sizeLuaNum = br.ReadByte(); // 8 (Double)
            byte integral = br.ReadByte();   // 0

            var file = new LuaObjFile();
            file.MainChunk = ReadChunk(br);
            return file;
        }

        private LuaChunk ReadChunk(BinaryReader br)
        {
            var chunk = new LuaChunk();

            chunk.SourceName = ReadLuaString(br);

            chunk.LineDefined = br.ReadInt32();
            chunk.LastLineDefined = br.ReadInt32();
            chunk.UpvaluesCount = br.ReadByte();
            chunk.ParamsCount = br.ReadByte();
            chunk.IsVararg = br.ReadByte();
            chunk.MaxStackSize = br.ReadByte();

            int codeCount = br.ReadInt32();
            for (int i = 0; i < codeCount; i++)
            {
                chunk.Instructions.Add(br.ReadUInt32());
            }

            int constCount = br.ReadInt32();
            for (int i = 0; i < constCount; i++)
            {
                byte type = br.ReadByte();
                chunk.Constants.Add(ReadConstant(br, type));
            }

            int protoCount = br.ReadInt32();
            for (int i = 0; i < protoCount; i++)
            {
                chunk.Prototypes.Add(ReadChunk(br)); 
            }
            int lineCount = br.ReadInt32();
            for (int i = 0; i < lineCount; i++) chunk.SourceLines.Add(br.ReadInt32());

            int localCount = br.ReadInt32();
            for (int i = 0; i < localCount; i++)
            {
                string name = ReadLuaString(br);
                int start = br.ReadInt32();
                int end = br.ReadInt32();
                chunk.Locals.Add(name);
            }

            int upvalCount = br.ReadInt32();
            for (int i = 0; i < upvalCount; i++) chunk.Upvalues.Add(ReadLuaString(br));

            return chunk;
        }

        private object ReadConstant(BinaryReader br, byte type)
        {
            switch (type)
            {
                case 0: return null; // Nil
                case 1: return br.ReadBoolean(); // Bool
                case 3: return br.ReadDouble(); // Number (Lua uses doubles)
                case 4: return ReadLuaString(br); // String
                default: throw new Exception($"Unknown Lua constant type: {type}");
            }
        }

        private string ReadLuaString(BinaryReader br)
        {
            // Lua strings are: Size (4 bytes) + Characters + Null Terminator
            int size = br.ReadInt32();
            if (size == 0) return null;

            byte[] bytes = br.ReadBytes(size);
            // Remove the last byte (null terminator) for C# string
            return Encoding.UTF8.GetString(bytes, 0, size - 1);
        }
    }
}