using BackendAPI.Services;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

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
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessageAsync(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error in HandleConnectionAsync: {ex.Message}");
                    break;
                }
            }
        }

        private async Task ProcessMessageAsync(string rawMessage)
        {
            try
            {
                // Try to parse as JSON with correlation ID
                var messageObject = JsonConvert.DeserializeObject<dynamic>(rawMessage);
                
                if (messageObject?.correlationId != null && messageObject?.data != null)
                {
                    // Message with correlation ID
                    string correlationId = messageObject.correlationId.ToString();
                    string actualMessage = messageObject.data.ToString();
                    
                    //Console.WriteLine($"[DEBUG] Processing message with correlation ID {correlationId}: {actualMessage}");
                    
                    // Process the actual command
                    var response = await CommunicationManager.Instance.HandleCommand(actualMessage);
                    
                    // Send response with correlation ID
                    await SendCorrelatedResponseAsync(correlationId, response ?? "Invalid command.");
                }
                else
                {
                    // Handle legacy messages without correlation ID
                    //Console.WriteLine($"[DEBUG] Processing legacy message: {rawMessage}");
                    var response = await CommunicationManager.Instance.HandleCommand(rawMessage);
                    
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
            catch (JsonException)
            {
                // Not JSON, treat as plain text (legacy support)
            // Console.WriteLine($"[DEBUG] Processing plain text message: {rawMessage}");
                var response = await CommunicationManager.Instance.HandleCommand(rawMessage);
                
                if (!string.IsNullOrEmpty(response))
                {
                    await SendMessageAsync(response);
                }
                else
                {
                    await SendMessageAsync("Invalid command.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error processing message: {ex.Message}");
                await SendMessageAsync("Error processing command.");
            }
        }

        // New method to send responses with correlation ID
        private async Task SendCorrelatedResponseAsync(string correlationId, string responseData)
        {
            try
            {
                var responseObject = new
                {
                    correlationId = correlationId,
                    data = responseData,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                var jsonResponse = JsonConvert.SerializeObject(responseObject);
                //Console.WriteLine($"[DEBUG] Sending correlated response: {jsonResponse}");
                
                await SendMessageAsync(jsonResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error sending correlated response: {ex.Message}");
            }
        }

        // Keep original method for backward compatibility
        public static async Task SendMessageAsync(string message)
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var messageBytes = Encoding.UTF8.GetBytes(message);
                    await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    //Console.WriteLine($"[DEBUG] Sent message: {message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error sending message: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("[WARNING] WebSocket is not connected, cannot send message");
            }
        }

        // Method to send correlated messages from external sources
        public static async Task SendCorrelatedMessageAsync(string correlationId, string data)
        {
            try
            {
                var responseObject = new
                {
                    correlationId = correlationId,
                    data = data,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                var jsonResponse = JsonConvert.SerializeObject(responseObject);
                await SendMessageAsync(jsonResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error sending correlated message: {ex.Message}");
            }
        }

        // Method to send broadcast messages (no correlation ID)
        public static async Task SendBroadcastAsync(string message)
        {
            try
            {
                var broadcastObject = new
                {
                    type = "broadcast",
                    data = message,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                var jsonBroadcast = JsonConvert.SerializeObject(broadcastObject);
                await SendMessageAsync(jsonBroadcast);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error sending broadcast: {ex.Message}");
            }
        }

        public static bool IsConnected()
        {
            return _webSocket != null && _webSocket.State == WebSocketState.Open;
        }
    }
}