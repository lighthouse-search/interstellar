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
            "keep_alive_timeout_from_minutes": 60
        },
        "cors": {
            "allowed_origins": [
                "https://example.com"
            ]
        }
    },
}
```
| Key | Type | Possible values | description
|----------|----------|----------|----------|
| max_request_body_size | int ||  |
| keep_alive_timeout_from_minutes | int ||  |

### cors
| Key | Type | Possible values | description
|----------|----------|----------|----------|
| allowed_origins | array[string] ||  |

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
| Key | Type | Possible values | description
|----------|----------|----------|----------|
| type | string | s3, remote_server |  |
| host | string ||  |
| bucket_name | string ||  |
| access_key_env | string ||  |
| secret_access_key_env | string ||  |
| region | string ||  |

### Type: remote_server
```
{
    "storage": {
        "upload_server": {
            "type": "remote_server",
            "get_upload_urls": "http://127.0.0.1:4455/api/internal/video/get-upload-urls"
        }
    },
}
```
| Key | Type | Possible values | description
|----------|----------|----------|----------|
| type | string | s3, remote_server |  |
| get_upload_urls | string ||  |

## authentication.(your_awesome_storage_name)
### Type: remote_server
```
{
    "authentication": {
        "my_awesome_auth_server": {
            "type": "remote_server",
            "chunk_authorisation": "http://127.0.0.1:4455/api/internal/video/chunk-authorisation"
        }
    },
}
```
| Key | Type | Possible values | description
|----------|----------|----------|----------|
| type | string | remote_server |  |
| chunk_authorisation | string ||  |

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
| Key | Type | Possible values | description
|----------|----------|----------|----------|
| type | string | remote_server |  |
| hosts | string[] ||  |
| username_env | string ||  |
| password_env | string ||  |

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
            "database_reference111111": {
                "id": "my_awesome_redis"
            },
            "status": {
                "status_update": "http://127.0.0.1:4455/api/internal/video/status/update"
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
            "video_limits": {
                "min": {
                    "stream": {
                        "width": 256,
                        "height": 144,
                        "duration": 1,
                        "size": 1,
                        "bit_rate": 0
                    },
                    "format": {
                        "probe_score": 98
                    }
                },
                "max": {
                    "video_streams": 1,
                    "audio_streams": -1,
                    "stream": {
                        "width": 3840,
                        "height": 2160
                    },
                    "format": {
                        "size": 5000000000,
                        "bit_rate": 600000000,
                        "nb_streams": 10000
                    }
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
                    "reencode": [
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
            }
        }
    },
```
| Key | Type | Possible values | description
|----------|----------|----------|----------|
| mode | string | file, video |  |
| storage_reference.id | string] ||  |
| storage_reference.path | string ||  |
| authentication_reference.id | string ||  |
| database_reference.id | string ||  |
| status.status_update | string ||  |
| features.file_types | string[] ||  |
| file_alias.[input filename] | string | Any valid filename extension. |  |

#### video_limits.(min/max/must_be/must_not_be/includes/does_not_include)

> :light_bulb: This is a little complicated, but it's actually not that hard. When you set a command in post_processing.validation.commands, the output of your last command is available here (provided it's JSON). Here you, can statically test those values are within certain boundries.

**It's important to note:**

```video_limits.min``` and ```video_limits.max```: check **only** int values as they're doing "<"/">" operations.

```video_limits.must_be``` and ```video_limits.must_not_be```: check **only** string values as they're doing "!="/"==" operations.

```video_limits.includes``` and ```video_limits.does_not_include```: check **only** check string values as they're doing .includes(value) operations.

[Here is an example using video_limits and FFprobe's output](docs/config/min_max__must_be_must_not_be__includes_not_includes/ffprobe.md)