using Core.Workers;
using Models.Commands;

namespace Core.Interfaces;

public interface IDataChannelWorkerFactory
{
    BaseChannelWorker? Create(IControlCommand command);
}