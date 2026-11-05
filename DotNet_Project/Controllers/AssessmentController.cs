using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

public class AssessmentController : Controller
{
    public IActionResult Index() => View("~/Views/Student/Assessment.cshtml");

    public IActionResult Submitted() => View();
}
