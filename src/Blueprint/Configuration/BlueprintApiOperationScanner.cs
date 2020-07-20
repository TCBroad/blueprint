using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if !NET472
using System.Runtime.Loader;
#endif
using Blueprint.Authorisation;
using Blueprint.Middleware;
using Blueprint.Utilities;

namespace Blueprint.Configuration
{
    /// <summary>
    /// Provides the scanning and creation of <see cref="ApiOperationDescriptor" />s and associated
    /// <see cref="ApiResource" />s and <see cref="Link" />s.
    /// </summary>
    public class BlueprintApiOperationScanner
    {
        // As scan or add operations are executed we add a number of actions that will be executed at a later
        // time to perform the actual scanning. This is done to allow the global conventions to be applied to
        // the scans, even if they are registered after the scan calls
        // i.e. scanOperations.Add(addType => addType(theTypeToRegisterFromMethodCall).
        private readonly List<Action<RegisterOperation>> scanOperations = new List<Action<RegisterOperation>>();

        private readonly List<IOperationScannerConvention> conventions = new List<IOperationScannerConvention>();

        /// <summary>
        /// Initialises a new instance of the <see cref="BlueprintApiOperationScanner" /> class.
        /// </summary>
        public BlueprintApiOperationScanner()
        {
            conventions.Add(new XmlDocResponseConvention());
            conventions.Add(new ApiExceptionFactoryResponseConvention());
        }

        private delegate void RegisterOperation(Type operationType, string source);

        internal List<Assembly> ScannedAssemblies { get; } = new List<Assembly>();

        /// <summary>
        /// Adds an <see cref="IOperationScannerConvention" /> that will be invoked for every
        /// <see cref="IApiOperation" /> that has been registered with this scanner to enable it to contribute and / or
        /// change details of the <see cref="ApiOperationDescriptor" />s, in addition to providing global filtering
        /// capabilities.
        /// </summary>
        /// <param name="contributor">The contributor.</param>
        /// <returns>This <see cref="BlueprintApiOperationScanner"/> for further configuration.</returns>
        public BlueprintApiOperationScanner AddConvention(IOperationScannerConvention contributor)
        {
            Guard.NotNull(nameof(contributor), contributor);

            conventions.Add(contributor);

            return this;
        }

        /// <summary>
        /// Scans the calling assembly (<see cref="Assembly.GetCallingAssembly"/> for operations and handlers to
        /// register, with an optional filter function to exclude found types.
        /// </summary>
        /// <param name="filter">The optional filter over found operations.</param>
        /// <returns>This <see cref="BlueprintApiOperationScanner"/> for further configuration.</returns>
        public BlueprintApiOperationScanner ScanCallingAssembly(Func<Type, bool> filter = null)
        {
            return this.Scan(Assembly.GetCallingAssembly(), filter);
        }

        /// <summary>
        /// Scans the assembly that the <typeparamref name="T" /> type is contained in, plus
        /// any recursive references that assembly has that match the given filter.
        /// </summary>
        /// <remarks>
        /// Although it is possible to pass an assembly filter that always returns <c>true</c> it
        /// is highly recommended that this is <b>NOT</b> done as that can leave to a performance
        /// penalty on startup as thousands of types may need to be checked.
        /// </remarks>
        /// <param name="assemblyFilter">A filter to determine whether to scan a given assembly.</param>
        /// <param name="filter">A type filter to exclude types from the search.</param>
        /// <typeparam name="T">The type from which an <see cref="Assembly"/> is loaded and recursively
        /// scanned.</typeparam>
        /// <returns>This <see cref="BlueprintApiOperationScanner"/> for further configuration.</returns>
        public BlueprintApiOperationScanner ScanReferencedAssembliesOf<T>(
            Func<AssemblyName, bool> assemblyFilter,
            Func<Type, bool> filter = null)
        {
            void ScanRecursive(Assembly a, ISet<Assembly> seen)
            {
                if (seen.Contains(a))
                {
                    return;
                }

                seen.Add(a);

                if (assemblyFilter(a.GetName()))
                {
                    Scan(a, filter);
                }

                foreach (var referenced in a.GetReferencedAssemblies())
                {
#if !NET472
                    ScanRecursive(AssemblyLoadContext.Default.LoadFromAssemblyName(referenced), seen);
#endif

#if NET472
                    ScanRecursive(Assembly.Load(referenced), seen);
#endif
                }
            }

            ScanRecursive(typeof(T).Assembly, new HashSet<Assembly>());

            return this;
        }

        /// <summary>
        /// Scans the given assemblies for operations and handlers to register, with an optional filter function to exclude
        /// found types.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <param name="filter">The optional filter over found operations.</param>
        /// <returns>This <see cref="BlueprintApiOperationScanner"/> for further configuration.</returns>
        public BlueprintApiOperationScanner Scan(Assembly[] assemblies, Func<Type, bool> filter = null)
        {
            foreach (var assembly in assemblies)
            {
                Scan(assembly, filter);
            }

            return this;
        }

        /// <summary>
        /// Scans the given assembly for operations and handlers to register, with an optional filter function to exclude
        /// found types.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <param name="filter">The optional filter over found operations.</param>
        /// <returns>This <see cref="BlueprintApiOperationScanner"/> for further configuration.</returns>
        public BlueprintApiOperationScanner Scan(Assembly assembly, Func<Type, bool> filter = null)
        {
            if (ScannedAssemblies.Contains(assembly))
            {
                return this;
            }

            ScannedAssemblies.Add(assembly);

            scanOperations.Add((add) =>
            {
                DoScan(assembly, filter, add);
            });

            return this;
        }

