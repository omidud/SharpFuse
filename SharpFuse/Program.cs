using System;
using System.IO;
using SharpFuse.Engine;

namespace SharpFuse
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("🔥 SharpFuse — C# Source Fusion Tool");
            Console.WriteLine();

            if (args.Length < 1)
            {
                PrintUsage();
                return 1;
            }

            string inputDirectory = args[0];
            string outputFile = null;
            string forcedRootNamespace = null;

            // Parse args:
            // SharpFuse <inputDirectory> [outputFile] [--root=Namespace]
            for (int i = 1; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.StartsWith("--root=", StringComparison.OrdinalIgnoreCase))
                {
                    forcedRootNamespace = arg.Substring("--root=".Length).Trim();
                    continue;
                }

                if (!arg.StartsWith("--") && outputFile == null)
                {
                    outputFile = arg;
                }
            }

            if (!Directory.Exists(inputDirectory))
            {
                Console.WriteLine("Input directory not found: " + inputDirectory);
                return 1;
            }

            // If output not provided, auto-generate: <inputDirectory>\<RootNamespace>.cs
            if (string.IsNullOrWhiteSpace(outputFile))
            {
                if (string.IsNullOrWhiteSpace(forcedRootNamespace))
                {
                    Console.WriteLine("Error: You must provide either an output file or the --root option.");
                    Console.WriteLine();
                    PrintUsage();
                    return 1;
                }

                outputFile = Path.Combine(inputDirectory, forcedRootNamespace + ".cs");
            }

            try
            {
                var engine = new FusionEngine();

                var result = engine.Fuse(new FusionEngine.Options
                {
                    InputDirectory = inputDirectory,
                    OutputFile = outputFile,
                    ForcedRootNamespace = forcedRootNamespace,
                    AddFileHeaders = true,
                    Recursive = true,
                    ExcludeGeneratedFiles = true
                });

                Console.WriteLine("✅ Done.");
                Console.WriteLine("Root Namespace  : " + result.RootNamespace);
                Console.WriteLine("Files Processed : " + result.FilesProcessed);
                Console.WriteLine("Members Emitted : " + result.MembersEmitted);
                Console.WriteLine("Usings Emitted  : " + result.UsingsEmitted);
                Console.WriteLine("Output File     : " + Path.GetFullPath(outputFile));

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error: " + ex.Message);
                return 2;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  SharpFuse <inputDirectory> [outputFile] [--root=Namespace]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  SharpFuse ../SharpFuse.Test --root=TestNs");
            Console.WriteLine("    -> outputs to ../SharpFuse.Test/TestNs.cs");
            Console.WriteLine();
            Console.WriteLine("  SharpFuse ../SharpFuse.Test ../SharpFuse.Test/Merged.cs --root=TestNs");
            Console.WriteLine("    -> outputs to ../SharpFuse.Test/Merged.cs");
        }
    }
}
