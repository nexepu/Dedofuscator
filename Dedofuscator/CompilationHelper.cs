using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;

namespace Dedofuscator
{
    public class CompilationHelper
    {
        public static dynamic CreateFunctionsClass(string code)
        {

            var obj = CSharpScript.Create(code, ScriptOptions.Default.WithReferences(Assembly.GetExecutingAssembly()));
            var d = CSharpCompilation.Create("Functions.dll", options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(code) },
                references: new[] {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)

                });
            var dll = new MemoryStream();
            var result = d.Emit(dll);
            if (!result.Success)
            {
                Console.WriteLine($"An error occured while compiling functions used by the HumanCheck -> {String.Join(',', result.Diagnostics.Select(x=>x.ToString()).ToArray())}");
                Environment.Exit(-1);
            }
            dll.Position = 0;
            var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(dll);
            dynamic instance = assembly.CreateInstance(assembly.DefinedTypes.First().Name);
            return instance;
        }
    }
}
