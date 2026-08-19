using GM3P.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace GM3P.Patching
{
    public class RunThirdPartyApps
    {
        public async Task RunOtherApps(string winapp = $"C:\\Windows\\System32\\cmd.exe", string winarg = $"dir", string nixapp = "/bin/bash", string nixarg = $"ls -l", bool CreateNoWindow = false, bool UseShellExecute = false, bool RedirectStandardOutput = true)
        {
            using (var process = new Process())
            {
                if (OperatingSystem.IsWindows())
                {
                    process.StartInfo.FileName = winapp;
                    process.StartInfo.Arguments =
                        winarg;
                }
                else if (OperatingSystem.IsLinux())
                {
                    process.StartInfo.FileName = nixapp;
                    process.StartInfo.Arguments =
                        nixarg;
                }

                process.StartInfo.CreateNoWindow = CreateNoWindow;
                process.StartInfo.UseShellExecute = UseShellExecute;
                process.StartInfo.RedirectStandardOutput = RedirectStandardOutput;
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                Console.WriteLine(output);

                await process.WaitForExitAsync();
            }
        }
        public class RunAPI
        {
            // This class is responsible for making HTTP requests to external APIs. Stolen from: https://stackoverflow.com/questions/27108264/how-to-properly-make-a-http-web-get-request
            public class HttpService
            {
                private readonly HttpClient _client;

                public HttpService()
                {
                    HttpClientHandler handler = new HttpClientHandler
                    {
                        AutomaticDecompression = DecompressionMethods.All
                    };

                    _client = new HttpClient(handler);
                }

                public async Task<string> GetAsync(string uri)
                {
                    using HttpResponseMessage response = await _client.GetAsync(uri);

                    return await response.Content.ReadAsStringAsync();
                }

                public async Task<string> PostAsync(string uri, string data, string contentType)
                {
                    using HttpContent content = new StringContent(data, Encoding.UTF8, contentType);

                    HttpRequestMessage requestMessage = new HttpRequestMessage()
                    {
                        Content = content,
                        Method = HttpMethod.Post,
                        RequestUri = new Uri(uri)
                    };

                    using HttpResponseMessage response = await _client.SendAsync(requestMessage);

                    return await response.Content.ReadAsStringAsync();
                }
            }
        }
    }
}
