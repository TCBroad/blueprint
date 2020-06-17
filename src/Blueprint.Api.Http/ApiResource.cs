﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Blueprint.Api.Authorisation;
using Blueprint.Core.ThirdParty;
using Newtonsoft.Json;

namespace Blueprint.Api.Http
{
    /// <summary>
    /// An ApiResource that can be returned from an operation that has a standard format
    /// to be consumed by clients, including resource type ($object), links ($links) and whether this
    /// represents a 'partial' response ($partial).
    /// </summary>
    public class ApiResource : ILinkableResource, IHaveResourceKey
    {
        private static readonly ConcurrentDictionary<Type, string> TypeNameCache = new ConcurrentDictionary<Type, string>();

        private readonly Dictionary<string, Link> links = new Dictionary<string, Link>(5);

        /// <summary>
        /// Initializes a new instance of ApiResource, setting the <see cref="Object"/> property
        /// to a conventional name of the implementing class being passed to <see cref="GetTypeName"/>.
        /// </summary>
        public ApiResource()
        {
            Object = GetTypeName(GetType());
        }

        /// <summary>
        /// The object type of this resource, used to indicate to clients what they
        /// are dealing with (i.e. 'user', 'account', 'group').
        /// </summary>
        [JsonProperty(PropertyName = "$object")]
        [JsonPropertyName("$object")]
        public string Object { get; protected set; }

        /// <summary>
        /// The links for this resource, other endpoints that apply to this resource
        /// in it's current state.
        /// </summary>
        [DoNotCompare]
        [JsonProperty(PropertyName = "$links")]
        [JsonPropertyName("$links")]
        public IDictionary<string, Link> Links => links;

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public virtual string ResourceKey => null;

        /// <summary>
        /// Gets or sets a value indicating whether or not this resource is 'partial', an indication that not all properties have been
        /// populated for performance reasons and a client should reload from the 'self' link to retrieve all values.
        /// </summary>
        [JsonProperty(PropertyName = "$partial")]
        [JsonPropertyName("$partial")]
        public bool IsPartial { get; set; }

        /// <summary>
        /// Gets the type name (exposed as 'object' from the API) of the given resource type, applying
        /// default conventions of removing 'ApiResource', 'Resource' and 'DTO' string from the type name.
        /// </summary>
        /// <param name="resourceType">The type of the resource (extending from <see cref="ApiResource"/>).</param>
        /// <returns>The public "type name" of the resource type. Used in properties like <see cref="Object"/>.</returns>
        public static string GetTypeName(Type resourceType)
        {
            return TypeNameCache.GetOrAdd(
                resourceType,
                t => t.Name
                    .Replace("ApiResource", string.Empty)
                    .Replace("Resource", string.Empty)
                    .Replace("DTO", string.Empty)
                    .Camelize());
        }

        /// <summary>
        /// Adds a new link with the specific rel (relation) and <see cref="Link"/>
        /// definition, throwing an exception if a link with the specific relation already
        /// exists.
        /// </summary>
        /// <param name="rel">The relation the link has to this resource.</param>
        /// <param name="link">The link to be added.</param>
        /// <exception cref="InvalidOperationException">If a link with the same relation type has already been added.</exception>
        public void AddLink(string rel, Link link)
        {
            if (links.ContainsKey(rel))
            {
                throw new InvalidOperationException(
                    $"Cannot add multiple links with the same relation of '{rel}' to the api resource '{GetType().Name}'");
            }

            // Allow exception to bubble if link already exists. Allow normal success path to be quicker instead
            // of checking for duplicates (could do so in tests?)
            links.Add(rel, link);
        }
    }
}