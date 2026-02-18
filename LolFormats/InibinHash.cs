namespace LolFormats
{
    public static class InibinHash
    {
        // The magic number used in League's hashing algorithm
        private const uint Prime = 65599;

        /// <summary>
        /// Calculates the hash of a string used in Inibin files.
        /// </summary>
        public static uint Hash(string text, uint startHash = 0)
        {
            if (string.IsNullOrEmpty(text))
                return startHash;

            uint hash = startHash;

            foreach (char c in text)
            {
                char lowerChar = char.ToLowerInvariant(c);
                unchecked
                {
                    hash = (uint)lowerChar + (Prime * hash);
                }
            }

            return hash;
        }

        /// <summary>
        /// Calculates the hash for a specific Section and Property.
        /// Example: Hash("Data", "BaseHP")
        /// </summary>
        public static uint Hash(string section, string property)
        {
            uint sectionHash = Hash(section);
            sectionHash = Hash("*", sectionHash);

            return Hash(property, sectionHash);
        }
    }
}