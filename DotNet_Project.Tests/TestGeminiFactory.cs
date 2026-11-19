using DotNet_Project.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNet_Project.Tests;

public static class TestGeminiFactory
{
    public static IGeminiAiService CreateTestService()
    {
        return new GeminiAiService(
            new HttpClient(),
            new ConfigurationBuilder().Build(),
            NullLogger<GeminiAiService>.Instance);
    }
}
