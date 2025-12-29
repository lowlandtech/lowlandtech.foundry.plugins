namespace LowlandTech.Foundry.PluginCore.Abstractions;

/// <summary>
/// Represents the lifecycle state of a plugin.
/// </summary>
public enum PluginState
{
    /// <summary>
    /// Plugin has been discovered but not yet installed.
    /// </summary>
    Discovered,

    /// <summary>
    /// Plugin has been installed but is not currently activated.
    /// </summary>
    Installed,

    /// <summary>
    /// Plugin is installed and actively running.
    /// </summary>
    Activated,

    /// <summary>
    /// Plugin is installed but has been disabled by the user.
    /// </summary>
    Disabled,

    /// <summary>
    /// Plugin failed to load or activate due to an error.
    /// </summary>
    Error
}
