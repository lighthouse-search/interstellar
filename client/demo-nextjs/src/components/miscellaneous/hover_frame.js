'use client'
import { useEffect, useRef, useState } from "react";
import "./css/hover_frame.css";
import './../global.css';

export default function HoverFrame(props) {
    if (!props.hover || props.disabled == true) {
        return (
            props.children
        )
    };

    const [showing, set_showing] = useState(false);
    const hoverRef = useRef(null);
    const dropdownRef = useRef(null);

    const handleClickOutside = (event) => {
        if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
            set_showing(false);
        }
    };

    useEffect(() => {
        document.addEventListener('click', handleClickOutside);
        return () => {
            document.removeEventListener('click', handleClickOutside);
        };
    }, []);

    useEffect(() => {
        if (showing && hoverRef.current) {
            const rect = hoverRef.current.getBoundingClientRect();
            if (rect.right > window.innerWidth) {
                hoverRef.current.style.left = `-${rect.right - window.innerWidth}px`;
            }
            if (rect.bottom > window.innerHeight) {
                hoverRef.current.style.top = `-${rect.height}px`;
            }
        }
    }, [showing]);

    return (
        <div ref={dropdownRef} key={props.tkey} style={props.style} className={`hoverframe ${props.className}`}>
            <div onClick={() => { set_showing(!showing); }}>{props.children}</div>
            {showing == true && <span style={props.hover_style} class="hoverframetext supershade">{props.hover}</span>}
        </div>
    )
}