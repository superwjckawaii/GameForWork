using System.IO.Pipes;

namespace GameForWork.GodotClient;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = "GameForWork.SingleInstance";
    private const string PipeName = "GameForWork.Activate";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenerTask;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        IsPrimary = createdNew;
    }

    public bool IsPrimary { get; }

    public void NotifyPrimary()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1_000);
            client.WriteByte(1);
        }
        catch (IOException)
        {
            // The primary process may still be starting; exiting remains safer than opening two writers.
        }
        catch (TimeoutException)
        {
        }
    }

    public void StartListening(Action activate)
    {
        ArgumentNullException.ThrowIfNull(activate);
        if (!IsPrimary || _listenerTask is not null)
        {
            return;
        }

        _listenerTask = Task.Run(async () =>
        {
            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(_cancellation.Token).ConfigureAwait(false);
                    _ = server.ReadByte();
                    activate();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    if (_cancellation.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        });
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _cancellation.Dispose();
        GC.SuppressFinalize(this);
    }
}
