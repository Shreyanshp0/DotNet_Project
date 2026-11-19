using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Controllers;

[ApiController]
[Route("api/report")]
[Authorize(Roles = "Parent")]
public class ReportApiController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IAIReportService reportService) : ControllerBase
{
    [HttpGet("{assessmentId:int}")]
    public async Task<IActionResult> GetReport([FromRoute] int assessmentId)
    {
        var parentId = userManager.GetUserId(User)!;

        var assessment = await db.Assessments
            .Include(a => a.ChildProfile)
            .Include(a => a.AIReport)
            .FirstOrDefaultAsync(a => a.Id == assessmentId);

        if (assessment is null || assessment.ChildProfile.ParentUserId != parentId)
        {
            return NotFound(ApiErrorResponse.WithError("Report not ready yet or not found."));
        }

        var report = assessment.AIReport;
        if (report is null)
        {
            // If the report wasn't generated yet, generate it on demand
            try
            {
                report = await reportService.GenerateReportAsync(assessment.Id);
            }
            catch
            {
                return NotFound(ApiErrorResponse.WithError("Report not ready yet or not found."));
            }
        }

        var response = reportService.FormatResponse(report, assessment, assessment.ChildProfile);
        return Ok(response);
    }
}
