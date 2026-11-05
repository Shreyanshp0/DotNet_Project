using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

public class AccountController : Controller
{
    [HttpGet]
    public IActionResult Login() => View();

    [HttpGet]
    public IActionResult Register() => View();
}
