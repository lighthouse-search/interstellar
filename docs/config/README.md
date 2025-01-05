Configuration
==============

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