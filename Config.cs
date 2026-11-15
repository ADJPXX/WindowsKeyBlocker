namespace WindowsKeyBlocker;

public class Config
{
    public HashSet<string> Executables { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}