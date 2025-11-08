using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Core.Interfaces;
using Microsoft.Extensions.Options;
using Models.Commands;
using Models.Messages;

namespace WebApi.BackgroundWorkers;

public class ControlChannelWorker : BackgroundService
{
    private readonly ILogger<ControlChannelWorker> _logger;
    private readonly NodeSettings _nodeSettings;
    private readonly IDataChannelWorkerFactory _dataWorkerFactory;

    public ControlChannelWorker(
        ILogger<ControlChannelWorker> logger,
        IOptions<NodeSettings> nodeSettings,
        IDataChannelWorkerFactory dataWorkerFactory)
    {
        _logger = logger;
        _nodeSettings = nodeSettings.Value;
        _dataWorkerFactory = dataWorkerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var controlUrl = new Uri(_nodeSettings.HighwayUrl);
        var retryDelay = TimeSpan.FromSeconds(_nodeSettings.ControlRetryDelay);

        _logger.LogInformation(
            "{WorkerName} запущен. Адрес управляющего соединения {Url}",
            nameof(ControlChannelWorker),
            controlUrl.ToString()
        );

        while (!ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();

            try
            {
                _logger.LogInformation("Попытка установить управляющее соединение с Highway");

                await ws.ConnectAsync(controlUrl, ct);

                _logger.LogInformation("Управляющее соединение успешно установлено");

                await ListenControlLoopAsync(ws, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    message: "Ошибка в установлении управляющего соединения. Повторная попытка через {Seconds} секунд",
                    _nodeSettings.ControlRetryDelay
                );
            }     

            if (ct.IsCancellationRequested)
                break;

            _logger.LogInformation(
                "Повторная попытка подключения к контрольному каналу по адресу {Url} через {Seconds} секунд",
                controlUrl.ToString(),
                retryDelay.TotalSeconds
            );

            try
            {
                await Task.Delay(retryDelay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("{WorkerName} остановлен", nameof(ControlChannelWorker));
    }

    private async Task ListenControlLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
         var buffer = new byte[1024 * 4];

         while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
         {
             var sb = new StringBuilder();
             WebSocketReceiveResult result;

             do
             {
                 result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                 if (result.MessageType == WebSocketMessageType.Close)
                 {
                     _logger.LogInformation("Управляющее соединение закрыто со стороны Highway");
                     await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                     return;
                 }

                 if (result.MessageType != WebSocketMessageType.Text)
                 {
                     _logger.LogWarning("Неожиданный тип сообщения по управляющему соединению. Тип - {Type}", result.MessageType);
                     continue;
                 }

                 sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
             } while (!result.EndOfMessage);

             var json = sb.ToString();

             if (string.IsNullOrWhiteSpace(json))
                 continue;

             await HandleControlMessageAsync(json, ct);
         }
    }

    private async Task HandleControlMessageAsync(string json, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
            {
                _logger.LogWarning("В управляющем сообщении отсутствует поле 'Type'");
                return;
            }

            var typeProperty = typeElement.GetString();
            if (string.IsNullOrWhiteSpace(typeProperty))
            {
                _logger.LogWarning("Пустой 'Type' в управляющем сообщении");
                return;
            }

            if (!Enum.TryParse<ControlMessageType>(typeProperty, true, out var type))
            {
                _logger.LogWarning("Неизвестный 'Type' в управляющем сообщении. Присланный тип - {Type}", typeProperty);
                return;
            }

            IControlCommand? command = type switch
            {
                ControlMessageType.UploadChunks => JsonSerializer.Deserialize<UploadChunksCommand>(json),

                _ => null
            };

            if (command == null)
            {
                _logger.LogWarning("Не удалось десериализовать команду типа {Type}, Json = {Json}", typeProperty, json);
                return;
            }

            var worker = _dataWorkerFactory.Create(command);
            if (worker == null)
            {
                _logger.LogWarning("Для типа команды {Type} не определен Worker", typeProperty);
                return;
            }

            _ = Task.Run(() => worker.RunAsync(ct), ct);

            _logger.LogInformation(
                "Для команды {Type} создан и запущен Worker {WorkerType}",
                typeProperty,
                worker.GetType().Name
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки управляющего сообщения");
        }

        await Task.CompletedTask;
    }
}