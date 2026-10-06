using WorkDelta.Core.Services;

namespace WorkDelta.Core.Tests;

public sealed class PathPolicyTests
{
    [Theory]
    [InlineData("node_modules/package/index.js")]
    [InlineData("src/bin/output.txt")]
    [InlineData(".git/config")]
    [InlineData("build/result.cs")]
    [InlineData("notes.tmp")]
    public void ShouldIgnore_KnownNoise_ReturnsTrue(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "workdelta-policy");
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(new PathPolicy().ShouldIgnore(root, fullPath));
    }

    [Theory]
    [InlineData("src/App.cs")]
    [InlineData("README.md")]
    [InlineData("docs/设计说明.txt")]
    public void ShouldIgnore_ProjectText_ReturnsFalse(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "workdelta-policy");
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.False(new PathPolicy().ShouldIgnore(root, fullPath));
    }

    [Fact]
    public void ShouldIgnore_CustomGlob_ReturnsTrue()
    {
        var root = Path.Combine(Path.GetTempPath(), "workdelta-policy-custom");
        var policy = new PathPolicy();
        policy.SetCustomRules(root, "generated/*\n*.secret");

        Assert.True(policy.ShouldIgnore(root, Path.Combine(root, "generated", "result.txt")));
        Assert.True(policy.ShouldIgnore(root, Path.Combine(root, "keys.secret")));
        Assert.False(policy.ShouldIgnore(root, Path.Combine(root, "notes.txt")));
    }
}
