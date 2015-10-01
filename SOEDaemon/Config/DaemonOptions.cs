using System.Text;
using CommandLine;
using CommandLine.Text;

namespace SOEDaemon
{
    internal class DaemonOptions
    {
        [Option('c', "config", DefaultValue = "config.cfg", HelpText = "The configuration for this daemon.")]
        public string ConfigFile { get; set; }

        [Option('v', "verbose", DefaultValue = false, HelpText = "Ouput daemon-specific messages to stdout.")]
        public bool Verbose { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            // Create the string builder
            StringBuilder usage = new StringBuilder();

            // Write the message
            usage.AppendLine("LibSOE's daemon utility");
            usage.AppendLine("https://github.com/Joshsora/LibSOE");
            usage.AppendLine("Read the wiki pages for application-specific help!");
            usage.AppendLine();

            usage.AppendLine(HelpText.AutoBuild(this,
                (current) => HelpText.DefaultParsingErrorsHandler(this, current))
            );

            // Return the help message
            return usage.ToString();
        }
    }
}