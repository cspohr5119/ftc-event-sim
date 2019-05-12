using System;
using System.Reflection;
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

        public void Override(Options options, string[] args)
        {
            // Set any option value from a string expression
            // in the format of "OptionItem=value"
            // Supports dotted options such as "Output.Headers=false"

            foreach (string arg in args)
            { 
                var parts = arg.Split('=');
                if (parts.Length != 2)
                    continue;

                string name = parts[0];
                string value = parts[1];
                object obj;

                FieldInfo prop;
                var nameParts = name.Split('.');
                if (nameParts.Length == 2)
                {
                    obj = options.GetType().GetProperty(nameParts[0]).GetValue(options);
                    prop = obj.GetType().GetField(nameParts[1]);
                }
                else
                {
                    obj = options;
                    prop = options.GetType().GetField(nameParts[0]);
                }

                if (prop == null)
                    continue;

                prop.SetValue(obj, Convert.ChangeType(value, prop.FieldType));
            }
        }
    }
}
