using System.Collections.Generic;
using System.IO;

namespace LolFormats
{
    public class InibinDictionary
    {
        private Dictionary<uint, string> _hashToName = new Dictionary<uint, string>();

        private Dictionary<string, uint> _nameToHash = new Dictionary<string, uint>();

        public void Add(string name)
        {
            uint hash = InibinHash.Hash(name);
            if (!_hashToName.ContainsKey(hash))
            {
                _hashToName[hash] = name;
                _nameToHash[name] = hash;
            }
        }

        public void Add(string section, string property)
        {
            uint hash = InibinHash.Hash(section, property);
            string fullName = $"{section}*{property}"; 

            if (!_hashToName.ContainsKey(hash))
            {
                _hashToName[hash] = fullName;
                _nameToHash[fullName] = hash;
            }
        }

        public string GetName(uint hash)
        {
            if (_hashToName.TryGetValue(hash, out string name))
            {
                return name;
            }
            return null;
        }

        public string TryGuessHash(uint targetHash, IEnumerable<string> sections, IEnumerable<string> properties)
        {
            foreach (var sec in sections)
            {
                foreach (var prop in properties)
                {
                    if (InibinHash.Hash(sec, prop) == targetHash)
                    {
                        Add(sec, prop);
                        return $"{sec}*{prop}";
                    }
                }
            }
            return null; 
        }
        public static InibinDictionary LoadDefault()
        {
            var dict = new InibinDictionary();
            string filePath = "dictionary.txt";

            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                        continue;

                    string cleanLine = line.Trim();
                    if (cleanLine.Contains("*"))
                    {
                        var parts = cleanLine.Split('*');
                        if (parts.Length == 2)
                        {
                            dict.Add(parts[0], parts[1]);
                        }
                    }
                    else
                    {
                        dict.Add(cleanLine);
                    }
                }
            }

            return dict;
        }
    }
}