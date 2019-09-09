using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Blueprint.Compiler.Model;
using Blueprint.Compiler.Util;

namespace Blueprint.Compiler
{
    public class GeneratedAssembly
    {
        public readonly List<GeneratedType> GeneratedTypes = new List<GeneratedType>();

        public GeneratedAssembly(GenerationRules generationRules)
        {
            GenerationRules = generationRules;
        }

        public GenerationRules GenerationRules { get; }
        
        private readonly HashSet<Assembly> assemblies = new HashSet<Assembly>();

        public void ReferenceAssembly(Assembly assembly)
        {
            assemblies.Add(assembly);
        }
        
        public GeneratedType AddType(string typeName, Type baseType)
        {
            // TODO -- assert that it's been generated already?

            var generatedType = new GeneratedType(GenerationRules, typeName);
            if (baseType.IsInterface)
            {
                generatedType.Implements(baseType);
            }
            else
            {
                generatedType.InheritsFrom(baseType);
            }

            GeneratedTypes.Add(generatedType);

            return generatedType;
        }

        public void CompileAll()
        {
            var code = GenerateCode();

            var generator = BuildGenerator(GenerationRules);

            var assembly = generator.Generate(code);

            var generated = assembly.GetExportedTypes().ToArray();

            foreach (var generatedType in GeneratedTypes)
            {
                generatedType.FindType(generated);
            }
        }

        public string GenerateCode()
        {
            foreach (var generatedType in GeneratedTypes)
            {
                generatedType.ArrangeFrames();
            }

            var namespaces = GeneratedTypes
                .SelectMany(x => x.AllInjectedFields)
                .Select(x => x.ArgType.Namespace)
                .Concat(new string[]{typeof(Task).Namespace})
                .Distinct().ToList();

            var writer = new SourceWriter();

            foreach (var ns in namespaces.OrderBy(x => x))
            {
                writer.Write($"using {ns};");
            }

            writer.BlankLine();

            writer.Namespace(GenerationRules.ApplicationNamespace);

            foreach (var @class in GeneratedTypes)
            {
                writer.WriteLine($"// START: {@class.TypeName}");
                @class.Write(writer);
                writer.WriteLine($"// END: {@class.TypeName}");

                writer.WriteLine("");
                writer.WriteLine("");
            }

            writer.FinishBlock();


            var code = writer.Code();

            AttachSourceCodeToChains(code);


            return code;
        }

        private AssemblyGenerator BuildGenerator(GenerationRules generation)
        {
            var generator = new AssemblyGenerator(generation);

            foreach (var assembly in this.assemblies)
            {
                generator.ReferenceAssembly(assembly);
            }

            var assemblies = GeneratedTypes
                .SelectMany(x => x.AssemblyReferences())
                .Distinct().ToArray();

            foreach (var x in assemblies)
            {
                generator.ReferenceAssembly(x);
            }

            return generator;
        }

        private void AttachSourceCodeToChains(string code)
        {
            var parser = new SourceCodeParser(code);
            foreach (var type in GeneratedTypes)
            {
                type.SourceCode = parser.CodeFor(type.TypeName);
            }
        }
    }
}
