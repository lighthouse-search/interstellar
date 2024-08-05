🦄 Uploading video (with custom APIs)
==============

Description: Create a video ID within your existing API, upload chunks to Insterstellar, use your existing API to authenticate those chunks.

## Pipeline:
- #1: In your API, create an endpoint (e.g ``/video/create``). The user's client should call this endpoint, report the size of their video, and return a video_id. If using authentication (**strongly recommended**), you should authenticate the user and lock the video upload to that user. This endpoint is relevant to #3 ([Chunk-authentication](https://github.com/oracularhades/interstellar/blob/main/docs/upload-a-video/index.md#external-api-chunk-authentication))
- #2 [Upload-chunks](https://github.com/oracularhades/interstellar/blob/main/docs/upload-a-video/index.md#upload-chunks) - Send relevant data in this endpoint.
- #3 [Chunk-authentication](https://github.com/oracularhades/interstellar/blob/main/docs/upload-a-video/index.md#external-api-chunk-authentication) - (If you're using authentication on your Interstellar instances Interstellar will use this endpoint to communicate with your API internally and authenticate an incoming chunk.

## Upload chunks

```POST /interstellar/v1/upload-chunk```
Body (JSON):
| Key           | Description                                                                                                                       | Optional |
|---------------|-----------------------------------------------------------------------------------------------------------------------------------|----------|
| video_id      | The id of your video, this will be sent to the specified external api/auth service. This is required so chunks can easily be tracked between implementations and external APIs.                                                                                                                                                                                                                                                                                                                               | false    |
| chunk         | (Number) sequence for chunk. For example, if you were uploading your first chunk of data, you'd label it 0. Obviously, chunks of video must remain in order, this number allows a video to be reconstructed properly later.                      | false    |
| authorization | A string (passing a JWT is recommended) to authenticate with your external api/auth service. This will be sent in [chunk-authentication](https://github.com/oracularhades/interstellar/blob/main/docs/upload-a-video/index.md#external-api-chunk-authentication)                                                 | false    |
| file_type      | Interstellar server will store uploaded chunks in the specified file-type on disk, it must be a valid video format.               | false    |

Some notes:
- When uploading, it doesn't matter how much data you send. Interstellar will mark how much data you've uploaded (either by asking the specific external API to do so, or storing that data itself, based on configuration) and reject further data after the expected size threshold is reached. You can send ``2 bytes`` in one request, ``3 megabytes`` the next, and Interstellar will adopt accordingly. This is why you aren't required to specify chunk size in an upload. Interstellar is designed this way so developers can seemlessly adjust the upload speed based on the user's connection.

## (External API) Chunk authentication
*This endpoint is specified in your configuration file, it is external from Interstellar. It could be something like https://internal.example.com/video/auth*

There is no explicit requirement to authenticate but authentication checks before uploading are obviously recommended, to prevent spam (such as someone uploading a video that doesn't exist or they don't own). Interstellar can call back to the chunk authentication endpoint specified in the config, either:
- Every chunk
- Check once and cache successful authentication for X amount of time. Though this requires Redis connection, so data such as "bits uploaded" ("Uploaded" field) can be tracked by Interstellar.

It depends on your implementation, but either is fine, as Interstellar checks the uploader hasn't exceeded the expected video size.

Your external API can respond with:
### 200 (Successful authentication)

| Key       | Type   | Description                                                                                                                                                                                                 |
|-----------|--------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| video_id  | string | id of uploading video.                                                                                                                                                                                       |
| file_type | array | (optional) Restrict an upload specific file-type(s). Please note, until Interstellar does FFmpeg encoding (if you've enabled it), Interstellar will not verify the file content matches file-extension, but Interstellar will communicate to the client that only certain file-types are accepted. |
| uploaded  | number | Number of bits already uploaded.                                                                                                                                                                             |
| max_size  | number | Maximum size of the video in bits.                                                                                                                                                                           |

  ```
  {
    video_id: "opb2srmt4vc",
    file_type: ["avi"],
    uploaded: 0,
    max_size: 32000000000
  }
  ```

### * (Failed authentication)
| Key       | Type   | Description                                                                                                                                                                                                 |
|-----------|--------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| interstellar_public_error_message | string | (optional) Send an error message back to the original client via Interstellar.                                                                                                      |

  ```
  {
    interstellar_public_error_message: "Authentication failed - you do not have permission to upload the specified video."
  }
  ```

## Threading
Interstellar's chunk uploading implementation is amazing. Among instances, chunks are stateless (among Interstellar instances). This not only allows for resumable uploads, but chunks being stateless is what makes Interstellar so fast. Say you're running a cluster of Interstellar instances, all that matters is each instance has the same configuration and it literally does not matter what Interstellar instance your client sends data to, and in what order. You can have multiple connections open, to multiple instances (or a single instance) and Interstellar will simply reconstruct the video data when all data has been uploaded based of the chunk sequence.

### Single instance
You can have multiple HTTPS connections uploading chunk data at once, so you aren't limited by latency of backbones. This in of itself works _very_ well. For example, you can have 3 connections (40MB if your internet and browser can keep up) and as soon as a connection finishes, you're immediately sending another 40MB. This means you could be sending 120MB/s at once. It's very quick, and works *shockingly* well.

### Multi-instance
Say you have a load balancer at ```https://ingest.example.com/interstellar/v1/upload-chunk```, you have a cluster with 3 Interstellar containers running. Your load balancer is doing round-robin load-balancing (each request goes to a new instance). Because of Interstellars chunk implementation, that Interstellar server can authenticate and store that chunk data, no matter how random the uploaded chunk order is.
