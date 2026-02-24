using System.Collections.Generic;
using System.Globalization;

namespace LolFormats
{
    public class InibinProperty
    {
        public uint Hash { get; set; }       
        public string Name { get; set; }   
        public object Value { get; set; } 
        public int TypeId { get; set; }
        public string ValueStr
        {
            get
            {
                if (Value == null) return "null";

                if (Value is float[] fArr)
                    return string.Join(" ", fArr.Select(f => f.ToString(CultureInfo.InvariantCulture)));

                if (Value is bool bVal)
                    return bVal ? "1" : "0";

                if (Value is Dictionary<object, object> dict)
                    return FormatDictionary(dict);

                return Value.ToString();
            }
            set
            {
                try
                {
                    Value = ParseStringValue(value, TypeId);
                }
                catch
                {
                   
                }
            }
        }
        private string FormatDictionary(Dictionary<object, object> dict)
        {
            var entries = dict.Select(kvp =>
            {
                string key = kvp.Key is double || kvp.Key is int ? $"[{kvp.Key}]" : kvp.Key.ToString();
                string val = kvp.Value is Dictionary<object, object> subDict
                    ? FormatDictionary(subDict)
                    : (kvp.Value?.ToString() ?? "null");
                return $"{key}={val}";
            });
            return "{" + string.Join(", ", entries) + "}";
        }
        private object ParseStringValue(string input, int typeId)
        {
            var culture = CultureInfo.InvariantCulture;
            if (typeId >= 6 && typeId <= 11)
            {
                var parts = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                return parts.Select(p => float.Parse(p, culture)).ToArray();
            }

            switch (typeId)
            {
                case 0: return int.Parse(input);
                case 1: return float.Parse(input, culture);
                case 2: return float.Parse(input, culture); // ByteDiv10 is stored as float in memory
                case 3: return short.Parse(input);
                case 4: return byte.Parse(input);
                case 5:
                    if (input == "1") return true;
                    if (input == "0") return false;
                    return bool.Parse(input);
                case 12: return input;
                default: return input;
            }
        }
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? $"Hash: {Hash}" : Name;
        }
    }

    public class InibinSection
    {
        public uint Hash { get; set; }
        public string Name { get; set; }

        public List<InibinProperty> Properties { get; set; } = new List<InibinProperty>();

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? $"Section: {Hash}" : Name;
        }
    }

    public class InibinFile
    {
        public byte Version { get; set; }
        public List<InibinSection> Sections { get; set; } = new List<InibinSection>();
        public InibinFile()
        {
            //Sections.Add(new InibinSection { Name = "Raw Data", Hash = 0 });
        }
    }
}