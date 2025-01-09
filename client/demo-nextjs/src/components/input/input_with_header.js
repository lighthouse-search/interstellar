import "./css/input_with_header.css";
import './../global.css';

export default function Input_with_header(props) {
    return (
        <div className={`input_with_header ${props.className}`}>
            {typeof props.header == "string" && <p className="input_with_header_header">{props.header}</p>}
            {typeof props.header != "string" && <div className="input_with_header_header">{props.header}</div>}
            <div className={`input_with_header_content ${props.content_className}`}>
                {!props.children && <input placeholder={props.placeholder} value={props.value} onChange={props.onChange} style={props.style} type={props.type} className={`input_with_header_input ${props.className}`} onKeyPress={e => e.key === 'Enter' && props.onKeyPress}/>}
                {props.children && props.children}
            </div>
        </div>
    )
}