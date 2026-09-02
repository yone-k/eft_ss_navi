using System.Reflection;
using EftSsMap.App.Monitoring;

namespace EftSsMap.App.Tests.Monitoring;

public sealed class FileSystemWatcherCreatedSourceTests
{
    [Fact]
    public void ShouldForwardWatcherErrorAndUnsubscribeWhenDisposed()
    {
        var directory = Directory.CreateTempSubdirectory("eft-ss-map-watcher-");
        try
        {
            var source = new FileSystemWatcherCreatedSource(directory.FullName);
            var expected = new InternalBufferOverflowException("overflow");
            Exception? received = null;
            source.Error += (_, eventArgs) => received = eventArgs.Exception;
            var watcher = GetWatcher(source);

            RaiseError(watcher, expected);

            Assert.Same(expected, received);
            source.Dispose();
            received = null;
            RaiseError(watcher, new IOException("after dispose"));
            Assert.Null(received);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static FileSystemWatcher GetWatcher(FileSystemWatcherCreatedSource source)
    {
        var field = typeof(FileSystemWatcherCreatedSource).GetField(
            "_watcher",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<FileSystemWatcher>(field?.GetValue(source));
    }

    private static void RaiseError(FileSystemWatcher watcher, Exception exception)
    {
        var method = typeof(FileSystemWatcher).GetMethod(
            "OnError",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(watcher, [new ErrorEventArgs(exception)]);
    }
}
