using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace HelpDesk.Tests.IntegrationTests.Tickets
{
    public class Tickets_Integration_ListAndDetails_Tests : IClassFixture<HelpDeskApiFactory>
    {
        private readonly HttpClient _client;

        public Tickets_Integration_ListAndDetails_Tests(HelpDeskApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        private async Task<int> CreateTicketAsync(
            string title,
            string priority = "Crítica",
            int categoryId = 1,
            int requesterId = 1)
        {
            var dto = new
            {
                title,
                description = "Descrição de teste",
                categoryId,
                priority
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
        public async Task List_Should_Return_Created_Ticket_When_Filter_By_Title()
        {
            // Arrange
            var title = "Ticket para listagem";
            var ticketId = await CreateTicketAsync(title);

            // Act
            var response = await _client.GetAsync($"/api/tickets?title={Uri.EscapeDataString("listagem")}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Ticket para listagem");
            body.Should().Contain("\"id\":" + ticketId);
        }

        [Fact]
        public async Task List_Should_Return_400_When_Status_Is_Invalid()
        {
            // Arrange
            var invalidStatus = "Inexistente";

            // Act
            var response = await _client.GetAsync($"/api/tickets?status={invalidStatus}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task GetById_Should_Return_Details_When_Ticket_Exists()
        {
            // Arrange
            var ticketId = await CreateTicketAsync("Ticket para detalhes");

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tickets/{ticketId}");
            request.Headers.Add("userId", "1");

            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("id").GetInt32().Should().Be(ticketId);
            doc.RootElement.GetProperty("title").GetString().Should().Be("Ticket para detalhes");
        }

        [Fact]
        public async Task GetById_Should_Return_404_When_Ticket_Does_Not_Exist()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/tickets/9999");
            request.Headers.Add("userId", "1");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
