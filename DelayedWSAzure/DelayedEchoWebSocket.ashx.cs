using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace DelayedWSAzure
{
    /// <summary>
    /// Implements a WebSocket server that echoes input back after
    /// a thirty second delay.
    /// This will be used to test and demonstrate how a UWP WebSockets client
    /// can configure itself to recieve Websockets data even when it's in the 
    /// background.
    /// Started with this: http://paulbatum.github.io/WebSocket-Samples/AspNetWebSocketEcho/
    /// Redid with the contents of the Server folder in the Win8 ControlChannelTrigger sample.
    /// </summary>
    public class DelayedEchoWebSocket : IHttpHandler
    {
        private const int MaxBufferSize = 64 * 1024;

        public void ProcessRequest(HttpContext context)
        {
            try
            {
                if (context.IsWebSocketRequest)
                    context.AcceptWebSocketRequest(HandleWebSocket);
                else
                    context.Response.StatusCode = 400;
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                context.Response.StatusDescription = ex.Message;
                context.Response.End();
            }
        }

        private async Task HandleWebSocket(WebSocketContext wsContext)
        {
            byte[] receiveBuffer = new byte[MaxBufferSize];
            ArraySegment<byte> buffer = new ArraySegment<byte>(receiveBuffer);
            WebSocket socket = wsContext.WebSocket;
            string userString;

            // send a "connected" message on new connection
            if (socket.State == WebSocketState.Open)
            {
                var announceString = "EchoWebSocket Connected at: " + DateTime.Now.ToString();
                ArraySegment<byte> outputBuffer2 = new ArraySegment<byte>(Encoding.UTF8.GetBytes(announceString));
                await socket.SendAsync(outputBuffer2, WebSocketMessageType.Text, true, CancellationToken.None);
            }

            // keep echoing while connected
            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult receiveResult = await socket.ReceiveAsync(buffer, CancellationToken.None);

                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    // Echo back code and reason strings if possible
                    if (receiveResult.CloseStatus == WebSocketCloseStatus.Empty)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None);
                    }
                    else
                    {
                        await socket.CloseAsync(
                            receiveResult.CloseStatus.GetValueOrDefault(),
                            receiveResult.CloseStatusDescription,
                            CancellationToken.None);
                    }
                    return;
                }

                int offset = receiveResult.Count;

                while (receiveResult.EndOfMessage == false)
                {
                    receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, offset, MaxBufferSize - offset), CancellationToken.None);
                    offset += receiveResult.Count;
                }

                if (receiveResult.MessageType == WebSocketMessageType.Text)
                {
                    string cmdString = Encoding.UTF8.GetString(receiveBuffer, 0, offset);
                    userString = cmdString;
                    if (userString == ".close")
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Win8 server says goodbye", CancellationToken.None);
                    }
                    else if (userString == ".abort")
                    {
                        socket.Abort();
                    }
                    else
                    {
                        userString = "You said: \"" + userString + "\"";

                        ArraySegment<byte> outputBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(userString));

                        await socket.SendAsync(outputBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                else if (receiveResult.MessageType == WebSocketMessageType.Binary)
                {
                    // Artificially inject a delay to enable the ControlChannelTrigger client app 
                    // to be suspended after sending the HttpRequest.
                    System.Threading.Thread.Sleep(25000);

                    ArraySegment<byte> outputBuffer = new ArraySegment<byte>(receiveBuffer, 0, receiveResult.Count);
                    await socket.SendAsync(outputBuffer, WebSocketMessageType.Binary, true, CancellationToken.None);
                }
            }
        }

        public bool IsReusable
        {
            get
            {
                return true;
            }
        }
    }
}