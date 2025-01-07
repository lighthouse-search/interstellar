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
        public static async Task setup(string asset_id)
        {
            // Ensure the required directories exist.
            System.IO.Directory.CreateDirectory($"/tmp/user_ingest_before_reencoding/{asset_id}");
            System.IO.Directory.CreateDirectory($"/tmp/user_ingest_before_reencoding/{asset_id}/chunks");
            System.IO.Directory.CreateDirectory($"/tmp/user_ingest_reencoded/{asset_id}");
        }
        public static async Task clean_up(HttpContext context, string asset_id)
        {
            Directory.Delete($"/tmp/user_ingest_before_reencoding/{asset_id}", true);
            Directory.Delete($"/tmp/user_ingest_reencoded/{asset_id}", true);
        }
        public static async Task Fatal_error(HttpContext context, string asset_id)
        {
            if (asset_id != null) {
                await clean_up(context, asset_id);
            }

            Console.WriteLine($"context.Response.HasStarted {context.Response.HasStarted}");
            if (context != null && context.Response.HasStarted == false) {
                await context.Response.CompleteAsync();
                context.Response.StatusCode = 500;
            }
            return;
        }
    }
}