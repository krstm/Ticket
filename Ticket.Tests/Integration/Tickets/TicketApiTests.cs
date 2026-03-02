using System.Net;
using System.Net.Http;
using System.Text.Json;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Domain.Enums;
using Ticket.Tests.TestUtilities;

namespace Ticket.Tests.Integration.Tickets;

public class TicketApiTests : IntegrationTestBase
{
    public TicketApiTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task TicketLifecycle_Should_Create_Update_Status_And_Filter()
    {
        var category = await CreateCategoryAsync("Support");
        var department = await CreateDepartmentAsync("IT Ops", "ops.agent@example.com");
        var requesterActor = BuildActor("Jane Doe", "jane@example.com", TicketActorType.Requester);
        var departmentActor = BuildActor("Ops Agent", department.Members.First().Email, TicketActorType.DepartmentMember);

        using var createResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Email not working",
            Description = "<script>alert('x')</script> cannot send email",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Priority = TicketPriority.High,
            Requester = new()
            {
                Name = "Jane Doe",
                Email = "jane@example.com"
            }
        }));
        await EnsureSuccessAsync(createResponse);
        var created = await DeserializeAsync<TicketDetailsDto>(createResponse);

        Assert.Single(NotificationSpy.CreatedEvents);
        Assert.Contains("ops.agent@example.com", NotificationSpy.CreatedEvents.Single().Recipients);

        var updateRequest = new TicketUpdateRequest
        {
            Title = "Email down",
            Description = "Updated description",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Priority = TicketPriority.Critical,
            Actor = requesterActor,
            Requester = new()
            {
                Name = "Jane Doe",
                Email = "jane@example.com"
            }
        };

        using var updateMessage = new HttpRequestMessage(HttpMethod.Put, $"/tickets/{created.Id}")
        {
            Content = AsJson(updateRequest)
        };
        updateMessage.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(created.RowVersion));
        using var updateResponse = await Client.SendAsync(updateMessage);
        await EnsureSuccessAsync(updateResponse);
        var updated = await DeserializeAsync<TicketDetailsDto>(updateResponse);

        using var progressMessage = new HttpRequestMessage(HttpMethod.Patch, $"/tickets/{created.Id}/status")
        {
            Content = AsJson(new TicketStatusUpdateRequest
            {
                Status = TicketStatus.InProgress,
                ChangedBy = "ops",
                Actor = departmentActor
            })
        };
        progressMessage.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(updated.RowVersion));
        using var progressResponse = await Client.SendAsync(progressMessage);
        await EnsureSuccessAsync(progressResponse);
        var progressed = await DeserializeAsync<TicketDetailsDto>(progressResponse);

        using var statusMessage = new HttpRequestMessage(HttpMethod.Patch, $"/tickets/{created.Id}/status")
        {
            Content = AsJson(new TicketStatusUpdateRequest
            {
                Status = TicketStatus.Resolved,
                ChangedBy = "tester",
                Note = "Issue fixed",
                Actor = requesterActor
            })
        };
        statusMessage.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(progressed.RowVersion));
        using var statusResponse = await Client.SendAsync(statusMessage);
        await EnsureSuccessAsync(statusResponse);

        Assert.Single(NotificationSpy.ResolvedEvents);
        Assert.Contains("ops.agent@example.com", NotificationSpy.ResolvedEvents.Single().Recipients);

        using var listResponse = await Client.GetAsync("/tickets?Statuses=Resolved&SearchTerm=email");
        await EnsureSuccessAsync(listResponse);
        var list = await DeserializeAsync<PagedResult<TicketSummaryDto>>(listResponse);

        Assert.Single(list.Items);
        Assert.Equal(TicketStatus.Resolved, list.Items.First().Status);
        Assert.True(list.TotalCount >= 1);
    }

    [Fact]
    public async Task Concurrency_Should_Return409_On_Stale_RowVersion()
    {
        var category = await CreateCategoryAsync("Networking");
        var department = await CreateDepartmentAsync("NetOps", "netops@example.com");
        var actor = BuildActor("Requester", "req@example.com", TicketActorType.Requester);
        using var createResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "VPN outage",
            Description = "vpn down",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Requester = new()
            {
                Name = "Requester",
                Email = "req@example.com"
            }
        }));
        await EnsureSuccessAsync(createResponse);
        var created = await DeserializeAsync<TicketDetailsDto>(createResponse);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/tickets/{created.Id}")
        {
            Content = AsJson(new TicketUpdateRequest
            {
                Title = "VPN outage - edit",
                Description = "desc",
                CategoryId = category.Id,
                DepartmentId = department.Id,
                Actor = actor,
                Requester = new()
                {
                    Name = "Requester",
                    Email = "req@example.com"
                }
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(created.RowVersion));
        using var response1 = await Client.SendAsync(request);
        await EnsureSuccessAsync(response1);

        using var staleRequest = new HttpRequestMessage(HttpMethod.Put, $"/tickets/{created.Id}")
        {
            Content = AsJson(new TicketUpdateRequest
            {
                Title = "Stale update",
                Description = "desc",
                CategoryId = category.Id,
                DepartmentId = department.Id,
                Actor = actor,
                Requester = new()
                {
                    Name = "Requester",
                    Email = "req@example.com"
                }
            })
        };
        staleRequest.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(created.RowVersion));
        using var staleResponse = await Client.SendAsync(staleRequest);

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    [Fact]
    public async Task Reports_ShouldSummarizeTickets()
    {
        var categoryA = await CreateCategoryAsync("HR");
        var categoryB = await CreateCategoryAsync("Finance");
        var departmentA = await CreateDepartmentAsync("PeopleOps", "people@example.com");
        var departmentB = await CreateDepartmentAsync("FinOps", "fin@example.com");

        using (var formTicketResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Access form",
            Description = "Need form",
            CategoryId = categoryA.Id,
            DepartmentId = departmentA.Id,
            Priority = TicketPriority.Low,
            Requester = new()
            {
                Name = "HR User",
                Email = "hr@example.com"
            }
        })))
        {
            await EnsureSuccessAsync(formTicketResponse);
        }

        using (var budgetTicketResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Budget tool issue",
            Description = "Cannot open tool",
            CategoryId = categoryB.Id,
            DepartmentId = departmentB.Id,
            Priority = TicketPriority.High,
            Requester = new()
            {
                Name = "Finance User",
                Email = "fin@example.com"
            }
        })))
        {
            await EnsureSuccessAsync(budgetTicketResponse);
        }

        using var response = await Client.GetAsync("/reports/summary?groupBy=department");
        response.EnsureSuccessStatusCode();
        var summaries = await DeserializeAsync<List<ReportBucketDto>>(response);

        Assert.Contains(summaries, s => s.Bucket == departmentA.Name && s.Count == 1);
        Assert.Contains(summaries, s => s.Bucket == departmentB.Name && s.Count == 1);
    }

    [Fact]
    public async Task KeysetPagination_Should_ProduceStableSlices()
    {
        var category = await CreateCategoryAsync("Infra");
        var department = await CreateDepartmentAsync("InfraOps", "infra@example.com");
        for (var i = 0; i < 5; i++)
        {
            using var ticketResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
            {
                Title = $"Batch {i}",
                Description = "body",
                CategoryId = category.Id,
                DepartmentId = department.Id,
                Requester = new()
                {
                    Name = $"Requester {i}",
                    Email = $"req{i}@example.com"
                }
            }));
            await EnsureSuccessAsync(ticketResponse);
        }

        using var firstPageResponse = await Client.GetAsync("/tickets?PageSize=2&SortDirection=Desc&SortBy=CreatedAt");
        await EnsureSuccessAsync(firstPageResponse);
        var firstPage = await DeserializeAsync<PagedResult<TicketSummaryDto>>(firstPageResponse);

        Assert.Equal(2, firstPage.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.NextPageToken));

        using var secondPageResponse = await Client.GetAsync($"/tickets?PageSize=2&SortDirection=Desc&SortBy=CreatedAt&PageToken={Uri.EscapeDataString(firstPage.NextPageToken!)}");
        await EnsureSuccessAsync(secondPageResponse);
        var secondPage = await DeserializeAsync<PagedResult<TicketSummaryDto>>(secondPageResponse);

        Assert.Equal(2, secondPage.Items.Count);
        Assert.Empty(firstPage.Items.Select(t => t.Id).Intersect(secondPage.Items.Select(t => t.Id)));
    }

    [Fact]
    public async Task SearchScope_TitleOnly_ShouldRestrictMatches()
    {
        var category = await CreateCategoryAsync("Apps");
        var department = await CreateDepartmentAsync("AppsOps", "apps@example.com");
        using (var appTicketResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Printer",
            Description = "Email outage",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Requester = new()
            {
                Name = "App User",
                Email = "app@example.com"
            }
        })))
        {
            await EnsureSuccessAsync(appTicketResponse);
        }

        using var descriptionSearch = await Client.GetAsync("/tickets?SearchTerm=email&SearchScope=FullContent");
        await EnsureSuccessAsync(descriptionSearch);
        var full = await DeserializeAsync<PagedResult<TicketSummaryDto>>(descriptionSearch);
        Assert.Single(full.Items);

        using var titleOnly = await Client.GetAsync("/tickets?SearchTerm=email&SearchScope=TitleOnly");
        await EnsureSuccessAsync(titleOnly);
        var scoped = await DeserializeAsync<PagedResult<TicketSummaryDto>>(titleOnly);
        Assert.Empty(scoped.Items);
    }

    [Fact]
    public async Task DomainEvents_ShouldPersistHistory_And_FireNotifications()
    {
        var category = await CreateCategoryAsync("Ops");
        var department = await CreateDepartmentAsync("OpsTeam", "ops@example.com");
        var requesterActor = BuildActor("Ops Requester", "ops.requester@example.com", TicketActorType.Requester);
        var departmentActor = BuildActor("Ops Engineer", department.Members.First().Email, TicketActorType.DepartmentMember);
        using var createResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Server issue",
            Description = "desc",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Requester = new()
            {
                Name = "Ops Requester",
                Email = "ops.requester@example.com"
            }
        }));
        await EnsureSuccessAsync(createResponse);
        var created = await DeserializeAsync<TicketDetailsDto>(createResponse);

        using var detailsResponse = await Client.GetAsync($"/tickets/{created.Id}");
        await EnsureSuccessAsync(detailsResponse);
        var details = await DeserializeAsync<TicketDetailsDto>(detailsResponse);
        Assert.Contains(details.History, h => h.Action.Contains("Ticket created", StringComparison.OrdinalIgnoreCase));
        Assert.Single(NotificationSpy.CreatedEvents);

        using var prepareMessage = new HttpRequestMessage(HttpMethod.Patch, $"/tickets/{created.Id}/status")
        {
            Content = AsJson(new TicketStatusUpdateRequest
            {
                Status = TicketStatus.InProgress,
                ChangedBy = "ops",
                Actor = departmentActor
            })
        };
        prepareMessage.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(created.RowVersion));
        using var prepareResponse = await Client.SendAsync(prepareMessage);
        await EnsureSuccessAsync(prepareResponse);
        var prepared = await DeserializeAsync<TicketDetailsDto>(prepareResponse);

        using var statusMessage = new HttpRequestMessage(HttpMethod.Patch, $"/tickets/{created.Id}/status")
        {
            Content = AsJson(new TicketStatusUpdateRequest
            {
                Status = TicketStatus.Resolved,
                ChangedBy = "ops",
                Actor = requesterActor
            })
        };
        statusMessage.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(prepared.RowVersion));
        using (var statusResponse = await Client.SendAsync(statusMessage))
        {
            await EnsureSuccessAsync(statusResponse);
        }

        Assert.Single(NotificationSpy.ResolvedEvents);
        using var resolvedDetailsResponse = await Client.GetAsync($"/tickets/{created.Id}");
        await EnsureSuccessAsync(resolvedDetailsResponse);
        var resolvedDetails = await DeserializeAsync<TicketDetailsDto>(resolvedDetailsResponse);
        Assert.Contains(resolvedDetails.History, h => h.Action.Contains("Status changed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DepartmentFilters_ShouldLimitSearchResults()
    {
        var category = await CreateCategoryAsync("Routing");
        var deptA = await CreateDepartmentAsync("NetworkA", "a@example.com");
        var deptB = await CreateDepartmentAsync("NetworkB", "b@example.com");

        using (var ticketAResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Router A",
            Description = "desc",
            CategoryId = category.Id,
            DepartmentId = deptA.Id,
            Requester = new()
            {
                Name = "User A",
                Email = "usera@example.com"
            }
        })))
        {
            await EnsureSuccessAsync(ticketAResponse);
        }

        using (var ticketBResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Router B",
            Description = "desc",
            CategoryId = category.Id,
            DepartmentId = deptB.Id,
            Requester = new()
            {
                Name = "User B",
                Email = "userb@example.com"
            }
        })))
        {
            await EnsureSuccessAsync(ticketBResponse);
        }

        using var response = await Client.GetAsync($"/tickets?DepartmentIds={deptA.Id}");
        await EnsureSuccessAsync(response);
        var result = await DeserializeAsync<PagedResult<TicketSummaryDto>>(response);
        Assert.Single(result.Items);
        Assert.Equal(deptA.Name, result.Items.Single().DepartmentName);
    }

    [Fact]
    public async Task DepartmentMember_EditPermissions_ShouldBeEnforced()
    {
        var category = await CreateCategoryAsync("Policies");
        var department = await CreateDepartmentAsync("PoliciesDept", "policy@example.com");
        var requesterActor = BuildActor("Requester", "requester@example.com", TicketActorType.Requester);
        var deptActor = BuildActor("Policy Member", department.Members.First().Email, TicketActorType.DepartmentMember);

        using var createResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Policy question",
            Description = "initial body",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Requester = new()
            {
                Name = "Requester",
                Email = "requester@example.com"
            }
        }));
        await EnsureSuccessAsync(createResponse);
        var ticket = await DeserializeAsync<TicketDetailsDto>(createResponse);

        using var allowedUpdate = new HttpRequestMessage(HttpMethod.Put, $"/tickets/{ticket.Id}")
        {
            Content = AsJson(new TicketUpdateRequest
            {
                Title = ticket.Title,
                Description = ticket.Description,
                CategoryId = category.Id,
                DepartmentId = department.Id,
                Priority = TicketPriority.High,
                Actor = deptActor,
                Requester = new()
                {
                    Name = "Requester",
                    Email = "requester@example.com"
                }
            })
        };
        allowedUpdate.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(ticket.RowVersion));
        using var allowedResponse = await Client.SendAsync(allowedUpdate);
        await EnsureSuccessAsync(allowedResponse);
        var allowedPayload = await DeserializeAsync<TicketDetailsDto>(allowedResponse);

        using var forbiddenUpdate = new HttpRequestMessage(HttpMethod.Put, $"/tickets/{ticket.Id}")
        {
            Content = AsJson(new TicketUpdateRequest
            {
                Title = ticket.Title,
                Description = "new body from dept",
                CategoryId = category.Id,
                DepartmentId = department.Id,
                Priority = allowedPayload.Priority,
                Actor = deptActor,
                Requester = new()
                {
                    Name = "Requester",
                    Email = "requester@example.com"
                }
            })
        };
        forbiddenUpdate.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(allowedPayload.RowVersion));
        using var forbiddenResponse = await Client.SendAsync(forbiddenUpdate);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        using var requesterUpdate = new HttpRequestMessage(HttpMethod.Put, $"/tickets/{ticket.Id}")
        {
            Content = AsJson(new TicketUpdateRequest
            {
                Title = ticket.Title,
                Description = "requester updated",
                CategoryId = category.Id,
                DepartmentId = department.Id,
                Priority = TicketPriority.Medium,
                Actor = requesterActor,
                Requester = new()
                {
                    Name = "Requester",
                    Email = "requester@example.com"
                }
            })
        };
        requesterUpdate.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(allowedPayload.RowVersion));
        using var requesterResponse = await Client.SendAsync(requesterUpdate);
        await EnsureSuccessAsync(requesterResponse);
    }

    [Fact]
    public async Task TicketComments_ShouldReturnChronologicalFeed()
    {
        var category = await CreateCategoryAsync("Chrono");
        var department = await CreateDepartmentAsync("ChronoDept", "chrono@example.com");

        using var createResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Comment order",
            Description = "desc",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Requester = new()
            {
                Name = "Chrono",
                Email = "chrono@example.com"
            }
        }));
        await EnsureSuccessAsync(createResponse);
        var ticket = await DeserializeAsync<TicketDetailsDto>(createResponse);

        var actor = BuildActor("Chrono Member", department.Members.First().Email, TicketActorType.DepartmentMember);

        using (var firstCommentResponse = await Client.PostAsync($"/tickets/{ticket.Id}/comments", AsJson(new TicketCommentCreateRequest
        {
            Body = "first",
            Actor = actor
        })))
        {
            await EnsureSuccessAsync(firstCommentResponse);
        }

        Clock.Advance(TimeSpan.FromSeconds(1));

        using (var secondCommentResponse = await Client.PostAsync($"/tickets/{ticket.Id}/comments", AsJson(new TicketCommentCreateRequest
        {
            Body = "second",
            Actor = actor
        })))
        {
            await EnsureSuccessAsync(secondCommentResponse);
        }

        using var response = await Client.GetAsync($"/tickets/{ticket.Id}/comments");
        await EnsureSuccessAsync(response);
        var comments = await DeserializeAsync<List<TicketCommentDto>>(response);
        Assert.Equal(new[] { "second", "first" }, comments.Select(c => c.Body));
    }

    private async Task<CategoryDto> CreateCategoryAsync(string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/categories")
        {
            Content = AsJson(new CategoryCreateRequest
            {
                Name = name
            })
        };
        using var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create category. Status: {response.StatusCode}, Body: {body}");
        }
        return await DeserializeAsync<CategoryDto>(response);
    }

    private async Task<DepartmentDto> CreateDepartmentAsync(string name, string primaryEmail)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/departments")
        {
            Content = AsJson(new DepartmentCreateRequest
            {
                Name = name,
                Members = new[]
                {
                    new DepartmentMemberRequest
                    {
                        FullName = $"{name} Member",
                        Email = primaryEmail,
                        NotifyOnTicketEmail = true,
                        IsActive = true
                    }
                }
            })
        };
        using var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create department. Status: {response.StatusCode}, Body: {body}");
        }

        return await DeserializeAsync<DepartmentDto>(response);
    }

    private static TicketActorContextDto BuildActor(string name, string email, TicketActorType type) =>
        new()
        {
            Name = name,
            Email = email,
            ActorType = type
        };

}
