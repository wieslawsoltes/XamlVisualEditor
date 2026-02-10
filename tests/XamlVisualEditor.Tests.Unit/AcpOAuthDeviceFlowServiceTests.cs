using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.App.Services;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class AcpOAuthDeviceFlowServiceTests
{
    [Fact]
    public async Task StartDeviceFlowParsesResponse()
    {
        QueueResponseHandler handler = new();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"device_code\":\"dev\",\"user_code\":\"user\",\"verification_uri\":\"https://example.com\",\"verification_uri_complete\":\"https://example.com/complete\",\"expires_in\":900,\"interval\":0}")
        });

        AcpOAuthDeviceFlowService service = new(new HttpClient(handler));
        var response = await service.StartDeviceFlowAsync("client", "scope", "https://example.com/device", CancellationToken.None);

        Assert.Equal("dev", response.DeviceCode);
        Assert.Equal("user", response.UserCode);
        Assert.Equal("https://example.com", response.VerificationUri);
        Assert.Equal("https://example.com/complete", response.VerificationUriComplete);
        Assert.Equal(900, response.ExpiresIn);
        Assert.Equal(0, response.Interval);
    }

    [Fact]
    public async Task CompleteDeviceFlowPollsUntilToken()
    {
        QueueResponseHandler handler = new();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"authorization_pending\"}")
        });
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"token\",\"refresh_token\":\"refresh\",\"expires_in\":3600,\"token_type\":\"bearer\"}")
        });

        AcpOAuthDeviceFlowService service = new(new HttpClient(handler));
        var token = await service.CompleteDeviceFlowAsync("client", "device", 0, "https://example.com/token", CancellationToken.None);

        Assert.Equal("token", token.AccessToken);
        Assert.Equal("refresh", token.RefreshToken);
        Assert.Equal(3600, token.ExpiresIn);
    }

    [Fact]
    public async Task RefreshTokenParsesResponse()
    {
        QueueResponseHandler handler = new();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"token2\",\"refresh_token\":\"refresh2\",\"expires_in\":1200,\"token_type\":\"bearer\"}")
        });

        AcpOAuthDeviceFlowService service = new(new HttpClient(handler));
        var token = await service.RefreshTokenAsync("client", "refresh", "https://example.com/token", CancellationToken.None);

        Assert.Equal("token2", token.AccessToken);
        Assert.Equal("refresh2", token.RefreshToken);
        Assert.Equal(1200, token.ExpiresIn);
    }

    private sealed class QueueResponseHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public void Enqueue(HttpResponseMessage response)
        {
            _responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
