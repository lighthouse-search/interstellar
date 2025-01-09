import './css/switch1.css';
import '../global.css';

export default function Switch1(props) {
    return (
        <label className="switch">
            <input checked={props.checked} onChange={props.onChange} type="checkbox"/>
            <span className="sliderswitch rounded"/>
        </label>
    )
}