using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LolFormats
{
    public class LuaObjWriter
    {
        private static readonly byte[] LuaHeader = {
            0x1B, 0x4C, 0x75, 0x61, // Signature
            0x51, // Version 5.1
            0x00, // Format 0
            0x01, // Little Endian
            0x04, // sizeof(int)
            0x04, // sizeof(size_t)
            0x04, // sizeof(Instruction)
            0x08, // sizeof(lua_Number)
            0x00  // Integral
        };

        public void Write(string filePath, InibinFile fileData)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(LuaHeader);

                var chunk = CompileFile(fileData);

                WriteChunk(bw, chunk);
            }
        }

        private LuaChunk CompileFile(InibinFile fileData)
        {
            var chunk = new LuaChunk();
            chunk.SourceName = "@Generated";
            chunk.LineDefined = 0;
            chunk.LastLineDefined = 0;
            chunk.UpvaluesCount = 0;
            chunk.ParamsCount = 0;
            chunk.IsVararg = 2; // Vararg flag
            chunk.MaxStackSize = 20; // Safe default

            var constMap = new Dictionary<object, int>();

            // Helper to get/add constant index
            int GetConst(object val)
            {
                if (constMap.ContainsKey(val)) return constMap[val];
                int idx = chunk.Constants.Count;
                chunk.Constants.Add(val);
                constMap[val] = idx;
                return idx;
            }
            chunk.Instructions.Add(CreateOp(LuaOpcode.NEWTABLE, 0, 0, 0));

            foreach (var section in fileData.Sections)
            {
                if (section.Name.StartsWith("Lua Globals") || section.Name.StartsWith("Main Chunk"))
                {
                    foreach (var prop in section.Properties)
                    {
                        EmitSetTable(chunk, 0, prop.Name, prop.Value, GetConst);
                    }
                }
                else
                {
                    int keyIdx = GetConst(section.Name);
                    chunk.Instructions.Add(CreateOp(LuaOpcode.LOADK, 1, keyIdx | 0x100 /* unused */, 0)); // LOADK R1 = Const

                    chunk.Instructions.Add(CreateOp(LuaOpcode.NEWTABLE, 2, 0, 0));

                    foreach (var prop in section.Properties)
                    {
                        EmitSetTable(chunk, 2, prop.Name, prop.Value, GetConst);
                    }

                    // Attach Sub-Table to Main Table (R0[Key] = R2)
                    // SETTABLE R0, K(keyIdx), R2
                    // SETTABLE A B C -> R[A][RK(B)] = RK(C)
                    // We use RK(B) as Constant (Bit 9 set)
                    int rkKey = keyIdx | 256; // Bit 9 set means constant
                    int rkVal = 2; // Register 2
                    chunk.Instructions.Add(CreateOp(LuaOpcode.SETTABLE, 0, rkKey, rkVal));
                }
            }

            // RETURN R0, 2 (Return 1 result)
            chunk.Instructions.Add(CreateOp(LuaOpcode.RETURN, 0, 2, 0));

            return chunk;
        }

        private void EmitSetTable(LuaChunk chunk, int tableReg, string keyName, object value, Func<object, int> getConst)
        {
            // We need to handle the Key
            // If key is "[1]", parse it as int 1. Else string.
            object keyObj = keyName;
            if (keyName.StartsWith("[") && keyName.EndsWith("]"))
            {
                if (int.TryParse(keyName.Substring(1, keyName.Length - 2), out int iVal))
                    keyObj = iVal;
            }

            int keyConst = getConst(keyObj);
            int valConst = getConst(value);
            chunk.Instructions.Add(CreateOp(LuaOpcode.SETTABLE, tableReg, keyConst | 256, valConst | 256));
        }

        private uint CreateOp(LuaOpcode op, int a, int b, int c)
        {
            // B: 9 bits (23-31)
            // C: 9 bits (14-22)
            // A: 8 bits (6-13)
            // Op: 6 bits (0-5)

            // For LOADK, Bx is combined B and C (18 bits)
            if (op == LuaOpcode.LOADK)
            {
                int bx = b; // In LOADK, the 2nd arg is Bx
                return (uint)((int)op | (a << 6) | (bx << 14));
            }

            return (uint)((int)op | (a << 6) | (c << 14) | (b << 23));
        }

        private void WriteChunk(BinaryWriter bw, LuaChunk chunk)
        {
            WriteLuaString(bw, chunk.SourceName);

            bw.Write(chunk.LineDefined);
            bw.Write(chunk.LastLineDefined);
            bw.Write(chunk.UpvaluesCount);
            bw.Write(chunk.ParamsCount);
            bw.Write(chunk.IsVararg);
            bw.Write(chunk.MaxStackSize);

            bw.Write(chunk.Instructions.Count);
            foreach (uint instr in chunk.Instructions) bw.Write(instr);

            bw.Write(chunk.Constants.Count);
            foreach (var c in chunk.Constants)
            {
                if (c == null) { bw.Write((byte)0); }
                else if (c is bool b) { bw.Write((byte)1); bw.Write(b); }
                else if (c is double d) { bw.Write((byte)3); bw.Write(d); }
                else if (c is int i) { bw.Write((byte)3); bw.Write((double)i); } // Lua uses doubles for ints
                else if (c is string s) { bw.Write((byte)4); WriteLuaString(bw, s); }
                else { bw.Write((byte)0); } 
            }

            // Prototypes (None for now)
            bw.Write(0);

            // Debug Info (Empty for generated files)
            bw.Write(0); // Lines
            bw.Write(0); // Locals
            bw.Write(0); // Upvalues
        }

        private void WriteLuaString(BinaryWriter bw, string str)
        {
            if (str == null)
            {
                bw.Write((int)0);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            bw.Write(bytes.Length + 1); // Size includes null terminator
            bw.Write(bytes);
            bw.Write((byte)0);
        }
    }
}