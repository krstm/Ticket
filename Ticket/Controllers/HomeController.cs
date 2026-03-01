using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Ticket.Models;
using Ticket.DTOs.Requests;
using Ticket.DTOs.ViewModels;
using Ticket.Domain.Enums;
using Ticket.Interfaces.Services;

namespace Ticket.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ITicketService _ticketService;

        public HomeController(ILogger<HomeController> logger, ITicketService ticketService)
        {
            _logger = logger;
            _ticketService = ticketService;
        }

        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var query = new TicketQueryParameters
            {
                Page = 1,
                PageSize = 20,
                SortBy = TicketSortBy.CreatedAt,
                SortDirection = SortDirection.Desc
            };

            var paged = await _ticketService.SearchAsync(query, ct);
            var timeline = paged.Items.Select(item => new TimelineItemViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Priority = item.Priority,
                Status = item.Status,
                CategoryName = item.CategoryName,
                DepartmentName = item.DepartmentName,
                CreatedAtUtc = item.CreatedAtUtc,
                DueAtUtc = item.DueAtUtc
            }).ToList();

            return View(timeline);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
