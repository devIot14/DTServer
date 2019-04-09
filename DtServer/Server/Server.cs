using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Windows.Networking.Sockets;

namespace ServerTcp
{
    public class Server
    {
        public List<string> Status;

        private const ushort PORT = 5037;

        public async void StartServer()
        {
            Status = new List<string>();
            
            try
            {
                Status.Add("AVVIO DEL SERVER TCP IN CORSO...");

                var streamSocketListener = new StreamSocketListener();

                Status.Add("AVVIO DEL SERVER TCP IN CORSO...");

                // The ConnectionReceived event is raised when connections are received.
                streamSocketListener.ConnectionReceived += this.StreamSocketListener_ConnectionReceived;

                Status.Add("AVVIO DEL SERVER TCP IN CORSO...");

                // Start listening for incoming TCP connections on the specified port. You can specify any port that's not currently in use.
                await streamSocketListener.BindServiceNameAsync(PORT.ToString());

                Status.Add("SERVER TCP AVVIATO...");
            }
            catch (Exception ex)
            {
                SocketErrorStatus webErrorStatus = Windows.Networking.Sockets.SocketError.GetStatus(ex.GetBaseException().HResult);
                Status.Add($"È STATO RISCONTRATA UNA ECCEZIONE NEL SERVER TCP\t->\t{webErrorStatus}");
            }
        }
        
        private async void StreamSocketListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            string request;
            using (var streamReader = new StreamReader(args.Socket.InputStream.AsStreamForRead()))
            {
                request = await streamReader.ReadLineAsync();
            }
            
            // Echo the request back as the response.
            using (Stream outputStream = args.Socket.OutputStream.AsStreamForWrite())
            {
                using (var streamWriter = new StreamWriter(outputStream))
                {
                    await streamWriter.WriteLineAsync(request);
                    await streamWriter.FlushAsync();
                }
            }

            sender.Dispose();
        }
    }
}
