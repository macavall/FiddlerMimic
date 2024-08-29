using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        int port = 8888;
        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"Listening on port {port}...");

        var clientTask = await listener.AcceptTcpClientAsync();
        await HandleClient(clientTask);

        Console.ReadLine();

        //while (true)
        //{
        //    var clientTask = await listener.AcceptTcpClientAsync();
        //    HandleClient(clientTask);
        //}
    }

    static SslStream CreateSslStream(TcpClient client, X509Certificate2 cert)
    {
        var stream = client.GetStream();
        var sslStream = new SslStream(stream, false);

        // Authenticate as server with the provided certificate
        try
        {
            sslStream.AuthenticateAsServer(cert, clientCertificateRequired: false, checkCertificateRevocation: true);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return sslStream;
    }

    static async Task HandleClient(TcpClient clientTask)
    {
        using (TcpClient client = clientTask)
        {
            using (NetworkStream clientStream = client.GetStream())
            {
                using (StreamReader reader = new StreamReader(clientStream))
                {
                    // Read the request from the client
                    string requestLine = await reader.ReadLineAsync();
                    Console.WriteLine(requestLine);

                    // Parse the request (example assumes CONNECT method)
                    string[] tokens = requestLine.Split(' ');
                    if (tokens.Length != 3 || tokens[0].ToUpper() != "CONNECT")
                    {
                        return;
                    }

                    string[] hostPort = tokens[1].Split(':');
                    string host = hostPort[0];
                    int port = int.Parse(hostPort[1]);

                    // Respond to the client that the connection was established
                    await clientStream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n"));

                    // Load the server certificate
                    var cert = new X509Certificate2("C:\\Users\\macavall\\FiddlerMimicPFX.pfx", "CoolKat61");

                    // Establish SSL/TLS connection with the client
                    try
                    {
                        SslStream clientSslStream = CreateSslStream(client, cert);

                        // Connect to the destination server
                        using (TcpClient server = new TcpClient(host, port))
                        {
                            using (SslStream serverSslStream = new SslStream(server.GetStream(), false))
                            {
                                // Establish SSL/TLS connection with the server
                                await serverSslStream.AuthenticateAsClientAsync(host);

                                // Capture and show the decrypted body of the POST request
                                var clientRequestBody = await ReadRequestBody(clientSslStream);
                                var clientResponseBody = await ReadResponseBody(clientSslStream);
                                
                                Console.WriteLine("Decrypted POST Request Body:");
                                Console.WriteLine("\n\n" + clientRequestBody + "\n\n");

                                Console.WriteLine("Decrypted POST Response Body:");
                                Console.WriteLine("\n\n" + clientResponseBody + "\n\n");

                                // Forward data between client and server, now using SSL/TLS streams
                                var clientToServer = clientSslStream.CopyToAsync(serverSslStream);
                                var serverToClient = serverSslStream.CopyToAsync(clientSslStream);

                                await Task.WhenAny(clientToServer, serverToClient);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }
        }
    }

    static async Task<string> ReadRequestBody(SslStream clientSslStream)
    {
        using (var memoryStream = new MemoryStream())
        {
            await clientSslStream.CopyToAsync(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
            {
                string requestBody = await reader.ReadToEndAsync();
                return requestBody;
            }
        }
    }

    static async Task<string> ReadResponseBody(SslStream clientSslStream)
    {
        using (var memoryStream = new MemoryStream())
        {
            await clientSslStream.CopyToAsync(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
            {
                string responseBody = await reader.ReadToEndAsync();
                return responseBody;
            }
        }
    }
}
