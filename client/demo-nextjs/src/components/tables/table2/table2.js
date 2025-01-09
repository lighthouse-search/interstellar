import { useEffect } from 'react';
import './table2.css';

export default function Table2(props) {
    let data = []
    if (props.data) {
        data = props.data;
    }

    useEffect(() => {
        
    })

    const columns_top = Object.keys(data).map((key) => {
        return (
            <p className='table2_topbar_text'>{key}</p>
        )
    });

    return (
        <div className='table2'>
            <div className='table2_topbar'>
                {columns_top}
            </div>
        </div>
    )
}