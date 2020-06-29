﻿using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using Blueprint.Api;
using Blueprint.Api.Configuration;
using Blueprint.Configuration;
using Blueprint.Http;
using Blueprint.Testing;
using FluentAssertions;
using NUnit.Framework;

namespace Blueprint.Tests.Api.ResourceEvent_Middleware
{
    public class Given_ResourceCreated_Result
    {
        public class CreationOperation : ICommand
        {
            [Required]
            public string IdToCreate { get; set; }

            public CreatedResourceEvent Invoke()
            {
                return new CreatedResourceEvent(new SelfQuery { Id = IdToCreate });
            }
        }

        [SelfLink(typeof(AwesomeApiResource), "/resources/{Id}")]
        public class SelfQuery : IQuery<AwesomeApiResource>
        {
            [Required]
            public string Id { get; set; }

            public AwesomeApiResource Invoke()
            {
                return new AwesomeApiResource
                {
                    Id = Id
                };
            }
        }

        public class CreatedResourceEvent : ResourceCreated<AwesomeApiResource>
        {
            public CreatedResourceEvent(IApiOperation<AwesomeApiResource> selfQuery) : base(selfQuery)
            {
            }
        }

        public class AwesomeApiResource : ApiResource
        {
            public string Id { get; set; }
        }

        [Test]
        public async Task When_ResourceCreated_And_Self_Query_Exists_Populates_Data()
        {
            // Arrange
            var executor = TestApiOperationExecutor.Create(o => o
                .WithOperation<CreationOperation>()
                .WithOperation<SelfQuery>()
                .Configure(a => a.AddHttp())
                .Pipeline(p => p.AddResourceEvents<NullResourceEventRepository>()));

            // Act
            // Note, do 2 executions to ensure correct parameters and being passed around
            var context1 = executor.HttpContextFor(new CreationOperation { IdToCreate = "1234" });
            var context2 = executor.HttpContextFor(new CreationOperation { IdToCreate = "9876" });

            var result1 = await executor.ExecuteAsync(context1);
            var result2 = await executor.ExecuteAsync(context2);

            // Assert
            result1.ShouldBeContent<CreatedResourceEvent>().Data.Id.Should().Be("1234");
            result2.ShouldBeContent<CreatedResourceEvent>().Data.Id.Should().Be("9876");
        }

        [Test]
        public async Task When_ResourceCreated_Then_Attributes_Populated()
        {
            using (var t = SystemTime.PauseForThread())
            {
                // Arrange
                var executor = TestApiOperationExecutor.Create(o => o
                    .WithOperation<CreationOperation>()
                    .WithOperation<SelfQuery>()
                    .Configure(a => a.AddHttp())
                    .Pipeline(p => p
                        .AddResourceEvents<NullResourceEventRepository>()
                        .AddAuth<TestUserAuthorisationContextFactory>()));

                // Act
                var context = executor.HttpContextFor(new CreationOperation { IdToCreate = "1234" })
                    .WithAuth(new Claim("sub", "User8547"));

                var result = await executor.ExecuteAsync(context);

                // Assert
                var @event = result.ShouldBeContent<CreatedResourceEvent>();

                @event.Created.UtcDateTime.Should().BeCloseTo(t.UtcNow);
                @event.Object.Should().Be("event");
                @event.EventId.Should().Be("awesome.created");
                @event.ChangeType.Should().Be(ResourceEventChangeType.Created);
                @event.ResourceObject.Should().Be("awesome");
            }
        }
    }
}
