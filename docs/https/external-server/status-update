Status update
==============

## What Interstellar will do.
When Interstellar recieves a chunk and config.mode.authentication.type is "remote_server", like this:
```
"modes": {
    "default": {
        "status": {
            "status_update": "http://127.0.0.1:4455/status/update"
        },
    }
}
```

Interstellar will send:
```
curl -X POST https://127.0.0.1:4455/status/update \
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
    "uploaded": [bytes uploaded so far],
    "files": [{ "object_key": "awesome_filename.mpd", "url": "https://s3.example.com/awesome_filename.mpd?AWSAccessKeyId=...&Signature=...&Expires=..." }]
}
```

### Error response
Everyone needs to error, it's alright, it's natural.

```
{
    "error": true
}
```