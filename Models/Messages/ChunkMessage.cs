namespace Models.Messages;

/// <summary>
/// Сообщение с чанком по каналу data
/// </summary>
public class ChunkMessage
{
    /// <summary>
    /// Идентификатор
    /// </summary>
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>
    /// Данные чанка
    /// </summary>
    public string DataBase64 { get; set; } = string.Empty;
}