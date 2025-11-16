using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace HelpDesk.Tests.IntegrationTests.Tickets
{
    public class Tickets_Integration_Workflow_Tests : IClassFixture<HelpDeskApiFactory>
    {
        private readonly HttpClient _client;


        public Tickets_Integration_Workflow_Tests(HelpDeskApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        private async Task<int> CreateUserAsync(string name, string email, string role, int managerId = 3)
        {
            var dto = new { name, email, role };
            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/users")
            {
                Content = content
            };
            request.Headers.Add("userId", managerId.ToString());

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("id").GetInt32();
        }

        private async Task<int> CreateTicketAsync(string title, int requesterId = 1)
        {
            var dto = new
            {
                title,
                description = "Ticket com fluxo completo",
                categoryId = 1,
                priority = "Alta"
            };

            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets")
            {
                Content = content
            };
            request.Headers.Add("userId", requesterId.ToString());

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("id").GetInt32();
        }

        [Fact]
        public async Task Assign_Should_Require_Requester_And_Valid_Agent()
        {
            // Arrange
            var agentId = await CreateUserAsync("Agent X", "agentx@helpdesk.com", "Agent");
            var ticketId = await CreateTicketAsync("Ticket para assign");

            var dto = new { agentId };
            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act 
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/assign")
            {
                Content = content
            };
            request.Headers.Add("userId", "3");

            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("\"agentId\":" + agentId);
        }

        [Fact]
        public async Task Assign_Should_Return_403_When_Agent_Tries_To_Assign()
        {
            // Arrange
            var agentId = await CreateUserAsync("Agent Y", "agenty@helpdesk.com", "Agent");
            var ticketId = await CreateTicketAsync("Ticket para assign por Agent");

            var dto = new { agentId };
            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/assign")
            {
                Content = content
            };
            request.Headers.Add("userId", "2");

            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Reopen_Should_Return_400_When_Ticket_Is_Cancelled()
        {
            // Arrange
            var ticketId = await CreateTicketAsync("Ticket não pode ser reaberto");

            var cancelDto = new { reason = "Cancelamento de teste" };
            var cancelContent = new StringContent(JsonSerializer.Serialize(cancelDto), Encoding.UTF8, "application/json");

            var cancelRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/cancel")
            {
                Content = cancelContent
            };
            cancelRequest.Headers.Add("userId", "1");

            var cancelResponse = await _client.SendAsync(cancelRequest);
            cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Act
            var reopenDto = new { reason = "Tentativa inválida" };
            var reopenContent = new StringContent(JsonSerializer.Serialize(reopenDto), Encoding.UTF8, "application/json");

            var reopenRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/reopen")
            {
                Content = reopenContent
            };
            reopenRequest.Headers.Add("userId", "1");

            var reopenResponse = await _client.SendAsync(reopenRequest);

            // Assert
            reopenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        [Fact]
        public async Task All_Workflow_Life_of_a_Ticket()
        {
            // Arrange
            var ticketId = await CreateTicketAsync("Ticket resolvido");

       
            var assignDto = new { agentId = 2 };
            var assignJson = JsonSerializer.Serialize(assignDto);
            var assignContent = new StringContent(assignJson, Encoding.UTF8, "application/json");

            var assignRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/assign")
            {
                Content = assignContent
            };
            assignRequest.Headers.Add("userId", "3");

            var assignResponse = await _client.SendAsync(assignRequest);
            assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var toInProgressDto = new { newStatus = "Em Andamento" };
            var toInProgressJson = JsonSerializer.Serialize(toInProgressDto);
            var toInProgressContent = new StringContent(toInProgressJson, Encoding.UTF8, "application/json");

            var toInProgressRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/status")
            {
                Content = toInProgressContent
            };
            toInProgressRequest.Headers.Add("userId", "2"); 

            var toInProgressResponse = await _client.SendAsync(toInProgressRequest);
            toInProgressResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var toResolvedDto = new { newStatus = "Resolvido" };
            var toResolvedJson = JsonSerializer.Serialize(toResolvedDto);
            var toResolvedContent = new StringContent(toResolvedJson, Encoding.UTF8, "application/json");

            var toResolvedRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/status")
            {
                Content = toResolvedContent
            };
            toResolvedRequest.Headers.Add("userId", "2");

            var toResolvedResponse = await _client.SendAsync(toResolvedRequest);
            toResolvedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Act
            var reopenDto = new { reason = "Ainda não está resolvido" };
            var reopenJson = JsonSerializer.Serialize(reopenDto);
            var reopenContent = new StringContent(reopenJson, Encoding.UTF8, "application/json");

            var reopenRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/reopen")
            {
                Content = reopenContent
            };

            reopenRequest.Headers.Add("userId", "1");

            var reopenResponse = await _client.SendAsync(reopenRequest);

            // Assert
            reopenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await reopenResponse.Content.ReadAsStringAsync();
            body.Should().NotBeNullOrWhiteSpace();
        }

    }
}
