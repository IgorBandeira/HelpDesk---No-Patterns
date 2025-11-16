using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace HelpDesk.Tests.IntegrationTests.Categories
{
    public class Categories_Integration_Tests : IClassFixture<HelpDeskApiFactory>
    {
        private readonly HttpClient _client;

        public Categories_Integration_Tests(HelpDeskApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Create_Category_Should_Work_As_Manager()
        {
            // Arrange
            var dto = new
            {
                name = "Categoria de Teste",
                parentId = (int?)null
            };

            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/categories")
            {
                Content = content
            };
            request.Headers.Add("userId", "3");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Categoria de Teste");
        }

        [Fact]
        public async Task List_Categories_Should_Return_Seeded_And_Created_Ones()
        {
            // Act
            var response = await _client.GetAsync("/api/categories?page=1&pageSize=50");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Infraestrutura");
        }
    }
}
