using System.Text.Json.Serialization;

namespace Models.Messages;

/// <summary>
/// Тип сообщения на контрольном канале
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ControlMessageType
{
    /// <summary>
    /// Команда Highway ноде - открыть data WebSocket и принимать чанки
    /// </summary>
    UploadChunks
}