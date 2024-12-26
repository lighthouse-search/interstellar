using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Interstellar;

namespace Interstellar
{
    public static class Handler
    {
        public static void Fatal_error(HttpContext context, string video_id)
        {
            if (video_id != null) {
                File_operations.DeleteDirectory($"/tmp/user_ingest_before_reencoding/{video_id}");
                File_operations.DeleteDirectory($"/tmp/user_ingest_reencoded/{video_id}");
            }

            context.Response.StatusCode = 500;
            return;
        }
    }
}