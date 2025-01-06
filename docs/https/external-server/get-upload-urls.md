Get upload urls
==============

## What Interstellar will do.
When Interstellar recieves a chunk and config.mode.authentication.type is "remote_server", like this:
```
"storage": {
    ""upload_server": {
        "type": "remote_server",
        "get_upload_urls": "http://127.0.0.1:4455/get-upload-urls"
    }
},
"modes": {
    "default": {
        "storage_reference": {
            "id": "upload_server",
            "path": "/%asset_id%"
        },
    }
}
```

Interstellar will send:
```
curl -X POST https://127.0.0.1:4455/get-upload-urls \
-H "Content-Type: application/json" \
-d '{
  "asset_id": [current_asset_id],
  "object_keys": [your object keys (filenames)]
}'
```

| Key | Type | Possible values | default | description
|----------|----------|----------|----------|----------|
| asset_id | int || | The asset_id of the chunk being uploaded. |
| object_keys | string[] || |  |

## What's expected of you.
### 200 status
```
{
    "asset_id": [current_asset_id],
    "object_keys": [{ "object_key": "awesome_filename.mpd", "url": "https://s3.example.com/awesome_filename.mpd?AWSAccessKeyId=...&Signature=...&Expires=..." }]
}
```

### Error response
Everyone needs to error, it's alright, it's natural.

```
{
    "error": true
}
```