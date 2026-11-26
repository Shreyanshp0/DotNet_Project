using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DotNet_Project.DTOs;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DotNet_Project.Tests;

[Collection("WebApp")]
public class ChatAndParentBuddyTests
{
    private readonly TestWebApplicationFactory _factory;

    public ChatAndParentBuddyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(bool allowAutoRedirect = true) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true
        });

    [Fact]
    public async Task ChildAndParentBuddy_PersistAcrossLogoutLogin_RemainIsolated_AndResizeSupported()
    {
        var parentAEmail = $"cb_pa_{Guid.NewGuid():N}@example.com";
        var parentBEmail = $"cb_pb_{Guid.NewGuid():N}@example.com";
        var parentPwd = "ParentPass1!";

        var parentA = CreateClient();
        await parentA.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Parent Buddy A",
            ["Email"] = parentAEmail,
            ["Password"] = parentPwd,
            ["ConfirmPassword"] = parentPwd
        }));

        var parentB = CreateClient();
        await parentB.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Parent Buddy B",
            ["Email"] = parentBEmail,
            ["Password"] = parentPwd,
            ["ConfirmPassword"] = parentPwd
        }));

        // Create Child under Parent A
        var addRes = await parentA.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Buddy Child",
            ["DateOfBirth"] = "2015-08-12"
        }));
        var credHtml = await addRes.Content.ReadAsStringAsync();
        var childUsername = Regex.Match(credHtml, @"id=""credChildId""[^>]*>([^<]+)<").Groups[1].Value.Trim();
        var tempPwd = WebUtility.HtmlDecode(Regex.Match(credHtml, @"id=""credTempPassword""[^>]*>([^<]+)<").Groups[1].Value.Trim());
        var childPwd = "ChildSafe1!";

        var childClient = CreateClient();
        await childClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childUsername,
            ["Password"] = tempPwd
        }));
        await childClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = tempPwd,
            ["NewPassword"] = childPwd,
            ["ConfirmNewPassword"] = childPwd
        }));

        // 1. Child creates two conversations
        var cMsg1 = await (await childClient.PostAsJsonAsync("/Chat/SendMessage", new ChatMessageRequest
        {
            Message = "Child conversation 1 message",
            ConversationId = "conv-child-1"
        })).Content.ReadFromJsonAsync<ChatSendMessageResponse>();
        Assert.NotNull(cMsg1);

        await childClient.PostAsJsonAsync("/Chat/SendMessage", new ChatMessageRequest
        {
            Message = "Child conversation 2 message",
            ConversationId = "conv-child-2"
        });

        // Child logs out and logs back in with a fresh client (simulating cleared sessionStorage)
        await childClient.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()));
        var childReLogin = CreateClient();
        await childReLogin.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childUsername,
            ["Password"] = childPwd
        }));

        var childConvs = await childReLogin.GetFromJsonAsync<List<ChatConversationSummaryDto>>("/api/chat/conversations");
        Assert.NotNull(childConvs);
        Assert.Equal(2, childConvs!.Count);

        var reopenedChildConv1 = await childReLogin.GetFromJsonAsync<ChatHistoryResponse>("/api/chat/history/conv-child-1");
        Assert.NotNull(reopenedChildConv1);
        Assert.Contains(reopenedChildConv1!.Messages, m => m.Message == "Child conversation 1 message");

        // 2. Parent A uses Parent Buddy (multiple conversations, persists across logout/login)
        var pBuddyPage = await parentA.GetAsync("/Parent/Buddy");
        Assert.Equal(HttpStatusCode.OK, pBuddyPage.StatusCode);
        var pBuddyHtml = await pBuddyPage.Content.ReadAsStringAsync();
        Assert.Contains("Understanding my child's emotions.", pBuddyHtml);

        await parentA.PostAsJsonAsync("/Parent/Buddy/SendMessage", new ChatMessageRequest
        {
            Message = "How can I help my child with academic stress?",
            ConversationId = "pconv-1"
        });
        await parentA.PostAsJsonAsync("/Parent/Buddy/SendMessage", new ChatMessageRequest
        {
            Message = "How can I create a supportive daily routine?",
            ConversationId = "pconv-2"
        });

        await parentA.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()));
        var parentReLogin = CreateClient();
        await parentReLogin.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = parentAEmail,
            ["Password"] = parentPwd
        }));

        var parentConvs = await parentReLogin.GetFromJsonAsync<List<ChatConversationSummaryDto>>("/api/parent-buddy/conversations");
        Assert.NotNull(parentConvs);
        Assert.Equal(2, parentConvs!.Count);

        var reopenedParentConv1 = await parentReLogin.GetFromJsonAsync<ChatHistoryResponse>("/api/parent-buddy/history/pconv-1");
        Assert.NotNull(reopenedParentConv1);
        Assert.Contains(reopenedParentConv1!.Messages, m => m.Message.Contains("academic stress"));
        Assert.DoesNotContain(reopenedParentConv1.Messages, m => m.Message.Contains("Child conversation"));

        // 3. Parent B cannot access Parent A's Parent Buddy conversation
        var parentBHist = await parentB.GetFromJsonAsync<ChatHistoryResponse>("/api/parent-buddy/history/pconv-1");
        Assert.NotNull(parentBHist);
        Assert.Empty(parentBHist!.Messages);

        // 4. Child cannot access Parent Buddy
        var childNoRedirect = CreateClient(allowAutoRedirect: false);
        await childNoRedirect.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childUsername,
            ["Password"] = childPwd
        }));
        var childToParentBuddy = await childNoRedirect.GetAsync("/Parent/Buddy");
        Assert.Equal(HttpStatusCode.Redirect, childToParentBuddy.StatusCode);
        Assert.Contains("/Account/AccessDenied", childToParentBuddy.Headers.Location?.ToString());

        // 5. Verify resizable CSS & JS persistence assets
        var css = await (await childReLogin.GetAsync("/css/buddy.css")).Content.ReadAsStringAsync();
        Assert.Contains("resize: both", css);
        Assert.Contains("overflow-wrap: anywhere", css);

        var js = await (await childReLogin.GetAsync("/js/buddy.js")).Content.ReadAsStringAsync();
        Assert.Contains("ResizeObserver", js);
        Assert.Contains("localStorage.setItem(sizeKey", js);

        // 6. Parent Buddy safety check
        Assert.True(ParentBuddySafetyEvaluator.IsImmediateDanger("My child says they want to hurt themselves"));
        var safeOut = ParentBuddySafetyEvaluator.EnsureSafeReply("I diagnose your child with anxiety.");
        Assert.DoesNotContain("diagnose", safeOut, StringComparison.OrdinalIgnoreCase);
    }
}
