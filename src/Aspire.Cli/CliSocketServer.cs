// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipelines;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using StreamJsonRpc;

namespace Aspire.Cli;

internal sealed class CliSocketServer : BackgroundService
{
    private readonly string _socketPath = "/tmp/aspire.cli.socket." + Guid.NewGuid().ToString("N");

    public string GetSocketPath()
    {
        return _socketPath;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var socketPath = GetSocketPath();
        var endPoint = new UnixDomainSocketEndPoint(socketPath);
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(endPoint);
        socket.Listen();
        
        using var clientSocket = await socket.AcceptAsync(stoppingToken).ConfigureAwait(false);
        using var stream = new NetworkStream(clientSocket);
        var server = new JsonRpc(stream, stream, new Proxy());
    }
}

internal sealed class Proxy
{
    [JsonRpcMethod("echo")]
#pragma warning disable CA1822 // Mark members as static
    public string Echo(string message) => message;
#pragma warning restore CA1822 // Mark members as static
}