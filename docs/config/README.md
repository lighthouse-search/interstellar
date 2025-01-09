Configuration
==============

> :warning: **Before reading, find an [example](/docs/config/examples)**: This documentation is much easier to read when comparing it to a configuration that matches your use-case.

## request
### limit
```
{
    "request": {
        "limit": {
            "max_request_body_size": 45000000,
            "keep_alive_timeout_from_minutes": 7200
        },
        "cors": {
            "allowed_origins": [
                "https://example.com"
            ]
        }
    },
}
```
| Key | Type | Possible values | default | description
|----------|----------|----------|----------|----------|
| max_request_body_size | int || 1,000,000,000 bytes (1GB)| The maximum HTTPS POST body size in bytes. |
| keep_alive_timeout_from_minutes | int || 7200 | How long an HTTPS request should be kept-alive in minutes. |

### cors
| Key | Type | Possible values | default | description
|----------|----------|----------|----------|----------|
| allowed_origins | array[string] |||[Access-Control-Allow-Origin](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Access-Control-Allow-Origin) |

## storage.(your_awesome_storage_name)
### Type: S3
```
{
    "storage": {
        "awesome_bucket": {
            "type": "s3",
            "host": "https://s3.example.com",
            "bucket_name": "lighthouse",
            "access_key_env": "",
            "secret_access_key_env": "",
            "region": "auto"
        }
    },
```
| Key | Type | Possible values | default | description
|----------|----------|----------|----------|----------|
| type | string | s3, remote_server ||  |
| host | string ||| The address of your S3 server. |
| bucket_name | string ||| Your S3 bucket name. |
| access_key_env | string ||| The name of the environment variable storing your ``access_key_env`` (e.g my_awesome_access_key) |
| secret_access_key_env | string ||| The name of the environment variable storing your ``secret_access_key_env`` (e.g my_awesome_secret_access_key) |
| region | string || auto | The region of your S3 server |

### Type: remote_server
```
{
    "storage": {
        "upload_server": {
            "type": "remote_server",
            "get_upload_urls": "https://127.0.0.1:8000/get-upload-urls"
        }
    },
}
```
| Key | Type | Possible values | default | description
|----------|----------|----------|----------|----------|
| type | string | s3, remote_server |  |
| get_upload_urls | string ||| The [Get upload urls](/docs/https/external-server/get-upload-urls.md) endpoint you'd like to use. |

## authentication.(your_awesome_storage_name)
### Type: remote_server
```
{
    "authentication": {
        "my_awesome_auth_server": {
            "type": "remote_server",
            "chunk_authorisation": "https://127.0.0.1:8000/chunk/authorisation"
        }
    },
}
```
| Key | Type | Possible values | description
|----------|----------|----------|----------|
| type | string | remote_server, redis |  |
| chunk_authorisation | string || The [Chunk Authorisation](/docs/https/external-server/chunk-authorisation.md) endpoint you'd like to use. |

## database.(your_awesome_storage_name)
### Type: redis
```
{
    "database": {
        "my_awesome_redis": {
            "type": "redis",
            "hosts": ["https://s3.example.com"],
            "username_env": "",
            "password_env": ""
        }
    },
}
```
| Key | Type | Possible values | default | description
|----------|----------|----------|----------|----------|
| type | string | remote_server ||  |
| hosts | string[] ||| Your Redis hosts. |
| username_env | string ||| The name of the environment variable storing your Redis username (e.g my_awesome_redis_username) |
| password_env | string ||| The name of the environment variable storing your Redis password (e.g my_awesome_redis_password) |

