using Models.Messages;

namespace Models.Commands;

public interface IControlCommand
{
     ControlMessageType Type { get; }
}