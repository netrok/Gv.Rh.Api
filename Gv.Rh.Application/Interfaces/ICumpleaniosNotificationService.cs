namespace Gv.Rh.Application.Interfaces;

public interface ICumpleaniosNotificationService
{
    Task<int> EnviarCumpleaniosHoyAsync(
        bool incluirEdad,
        CancellationToken cancellationToken = default);
}