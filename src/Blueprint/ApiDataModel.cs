﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Blueprint
{
    /// <summary>
    /// Provides a data model for an exposed API, a description of the resources that are being exposed, plus
    /// the service and entity operations that have been registered.
    /// </summary>
    public class ApiDataModel
    {
        private readonly Dictionary<Type, ApiOperationDescriptor> allOperations =
            new Dictionary<Type, ApiOperationDescriptor>();

        private readonly List<ApiOperationLink> allLinks = new List<ApiOperationLink>();

        // Dictionary lookups, contains values from allLinks
        private readonly Dictionary<Type, List<ApiOperationLink>> operationTypeToLinks = new Dictionary<Type, List<ApiOperationLink>>();
        private readonly Dictionary<Type, List<ApiOperationLink>> resourceTypeToLinks = new Dictionary<Type, List<ApiOperationLink>>();

        /// <summary>
        /// Gets all registered links for this model, with a link representing the association between a resource and a different
        /// operation (for example a link between the resource 'Jobs' and the operation 'Apply').
        /// </summary>
        public IEnumerable<ApiOperationLink> Links => allLinks;

        /// <summary>
        /// Gets all registered operations.
        /// </summary>
        public IEnumerable<ApiOperationDescriptor> Operations => allOperations.Values;

        /// <summary>
        /// Given a type that represents an API operation will construct a new <see cref="ApiOperationContext"/> that represents
        /// that operation.
        /// </summary>
        /// <param name="serviceProvider">The service provider under which the operation will execute.</param>
        /// <param name="type">The API operation to construct a context for.</param>
        /// <param name="token">A cancellation token to indicate the operation should stop.</param>
        /// <returns>A new <see cref="ApiOperationContext"/> representing the given type.</returns>
        public ApiOperationContext CreateOperationContext(IServiceProvider serviceProvider, Type type, CancellationToken token)
        {
            if (allOperations.TryGetValue(type, out var operation))
            {
                return new ApiOperationContext(serviceProvider, this, operation, token);
            }

            throw new InvalidOperationException($"Cannot find a registered operation of the type '{type.Name}'.");
        }

        /// <summary>
        /// Given a configured operation instance will create a new <see cref="ApiOperationContext" />.
        /// </summary>
        /// <param name="serviceProvider">The service provider under which the operation will execute.</param>
        /// <param name="operation">The configured operation instance.</param>
        /// <param name="token">A cancellation token to indicate the operation should stop.</param>
        /// <returns>A new <see cref="ApiOperationContext"/> representing the given operation.</returns>
        public ApiOperationContext CreateOperationContext(
            IServiceProvider serviceProvider,
            object operation,
            CancellationToken token)
        {
            return new ApiOperationContext(serviceProvider, this, operation, token);
        }

        /// <summary>
        /// Registers the given operation descriptor.
        /// </summary>
        /// <param name="descriptor">The descriptor to register, must be non-null.</param>
        public void RegisterOperation(ApiOperationDescriptor descriptor)
        {
            Guard.NotNull(nameof(descriptor), descriptor);

            allOperations[descriptor.OperationType] = descriptor;

            foreach (var link in descriptor.Links)
            {
                RegisterLink(link);
            }
        }

        /// <summary>
        /// Registers the specified link, checking that the link represents a unique combination of HTTP method
        /// and URL format to avoid clashes.
        /// </summary>
        /// <remarks>
        /// In addition the link <see cref="ApiOperationLink.Rel" /> property will be checked to ensure no two links with
        /// the same rel can be associated with a single resource type.
        /// </remarks>
        /// <param name="link">The link to register.</param>
        /// <exception cref="InvalidOperationException">If the link is not unique.</exception>
        private void RegisterLink(ApiOperationLink link)
        {
            var existing = allLinks.Where(l =>
                    l.UrlFormat.Equals(link.UrlFormat, StringComparison.CurrentCultureIgnoreCase) &&
                    l.OperationDescriptor.Name == link.OperationDescriptor.Name)
                .ToList();

            if (existing.Any())
            {
                throw new InvalidOperationException(
                    $"Could not register {link} from {link.OperationDescriptor.Source} as it conflicts with existing Link registrations:\n\n" +
                    string.Join("\n", existing.Select(e => $" {e} sourced from {e.OperationDescriptor.Source}")));
            }

            allLinks.Add(link);

            if (!link.IsRootLink)
            {
                if (!resourceTypeToLinks.ContainsKey(link.ResourceType))
                {
                    resourceTypeToLinks[link.ResourceType] = new List<ApiOperationLink>();
                }

                if (resourceTypeToLinks[link.ResourceType].Any(l => l.Rel == link.Rel))
                {
                    throw new InvalidOperationException(
                        $"Duplicate link found for resource type {link.ResourceType.Name} with rel {link.Rel}");
                }

                resourceTypeToLinks[link.ResourceType].Add(link);
            }

            if (!operationTypeToLinks.ContainsKey(link.OperationDescriptor.OperationType))
            {
                operationTypeToLinks[link.OperationDescriptor.OperationType] = new List<ApiOperationLink>();
            }

            operationTypeToLinks[link.OperationDescriptor.OperationType].Add(link);
        }

        /// <summary>
        /// Gets all registered links for the given operation type. Multiple links (routes) can be specified for a single
        /// for a single operation as it is possible to access via different URLs depending on link association (i.e.
        /// could be /api/issues and /api/users/3/issues where the second one fills in a UserId property).
        /// </summary>
        /// <param name="operationType">The type of operation to get links for.</param>
        /// <returns>All associated links (routes) for the operation type.</returns>
        /// <exception cref="InvalidOperationException">If no operation of the specified type has been registered.</exception>
        public IEnumerable<ApiOperationLink> GetLinksForOperation(Type operationType)
        {
            if (!allOperations.TryGetValue(operationType, out var operationDescriptor))
            {
                throw new InvalidOperationException(
                    $"Cannot get links for operation '{operationType.Name}' as it has not been registered.");
            }

            if (!operationTypeToLinks.ContainsKey(operationDescriptor.OperationType))
            {
                return Enumerable.Empty<ApiOperationLink>();
            }

            return operationTypeToLinks[operationDescriptor.OperationType];
        }

        /// <summary>
        /// Gets all registered links for the given resource type. Links can be associated with a resource so that they get
        /// generated and returned in the $links properties of an <see cref="ApiResource"/> return value.
        /// </summary>
        /// <param name="resourceType">The resource type to load links for.</param>
        /// <returns>The (potentially empty) enumeration of registered links.</returns>
        public IEnumerable<ApiOperationLink> GetLinksForResource(Type resourceType)
        {
            Guard.NotNull(nameof(resourceType), resourceType);

            if (!resourceTypeToLinks.ContainsKey(resourceType))
            {
                return Enumerable.Empty<ApiOperationLink>();
            }

            return resourceTypeToLinks[resourceType];
        }

        /// <summary>
        /// Gets the link that has been registered for the given resource type with the specified 'rel'
        /// property.
        /// </summary>
        /// <param name="resourceType">The resource type to load for.</param>
        /// <param name="rel">The rel of the link to load.</param>
        /// <returns>The associated link, or <c>null</c> if no such link exists.</returns>
        public ApiOperationLink GetLinkFor(Type resourceType, string rel)
        {
            Guard.NotNull(nameof(resourceType), resourceType);

            if (!resourceTypeToLinks.TryGetValue(resourceType, out var links))
            {
                return null;
            }

            foreach (var link in links)
            {
                if (link.Rel == rel)
                {
                    return link;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all registered links that are not associated with any particular resource.
        /// </summary>
        /// <returns>All non-resource associated registered links.</returns>
        public IEnumerable<ApiOperationLink> GetRootLinks()
        {
            return allLinks.Where(l => l.IsRootLink);
        }

        /// <summary>
        /// Finds the <see cref="ApiOperationDescriptor" /> for an operation of the given type, which may be a concrete
        /// implementation of a registered interface-based operation.
        /// </summary>
        /// <param name="operationType">The type of operation to search for.</param>
        /// <returns>The <see cref="ApiOperationDescriptor" />.</returns>
        /// <exception cref="InvalidOperationException">If no operation descriptor exists.</exception>
        public ApiOperationDescriptor FindOperation(Type operationType)
        {
            if (!TryFindOperation(operationType, out var found))
            {
                throw new InvalidOperationException(@$"Could not find an operation of the type {operationType.FullName}.

Make sure that it has been correctly registered through the use of the .Operations(...) configuration method at startup.

Operations will be found polymorphically, meaning an operation could be registered as an interface with the model but found as a concrete implementation type at runtime");
            }

            return found;
        }

        /// <summary>
        /// Finds the <see cref="ApiOperationDescriptor" /> for an operation of the given type, which may be a concrete
        /// implementation of a registered interface-based operation.
        /// </summary>
        /// <param name="operationType">The type of operation to search for.</param>
        /// <param name="descriptor">The <see cref="ApiOperationDescriptor" />, or <c>null</c> if no such operation exists.</param>
        /// <returns>Whether an operation was found.</returns>
        public bool TryFindOperation(Type operationType, out ApiOperationDescriptor descriptor)
        {
            var found = Operations.SingleOrDefault(d => d.OperationType.IsAssignableFrom(operationType));

            if (found == null)
            {
                descriptor = default;
                return false;
            }

            descriptor = found;
            return true;
        }
    }
}
