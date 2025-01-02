using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;   
using System.Text;
using System.Net;
using Newtonsoft.Json;
using Interstellar;
using Tomlyn;
using Tomlyn.Model;
using System.Reflection;

namespace Interstellar
{
    public class Remote_server
    {
        public static async Task<Objects.presignedUrlAuthorization> Get_upload_urls(Objects.Config config, string[] files, string asset_id)
        {
            Objects.HTTPS_Request_Response upload_url_request = await Https.Request_POST(
                config.mode.storage.get_upload_urls,
                new Objects.presignedUrlsBody {
                    asset_id = asset_id,
                    files = files,
                }
            );
            Objects.presignedUrlAuthorization presignedurl_authorization_output = JsonConvert.DeserializeObject<Objects.presignedUrlAuthorization>(upload_url_request.response_body);
            if (presignedurl_authorization_output.ok != true) {
                // TODO: This error shouldn't return to the client.
                throw new Exception("Presigned URLs error");
            }

            return presignedurl_authorization_output;
        }
    }
}