using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Net;

namespace Interstellar
{
    public class Objects
    {
        public class request_body_chunk_authorisation {
            public string asset_id { get; set; }
            public string mode { get; set; }
            public long size { get; set; } = -1;
            public long chunk { get; set; } = -1;
            public string authorisation { get; set; }
        }

        public class response_body_chunk_authorisation {
            public string asset_id { get; set; }
            public long uploaded { get; set; }
            public long max_size { get; set; }
        }

        public class request_body_get_upload_urls {
            public string asset_id { get; set; }
            public string authorisation { get; set; }
            public string[] object_keys { get; set; }
        }

        public class response_body_get_upload_urls {
            public string asset_id { get; set; }
            public List<S3.ObjectKey_URL> object_keys { get; set; }
        }

        public class request_body_status_update {
            public string asset_id { get; set; }
            public string type { get; set; }
            public string section { get; set; }
            public long stage { get; set; } = -1;
            public long out_of { get; set; } = -1;
            public string authorisation { get; set; }
        }

        public class errorD {
            public bool error { get; set; }
            // public string type { get; set; }
            public string message { get; set; }
            public object additional { get; set; }
        }
    }
}