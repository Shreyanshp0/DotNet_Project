using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

public class StudentController : Controller
{
    public IActionResult Index() => View();

    public IActionResult MoodCheckIn() => View();

    public IActionResult Assessment() => View();

    public IActionResult Journal() => View();

    public IActionResult JournalHistory() => View();

    public IActionResult Buddy() => View();
}
