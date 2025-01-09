import './css/upload.css';
import "@/components/global.css";
import "@/../styles/global.css";
import Upload_file_collection from "@/components/internal_components/upload/phases/upload_file_collection";
import Upload_editing from "@/components/internal_components/upload/phases/upload_editing";
import Home1 from "@/components/home/home";
import { useEffect, useRef, useState } from "react";

export async function getServerSideProps() {
    const interstellar_address = process.env.interstellar_address;
    const management_address = process.env.management_address;
  
    return {
        props: { interstellar_address, management_address }
    };
}

export default function Upload(props) {
    const should_run = useRef(true);

    const [modes, set_modes] = useState(null);
    const [mode, set_mode] = useState(null);
    const [state, set_state] = useState(null);
    const active_connections = useRef(0);

    const [chunk_size, set_chunk_size] = useState(100 * 1000000);
    const [max_threads, set_max_threads] = useState(3);

    const chunk = useRef(0);
    const start = useRef(0);

    const [start_state, set_start_state] = useState(0);
    const [file_size_state, set_file_size_state] = useState(0);
    const [video_id, set_video_id] = useState(null);
    const [video_state, set_video_state] = useState(null);
    const [queue, set_queue] = useState([]);
    const [server, set_server] = useState(props.interstellar_address);

    async function update_queue(data) {
        let quick_v = queue;
        quick_v.push(data);
        set_queue(quick_v.sort((a, b) => a.chunk - b.chunk));
    }

    function server_url(pathname, address) {
        let server_url = new URL(address);
        server_url.pathname = pathname;
        return server_url.href;
    }

    function upload(id, start, chunk_size, file, chunk) {
        return new Promise(async (resolve, reject) => {
        let binaryValue = file.slice(start, start + chunk_size, file.type);

        let hashData = new TextEncoder().encode(binaryValue);
        let hashBuffer = await crypto.subtle.digest("SHA-256", hashData);
        let hashArray = Array.from(new Uint8Array(hashBuffer));
        let hashHex = hashArray.map(byte => byte.toString(16).padStart(2, '0')).join('');

        const signed_object = {
            asset_id: id,
            mode: mode,
            file_type: file.type,
            chunk: chunk,
            video_hash: hashHex
        }
        await update_queue({ url: server_url("/upload-chunk", server), data: signed_object, chunk });

        // Not included in the JWT, be extremely careful what goes here, as things here can break authentication. The file data is put here because it's too big and instead has a hash that will get verified.
        let formData = new FormData();
        formData.append("metadata", JSON.stringify(signed_object));
        formData.append("file", binaryValue);

        let headers = {
            size: file.size,
            "Authorization": "EXAMPLE_BEARER_TOKEN"
        };

        await fetch(server_url("/upload-chunk", server), {
            method: 'POST', // *GET, POST, PUT, DELETE, etc.
            mode: 'cors', // no-cors, *cors, same-origin
            cache: 'reload', // *default, no-cache, reload, force-cache, only-if-cached
            credentials: 'same-origin', // include, *same-origin, omit
            redirect: 'error', // manual, *follow, error
            referrerPolicy: 'no-referrer', // no-referrer, *no-referrer-when-downgrade, origin, origin-when-cross-origin, same-origin, strict-origin, strict-origin-when-cross-origin, unsafe-url
            headers: headers,
            body: formData,
        }).then(response => response.json())
        .then(data => {
            if (data.error == false) {
                if (data.type == "processing") {
                    set_video_state("processing");
                }
                if (active_connections != 0) {
                    active_connections.current = active_connections.current-1;
                }
            }
            if (data.error == true) {
                console.log("error: "+data.message);
                // setError(data.message);
                alert(data.message);
                reject(data.message);
            }
        }).catch((error) => {
            setTimeout(() => { active_connections.current = active_connections.current-1; }, 1000);
            reject(error);
            console.log("Something went wrong, check your internet connection and try again.", error);
            return;
        });

        binaryValue = null;
        hashData = null;
        hashBuffer = null;
        hashArray = null;
        hashHex = null;
        formData = null;
        });
    }

    function connection_handling(video_id, file) {
        set_file_size_state(file.size);

        if (start.current < file.size) {
            let stop = false;
            if (active_connections.current < max_threads) {
                console.log("STARTING CHUNK", chunk.current, active_connections.current, max_threads);
                active_connections.current = active_connections.current+1;
                upload(video_id, start.current, chunk_size, file, chunk.current);
                if (start.current+chunk_size >= file.size) {
                    start.current = file.size-start.current;
                    console.log("1", start.current);
                    stop = true;
                } else {
                    start.current += chunk_size;
                    console.log("2", start.current);
                }
                chunk.current = chunk.current+1;
                set_start_state(start.current);
            } else {
                console.log("Too many connections, holding");
            }
            if (start.current == file.size || stop == true) {
                return;
            }
            setTimeout((() => { connection_handling(video_id, file) }), 800);
        } else {
            return;
        }
    }

    async function upload_video(file) {
        let new_video_id = await get_asset_id(file);
        set_video_id(new_video_id);
        set_state("uploading");
        connection_handling(new_video_id, file);
    }

    async function get_asset_id(file) {
        const response = await fetch(server_url("/get-asset-id", props.management_address), {
            method: 'POST', // *GET, POST, PUT, DELETE, etc.
            mode: 'cors', // no-cors, *cors, same-origin
            cache: 'reload', // *default, no-cache, reload, force-cache, only-if-cached
            credentials: 'same-origin', // include, *same-origin, omit
            redirect: 'error', // manual, *follow, error
            referrerPolicy: 'no-referrer', // no-referrer, *no-referrer-when-downgrade, origin, origin-when-cross-origin, same-origin, strict-origin, strict-origin-when-cross-origin, unsafe-url
            body: JSON.stringify({
                size: file.size
            }),
        });
        if (response.status == 200) {
            const json = await response.json();
            return json.asset_id;
        } else {
            alert("Something went wrong [get-asset-id failure].");
            return;
        }
    }

    async function mode_list() {
        await fetch(server_url("/mode/list", server), {
            method: 'GET', // *GET, POST, PUT, DELETE, etc.
            mode: 'cors', // no-cors, *cors, same-origin
            cache: 'reload', // *default, no-cache, reload, force-cache, only-if-cached
            credentials: 'same-origin', // include, *same-origin, omit
            redirect: 'error', // manual, *follow, error
            referrerPolicy: 'no-referrer', // no-referrer, *no-referrer-when-downgrade, origin, origin-when-cross-origin, same-origin, strict-origin, strict-origin-when-cross-origin, unsafe-url
        }).then(response => response.json())
        .then(data => {
            if (data.ok == true) {
                set_modes(data.data);
            }
            if (data.error == true) {
                alert(data.message);
                return;
            }
        });
    }

    useEffect(() => {
        if (should_run.current != true) { return; }
        should_run.current = false;

        mode_list();
    });

    return (
        <Home1 style={{ alignItems: "center", justifyContent: "center" }}>
            {state == null && <Upload_file_collection on_video_input_change={(e) => { upload_video(e); }} modes={modes} mode={mode} set_mode={set_mode} chunk_size={chunk_size} set_chunk_size={set_chunk_size} max_threads={max_threads} set_max_threads={set_max_threads} server={server} set_server={set_server}/>}
            {state == "uploading" && <Upload_editing video_id={video_id} video_state={video_state} uploaded={start_state} size={file_size_state} queue={queue} server={server} set_server={set_server}/>}
        </Home1>
    )
}