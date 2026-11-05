using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

public class ParentController : Controller
{
    public IActionResult Index() => View();

    public IActionResult Report(int id = 1042) => View();

    public IActionResult History() => View();

    public IActionResult DailyActivity() => View();

    public IActionResult QuestionnaireDetails() => View();

    public IActionResult JournalDetails() => View();
}