        /// <summary>
        /// Bulk registers all operations in the given enumeration.
        /// </summary>
        /// <param name="types">The types to register.</param>
        /// <param name="source">The source of these operations, useful for tracking <em>where</em> an operation comes from. Used for
        /// diagnostics.</param>
        /// <returns>This <see cref="BlueprintApiOperationScanner"/> for further configuration.</returns>
        /// <see cref="AddOperation" />
        public BlueprintApiOperationScanner AddOperations(IEnumerable<Type> types, string source = "AddOperations()")
        {
            foreach (var type in types)
            {
                AddOperation(type, source);
            }

            return this;
        }

        /// <summary>
        /// Adds the operation identified by <typeparamref name="T"/>.
        /// </summary>
        /// <param name="source">The source of this operation, useful for tracking <em>where</em> an operation comes from. Used for
        /// diagnostics.</param>
        /// <typeparam name="T">The type of <see cref="IApiOperation"/> to register.</typeparam>
        /// <returns>This <see cref="BlueprintApiOperationScanner"/> for further configuration.</returns>
        public BlueprintApiOperationScanner AddOperation<T>(string source = "AddOperation<T>") where T : IApiOperation
        {
            AddOperation(typeof(T), source);

            return this;
        }

        /// <summary>
        /// Adds the operation identified by <paramref name="type" />.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> representing the <see cref="IApiOperation"/> to register.</param>
        /// <param name="source">The source of this operation, useful for tracking <em>where</em> an operation comes from. Used for
        /// diagnostics.</param>
        /// <returns>This <see cref="BlueprintApiOperationScanner"/> for further configuration.</returns>
        public BlueprintApiOperationScanner AddOperation(Type type, string source = "AddOperation(type)")
        {
            if (!typeof(IApiOperation).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Type {type.FullName} does not implement {nameof(IApiOperation)}, cannot register.");
            }

            scanOperations.Add(a => a(type, source));

            return this;
        }

        /// <summary>
        /// Given the operations that have been added registers them and all associated data with the given
        /// <see cref="ApiDataModel" />.
        /// </summary>
        /// <param name="dataModel">The data model to register the operations to.</param>
        public void Register(ApiDataModel dataModel)
        {
            foreach (var scanOperation in scanOperations)
            {
                scanOperation((t, source) => Register(dataModel, t, source));
            }
        }

        private static IEnumerable<Type> GetExportedTypesOfInterface(Assembly assembly, Type interfaceType)
        {
            var allExportedTypes = assembly.GetExportedTypes();

            return allExportedTypes.Where(t => !t.IsAbstract && t.GetInterface(interfaceType.Name) != null);
        }

        private static void RegisterResponses(ApiOperationDescriptor descriptor)
        {
            var typedOperation = descriptor.OperationType
                .GetInterfaces()
                .SingleOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IApiOperation<>));

            if (typedOperation != null)
            {
                descriptor.AddResponse(
                    new ResponseDescriptor(typedOperation.GetGenericArguments()[0], 200, "OK"));
            }

            descriptor.AddResponse(
                new ResponseDescriptor(typeof(UnhandledExceptionOperationResult), 500, "Unexpected error"));

            descriptor.AddResponse(
                new ResponseDescriptor(typeof(ValidationFailedOperationResult), 422, "Validation failure"));
        }

        private void DoScan(Assembly assembly, Func<Type, bool> filter, RegisterOperation register)
        {
            var source = $"Scan {assembly.FullName}";

            foreach (var type in GetExportedTypesOfInterface(assembly, typeof(IApiOperation)))
            {
                var canInclude = filter is null || filter(type);

                foreach (var globalFilters in conventions)
                {
                    if (!globalFilters.ShouldInclude(type))
                    {
                        canInclude = false;
                        break;
                    }
                }

                if (canInclude)
                {
                    register(type, source);
                }
            }
        }

        private void Register(ApiDataModel dataModel, Type type, string source)
        {
            var apiOperationDescriptor = CreateApiOperationDescriptor(type, source);

            dataModel.RegisterOperation(apiOperationDescriptor);
        }

        private ApiOperationDescriptor CreateApiOperationDescriptor(Type type, string source)
        {
            var descriptor = new ApiOperationDescriptor(type, source)
            {
                AnonymousAccessAllowed = type.HasAttribute<AllowAnonymousAttribute>(true),
                IsExposed = type.HasAttribute<UnexposedOperationAttribute>(true) == false,
                ShouldAudit = !type.HasAttribute<DoNotAuditOperationAttribute>(true),
                RecordPerformanceMetrics = !type.HasAttribute<DoNotRecordPerformanceMetricsAttribute>(true),
            };

            foreach (var linkAttribute in type.GetCustomAttributes<LinkAttribute>())
            {
                descriptor.AddLink(
                    new ApiOperationLink(descriptor, linkAttribute.Url, linkAttribute.Rel ?? descriptor.Name)
                    {
                        ResourceType = linkAttribute.ResourceType,
                    });
            }

            RegisterResponses(descriptor);

            foreach (var c in conventions)
            {
                c.Apply(descriptor);
            }

            return descriptor;
        }
    }
}
