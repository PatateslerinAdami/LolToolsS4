using System.Collections.Generic;
using System.Linq;

namespace LolFormats
{
    public class TroybinResolver
    {
        private readonly HashSet<string> _commonProperties;

        public TroybinResolver(IEnumerable<string> commonProperties)
        {
            _commonProperties = new HashSet<string>(commonProperties);
        }

        public void Resolve(InibinFile file)
        {
            uint systemHash = InibinHash.Hash("System");
            var systemSection = file.Sections.FirstOrDefault(s => s.Hash == systemHash || s.Name == "System");

            if (systemSection == null) return;

            var dynamicGroupNames = new HashSet<string>();

            foreach (var prop in systemSection.Properties)
            {
                if (!string.IsNullOrEmpty(prop.Name) && prop.Name.StartsWith("GroupPart"))
                {
                    if (prop.Value is string groupName && !string.IsNullOrWhiteSpace(groupName))
                    {
                        dynamicGroupNames.Add(groupName);
                    }
                }
            }

            if (dynamicGroupNames.Count == 0) return;

            var matches = new List<(InibinSection oldSection, InibinProperty prop, string newSectionName, string newPropName)>();

            foreach (var section in file.Sections)
            {
                foreach (var prop in section.Properties)
                {
                    bool isUnknown = string.IsNullOrEmpty(prop.Name) || prop.Name.StartsWith("Unknown_");

                    if (!isUnknown) continue;

                    foreach (var groupName in dynamicGroupNames)
                    {
                        foreach (var propName in _commonProperties)
                        {
                            uint calculatedHash = InibinHash.Hash(groupName, propName);

                            if (calculatedHash == prop.Hash)
                            {
                                matches.Add((section, prop, groupName, propName));
                                goto NextProperty;
                            }
                        }
                    }
                NextProperty:;
                }
            }

            foreach (var match in matches)
            {
                match.oldSection.Properties.Remove(match.prop);

                match.prop.Name = match.newPropName;

                var targetSection = file.Sections.FirstOrDefault(s => s.Name == match.newSectionName);
                if (targetSection == null)
                {
                    targetSection = new InibinSection
                    {
                        Name = match.newSectionName,
                        Hash = InibinHash.Hash(match.newSectionName)
                    };
                    file.Sections.Add(targetSection);
                }

                targetSection.Properties.Add(match.prop);
            }

            file.Sections.RemoveAll(s => s.Properties.Count == 0);
            file.Sections.Sort((a, b) => string.Compare(a.Name, b.Name));
        }
    }
}