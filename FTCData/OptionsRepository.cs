using System.Xml.Serialization;
using FTCData.Models;

namespace FTCData
{
    public class OptionsRepository
    {
        public Options GetOptionsFromFile(string optionsFile)
        {
            using (var stream = System.IO.File.OpenRead(optionsFile))
            {
                var serializer = new XmlSerializer(typeof(Options));
                return serializer.Deserialize(stream) as Options;
            }
        }

        public void WriteOptionsToFile(Options options, string optionsFile)
        {
            using (var writer = new System.IO.StreamWriter(optionsFile))
            {
                var serializer = new XmlSerializer(options.GetType());
                serializer.Serialize(writer, options);
                writer.Flush();
            }
        }
    }
}
