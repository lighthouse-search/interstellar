Chunk authorisation
==============

Interstellar needs to keep track of incoming chunks, is the client authorised to upload this chunk? how much of the video has been uploaded? is the client trying to upload too much? Interstellar relies on either Redis or a Chunk authorisation external API call to track this. In this doc, we'll talk about the Chunk authorisation API.

## What Interstellar will do.
When Interstellar recieves a chunk and config.mode.authentication.type is "remote_server", like this:
```
"authentication": {
    "my_awesome_auth_server": {
        "type": "remote_server",
        "chunk_authorisation": "https://127.0.0.1:4455/chunk-authorisation"
    }
},
"modes": {
    "default": {
        "authentication_reference": {
            "id": "my_awesome_auth_server"
        },
    }
}
```

Interstellar will send:
```
curl -X POST https://127.0.0.1:4455/chunk-authorisation \
-H "Content-Type: application/json" \
-d '{
  "mode": "default",
  "asset_id": [current_asset_id],
  "size": [size of incoming chunk],
  "chunk": [order of incoming chunk]
}'
```

| Key | Type | Possible values | default | description
|----------|----------|----------|----------|----------|
| asset_id | int || | The asset_id of the chunk being uploaded. |
| mode | string || | The mode the client is attempting to use for the transfer. |
| size | int || | Size of the uploaded chunk (in bytes). As chunk authorisation come in, you need to increment this size in your database to ensure the client does not exceed the permissable filesize of the total file. |
| chunk | int || | Order of the chunk. Interstellar numbers chunks as data will not get sent linearly during multi-threaded uploads. Interstellar needs to know the original order of the chunks so it can reconstruct the file without corruption. You don't need to (and probably shouldn't) handle this data, ``size`` is much more important.|

## What's expected of you.
### 200 status
```
{
    "asset_id": [current_asset_id],
    "uploaded": [bytes uploaded so far],
    "max_size": [maximum size (in bytes) the total file can be]
}
```
| Key | Type | Possible values | default | description
|----------|----------|----------|----------|----------|
| asset_id | int || | The asset_id of the chunk being uploaded. |
| uploaded | int || | Bytes that have been uploaded so far. **Not** Including the bytes being uploaded in this chunk (e.g. the first chunk-authorisation response's "uploaded" should always be 0. It's not body.size+uploaded, it's just uploaded) |
| max_size | int || | Total bytes that can be uploaded in this overall transfer. |

### Error response
Everyone needs to error, it's alright, it's natural.

**Please note:** Interstellar will send your ``message`` to the client.
```
{
    "error": true,
    "message": [your error message, which definitely won't be vague...]
}
```