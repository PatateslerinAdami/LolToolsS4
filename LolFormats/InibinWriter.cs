using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace LolFormats
{
    public class InibinWriter
    {
        public void Write(string filePath, InibinFile file)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                Write(bw, file);
            }
        }

        public void Write(BinaryWriter bw, InibinFile file)
        {
            bw.Write(file.Version);
            var allProperties = new List<InibinProperty>();
            foreach (var section in file.Sections)
            {
                allProperties.AddRange(section.Properties);
            }
            var stringProps = allProperties.Where(p => p.TypeId == 12).ToList();
            byte[] stringBlock = CreateStringBlock(stringProps, out Dictionary<uint, ushort> stringOffsets);

            bw.Write((ushort)stringBlock.Length);

            ushort flags = 0;
            var propertiesByType = allProperties.GroupBy(p => p.TypeId).ToDictionary(g => g.Key, g => g.ToList());

            for (int i = 0; i < 16; i++)
            {
                if (propertiesByType.ContainsKey(i) && propertiesByType[i].Count > 0)
                {
                    flags |= (ushort)(1 << i);
                }
            }
            bw.Write(flags);

            for (int i = 0; i < 16; i++)
            {
                if ((flags & (1 << i)) != 0)
                {
                    var props = propertiesByType[i];

                    props.Sort((a, b) => a.Hash.CompareTo(b.Hash));

                    WriteTypeBlock(bw, i, props, stringOffsets);
                }
            }

            bw.Write(stringBlock);
        }

        private byte[] CreateStringBlock(List<InibinProperty> stringProps, out Dictionary<uint, ushort> offsets)
        {
            offsets = new Dictionary<uint, ushort>();
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                foreach (var prop in stringProps)
                {
                    offsets[prop.Hash] = (ushort)ms.Position;
                    string val = prop.Value.ToString();
                    byte[] bytes = Encoding.UTF8.GetBytes(val);
                    writer.Write(bytes);
                    writer.Write((byte)0);
                }
                return ms.ToArray();
            }
        }

        private void WriteTypeBlock(BinaryWriter bw, int typeId, List<InibinProperty> props, Dictionary<uint, ushort> stringOffsets)
        {
            ushort count = (ushort)props.Count;
            bw.Write(count);

            foreach (var p in props) bw.Write(p.Hash);

            if (typeId == 12) // Strings are special (we write offsets)
            {
                foreach (var p in props) bw.Write(stringOffsets[p.Hash]);
            }
            else if (typeId == 5) 
            {
                WriteBools(bw, props);
            }
            else 
            {
                foreach (var p in props)
                {
                    WriteValue(bw, typeId, p.Value);
                }
            }
        }

        private void WriteBools(BinaryWriter bw, List<InibinProperty> props)
        {
            int byteCount = (int)Math.Ceiling(props.Count / 8.0);
            byte[] bytes = new byte[byteCount];

            for (int i = 0; i < props.Count; i++)
            {
                bool val = Convert.ToBoolean(props[i].Value);
                if (val)
                {
                    int byteIndex = i / 8;
                    int bitIndex = i % 8;
                    bytes[byteIndex] |= (byte)(1 << bitIndex);
                }
            }
            bw.Write(bytes);
        }

        private void WriteValue(BinaryWriter bw, int typeId, object value)
        {
            string s = value.ToString();

            switch (typeId)
            {
                case 0: bw.Write(Convert.ToInt32(s)); break; // Int32
                case 1: bw.Write(Convert.ToSingle(s)); break; // Float
                case 2: bw.Write((byte)(Convert.ToSingle(s) * 10)); break; // ByteDiv10
                case 3: bw.Write(Convert.ToInt16(s)); break; // Short
                case 4: bw.Write(Convert.ToByte(s)); break; // Byte
                default:
                    break;
            }
        }
    }
}