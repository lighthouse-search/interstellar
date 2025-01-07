using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;

namespace Interstellar {
    public class Https
    {
        public static async Task<Objects.HTTPS_Request_Response> Request_POST(string url, object body)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string request_body = JsonConvert.SerializeObject(body);
            var content = new StringContent(request_body, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(url, content);

            string response_body = await response.Content.ReadAsStringAsync();
            
            Objects.HTTPS_Request_Response output = new Objects.HTTPS_Request_Response
            {
                response_body = response_body,
                status_code = response.StatusCode,
                client = client
            };

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Error: " + response.StatusCode);
                throw new Exception(JsonConvert.SerializeObject(output));
            }
            return output;
        }

        public static async Task Upload_file(string url, string path)
        {
            byte[] filedata = await File.ReadAllBytesAsync(path);
            var wc = new WebClient();
            wc.UploadData(url, "PUT", filedata);
        }

        private static async Task Upload_directory_file_handler(Objects.File_path_URL element, List<Objects.File_path_URL> files, long files_count)
        {
            string url = element.url;
            string file_path = element.file_path;
            string progress = $"{files.IndexOf(element)}/{files_count-1}";

            Console.WriteLine($"Moving to storage... {progress}: {file_path}");

            await Https.Upload_file(url, file_path);

            Console.WriteLine($"Moved to storage {progress}: {file_path}");
        }

        public static async Task Upload_directory(Objects.Config config, List<Objects.File_path_URL> files)
        {
            Console.WriteLine("Starting directory upload...");
            int active_connections = 0;
            int filecount_offset = 0;
            long files_count = files.Count-1; // We -1 as .Count starts from 1, not 0.
            
            if (config.mode.storage.multithreaded_transfer.enabled == true) {
                // Multithreaded transfers to storage are on by default and recommended.
                while (filecount_offset < files_count)
                {
                    if (active_connections < config.mode.storage.multithreaded_transfer.thread_limit) {
                        Thread thread1 = new Thread(async () => {
                            active_connections++;
                            Objects.File_path_URL element = files[filecount_offset];
                            filecount_offset++;

                            await Upload_directory_file_handler(element, files, files_count);
                            
                            active_connections--;
                        });

                        thread1.Start();
                    }
                }
            } else {
                foreach (Objects.File_path_URL element in files) {
                    await Upload_directory_file_handler(element, files, files_count);
                }
            }

            // Wait for any remaining transfers to complete.
            // "active_connections > 0" is here because when we do "offset++", we haven't finished the transfer yet, so the while loop will prematurely close. 
            while (active_connections > 0) {}
            Console.WriteLine("Finished directory upload.");
        }
    }
}