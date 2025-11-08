using Core.Interfaces;
using Core.Workers;
using Microsoft.Extensions.Logging;
using Models.Commands;
using Models.Messages;
using Models.Session;

namespace Core.Services;

public class DataChannelWorkerFactory : IDataChannelWorkerFactory
{
    private readonly IChunkStorage _chunkStorage;
    private readonly ILoggerFactory _loggerFactory;

    public DataChannelWorkerFactory(IChunkStorage chunkStorage, ILoggerFactory loggerFactory)
    {
        _chunkStorage = chunkStorage;
        _loggerFactory = loggerFactory;
    }

    public BaseChannelWorker? Create(IControlCommand command)
    {
        switch (command.Type)
        {
            case ControlMessageType.UploadChunks:
                return CreateUploadWorker((UploadChunksCommand)command);
            
            default: return null;
        }
    }

    private BaseChannelWorker CreateUploadWorker(UploadChunksCommand cmd)
    {
        if (!Guid.TryParse(cmd.SessionId, out var sessionGuid))
            throw new ArgumentException($"Невалидный идентификатор сессии: {cmd.SessionId}", nameof(cmd));

        var sessionId = new SessionId(sessionGuid);
        var uri = new Uri(cmd.DataUrl);

        var logger = _loggerFactory.CreateLogger<UploadDataChannelWorker>();

        return new UploadDataChannelWorker(
            sessionId,
            uri,
            _chunkStorage,
            logger
        );
    }
}