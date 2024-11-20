using BackendAPI.Services;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace RasberryAPI.Middlewares
{
    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private static WebSocket? _webSocket;

        public WebSocketMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path == "/ws" && context.WebSockets.IsWebSocketRequest)
            {
                _webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await HandleConnectionAsync(_webSocket);
            }
            else
            {
                await _next(context);
            }
        }

        private async Task HandleConnectionAsync(WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var response = await CommunicationManager.Instance.HandleCommand(message);

                
                if (!string.IsNullOrEmpty(response))
                {
                    await SendMessageAsync(response);
                }
                else
                {
                    await SendMessageAsync("Invalid command.");

                }
            }
        }

        public static async Task SendMessageAsync(string message)
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                
                await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public static bool IsConnected()
        {
            return _webSocket != null && _webSocket.State == WebSocketState.Open;
        }
    }
}
