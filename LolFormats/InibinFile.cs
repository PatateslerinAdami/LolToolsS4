using System.Collections.Generic;

namespace LolFormats
{
    public class InibinProperty
    {
        public uint Hash { get; set; }       
        public string Name { get; set; }   
        public object Value { get; set; } 
        public int TypeId { get; set; } 

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