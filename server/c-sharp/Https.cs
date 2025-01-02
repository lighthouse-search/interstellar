using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Interstellar;

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
    }
}