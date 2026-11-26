using DotNet_Project.Data;
using DotNet_Project.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Services;

public enum SupportActionOutcome
{
    Success,
    NotFound,
    Forbidden,
    InvalidState
}

public interface ISupportRequestService
{
    Task<(SupportRequest? Request, bool IsDuplicate, string? ValidationError)> CreateAsync(string childUserId, string? reason, string? message);
    Task<List<SupportRequest>> GetForChildAsync(string childUserId);
    Task<List<SupportRequest>> GetForParentAsync(string parentUserId);
    Task<List<SupportRequest>> GetUnresolvedForParentAsync(string parentUserId);
    Task<List<SupportRequest>> GetResolvedForParentAsync(string parentUserId);
    Task<Dictionary<int, int>> GetUnresolvedCountsByChildAsync(string parentUserId);
    Task<List<SupportRequest>> GetUnresolvedForChildAsync(string parentUserId, int childProfileId);
    Task<List<SupportRequest>> GetResolvedForChildAsync(string parentUserId, int childProfileId);
    Task<SupportActionOutcome> AcknowledgeAsync(string parentUserId, int requestId, string? parentResponse = null);
    Task<SupportActionOutcome> ResolveAsync(string parentUserId, int requestId, string? parentResponse = null);
}

public class SupportRequestService(ApplicationDbContext db) : ISupportRequestService
{
    public static readonly IReadOnlyList<string> AllowedReasons =
    [
        "I feel upset.",
        "Something is worrying me.",
        "I am having a difficult day.",
        "I need help with something.",
        "I would prefer not to say."
    ];

    private static readonly Dictionary<string, string> ReasonMap =
        AllowedReasons.ToDictionary(r => r, r => r, StringComparer.OrdinalIgnoreCase);

    public async Task<(SupportRequest? Request, bool IsDuplicate, string? ValidationError)> CreateAsync(
        string childUserId,
        string? reason,
        string? message)
    {
        var profile = await db.ChildProfiles
            .FirstOrDefaultAsync(c => c.ChildUserId == childUserId);

        if (profile is null)
        {
            return (null, false, "Linked child profile not found.");
        }

        var canonicalReason = "I would prefer not to say.";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            var trimmedReason = reason.Trim();
            if (!ReasonMap.TryGetValue(trimmedReason, out var matched))
            {
                return (null, false, "Please select a valid support reason.");
            }
            canonicalReason = matched;
        }

        string? cleanMessage = null;
        if (!string.IsNullOrWhiteSpace(message))
        {
            cleanMessage = message.Trim();
            if (cleanMessage.Length > 500)
            {
                return (null, false, "Optional message cannot exceed 500 characters.");
            }
        }

        // Prevent accidental double-clicks within a 2-minute window for identical pending requests
        var recentCutoff = DateTime.UtcNow.AddMinutes(-2);
        var existingPending = await db.SupportRequests
            .Where(r => r.ChildUserId == childUserId &&
                        r.Status == SupportRequestStatus.Pending &&
                        r.Reason == canonicalReason &&
                        r.CreatedAt >= recentCutoff)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (existingPending is not null && string.Equals(existingPending.Message ?? "", cleanMessage ?? "", StringComparison.Ordinal))
        {
            return (existingPending, true, null);
        }

        var request = new SupportRequest
        {
            ChildProfileId = profile.Id,
            ChildUserId = childUserId,
            ParentUserId = profile.ParentUserId,
            Reason = canonicalReason,
            Message = cleanMessage,
            Status = SupportRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.SupportRequests.Add(request);
        await db.SaveChangesAsync();

        return (request, false, null);
    }