## Mode
```
"modes": {
        "default": {
            "type": "video",
            "storage_reference": {
                "id": "upload_server",
                "path": "/%asset_id%"
            },
            "authentication_reference": {
                "id": "my_awesome_auth_server"
            },
            "database_reference": {
                "id": "my_awesome_redis"
            },
            "status": {
                "status_update": "https://127.0.0.1:8000/status/update"
            },
            "features": {
                "file_types": [
                    "mp4",
                    "mkv",
                    "mov",
                    "flv",
                    "xflv",
                    "webm",
                    "avi",
                    "x-matroska",
                    "xmatroska",
                    "quicktime"
                ],
                "file_alias": {
                    "x-matroska": "mkv"
                }
            },
            "post_processing": {
                "commands": {
                    "validation": [
                        {
                            "program": "ffprobe",
                            "commands": [
                                "-v quiet -print_format json -show_format -show_streams /tmp/user_ingest_before_reencoding/%asset_id%/%asset_id%.%file_type%"
                            ]
                        }
                    ],
                    "processing": [
                        {
                            "program": "ffmpeg",
                            "commands": [
                                "-i /tmp/user_ingest_before_reencoding/%asset_id%/%asset_id%.%file_type% -vcodec libx264 -maxrate 400k -crf 28 -filter:v fps=60 /tmp/user_ingest_before_reencoding/%asset_id%/%asset_id%.avi -y -preset medium -progress pipe:1"
                            ]
                        },
                        {
                            "program": "ffmpeg",
                            "commands": [
                                "-i /tmp/user_ingest_before_reencoding/%asset_id%/%asset_id%.avi",
                                "-map 0:v:0 -map 0:a:0 -map 0:v:0 -map 0:a:0 -map 0:v:0 -map 0:a:0",
                                "-vf \"scale=-2:144\" -c:v libx264 -crf 28 -profile:v baseline -b:v:0 250k",
                                "-vf \"scale=-2:240\" -c:v libx264 -crf 28 -profile:v baseline -b:v:1 500k",
                                "-vf \"scale=-2:360\" -c:v libx264 -crf 28 -profile:v main -b:v:2 750k",
                                "-vf \"scale=-2:480\" -c:v libx264 -crf 28 -profile:v main -b:v:3 1000k",
                                "-vf \"scale=-2:720\" -c:v libx264 -crf 28 -profile:v high -b:v:4 1500k",
                                "-vf \"scale=-2:1080\" -c:v libx264 -crf 28 -profile:v high -b:v:5 2500k",
                                "-f dash /tmp/user_ingest_reencoded/%asset_id%/video.mpd -y -preset medium"
                            ]
                        },
                        {
                            "program": "ffmpeg",
                            "commands":
                            [
                                "-i /tmp/user_ingest_before_reencoding/%asset_id%/%asset_id%.avi",
                                "-preset veryfast",
                                "-vf \"scale=-1:480\"",
                                "-frames:v 1",
                                "/tmp/user_ingest_reencoded/%asset_id%/auto_generated_thumbnail.jpeg"
                            ]
                        }
                    ]
                }
            },
            "video_limits": {
                "min": {
                    
                },
                "max": {

                },
                "must_be": {
                    
                },
                "must_not_be": {

                },
                "includes": {

                },
                "does_not_include": {

                }
            },
        }
    },
```
| Key | Type | Possible values | default | description
|----------|----------|----------|----------|----------|
| mode | string | file, video || |
| storage_reference.id | string ||| The [storage](docs/config?ref_type=heads#storageyour_awesome_storage_name) server you'd like to store these files in. |
| storage_reference.path | string || /%asset_id% | Path in storage you want to store this file at. |
| authentication_reference.id | string ||| [Authentication](docs/config?ref_type=heads#authenticationyour_awesome_storage_name) server you'd like use. |
| database_reference.id | string ||| [Database](docs/config?ref_type=heads#authenticationyour_awesome_storage_name) server you'd like to use. |
| status.status_update | string ||| The [Status update](/docs/https/external-server/status-update.md) endpoint you'd like to use. |
| features.file_types | string[] ||| Your accepted file types. |
| file_alias.[input filename] | string ||| Any valid filename extension. |

#### post_processing.commands.[type (either 'validation' or 'reencode')]
| Key | Type | Possible values | default | description
|----------|----------|----------|----------|----------|
| post_processing.commands.[type].program | string ||| The process you'd like to run (e.g. ```ffmpeg```)
| post_processing.commands.[type].commands | string[] ||| Your command arguments. (e.g. ```-i /tmp/user_ingest_before_reencoding/%asset_id%/%asset_id%.%file_type% /tmp/user_ingest_before_reencoding/%asset_id%/%asset_id%.avi -y -preset medium```)

#### video_limits.(min/max/must_be/must_not_be/includes/does_not_include)

> :light_bulb: This is a little complicated, but it's actually not that hard. When you set a command in post_processing.validation.commands, the output of your last command is available here (provided it's JSON). Here you, can statically test those values are within certain boundries.

**It's important to note:**

```video_limits.min``` and ```video_limits.max```: check **only** int values as they're doing "<"/">" operations.

```video_limits.must_be``` and ```video_limits.must_not_be```: check **only** string values as they're doing "!="/"==" operations.

```video_limits.includes``` and ```video_limits.does_not_include```: check **only** check string values as they're doing .includes(value) operations.

[Here is an example using video_limits and FFprobe's output](docs/config/min_max__must_be_must_not_be__includes_not_includes/ffprobe.md)