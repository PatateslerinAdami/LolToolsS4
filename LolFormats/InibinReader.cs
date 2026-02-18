using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LolFormats
{
    public class InibinReader
    {
        private enum InibinType
        {
            Int32 = 0,          // 1x int
            Float = 1,          // 1x float
            ByteDiv10 = 2,      // 1x byte * 0.1
            Int16 = 3,          // 1x short
            Byte = 4,           // 1x byte
            Boolean = 5,        // bitpacked bools
            Vec3ByteDiv10 = 6,  // 3x byte * 0.1
            Vec3Float = 7,      // 3x float
            Vec2ByteDiv10 = 8,  // 2x byte * 0.1
            Vec2Float = 9,      // 2x float
            Vec4ByteDiv10 = 10, // 4x byte * 0.1
            Vec4Float = 11,     // 4x float
            String = 12,        // Strings
            // Types 13, 14, 15 are usually unused or reserved
        }

        public InibinFile Read(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                return Read(br);
            }
        }

        public InibinFile Read(BinaryReader br)
        {
            var result = new InibinFile();

            result.Version = br.ReadByte();

            if (result.Version == 2)
            {
                ReadV2(br, result);
            }
            else if (result.Version == 1)
            {
                throw new NotSupportedException("Inibin Version 1 is not yet supported in this editor.");
            }
            else
            {
                throw new Exception($"Unknown Inibin Version: {result.Version}");
            }

            return result;
        }

        private void ReadV2(BinaryReader br, InibinFile file)
        {
            ushort stringsLength = br.ReadUInt16();

            ushort flags = br.ReadUInt16();

            for (int i = 0; i < 16; i++)
            {
                bool isPresent = (flags & (1 << i)) != 0;

                if (isPresent)
                {
                    ReadType(br, (InibinType)i, file, stringsLength);
                }
            }
        }

        private void ReadType(BinaryReader br, InibinType type, InibinFile file, ushort stringsLength)
        {
            // Special handling for Booleans (Type 5) and Strings (Type 12)
            if (type == InibinType.Boolean)
            {
                ReadBools(br, file);
                return;
            }
            if (type == InibinType.String)
            {
                ReadStrings(br, file, stringsLength);
                return;
            }

            ushort count = br.ReadUInt16();

            uint[] keys = new uint[count];
            for (int k = 0; k < count; k++)
            {
                keys[k] = br.ReadUInt32();
            }

            for (int k = 0; k < count; k++)
            {
                object value = ReadValue(br, type);

                AddPropertyToFile(file, keys[k], value, (int)type);
            }
        }

        private object ReadValue(BinaryReader br, InibinType type)
        {
            switch (type)
            {
                case InibinType.Int32: return br.ReadInt32();
                case InibinType.Float: return br.ReadSingle();
                case InibinType.ByteDiv10: return br.ReadByte() * 0.1f;
                case InibinType.Int16: return br.ReadInt16();
                case InibinType.Byte: return br.ReadByte();

                case InibinType.Vec3ByteDiv10:
                    return new float[] { br.ReadByte() * 0.1f, br.ReadByte() * 0.1f, br.ReadByte() * 0.1f };
                case InibinType.Vec3Float:
                    return new float[] { br.ReadSingle(), br.ReadSingle(), br.ReadSingle() };
                case InibinType.Vec2ByteDiv10:
                    return new float[] { br.ReadByte() * 0.1f, br.ReadByte() * 0.1f };
                case InibinType.Vec2Float:
                    return new float[] { br.ReadSingle(), br.ReadSingle() };
                case InibinType.Vec4ByteDiv10:
                    return new float[] { br.ReadByte() * 0.1f, br.ReadByte() * 0.1f, br.ReadByte() * 0.1f, br.ReadByte() * 0.1f };
                case InibinType.Vec4Float:
                    return new float[] { br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle() };

                default: return null;
            }
        }

        private void ReadBools(BinaryReader br, InibinFile file)
        {
            ushort count = br.ReadUInt16();
            uint[] keys = new uint[count];
            for (int k = 0; k < count; k++) keys[k] = br.ReadUInt32();

            // Calculate how many bytes the bools take up (8 bools per byte)
            int byteCount = (int)Math.Ceiling(count / 8.0);
            byte[] boolBytes = br.ReadBytes(byteCount);

            for (int i = 0; i < count; i++)
            {
                // Bitwise magic to extract 1 bit
                int byteIndex = i / 8;
                int bitIndex = i % 8;
                bool val = ((boolBytes[byteIndex] >> bitIndex) & 1) == 1;

                AddPropertyToFile(file, keys[i], val, (int)InibinType.Boolean);
            }
        }

        private void ReadStrings(BinaryReader br, InibinFile file, ushort stringsLength)
        {
            ushort count = br.ReadUInt16();
            uint[] keys = new uint[count];
            ushort[] offsets = new ushort[count];

            // Read keys
            for (int k = 0; k < count; k++) keys[k] = br.ReadUInt32();
            // Read offsets (where the string starts in the data block)
            for (int k = 0; k < count; k++) offsets[k] = br.ReadUInt16();

            // Read the big block of characters
            byte[] stringData = br.ReadBytes(stringsLength);

            for (int k = 0; k < count; k++)
            {
                int offset = offsets[k];
                string val = ReadNullTerminatedString(stringData, offset);
                AddPropertyToFile(file, keys[k], val, (int)InibinType.String);
            }
        }

        private string ReadNullTerminatedString(byte[] data, int offset)
        {
            int end = offset;
            while (end < data.Length && data[end] != 0)
            {
                end++;
            }
            return Encoding.UTF8.GetString(data, offset, end - offset);
        }

        private void AddPropertyToFile(InibinFile file, uint hash, object value, int typeId)
        {
            if (file.Sections.Count == 0)
            {
                file.Sections.Add(new InibinSection { Name = "Unknown Section", Hash = 0 });
            }

            var prop = new InibinProperty
            {
                Hash = hash,
                Value = value,
                TypeId = typeId,
                Name = null 
            };

            file.Sections[0].Properties.Add(prop);
        }
    }
}