namespace ControllerWheel;

/// <summary>The driver operations used by cloak ownership and recovery. Allows failure-injection
/// tests without changing a machine's real driver configuration.</summary>
public interface IHidHideService
{
    bool IsInstalled { get; }
    bool IsActive { get; set; }
    bool IsAppListInverted { get; set; }
    IReadOnlyList<string> ApplicationPaths { get; }
    IReadOnlyList<string> BlockedInstanceIds { get; }
    void AddApplicationPath(string path);
    void RemoveApplicationPath(string path);
    void AddBlockedInstanceId(string id);
    void RemoveBlockedInstanceId(string id);
}
