using FluentAssertions;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace HelpDesk.Tests.IntegrationTests.Tickets
{
    public class Tickets_Integration_Create_Tests(HelpDeskApiFactory factory) : IClassFixture<HelpDeskApiFactory>
    {
        private readonly HttpClient _client = factory.CreateClient();

        [Fact]
        public async Task Create_Should_Return_201_And_Body_When_Request_Is_Valid()
        {
            // Arrange
            var dto = new
            {
                title = "Erro ao acessar o sistema",
                description = "Ao tentar logar, aparece erro 500.",
                categoryId = 1,
                priority = "Crítica"
            };

            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets")
            {
                Content = content
            };
            request.Headers.Add("userId", "1");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Erro ao acessar o sistema");
            body.Should().Contain("Crítica");
        }

        [Fact]
        public async Task Create_Should_Return_400_When_Title_Is_Empty()
        {
            // Arrange
            var dto = new
            {
                title = "",
                description = "Descrição qualquer",
                categoryId = 1,
                priority = "Baixa"
            };

            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets")
            {
                Content = content
            };
            request.Headers.Add("userId", "1");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var message = await response.Content.ReadAsStringAsync();
            message.Should().Contain("Título é obrigatório");
        }
    }
}
