# 🔥 SharpFuse

> Surgical C# source fusion powered by Roslyn.

SharpFuse is a modern .NET 8 CLI tool that merges multiple C# source
files into a single consolidated file --- safely, cleanly, and
intelligently.

Unlike naive text-based merging, SharpFuse uses Roslyn to parse and
reconstruct syntax trees, ensuring valid structure, preserved members,
and properly deduplicated using directives.

------------------------------------------------------------------------

## ✨ Features

-   🔥 Merge multiple `.cs` files into one
-   🧠 Powered by Roslyn (real syntax parsing, no regex hacks)
-   📦 Preserves and deduplicates `using` statements
-   🧬 Supports multiple namespaces
-   🏗 Flattens nested namespaces
-   📑 Adds file-origin headers for traceability
-   ⚙ Compatible with legacy C# 7 code and modern C# versions
-   🚀 Built on .NET 8

------------------------------------------------------------------------

## 📦 Example

### Input

/MyProject ├── Class1.cs ├── Class2.cs └── Sub/Feature.cs

### Output

Merged.cs

``` csharp
namespace MyProject
{
    // ===== From: Class1.cs =====
    public class Class1 { ... }

    // ===== From: Class2.cs =====
    public class Class2 { ... }

    // ===== From: Feature.cs =====
    public class Feature { ... }
}
```

------------------------------------------------------------------------

## 🚀 Installation

### Option 1 --- Clone and Build

``` bash
git clone https://github.com/omidud/SharpFuse.git
cd SharpFuse
dotnet build -c Release
```

Run:

``` bash
dotnet run -- <inputDirectory> <outputFile>
```

------------------------------------------------------------------------

## 🛠 Usage

``` bash
SharpFuse <inputDirectory> <outputFile> [--root=NamespaceName]
```

### Arguments

  Argument             Description
  -------------------- ---------------------------------
  `<inputDirectory>`   Folder containing `.cs` files
  `<outputFile>`       Destination merged file
  `--root=Name`        (Optional) Force root namespace

### Example

``` bash
SharpFuse ./src ./Merged.cs --root=MyNamespace
```

------------------------------------------------------------------------

## 🧠 How It Works

SharpFuse:

1.  Parses every `.cs` file using Roslyn
2.  Extracts namespaces and members
3.  Deduplicates `using` directives
4.  Flattens nested namespaces
5.  Reconstructs a clean syntax tree
6.  Emits a single formatted output file

No broken braces.\
No malformed syntax.\
No regex disasters.

------------------------------------------------------------------------

## 📌 Roadmap

-   [ ] Support `--lang=<version>` (C# version parsing option)
-   [ ] Read `<RootNamespace>` from `.csproj`
-   [ ] Option to preserve original namespaces
-   [ ] `dotnet tool` global installation
-   [ ] Optional member sorting
-   [ ] Exclude patterns via CLI

------------------------------------------------------------------------

## 📜 License

MIT License\
Copyright (c) 2026 Omar Laracuente (omidud)

------------------------------------------------------------------------

## 👨‍💻 Author

Created by **Omar Laracuente**\
GitHub: https://github.com/omidud

------------------------------------------------------------------------

## ⚡ Philosophy

SharpFuse exists for developers who prefer control over magic.

Sometimes you need: - A monolithic SDK file - A single-distribution
source file - A flattened code artifact - Or simply to understand
everything in one place

SharpFuse makes that possible --- safely.
