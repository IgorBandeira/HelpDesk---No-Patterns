using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace HelpDesk.Tests.IntegrationTests.Comments
{
    public class Comments_Integration_Tests : IClassFixture<HelpDeskApiFactory>
    {
        private readonly HttpClient _client;

        public Comments_Integration_Tests(HelpDeskApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        private async Task<int> CreateTicketAsync()
        {
            var dto = new
            {
                title = "Ticket para comentários",
                description = "Teste de comentários",
                categoryId = 1,
                priority = "Média"
            };

            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets")
            {
                Content = content
            };
            request.Headers.Add("userId", "1");

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("id").GetInt32();
        }

        [Fact]
        public async Task Add_Comment_Should_Create_Public_Comment()
        {
            // Arrange
            var ticketId = await CreateTicketAsync();

            var dto = new
            {
                message = "Comentário público de integração",
                visibility = "Público"
            };

            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/comments")
            {
                Content = content
            };
            request.Headers.Add("userId", "1"); 

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Comentário público de integração");
        }

        [Fact]
        public async Task Get_Comments_Should_Return_List_For_Ticket()
        {
            // Arrange
            var ticketId = await CreateTicketAsync();

            await AddCommentAsync(ticketId, "Primeiro comentário", "Público");

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tickets/{ticketId}/comments");
            request.Headers.Add("userId", "1");

            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Primeiro comentário");
        }

        private async Task AddCommentAsync(int ticketId, string message, string visibility)
        {
            var dto = new { message, visibility };
            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/comments")
            {
                Content = content
            };
            request.Headers.Add("userId", "2");

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }
}
