namespace WebApi;

public class NodeSettings
{
    public string HighwayUrl { get; set; } = "ws://localhost:5005/node/control";

    public string StorageDirectory { get; set; } = "storage";

    public string NodeName { get; set; } = "node-konstantin";

    public int ControlRetryDelay { get; set; } = 5;
}