using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DotNet_Project.Controllers;

[Authorize(Roles = "Child")]
public class AssessmentController : Controller
{
    public IActionResult Index() => View("~/Views/Child/Assessment.cshtml");

    public IActionResult Submitted() => View();
}
