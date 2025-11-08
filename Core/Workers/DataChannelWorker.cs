using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Core.Interfaces;
using Microsoft.Extensions.Logging;
using Models.Chunk;
using Models.Messages;
using Models.Session;

namespace Core.Workers;

public class UploadDataChannelWorker : BaseChannelWorker
{
    private readonly SessionId _sessionId;
    private readonly Uri _uri;
    private readonly IChunkStorage _chunkStorage;
    private readonly ILogger<UploadDataChannelWorker> _logger;

    public UploadDataChannelWorker(
        SessionId sessionId,
        Uri uri,
        IChunkStorage chunkStorage,
        ILogger<UploadDataChannelWorker> logger)
    {
        _sessionId = sessionId;
        _uri = uri;
        _chunkStorage = chunkStorage;
        _logger = logger;
    }


    public override async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "Запуск {WorkerName} с сессией {SessionId} по адресу {Url}",
            nameof(UploadDataChannelWorker),
            _sessionId,
            _uri
        );

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(_uri, ct);

        var buffer = new byte[64 * 1024];

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            var sb = new StringBuilder();
            WebSocketReceiveResult result;

            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Websocket соединение с идентификатором {SessionId} закрыт", _sessionId);
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, ct);
                    return;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    _logger.LogWarning("Неожиданный тип сообщения по соединению {SessionId}. Тип - {Type}", _sessionId, result.MessageType);
                    break;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            var json = sb.ToString();
            if (string.IsNullOrWhiteSpace(json))
                continue;

            ChunkMessage? msg = null;

            try
            {
                msg = JsonSerializer.Deserialize<ChunkMessage>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка десериализации сообщения направленное {WorkerName}", nameof(UploadDataChannelWorker));
            }

            if (msg is null)
            {
                _logger.LogWarning("Прислано пустое сообщение, json={Json}", json);
                continue;
            }
            
            if (!Guid.TryParse(msg.ChunkId, out var chunkGuid))
            {
                _logger.LogWarning("Невалидный ChunkId: {ChunkId}", msg.ChunkId);
                continue;
            }

            var chunkId = new ChunkId(chunkGuid);
            var bytes = Convert.FromBase64String(msg.DataBase64);

            await _chunkStorage.StoreAsync(chunkId, bytes, ct);

            _logger.LogInformation(
                "Был сохранен чанк {ChunkId} размером {Size} в рамках сессии {SessionId}",
                chunkId,
                bytes.Length,
                _sessionId
            );
        }
    }
}