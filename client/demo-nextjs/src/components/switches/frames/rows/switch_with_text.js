import Switch1 from "@/components/switches/switch1.js";
import './css/switch_with_text.css';

export default function Switch_with_text(props) {
    return (
        <div className="Switch_with_text">
            <div className="Switch_with_text_topbar">
                {!props.children && <Switch1 checked={props.checked} onChange={props.onChange}/>}
                {props.children && props.children}
                <p className="Switch_with_text_header">{props.header}</p>
            </div>
            <p className="Switch_with_text_description greyText">{props.description}</p>
        </div>
    )
}