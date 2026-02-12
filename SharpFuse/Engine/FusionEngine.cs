using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Reflection;

namespace SharpFuse.Engine
{
    public sealed class FusionEngine
    {
        public sealed class Options
        {
            public string InputDirectory { get; set; }
            public string OutputFile { get; set; }

            /// <summary>
            /// If provided, forces the root namespace in the output.
            /// If null/empty, SharpFuse will auto-detect from input files.
            /// </summary>
            public string ForcedRootNamespace { get; set; }

            /// <summary>
            /// Adds "// ===== From: File.cs =====" headers above each top-level member.
            /// </summary>
            public bool AddFileHeaders { get; set; } = true;

            /// <summary>
            /// If true, searches subdirectories.
            /// </summary>
            public bool Recursive { get; set; } = true;

            /// <summary>
            /// Exclude common generated files.
            /// </summary>
            public bool ExcludeGeneratedFiles { get; set; } = true;
        }

        public sealed class Result
        {
            public string RootNamespace { get; set; }
            public int FilesProcessed { get; set; }
            public int MembersEmitted { get; set; }
            public int UsingsEmitted { get; set; }
        }

        public Result Fuse(Options options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.InputDirectory)) throw new ArgumentException("InputDirectory is required.");
            if (string.IsNullOrWhiteSpace(options.OutputFile)) throw new ArgumentException("OutputFile is required.");

            if (!Directory.Exists(options.InputDirectory))
                throw new DirectoryNotFoundException("Input directory not found: " + options.InputDirectory);

            // Collect files
            var search = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(options.InputDirectory, "*.cs", search).ToList();

            if (options.ExcludeGeneratedFiles)
            {
                files = files
                    .Where(f => !EndsWithAny(f, ".g.cs", ".designer.cs", ".AssemblyInfo.cs"))
                    .ToList();
            }

            // Avoid accidentally including output file if it already exists in the same tree
            var outputFull = Path.GetFullPath(options.OutputFile);
            files = files.Where(f => !string.Equals(Path.GetFullPath(f), outputFull, StringComparison.OrdinalIgnoreCase)).ToList();

            var allUsings = new List<UsingDirectiveSyntax>();
            var allMembers = new List<MemberDeclarationSyntax>();
            var allNamespacesFound = new List<string>();

