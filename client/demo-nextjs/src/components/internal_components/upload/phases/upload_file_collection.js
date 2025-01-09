import './css/phases.css';
import './css/upload_file_collection.css';
import '@/../styles/global.css';
import '@/components/global.css';
import { useEffect, useRef, useState } from 'react';
import { get_ping_time } from '@/global';
import Input_with_header from '@/components/input/input_with_header';

export default function Upload_file_collection(props) {
    const should_run = useRef(true);
    const [ping, set_ping] = useState(null);
    const [file, set_file] = useState(null);

    async function check_ping() {
        if (window.document.hasFocus() == true && props.server) {
            set_ping(await get_ping_time(props.server));
        }
    }

    useEffect(() => {
        if (should_run.current == props.server || !props.server) { return; }
        should_run.current = props.server;

        check_ping();
        clearInterval(check_ping);
        setInterval(check_ping, 1000);
    });

    return (
        <div className="phases_frame">
            <div className='top'>
                <div className='row align_center column_gap_2px'>
                    <img src="/emojis/2604_comet_3d.png"/>
                    <p>Interstellar demo</p>
                </div>
            </div>

            <label className='file_net hover' for="video_upload">
                {file == null && <p className='greyText'>Select File</p>}
                {file && file.name && <p className='greyText'>{file.name}</p>}
            </label>
            {/* accept=".mp4, .mov, .wmv, .avi, .avchd, .flv, .f4v, .swf, .mkv, .webm" */}
            <input id="video_upload" type="file" onChange={(e) => { set_file(e.target.files[0]); }} style={{ visibility: "collapse" }}/>
            
            {props.modes && <Input_with_header header="Mode">
                <select value={props.mode} onChange={(e) => { props.set_mode(e.target.value); }} style={{ width: "100%" }}>
                    {props.modes.map((mode) => {
                        return (
                            <option>{mode}</option>
                        )
                    })}
                </select>
            </Input_with_header>}

            <Input_with_header header={[`Server`, <p className='greyText'>|</p>, <p className='greyText'>{ping}ms</p>]} value={props.server} onChange={(e) => { return; props.set_server(e.target.value); }}/>
            <div className='row column_gap_4px'>
                <Input_with_header header="Max threads" type="number" value={props.max_threads} onChange={(e) => { props.set_max_threads(e.target.value); }}/>
                <Input_with_header header="Chunk size (megabytes)" type="number" value={props.chunk_size / 1000000} onChange={(e) => { props.set_chunk_size(e.target.value * 1000000); }}/>
            </div>

            <button onClick={() => { props.on_video_input_change(file); }}>Upload</button>

            {/* <Link href="/upload/tus">Compare Interstellar to Tus</Link> */}
        </div>
    )
}