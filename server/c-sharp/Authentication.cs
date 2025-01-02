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
    public class Authentication
    {
        public static async Task<Objects.chunkAuthorization> chunk_authorisation(Objects.Config config, string mode, string asset_id, long file_size, long chunk)
        {
            Objects.HTTPS_Request_Response chunk_authorisation_response = await Https.Request_POST(
                config.mode.authentication.chunk_authorisation,
                new Objects.chunkAuthorizationBody {
                    mode = mode,
                    asset_id = asset_id,
                    size = file_size,
                    chunk = chunk
                }
            );
            Objects.chunkAuthorization chunk_authorization_output = JsonConvert.DeserializeObject<Objects.chunkAuthorization>(chunk_authorisation_response.response_body);
            return chunk_authorization_output;
        }
    }
}