            foreach (var file in files)
            {
                var text = File.ReadAllText(file);

                // Parse using Roslyn
                var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest));
                var root = tree.GetCompilationUnitRoot();

                // Top-level usings
                allUsings.AddRange(root.Usings);

                // Members (namespaces + globals)
                CollectMembersAndUsings(
                    root,
                    allMembers,
                    allUsings,
                    allNamespacesFound,
                    file,
                    options.AddFileHeaders);
            }

            // Determine root namespace
            var rootNamespace = !string.IsNullOrWhiteSpace(options.ForcedRootNamespace)
                ? options.ForcedRootNamespace.Trim()
                : (PickRootNamespace(allNamespacesFound) ?? "Merged");

            // Deduplicate usings
            var mergedUsings = DedupUsings(allUsings);

            // Wrap everything in root namespace
            var mergedNamespaceNode =
                SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(rootNamespace))
                    .WithMembers(SyntaxFactory.List(allMembers));

            var compilationUnit =
                SyntaxFactory.CompilationUnit()
                    .WithUsings(SyntaxFactory.List(mergedUsings))
                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(mergedNamespaceNode))
                    .NormalizeWhitespace();

            // Format output (nice formatting)
            using (var workspace = new AdhocWorkspace())
            {
                var formatted = Formatter.Format(compilationUnit, workspace).ToFullString();

                var banner = BuildBanner();

                formatted = banner + Environment.NewLine + formatted;

                // Ensure output directory exists
                var outDir = Path.GetDirectoryName(outputFull);
                if (!string.IsNullOrWhiteSpace(outDir) && !Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                File.WriteAllText(outputFull, formatted);
            }

            return new Result
            {
                RootNamespace = rootNamespace,
                FilesProcessed = files.Count,
                MembersEmitted = allMembers.Count,
                UsingsEmitted = mergedUsings.Count
            };
        }

        private static void CollectMembersAndUsings(
            CompilationUnitSyntax root,
            List<MemberDeclarationSyntax> membersOut,
            List<UsingDirectiveSyntax> usingsOut,
            List<string> namespacesOut,
            string filePath,
            bool addFileHeaders)
        {
            // File-scoped namespaces: namespace X.Y;
            foreach (var fs in root.Members.OfType<FileScopedNamespaceDeclarationSyntax>())
            {
                namespacesOut.Add(fs.Name.ToString());
                usingsOut.AddRange(fs.Usings);

                foreach (var m in fs.Members)
                    membersOut.Add(addFileHeaders ? AddFileHeaderComment(m, filePath) : m);
            }

            // Block namespaces: namespace X.Y { ... }
            foreach (var ns in root.Members.OfType<NamespaceDeclarationSyntax>())
                CollectFromNamespace(ns, membersOut, usingsOut, namespacesOut, filePath, addFileHeaders);

            // Global members (no namespace)
            foreach (var m in root.Members.Where(m =>
                         m is not NamespaceDeclarationSyntax &&
                         m is not FileScopedNamespaceDeclarationSyntax))
            {
                membersOut.Add(addFileHeaders ? AddFileHeaderComment(m, filePath) : m);
            }
        }

        private static void CollectFromNamespace(
            NamespaceDeclarationSyntax ns,
            List<MemberDeclarationSyntax> membersOut,
            List<UsingDirectiveSyntax> usingsOut,
            List<string> namespacesOut,
            string filePath,
            bool addFileHeaders)
        {
            namespacesOut.Add(ns.Name.ToString());
            usingsOut.AddRange(ns.Usings);

            foreach (var m in ns.Members)
            {
                if (m is NamespaceDeclarationSyntax nested)
                {
                    // Flatten nested namespaces
                    CollectFromNamespace(nested, membersOut, usingsOut, namespacesOut, filePath, addFileHeaders);
                }
                else
                {
                    membersOut.Add(addFileHeaders ? AddFileHeaderComment(m, filePath) : m);
                }
            }
        }

        private static MemberDeclarationSyntax AddFileHeaderComment(MemberDeclarationSyntax member, string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var comment = SyntaxFactory.Comment("// ===== From: " + fileName + " =====");
            var trivia = SyntaxFactory.TriviaList(comment, SyntaxFactory.ElasticCarriageReturnLineFeed);

            return member.WithLeadingTrivia(trivia.AddRange(member.GetLeadingTrivia()));
        }

        private static List<UsingDirectiveSyntax> DedupUsings(IEnumerable<UsingDirectiveSyntax> usings)
        {
            var unique = usings
                .Select(u => u.WithLeadingTrivia().WithTrailingTrivia())
                .GroupBy(u => u.ToString())
                .Select(g => g.First())
                .ToList();

            // System.* first
            return unique
                .OrderBy(u => !u.Name.ToString().StartsWith("System", StringComparison.Ordinal))
                .ThenBy(u => u.Name.ToString(), StringComparer.Ordinal)
                .ToList();
        }

        private static string PickRootNamespace(List<string> namespacesFound)
        {
            if (namespacesFound == null || namespacesFound.Count == 0)
                return null;

            var candidates = namespacesFound
                .Select(ns => ns.Split('.')[0])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (candidates.Count == 0)
                return null;

            return candidates
                .GroupBy(s => s)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .First().Key;
        }

        private static bool EndsWithAny(string path, params string[] suffixes)
        {
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (path.EndsWith(suffixes[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string BuildBanner()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "unknown";

            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return
@"// ------------------------------------------------------------
//  Generated by SharpFuse v" + version + @"
//  https://github.com/omidud/SharpFuse
//  Generation Date: " + now + @"
// ------------------------------------------------------------";
        }


    }
}
