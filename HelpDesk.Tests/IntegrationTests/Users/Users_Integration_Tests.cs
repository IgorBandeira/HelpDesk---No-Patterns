using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace HelpDesk.Tests.IntegrationTests.Users
{
    public class Users_Integration_Tests : IClassFixture<HelpDeskApiFactory>
    {
        private readonly HttpClient _client;

        public Users_Integration_Tests(HelpDeskApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Create_User_Should_Require_Manager_And_Valid_Role()
        {
            // Arrange
            var dto = new
            {
                name = "Novo Requester",
                email = "novo.requester@helpdesk.com",
                role = "Requester"
            };

            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/users")
            {
                Content = content
            };
            request.Headers.Add("userId", "3");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("novo.requester@helpdesk.com");
            body.Should().Contain("Requester");
        }

        [Fact]
        public async Task Create_User_Should_Return_403_When_Non_Manager()
        {
            // Arrange
            var dto = new
            {
                name = "Usuario Não Autorizado",
                email = "nao.autorizado@helpdesk.com",
                role = "Requester"
            };

            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/users")
            {
                Content = content
            };
            request.Headers.Add("userId", "1");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task List_Users_Should_Filter_By_Role()
        {
            // Act
            var response = await _client.GetAsync("/api/users?role=Manager&page=1&pageSize=20");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Manager");
        }
    }
}
