using System;

namespace EventSim
{
    public class ConsoleOutput
    {
        private readonly FTCData.Models.Options _options;
        public ConsoleOutput(FTCData.Models.Options options)
        {
            _options = options;
        }
        public void WriteLine(string output, bool writeIt)
        {
            if (writeIt)
                Console.WriteLine(output);
        }

        public void WriteStatus(string output)
        {
            WriteLine(output, _options.Output.Status);
        }

        public void WriteHeading(string output)
        {
            WriteLine(output, _options.Output.Headings);
        }

        public void WriteTitle(string output)
        {
            WriteLine(output, _options.Output.Title);
        }
    }
}
