﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using OpenDirectoryDownloader.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace OpenDirectoryDownloader.FileUpload
{
    public class GoFileIo
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static async Task<GoFilesFile> UploadFile(HttpClient httpClient, string path)
        {
            int retries = 0;
            int maxRetries = 5;

            while (retries < maxRetries)
            {
                try
                {
                    string jsonServer = await httpClient.GetStringAsync("https://apiv2.gofile.io/getServer");

                    JObject result = JObject.Parse(jsonServer);

                    if (result["status"].Value<string>() == "error")
                    {
                        throw new Exception("GoFile.io error, probably in maintenance");
                    }

                    string server = result.SelectToken("data.server").Value<string>();

                    using (MultipartFormDataContent multipartFormDataContent = new MultipartFormDataContent($"Upload----{Guid.NewGuid()}"))
                    {
                        multipartFormDataContent.Add(new StreamContent(new FileStream(path, FileMode.Open)), "filesUploaded", Path.GetFileName(path));
                        multipartFormDataContent.Add(new StringContent("file"), "category");
                        multipartFormDataContent.Add(new StringContent("0"), "category");

                        using (HttpResponseMessage httpResponseMessage = await httpClient.PostAsync($"https://{server}.gofile.io/upload", multipartFormDataContent))
                        {
                            if (httpResponseMessage.IsSuccessStatusCode)
                            {
                                string response = await httpResponseMessage.Content.ReadAsStringAsync();

                                Logger.Debug($"Response from GoFile.io: {response}");

                                return JsonConvert.DeserializeObject<GoFilesFile>(response);
                            }
                            else
                            {
                                retries++;
                                Logger.Error($"Error uploading file... Retry in 5 seconds!!!");
                                await Task.Delay(TimeSpan.FromSeconds(5));
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    retries++;
                    Logger.Error($"Error uploading file... Retry in 5 seconds!!!");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            throw new FriendlyException("Error uploading URLs");
        }
    }

    public class GoFilesFile
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("data")]
        public GoFilesFileData Data { get; set; }

        public string Url { get => $"https://gofile.io/?c={Data.Code}"; }
    }

    public class GoFilesFileData
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("removalCode")]
        public string RemovalCode { get; set; }
    }
}
