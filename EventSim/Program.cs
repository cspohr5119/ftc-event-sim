using FTCData;
using FTCData.Models;
using System;
using System.Diagnostics;

namespace EventSim
{
    class Program
    {
        static void Main(string[] args)
        {
            var optionsRepo = new OptionsRepository();
            Options options;
            Engine engine;

            // load options spcified from command line
            if (args.Length > 0)
            {
                try
                {
                    options = optionsRepo.GetOptionsFromFile(args[0]);
                    if (args.Length > 1)
                        optionsRepo.Override(options, args);

                    engine = new Engine(options);
                    engine.Output.WriteStatus("Loaded optionsFile " + args[0]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error reading options file {0}\n{1}", args[0], ex.Message);
                    return;
                }
            }
            else
            {
                // load options from default.xml
                try
                { 
                    options = optionsRepo.GetOptionsFromFile("default.xml");
                    engine = new Engine(options);
                    engine.Output.WriteStatus("Loaded optionsFile default.xml");
                }
                catch (Exception ex)
                {
                    options = new Options();
                    engine = new Engine(options);
                    engine.Output.WriteStatus("Loaded optionsFile " + args[0]);

                    Console.WriteLine("Error reading options file {0}\r\n{1}", args[0], ex.Message);
                    Console.WriteLine("Using internal defaults.");
                }
            }

            engine.Output.WriteTitle(options.Title);

            // Run the simulation!
            engine.RunTrials(options.Trials);

            if (Debugger.IsAttached)
            { 
                Console.WriteLine("Press a key");
                Console.ReadKey();
            }
        }
    }
}
