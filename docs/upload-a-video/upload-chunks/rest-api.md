```POST /interstellar/v1/upload-chunk```

Body (JSON):
| Key           | Description                                                                                                                       | Optional |
|---------------|-----------------------------------------------------------------------------------------------------------------------------------|----------|
| asset_id      | The id of your file, this will be sent to the specified external api/auth service. This is required so chunks can easily be tracked between implementations and external APIs.                                                                                                                                                                                                                                                                                                                               | false    |
| chunk         | (Number) sequence for chunk. For example, if you were uploading your first chunk of data, you'd label it 0. Obviously, chunks of video must remain in order, this number allows a video to be reconstructed properly later.                      | false    |
| authorization | A string (passing a JWT is recommended) to authenticate with your external api/auth service. This will be sent in [chunk-authentication](https://github.com/oracularhades/interstellar/blob/main/docs/upload-a-video/index.md#external-api-chunk-authentication)                                                 | false    |
| file_type      | Interstellar server will store uploaded chunks in the specified file-type on disk, it must be a valid video format.               | false    |

Some notes:
- When uploading, it doesn't matter how much data you send. Interstellar will mark how much data you've uploaded (either by asking the specific external API to do so, or storing that data itself, based on configuration) and reject further data after the expected size threshold is reached. You can send ``2 bytes`` in one request, ``3 megabytes`` the next, and Interstellar will adopt accordingly. This is why you aren't required to specify chunk size in an upload. Interstellar is designed this way so developers can seemlessly adjust the upload speed based on the user's connection.
