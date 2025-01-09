import './css/phases.css';
import './css/upload_editing.css';
import '@/../styles/global.css';
import '@/components/global.css';
import { useEffect, useRef, useState } from 'react';
import LoadingSpinner from '@/components/miscellaneous/loadingspinner';

export default function Upload_editing(props) {
    const should_run = useRef(true);
    const [debug, set_debug] = useState(false);
    const scrollableDivRef = useRef(null);

    const scroll_to_bottom = () => {
        if (scrollableDivRef.current) {
            scrollableDivRef.current.scrollTop = scrollableDivRef.current.clientHeight - scrollableDivRef.current.scrollHeight;
        }
    };

    useEffect(() => {
        scroll_to_bottom();

        if (should_run.current != true) { return; }
        should_run.current = false;

        const handleKeyDown = (event) => {
            if (event.key === 'Control') {
                set_debug(true);
            }
        };
    
        const handleKeyUp = (event) => {
            if (event.key === 'Control') {
                set_debug(false);
            }
        };
    
        window.addEventListener('keydown', handleKeyDown);
        window.addEventListener('keyup', handleKeyUp);
    });

    const queue_ul = (props.queue ? props.queue : []).map((data) => {
        return (
            <div className='queue_log outline'>
                <p className='title'>Chunk {data.chunk} - {data.url}</p>
                <p className='body greyText'>{JSON.stringify(data.data)}</p>
            </div>
        )
    });

    return (
        <div className={`phases_frame upload_editing secondary_element outline`}>
            {props.video_state != "processing" && <h2>Uploading {debug == false && <span className='greyText'>{Math.floor((props.uploaded / props.size) * 100)}%</span>}{debug == true && <span className='greyText'>{props.uploaded}/{props.size}</span>}</h2>}
            {props.video_state == "processing" && <div className='row column_gap_4 align_center'>
                <LoadingSpinner style={{width: 10, height: 10}}/>
                <p>Upload complete</p>
            </div>}
            
            <div ref={scrollableDivRef} className='content'>
                {queue_ul}
            </div>
        </div>
    )
}