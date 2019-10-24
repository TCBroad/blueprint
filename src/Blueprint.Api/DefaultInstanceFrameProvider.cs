﻿using System;
using System.Linq;
using Blueprint.Api.CodeGen;
using Blueprint.Compiler;
using Blueprint.Compiler.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Blueprint.Api
{
    public class DefaultInstanceFrameProvider : IInstanceFrameProvider
    {
        private readonly IServiceProvider provider;

        public DefaultInstanceFrameProvider(IServiceProvider provider)
        {
            this.provider = provider;
        }

        public GetInstanceFrame<T> VariableFromContainer<T>(GeneratedType generatedType, Type toLoad)
        {
            var registrations = (IServiceCollection)provider.GetService(typeof(IServiceCollection));
            var registrationsForType = registrations.Where(r => r.ServiceType == toLoad).ToList();

            if (registrationsForType.Any() == false)
            {
                throw new InvalidOperationException(
                    $"No registrations exist for the service type {toLoad.FullName}. If you are using the default IoC container that is " +
                    "built-in (Microsoft.Extensions.DependencyInjection) then you MUST register all services up-front, including concrete classes. If you" +
                    "are using an IoC container that does allow creating unregistered types (i.e. StructureMap) make sure you have registered that within" +
                    "your Blueprint setup.");
            }

            if (registrationsForType.Count == 1)
            {
                // When there is only one possible type that could be created from the IoC container
                // we can do a little more optimisation.
                var instanceRef = registrationsForType.Single();

                if (instanceRef.Lifetime == ServiceLifetime.Singleton)
                {
                    // We have a singleton object, which means we can have this injected at build time of the
                    // pipeline executor which will only be constructed once.
                    var injected = new InjectedField(toLoad);

                    generatedType.AllInjectedFields.Add(injected);

                    return new InjectedFrame<T>(injected);
                }
            }

            return new TransientInstanceFrame<T>(toLoad);
        }
    }
}
