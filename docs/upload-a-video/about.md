👾 How Interstellar works
==============

# Multiple connections, stateless upload.

# Threading
Interstellar's chunk uploading implementation is amazing. Chunks are stateless to Interstellar instances. When receiving chunks, Interstellar servers do not care what sequence you upload data, instead, when upload is complete, Interstellar reconstructs the uploaded data based on a chunk's sequence (specified in /upload-chunk) This not only allows for resumable uploads, but stateless is what Interstellar so fast. You can upload video to any instance, and provided all instances have the same configuration, it's irrelevant which instance you upload to, this is different from some protocols like Tus, which are single-instance and single-connection. More importantly, it's possible for multiple connections to the same instance, in the form of short/medium-lived PUT requests.

### Single instance
Multiple HTTPS connections can be uploading chunk data at once, you aren't limited by backbones latency. For example, you can have 3 connections (e.g 40MB, if your internet and browser can keep up) and as soon as a connection finishes, you're immediately sending another 40MB. This means you could be sending 120MB at any given time. It's very quick, and works *shockingly* well.

### Multi-instance
Say you have a load balancer at ```https://ingest.example.com/interstellar/v1/upload-chunk``` and a cluster with 3 Interstellar containers running. Your load balancer is doing round-robin load-balancing (each request goes to a new instance) and because of Interstellar's chunk implementation, each Interstellar server simply validates and stores the data, before the video is fully uploaded, reconstructed, and dealt with.
