using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace HelpDesk.Tests.IntegrationTests.Attachments
{
    public class Attachments_Integration_Upload_Tests : IClassFixture<HelpDeskApiFactory>
    {
        private readonly HttpClient _client;

        public Attachments_Integration_Upload_Tests(HelpDeskApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        private async Task<int> CreateTicketAsync()
        {
            var dto = new
            {
                title = "Ticket para teste de anexos (integração)",
                description = "Testando upload de anexos via API",
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

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("id").GetInt32();
        }

        [Fact]
        public async Task Upload_Should_Return_400_When_Extension_Is_Forbidden()
        {
            // Arrange
            var ticketId = await CreateTicketAsync();

            var bytes = Encoding.UTF8.GetBytes("binário fake");
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var form = new MultipartFormDataContent();
            form.Add(fileContent, "file", "evil.exe");

            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/attachments")
            {
                Content = form
            };

            request.Headers.Add("userId", "1");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Extensão proibida.");
        }

        [Fact]
        public async Task Upload_Should_Return_201_When_File_Is_Valid()
        {
            // Arrange
            var ticketId = await CreateTicketAsync();

            var bytes = Encoding.UTF8.GetBytes("conteúdo de teste do anexo");
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

            var form = new MultipartFormDataContent();
            form.Add(fileContent, "file", "ok.txt");

            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/attachments")
            {
                Content = form
            };
            request.Headers.Add("userId", "1");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("ok.txt");
            body.Should().Contain($"\"ticketId\":{ticketId}");
        }
    }
}
