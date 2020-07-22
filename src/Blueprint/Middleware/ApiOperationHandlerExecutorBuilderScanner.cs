using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Blueprint.Middleware
{
    /// <summary>
    /// An <see cref="IOperationExecutorBuilderScanner" /> that searches for <see cref="IApiOperationHandler{T}" /> and creates corresponding
    /// <see cref="ApiOperationHandlerExecutorBuilder"/>s.
    /// </summary>
    public class ApiOperationHandlerExecutorBuilderScanner : IOperationExecutorBuilderScanner
    {
        /// <inheritdoc />
        public IEnumerable<IOperationExecutorBuilder> FindHandlers(
            IServiceCollection services,
            IEnumerable<ApiOperationDescriptor> operations,
            IEnumerable<Assembly> scannedAssemblies)
        {
            foreach (var operation in operations)
            {
                var apiOperationHandlerType = typeof(IApiOperationHandler<>).MakeGenericType(operation.OperationType);

                // First, check if there has already been a handler registered for this operation in the service
                // collection
                var diRegistered = services.SingleOrDefault(d => apiOperationHandlerType.IsAssignableFrom(d.ServiceType));

                if (diRegistered != null)
                {
                    yield return new ApiOperationHandlerExecutorBuilder(
                        operation,
                        diRegistered.ServiceType,
                        "IoC as " + (diRegistered.ImplementationType ?? diRegistered.ImplementationInstance?.GetType() ?? diRegistered.ServiceType));
                }
                else
                {
                    // If not, we try to manually find and register the handler, looking for an implementation of
                    // IApiOperationHandler<{OperationType}> alongside the operation (i.e. in the same assembly)
                    var foundType = FindApiOperationHandler(operation, apiOperationHandlerType, scannedAssemblies);

                    if (foundType != null)
                    {
                        services.AddScoped(foundType, foundType);
                        yield return new ApiOperationHandlerExecutorBuilder(operation, foundType, $"Scanned {foundType}");
                    }
                }
            }
        }

        private static Type FindApiOperationHandler(
            ApiOperationDescriptor apiOperationDescriptor,
            Type apiOperationHandlerType,
            IEnumerable<Assembly> scannedAssemblies)
        {
            // Most likely is the handler lives beside the operation, check that assembly first.
            foreach (var t in apiOperationDescriptor.OperationType.Assembly.GetExportedTypes())
            {
                if (apiOperationHandlerType.IsAssignableFrom(t))
                {
                    return t;
                }
            }

            // We could not find it, check other scanned assemblies.
            foreach (var a in scannedAssemblies)
            {
                foreach (var t in a.GetExportedTypes())
                {
                    if (apiOperationHandlerType.IsAssignableFrom(t))
                    {
                        return t;
                    }
                }
            }

            return null;
        }
    }
}
