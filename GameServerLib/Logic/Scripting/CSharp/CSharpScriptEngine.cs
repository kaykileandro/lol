using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using LeagueSandbox.GameServer.Core.Logic;

namespace LeagueSandbox.GameServer.Logic.Scripting.CSharpScriptEngine
{
    public class CSharpScriptEngine
    {
        Assembly scriptAssembly = null;
        public CSharpScriptEngine()
        {
        }
        //Compile example assembly to load all the compiler resources, takes 1700 milliseconds first time
        public void prepareCompiler()
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(@"
            using System;

            namespace LoadCompilerNamespace
            {
                public class LoadCompilerClass
                {
                    public void LoadCompilerFunction(string message)
                    {
                        Benchmark.Log(message);
                    }
                }
            }");
            string assemblyName = Path.GetRandomFileName();
            MetadataReference[] references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
            };

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release).WithConcurrentBuild(true));

            using (var ms = new MemoryStream())
            {
                ///using (var pdb = new MemoryStream())
                //{
                EmitResult result = compilation.Emit(ms);//, pdb);
                //}
            }
        }
        public void loadSubdirectoryScripts(String folder)
        {
            String[] allfiles = System.IO.Directory.GetFiles(folder, "*.cs", System.IO.SearchOption.AllDirectories);
            load(new List<string>(allfiles));
        }
        //Takes about 300 milliseconds for a single script
        public void load(List<string> scriptLocations)
        {
            List<SyntaxTree> treeList = new List<SyntaxTree>();
            Parallel.For(0, scriptLocations.Count, (i)=> {
                Console.WriteLine("Loading script: " + scriptLocations[i]);
                using (StreamReader sr = new StreamReader(scriptLocations[i]))
                {
                    // Read the stream to a string, and write the string to the console.
                    SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sr.ReadToEnd());
                    lock (treeList)
                    {
                        treeList.Add(syntaxTree);
                    }
                }
            });
            
            string assemblyName = Path.GetRandomFileName();
            
            List<MetadataReference> references = new List<MetadataReference>();
            foreach (Assembly a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!a.IsDynamic) references.Add(MetadataReference.CreateFromFile(a.Location));
            }
            //Now add game reference
            references.Add(MetadataReference.CreateFromFile(typeof(Game).Assembly.Location));

            var op = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release).WithConcurrentBuild(true);
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: treeList,
                references: references,
                options: op);

            using (var ms = new MemoryStream())
            {
                //using (var pdb = new MemoryStream())
                {
                    EmitResult result = compilation.Emit(ms);//, pdb);
                    
                    if (!result.Success)
                    {
                        IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                            diagnostic.IsWarningAsError ||
                            diagnostic.Severity == DiagnosticSeverity.Error);

                        foreach (Diagnostic diagnostic in failures)
                        {
                            Location loc = diagnostic.Location;
                            Console.Error.WriteLine("{0}: {1} with location: {2}", diagnostic.Id, diagnostic.GetMessage(), loc.SourceTree.ToString());
                        }
                    }
                    else
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        
                        scriptAssembly = Assembly.Load(ms.ToArray());
                    }
                }
            }

            
        }
        public T getStaticMethod<T>(String scriptNamespace, String scriptClass, String scriptFunction)
        {
            if (scriptAssembly == null) return default(T);

            Type classType = scriptAssembly.GetType(scriptNamespace + "." + scriptClass);
            MethodInfo desiredFunction = classType.GetMethod(scriptFunction, BindingFlags.Public | BindingFlags.Static);

            Type typeParameterType = typeof(T);
            return (T)((object)Delegate.CreateDelegate(typeParameterType, desiredFunction));
        }
        public T createObject<T>(String scriptNamespace, String scriptClass)
        {
            if (scriptAssembly == null) return default(T);

            Type classType = scriptAssembly.GetType(scriptNamespace + "." + scriptClass);

            return (T)Activator.CreateInstance(classType);
        }
        public static object runFunctionOnObject(object obj, String method, params object[] args)
        {
            return obj.GetType().InvokeMember(method,
                            BindingFlags.Default | BindingFlags.InvokeMethod,
                            null,
                            obj,
                            args);
        }
        public static T getObjectMethod<T>(object obj, String scriptFunction)
        {
            Type classType = obj.GetType();
            MethodInfo desiredFunction = classType.GetMethod(scriptFunction, BindingFlags.Public | BindingFlags.Instance);

            Type typeParameterType = typeof(T);
            return (T)((object)Delegate.CreateDelegate(typeParameterType, obj, desiredFunction));
        }
    }
}
