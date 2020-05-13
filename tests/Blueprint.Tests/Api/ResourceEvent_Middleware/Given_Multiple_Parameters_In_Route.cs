namespace Blueprint.Tests.Api.ResourceEvent_Middleware
{
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;
    using Blueprint.Api;
    using Blueprint.Api.Configuration;
    using FluentAssertions;
    using NUnit.Framework;
    using Testing;

    public class Given_Multiple_Parameters_In_Route
    {
        public class CreationOperation : ICommand
        {
            [Required]
            public string IdToCreate { get; set; }

            public string EmailToCreate { get; set; }

            public CreatedResourceEvent Invoke()
            {
                return new CreatedResourceEvent(new SelfQuery { Id = IdToCreate, Email = EmailToCreate});
            }
        }

        // Pick any authorisation that we would not have
        [SelfLink(typeof(AwesomeApiResource), "/resources/{Id}/path/{Email}")]
        public class SelfQuery : IQuery<AwesomeApiResource>
        {
            [Required]
            public string Id { get; set; }

            [Required]
            public string Email { get; set; }

            public AwesomeApiResource Invoke()
            {
                return new AwesomeApiResource
                {
                    Id = Id,
                    Email = Email
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

            public string Email { get; set; }
        }

        [Test]
        public async Task When_HttpMessagePopulation_Operation_Then_Properties_Are_Populated()
        {
            // Arrange
            var executor = TestApiOperationExecutor.Create(o => o
                .WithOperation<CreationOperation>()
                .WithOperation<SelfQuery>()
                .Configure(a => a.AddHttp())
                .Pipeline(p => p.AddResourceEvents<NullResourceEventRepository>()));

            // Act
            var context = executor.HttpContextFor<CreationOperation>();
            ((CreationOperation)context.Operation).IdToCreate = "1234";
            ((CreationOperation)context.Operation).EmailToCreate = "test@email.com";

            var result = await executor.ExecuteAsync(context);

            // Assert
            result.ShouldBeContent<CreatedResourceEvent>().Data.Id.Should().Be("1234");
            result.ShouldBeContent<CreatedResourceEvent>().Data.Email.Should().Be("test@email.com");
        }
    }
}