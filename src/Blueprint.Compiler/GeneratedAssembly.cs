﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Blueprint.Compiler
{
    public class GeneratedAssembly
    {
        private readonly GenerationRules generationRules;
        private readonly HashSet<Assembly> assemblies = new HashSet<Assembly>();

        public GeneratedAssembly(GenerationRules generationRules)
        {
            this.generationRules = generationRules;
        }

        public List<GeneratedType> GeneratedTypes { get; } = new List<GeneratedType>();

        public void ReferenceAssembly(Assembly assembly)
        {
            assemblies.Add(assembly);
        }

        public GeneratedType AddType(string typeName, Type baseType)
        {
            var generatedType = new GeneratedType(this, typeName);

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

        public void CompileAll(IAssemblyGenerator generator)
        {
            foreach (var assemblyReference in assemblies)
            {
                generator.ReferenceAssembly(assemblyReference);
            }

            foreach (var generatedType in GeneratedTypes)
            {
                foreach (var x in generatedType.AssemblyReferences())
                {
                    generator.ReferenceAssembly(x);
                }

                generatedType.ArrangeFrames();

                // We generate the code for the type upfront as we allow adding namespaces etc. during the rendering of
                // frames so we need to do those, and _then_ gather namespaces
                var typeWriter = new SourceWriter();
                generatedType.Write(typeWriter);

                var namespaces = generatedType
                    .AllInjectedFields
                    .Select(x => x.VariableType.Namespace)
                    .Concat(new[] { typeof(Task).Namespace })
                    .Concat(generatedType.Namespaces)
                    .Distinct()
                    .ToList();

                var writer = new SourceWriter();

                writer.WriteComment("THIS FILE IS AUTOGENERATED");
                writer.WriteComment(generatedType.TypeName);
                writer.BlankLine();

                foreach (var ns in namespaces.OrderBy(x => x))
                {
                    writer.UsingNamespace(ns);
                }

                writer.BlankLine();

                writer.Namespace(generationRules.ApplicationNamespace);

                writer.Write(typeWriter.Code());

                writer.FinishBlock();

                var code = writer.Code();

                generatedType.SourceCode = code;
                generator.AddFile(generatedType.TypeName + ".cs", code);
            }

            var assembly = generator.Generate(generationRules);

            var generated = assembly.GetExportedTypes().ToArray();

            foreach (var generatedType in GeneratedTypes)
            {
                generatedType.FindType(generated);
            }
        }
    }
}
