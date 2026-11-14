using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Models;
using Ticketory.Application.Services;

namespace Ticketory.Api.Controllers;

public class HomeController(EventService eventService) : Controller
{
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var events = await eventService.GetPublishedEventsAsync(search, page, 9, cancellationToken);
        ViewBag.Search = search;
        return View(events);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet]
    [Route("Home/StatusCode")]
    public IActionResult StatusPage(int code)
    {
        ViewBag.StatusCode = code;
        return View("StatusCode");
    }
}
