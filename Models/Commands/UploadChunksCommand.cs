using System.Windows.Input;
using Models.Messages;

namespace Models.Commands;

public class UploadChunksCommand : IControlCommand
{
     public ControlMessageType Type { get; set; } 

     public string SessionId { get; set; } = string.Empty;

     /// <summary>
     /// Url, по которому нода должна открыть ws соединение
     /// для загрузки себе чанков
     /// </summary>
     public string DataUrl { get; set; } = string.Empty;
}