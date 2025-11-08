namespace Core.Workers;

public abstract class BaseChannelWorker
{
    public abstract Task RunAsync(CancellationToken ct);
}