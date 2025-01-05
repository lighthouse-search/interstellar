Status update
==============

## What Interstellar will do.
During key events in the transfer, Interstellar will send a ``POST`` request to the specified host in config.mode.status.status_update:
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
  "type": [..],
  "section": [..],
  "stage": [..],
  "out_of": [..],
  "video": [..]
}'
```

| Key | Type | Possible values | default | description
|----------|----------|----------|----------|----------|
| asset_id | string || | the asset_id of the status. |
| type | string || | the type of status. |
| section | string | processing, collecting_files, moving_to_storage, uploaded, failed | |  |
| stage | int || | Progress in the current section. e.g., if we're "moving_to_storage", how many files have we mvoed? |
| out_of | int || | out_of is an extension of "stage" except it shows the total. E.g. the total files we're moving to storage. |
| video.out_time_ms | int || | How much of the video FFmpeg has processed in milliseconds. |
| video.out_time | string || | How much of the video FFmpeg has processed, e.g. 00:00:23.010975 |

## What's expected of you.
You can either return any ```2xx status``` or any other status code to signal success or any other status code to signal an error. Interstellar doesn't mind if your status update fails and will continue the transfer as status updates are frequent.