    public async Task<List<SupportRequest>> GetForChildAsync(string childUserId)
    {
        return await db.SupportRequests
            .Where(r => r.ChildUserId == childUserId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToListAsync();
    }

    public async Task<List<SupportRequest>> GetForParentAsync(string parentUserId)
    {
        return await db.SupportRequests
            .Include(r => r.ChildProfile)
            .Where(r => r.ParentUserId == parentUserId && r.ChildProfile.ParentUserId == parentUserId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task<List<SupportRequest>> GetUnresolvedForParentAsync(string parentUserId)
    {
        return await db.SupportRequests
            .Include(r => r.ChildProfile)
            .Where(r => r.ParentUserId == parentUserId &&
                        r.ChildProfile.ParentUserId == parentUserId &&
                        (r.Status == SupportRequestStatus.Pending || r.Status == SupportRequestStatus.Acknowledged))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SupportRequest>> GetResolvedForParentAsync(string parentUserId)
    {
        return await db.SupportRequests
            .Include(r => r.ChildProfile)
            .Where(r => r.ParentUserId == parentUserId &&
                        r.ChildProfile.ParentUserId == parentUserId &&
                        r.Status == SupportRequestStatus.Resolved)
            .OrderByDescending(r => r.ResolvedAt ?? r.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task<Dictionary<int, int>> GetUnresolvedCountsByChildAsync(string parentUserId)
    {
        return await db.SupportRequests
            .Where(r => r.ParentUserId == parentUserId &&
                        r.ChildProfile.ParentUserId == parentUserId &&
                        (r.Status == SupportRequestStatus.Pending || r.Status == SupportRequestStatus.Acknowledged))
            .GroupBy(r => r.ChildProfileId)
            .Select(g => new { ChildProfileId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ChildProfileId, x => x.Count);
    }

    public async Task<List<SupportRequest>> GetUnresolvedForChildAsync(string parentUserId, int childProfileId)
    {
        return await db.SupportRequests
            .Include(r => r.ChildProfile)
            .Where(r => r.ChildProfileId == childProfileId &&
                        r.ParentUserId == parentUserId &&
                        r.ChildProfile.ParentUserId == parentUserId &&
                        (r.Status == SupportRequestStatus.Pending || r.Status == SupportRequestStatus.Acknowledged))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SupportRequest>> GetResolvedForChildAsync(string parentUserId, int childProfileId)
    {
        return await db.SupportRequests
            .Include(r => r.ChildProfile)
            .Where(r => r.ChildProfileId == childProfileId &&
                        r.ParentUserId == parentUserId &&
                        r.ChildProfile.ParentUserId == parentUserId &&
                        r.Status == SupportRequestStatus.Resolved)
            .OrderByDescending(r => r.ResolvedAt ?? r.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task<SupportActionOutcome> AcknowledgeAsync(string parentUserId, int requestId, string? parentResponse = null)
    {
        var req = await db.SupportRequests
            .Include(r => r.ChildProfile)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (req is null) return SupportActionOutcome.NotFound;
        if (req.ParentUserId != parentUserId || req.ChildProfile.ParentUserId != parentUserId)
            return SupportActionOutcome.Forbidden;

        if (req.Status != SupportRequestStatus.Pending)
            return SupportActionOutcome.InvalidState;

        req.Status = SupportRequestStatus.Acknowledged;
        req.AcknowledgedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(parentResponse))
        {
            var trimmed = parentResponse.Trim();
            req.ParentResponse = trimmed.Length > 500 ? trimmed[..500] : trimmed;
        }

        await db.SaveChangesAsync();
        return SupportActionOutcome.Success;
    }

    public async Task<SupportActionOutcome> ResolveAsync(string parentUserId, int requestId, string? parentResponse = null)
    {
        var req = await db.SupportRequests
            .Include(r => r.ChildProfile)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (req is null) return SupportActionOutcome.NotFound;
        if (req.ParentUserId != parentUserId || req.ChildProfile.ParentUserId != parentUserId)
            return SupportActionOutcome.Forbidden;

        if (req.Status != SupportRequestStatus.Pending && req.Status != SupportRequestStatus.Acknowledged)
            return SupportActionOutcome.InvalidState;

        req.Status = SupportRequestStatus.Resolved;
        req.ResolvedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(parentResponse))
        {
            var trimmed = parentResponse.Trim();
            req.ParentResponse = trimmed.Length > 500 ? trimmed[..500] : trimmed;
        }

        await db.SaveChangesAsync();
        return SupportActionOutcome.Success;
    }
}
