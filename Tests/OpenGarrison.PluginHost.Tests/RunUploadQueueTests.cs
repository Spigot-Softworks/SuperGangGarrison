using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using OpenGarrison.ClientShared;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class RunUploadQueueTests
{
    [Fact]
    public async Task CompletedRunsSaveWhileAnotherUploadIsWaitingAndSurviveRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "og2-pending-runs-" + Guid.NewGuid().ToString("N"));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var identity = new ClientIdentityDocument { ClientId = Guid.NewGuid().ToString(), FriendCode = "OG2-ABCD-EFGH", ClientSecret = "never-store-in-proof" };
        var api = new OpenGarrisonPresenceClient($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}");
        Task? upload = null;
        try
        {
            var queue = new RunUploadQueue(directory);
            await queue.AddAsync(identity.ClientId, identity.FriendCode, "ruleset", [1, 2, 3]);
            upload = queue.SynchronizeAsync(identity, api);
            using (var connection = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5)))
            {
                await queue.AddAsync(identity.ClientId, identity.FriendCode, "ruleset", [4, 5, 6])
                    .WaitAsync(TimeSpan.FromSeconds(2));
                Assert.False(upload.IsCompleted);
                Assert.Equal(2, Directory.GetFiles(directory, "*.json").Length);
            }
            listener.Stop();
            await upload.WaitAsync(TimeSpan.FromSeconds(12));
            var restarted = new RunUploadQueue(directory);
            await restarted.SynchronizeAsync(identity, api);
            var files = Directory.GetFiles(directory, "*.json");
            Assert.Equal(2, files.Length);
            var recordings = new List<byte[]>();
            foreach (var file in files)
            {
                var json = await File.ReadAllTextAsync(file);
                Assert.DoesNotContain(identity.ClientSecret, json);
                recordings.Add(JsonSerializer.Deserialize<PendingRunUpload>(json)!.Recording);
            }
            Assert.Contains(recordings, bytes => bytes.SequenceEqual(new byte[] { 1, 2, 3 }));
            Assert.Contains(recordings, bytes => bytes.SequenceEqual(new byte[] { 4, 5, 6 }));
        }
        finally
        {
            listener.Stop();
            if (upload is not null) await upload;
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
