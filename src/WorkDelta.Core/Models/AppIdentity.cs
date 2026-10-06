namespace WorkDelta.Core.Models;
public sealed record AppIdentity(string UserId, string DisplayName, string DeviceId, string DeviceName)
{
    public string GitEmail => $"user-{UserId[..8].ToLowerInvariant()}@workdelta.invalid";
}
