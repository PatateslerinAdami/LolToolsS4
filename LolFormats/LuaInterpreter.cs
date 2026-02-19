using System;
using System.Collections.Generic;

namespace LolFormats
{
    public class LuaInterpreter
    {
        private object[] _registers = new object[256];

        public Dictionary<object, object> Globals { get; private set; } = new Dictionary<object, object>();

        public Dictionary<object, object> Interpret(LuaObjFile file)
        {
            RunChunk(file.MainChunk);
            return Globals;
        }

        private void RunChunk(LuaChunk chunk)
        {
            var code = chunk.Instructions;
            var k = chunk.Constants;

            for (int pc = 0; pc < code.Count; pc++)
            {
                uint instruction = code[pc];
                // Opcode: 6 bits, A: 8 bits, C: 9 bits, B: 9 bits
                LuaOpcode op = (LuaOpcode)(instruction & 0x3F);
                int a = (int)((instruction >> 6) & 0xFF);
                int c = (int)((instruction >> 14) & 0x1FF);
                int b = (int)((instruction >> 23) & 0x1FF);
                int bx = (int)((instruction >> 14) & 0x3FFFF); // Combined B+C

                try
                {
                    switch (op)
                    {
                        case LuaOpcode.MOVE:
                            _registers[a] = _registers[b];
                            break;

                        case LuaOpcode.LOADK:
                            _registers[a] = k[bx];
                            break;

                        case LuaOpcode.LOADBOOL:
                            _registers[a] = (b != 0);
                            if (c != 0) pc++; 
                            break;

                        case LuaOpcode.LOADNIL:
                            for (int i = a; i <= b; i++) _registers[i] = null;
                            break;

                        case LuaOpcode.NEWTABLE:
                            _registers[a] = new Dictionary<object, object>();
                            break;
                        case LuaOpcode.CLOSURE:
                            // Register[A] = New Function Instance
                            // Bx is the index of the prototype in the chunk
                            if (bx < chunk.Prototypes.Count)
                            {
                                // We just store the prototype itself as the value for now
                                _registers[a] = chunk.Prototypes[bx];
                            }
                            break;
                        case LuaOpcode.SETGLOBAL:
                            object globalName = k[bx];
                            Globals[globalName] = _registers[a];
                            break;

                        case LuaOpcode.GETGLOBAL:
                            object gName = k[bx];
                            if (Globals.ContainsKey(gName))
                                _registers[a] = Globals[gName];
                            else
                                _registers[a] = null;
                            break;

                        case LuaOpcode.SETTABLE:
                            var table = _registers[a] as Dictionary<object, object>;
                            object key = GetRK(b, k);
                            object val = GetRK(c, k);

                            if (table != null && key != null)
                            {
                                table[key] = val;
                            }
                            break;
                        case LuaOpcode.GETTABLE:
                            // Register[A] = Register[B][Register[C]]
                            // Used when the script reads a value from a table
                            var sourceTable = _registers[b] as Dictionary<object, object>;
                            object index = GetRK(c, k);

                            if (sourceTable != null && index != null && sourceTable.ContainsKey(index))
                            {
                                _registers[a] = sourceTable[index];
                            }
                            else
                            {
                                _registers[a] = null;
                            }
                            break;
                        case LuaOpcode.SETLIST:
                            // R(A)[(C-1)*50 + i] = R(A+i), 1 <= i <= B

                            var listTable = _registers[a] as Dictionary<object, object>;
                            if (listTable != null)
                            {
                                int block = c;
                                if (block == 0)
                                {
                                    pc++;
                                    block = (int)code[pc];
                                }

                                int batchSize = 50; // Standard Lua batch size
                                int startIndex = (block - 1) * batchSize;

                                // B is the number of elements to set
                                // but usually B is explicit in these files.
                                int count = b;

                                for (int i = 1; i <= count; i++)
                                {
                                    int arrayIndex = startIndex + i;
                                    object valToSet = _registers[a + i];

                                    listTable[arrayIndex] = valToSet;
                                }
                            }
                            break;
                        case LuaOpcode.RETURN:
                            return;
                        default:
                            System.Diagnostics.Debug.WriteLine($"[Lua Warning] Unhandled Opcode: {op}");
                            break;
                    }
                }
                catch
                {
                }
            }
        }
        private object GetRK(int val, List<object> constants)
        {
            if (val > 255)
            {
                int constIndex = val - 256;
                if (constIndex < constants.Count)
                    return constants[constIndex];
                return null;
            }
            else
            {
                return _registers[val];
            }
        }
    }
}