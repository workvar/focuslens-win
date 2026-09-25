namespace FocusLens.Core.Permissions;

public enum PermissionState
{
    Granted,
    /// <summary>Switched off by the user or by policy.</summary>
    Denied,
    /// <summary>The hardware or component the feature needs is not there, so no switch can fix it.</summary>
    Unavailable,
}

/// <summary>Result of one check, ready to show: state, a plain-language detail, and what the Grant button does.</summary>
public sealed record PermissionStatus(PermissionState State, string Detail);

/// <summary>One thing FocusLens needs from Windows. Checks must be cheap, read-only and safe to repeat.</summary>
public interface IPermissionCheck
{
    string Id { get; }
    string Name { get; }
    /// <summary>What FocusLens uses it for, shown under the name.</summary>
    string Purpose { get; }
    /// <summary>Label for the fix button when the permission is missing, for example "Open Settings".</summary>
    string GrantLabel { get; }

    Task<PermissionStatus> CheckAsync();

    /// <summary>
    /// Fixes it when the app can (enable a setting), otherwise opens the exact Windows page where the user
    /// can. The caller re-checks afterwards.
    /// </summary>
    Task GrantAsync();
}
