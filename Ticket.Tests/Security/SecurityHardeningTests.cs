using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Domain.Enums;
using Ticket.Tests.TestUtilities;

namespace Ticket.Tests.Security;

public class SecurityHardeningTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task TicketCreation_ShouldSanitizeDescription()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client, "Security");
        var department = await CreateDepartmentAsync(client, "SecOps", "sec@example.com");

        var response = await client.PostAsync("/tickets", IntegrationTestBase.AsJson(new TicketCreateRequest
        {
            Title = "XSS attempt",
            Description = "<script>alert('boom')</script> body",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Priority = TicketPriority.Medium,
            Requester = new()
            {
                Name = "Security User",
                Email = "sec.user@example.com"
            }
        }));

        response.EnsureSuccessStatusCode();
        var ticket = await DeserializeAsync<TicketDetailsDto>(response);

        Assert.DoesNotContain("<script>", ticket.Description, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("<script>alert('boom')</script> body", ticket.Description, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RateLimiter_ShouldThrottleMutations()
    {
        using var factory = CustomWebApplicationFactory.Create(rateLimit: 5);
        var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client, "Ops");
        var department = await CreateDepartmentAsync(client, "OpsDept", "ops@example.com");

        var tickets = Enumerable.Range(0, 5).Select(i =>
        {
            var message = new HttpRequestMessage(HttpMethod.Post, "/tickets")
            {
                Content = IntegrationTestBase.AsJson(new TicketCreateRequest
                {
                    Title = $"Req {i}",
                    Description = "body",
                    CategoryId = category.Id,
                    DepartmentId = department.Id,
                    Requester = new()
                    {
                        Name = $"Requester {i}",
                        Email = $"req{i}@example.com"
                    }
                })
            };
            return client.SendAsync(message);
        }).ToArray();

        var responses = await Task.WhenAll(tickets);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task SqlInjectionLikeTerm_ShouldNotCrash()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client, "DBA");
        var department = await CreateDepartmentAsync(client, "DBTeam", "db@example.com");

        await client.PostAsync("/tickets", IntegrationTestBase.AsJson(new TicketCreateRequest
        {
            Title = "Database timeout",
            Description = "desc",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Requester = new()
            {
                Name = "DB User",
                Email = "db.user@example.com"
            }
        }));

        var response = await client.GetAsync("/tickets?SearchTerm=' OR 1=1;--");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InvalidPageToken_ShouldReturnBadRequest()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/tickets?SortBy=CreatedAt&SortDirection=Desc&PageToken=this-is-not-valid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnauthorizedActor_ShouldReceiveForbidden_OnUpdate()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client, "Backoffice");
        var department = await CreateDepartmentAsync(client, "BackofficeDept", "owner@example.com");

        var createResponse = await client.PostAsync("/tickets", IntegrationTestBase.AsJson(new TicketCreateRequest
        {
            Title = "Access issue",
            Description = "desc",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Requester = new()
            {
                Name = "Owner",
                Email = "owner@example.com"
            }
        }));
        createResponse.EnsureSuccessStatusCode();
        var ticket = await DeserializeAsync<TicketDetailsDto>(createResponse);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/tickets/{ticket.Id}")
        {
            Content = IntegrationTestBase.AsJson(new TicketUpdateRequest
            {
                Title = "Malicious edit",
                Description = "hacked",
                CategoryId = category.Id,
                DepartmentId = department.Id,
                Actor = new TicketActorContextDto
                {
                    Name = "Intruder",
                    Email = "intruder@example.com",
                    ActorType = TicketActorType.DepartmentMember
                },
                Requester = new()
                {
                    Name = "Owner",
                    Email = "owner@example.com"
                }
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(ticket.RowVersion));
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TicketComments_ShouldEnforceActorAndSanitize()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client, "Comments");
        var department = await CreateDepartmentAsync(client, "CommentsDept", "comments@example.com");

        var createResponse = await client.PostAsync("/tickets", IntegrationTestBase.AsJson(new TicketCreateRequest
        {
            Title = "Need update",
            Description = "desc",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Requester = new()
            {
                Name = "Comms",
                Email = "comms@example.com"
            }
        }));
        createResponse.EnsureSuccessStatusCode();
        var ticket = await DeserializeAsync<TicketDetailsDto>(createResponse);

        var commentResponse = await client.PostAsync($"/tickets/{ticket.Id}/comments", IntegrationTestBase.AsJson(new TicketCommentCreateRequest
        {
            Body = "<script>alert('x')</script>hello",
            Actor = new TicketActorContextDto
            {
                Name = "Dept Member",
                Email = department.Members.First().Email,
                ActorType = TicketActorType.DepartmentMember
            }
        }));
        commentResponse.EnsureSuccessStatusCode();
        var comment = await DeserializeAsync<TicketCommentDto>(commentResponse);
        Assert.DoesNotContain("<script>", comment.Body, StringComparison.OrdinalIgnoreCase);

        var outsiderResponse = await client.PostAsync($"/tickets/{ticket.Id}/comments", IntegrationTestBase.AsJson(new TicketCommentCreateRequest
        {
            Body = "legit",
            Actor = new TicketActorContextDto
            {
                Name = "Stranger",
                Email = "stranger@example.com",
                ActorType = TicketActorType.DepartmentMember
            }
        }));

        Assert.Equal(HttpStatusCode.Forbidden, outsiderResponse.StatusCode);
    }

    private static async Task<CategoryDto> CreateCategoryAsync(HttpClient client, string name)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/categories")
        {
            Content = IntegrationTestBase.AsJson(new CategoryCreateRequest
            {
                Name = name
            })
        };
        var response = await client.SendAsync(message);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create category. Status: {response.StatusCode}, Body: {body}");
        }
        return await DeserializeAsync<CategoryDto>(response);
    }

    private static async Task<DepartmentDto> CreateDepartmentAsync(HttpClient client, string name, string memberEmail)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/departments")
        {
            Content = IntegrationTestBase.AsJson(new DepartmentCreateRequest
            {
                Name = name,
                Members = new[]
                {
                    new DepartmentMemberRequest
                    {
                        FullName = $"{name} Member",
                        Email = memberEmail,
                        NotifyOnTicketEmail = true
                    }
                }
            })
        };
        var response = await client.SendAsync(message);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create department. Status: {response.StatusCode}, Body: {body}");
        }

        return await DeserializeAsync<DepartmentDto>(response);
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
        return result!;
    }
}
