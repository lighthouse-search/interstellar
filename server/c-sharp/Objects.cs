using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace Interstellar
{
    public class Objects
    {
        // Input objects.
        public class videoChunkObject {
            public string video_byte { get; set; }
            public string id { get; set; }
            public string deviceid { get; set; }
            public string signature { get; set; }
            public string timestamp { get; set; }
            public string file_type { get; set; }
        }

        // Internal objects.
        public class UploadSignedObject {
            public string video_id { get; set; }
            public string deviceid { get; set; }
            public string file_type { get; set; }
            public long chunk { get; set; } = -1;
            public string video_hash { get; set; }
        }
        public class chunkAuthorizationBody {
            public string signature { get; set; }
            public string signedObject { get; set; }
            public string ingestid { get; set; }
            public string client_deviceid { get; set; }
            public string client_data { get; set; }
            public string client_signature { get; set; }
            public long size { get; set; }
            public long chunk { get; set; }
            public string video_id { get; set; }
            public string jwt { get; set; }
        }
        public class presignedUrlsBody {
            public string video_id { get; set; }
            public UInt16[] signature { get; set; }
            public UInt16[] presigned_url_signature { get; set; }
            public string data { get; set; }
            public string presigned_url_data { get; set; }
            public string ingestid { get; set; }
            public string[] files { get; set; }
            public string jwt { get; set; }
            public string client_deviceid { get; set; }
        }
        public class sizing {
            public Objects.streams[] streams { get; set; }
        }
        public class streams {
            public int width { get; set; }
            public int height { get; set; }
        }
        public class ffprobevideoinfo {
            public Objects.ffprobevideoinfostream[] streams { get; set; }
            public Objects.ffprobevideoinfoformat format { get; set; }
        }
        public class ffprobevideoinfostream {
            public int index { get; set; }
            public string codec_name { get; set; }
            public string codec_long_name { get; set; }
            public string profile { get; set; }
            public string codec_type { get; set; }
            public string codec_tag { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public int coded_width { get; set; }
            public int coded_height { get; set; }
            public int closed_captions { get; set; }
            public int has_b_frames { get; set; }
            public string sample_aspect_ration { get; set; }
            public string display_aspect_ratio { get; set; }
            public string pix_fmt { get; set; }
            public int level { get; set; }
            public string color_range { get; set; }
            public string color_space { get; set; }
            public string color_transfer { get; set; }
            public string color_primaries { get; set; }
            public string chroma_location { get; set; }
            public string field_order { get; set; }
            public int refs { get; set; }
            public string is_avc { get; set; }
            public string nal_length_size { get; set; }
            public string r_frame_rate { get; set; }
            public string avg_frame_rate { get; set; }
            public string time_base { get; set; }
            public int start_pts { get; set; }
            public string start_time { get; set; }
            public string bits_per_raw_sample { get; set; }
            public Objects.ffprobevideoinfostream_disposition disposition { get; set; }
            public Objects.ffprobevideoinfostream_tags tags { get; set; }
        }
        public class ffprobevideoinfostream_disposition {
            public int dub { get; set; }
            public int original { get; set; }
            public int comment { get; set; }
            public int lyrics { get; set; }
            public int karaoke { get; set; }
            public int forced { get; set; }
            public int hearing_impaired { get; set; }
            public int visual_impaired { get; set; }
            public int clean_effects { get; set; }
            public int attached_pic { get; set; }
            public int timed_thumbnails { get; set; }
        }
        public class ffprobevideoinfostream_tags {
            public string DURATION { get; set; }
        }
        public class ffprobevideoinfoformat {
            public string filename { get; set; }
            public int nb_streams { get; set; }
            public int nb_programs { get; set; }
            public string format_name { get; set; }
            public string format_long_name { get; set; }
            public string start_time { get; set; }
            public string duration { get; set; }
            public string size { get; set; }
            public string bit_rate { get; set; }
            public int probe_score { get; set; }
            public Objects.ffprobevideoinfotags tags { get; set; }
        }
        public class ffprobevideoinfotags {
            public string ENCODER { get; set; }
        }
        public class chunkAuthorization {
            public string video_id { get; set; }
            public long uploaded { get; set; }
            public long max_size { get; set; }
            public bool error { get; set; }
            public string process_state { get; set; }
        }
        public class presignedUrlAuthorization {
            public bool ok { get; set; }
            public bool error { get; set; }
            public Objects.presignedUrlFilesArray[] files { get; set; }
        }
        public class presignedUrlFilesArray {
            public string file { get; set; }
            public string url { get; set; }
        }

        // Response objects.
        public class reqok {
            public bool ok { get; set; }
            public string reencoded_chunk { get; set; }
        }
        public class errorD {
            public bool error { get; set; }
            public string type { get; set; }
            public string message { get; set; }
            public bool denied { get; set; }
        }
        public class ingestStatusUpdate {
            public string type { get; set; }
            public string message { get; set; }
        }
    }
}