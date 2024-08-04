🦄 Uploading a video
==============

## Upload chunks

```POST /interstellar/v1/upload-chunk```
Body (JSON):
| Key           | Description                                                                                                                       | Optional |
|---------------|-----------------------------------------------------------------------------------------------------------------------------------|----------|
| video_id      | The id of your video, this will be sent to the specified external api/auth service. This is required so chunks can easily be tracked between implementations and external APIs.                                                                                                                                                                                                                       | false    |
| authorization | A string (passing a JWT is recommended) to authenticate with your external api/auth service.                                      | false    |
| filetype      | Interstellar server will store uploaded chunks in the specified file-type on disk, it must be a valid video format.               | false    |

## (External API) Chunk authentication
There is no explicit requirement to authenticate but authentication checks before uploading are obviously recommended, to prevent spam (such as someone uploading a video that doesn't exist or they don't own)
. Interstellar 
