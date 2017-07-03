using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using System;
using System.Threading;
using System.Text;
using System.IO;

namespace StartupBasic
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory logger)
        {
            logger.AddConsole((str, level) =>
            {
                //filter out framework log messages
                return !str.Contains("Microsoft.AspNetCore") && level >= LogLevel.Trace;
            });

            var log = logger.CreateLogger("");

            app.UseWebSockets();

            app.Use(async (context, next) =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    // Not a web socket request
                    await next();
                    return;
                }

                var socket = await context.WebSockets.AcceptWebSocketAsync();
                var bufferSize = new byte[4];
                var receiveBuffer = new ArraySegment<byte>(bufferSize);
                WebSocketReceiveResult result;

                using (var ms = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        result = await socket.ReceiveAsync(receiveBuffer, CancellationToken.None);
                        if (result.MessageType != WebSocketMessageType.Text)
                            throw new Exception("Unexpected Message");

                        ms.Write(receiveBuffer.Array, receiveBuffer.Offset, result.Count);

                        receiveBuffer = new ArraySegment<byte>(bufferSize);
                        log.LogDebug($"Receive count {++count}");
                    }
                    while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    string clientRequest = string.Empty;
                    using (var reader = new StreamReader(ms, Encoding.UTF8))
                    {
                        clientRequest = await reader.ReadToEndAsync();
                    }

                    log.LogDebug($"Receive: {clientRequest}");

                    var serverReply = Encoding.UTF8.GetBytes("Echo " + clientRequest);
                    var replyBuffer = new ArraySegment<byte>(serverReply);
                    await socket.SendAsync(replyBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            });

            app.Run(async context =>
            {
                context.Response.Headers.Add("content-type", "text/html");
                await context.Response.WriteAsync(@"
<html>                
    <head>
        <script src=""https://code.jquery.com/jquery-3.2.1.min.js"" integrity=""sha256-hwg4gsxgFZhOsEEamdOYGBf13FyQuiTwlAQgxVSNgt4="" crossorigin=""anonymous""></script>
    </head>
    <body>
        <h1>Web Socket</h1>
        <input type=""text"" length=""50"" id=""msg"" value=""hello world""/> <button type=""button"" id=""send"">Send</button>
        <br/>
        <script>
            $(function(){
                var url = ""ws://localhost:5000"";
                var socket = new WebSocket(url);
                var send = $(""#send"");
                var msg = $(""#msg"");

                socket.onopen = function(e){
                    send.click(function(){
                        socket.send(msg.val());                    
                    });
                };

                socket.onmessage = function(e){
                    var response = e.data;
                    alert(response.trim());
                };
            });
        </script>
    </body>
</html>");
            });
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
              .UseKestrel()
              .UseStartup<Startup>()
              .Build();

            host.Run();
        }
    }
}