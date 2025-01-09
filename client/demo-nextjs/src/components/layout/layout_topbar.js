import './css/layout_topbar.css';
import './../global.css';

export default function Layout_Topbar(props) {
    return (
        <div style={props.style} className={`layout_topbar ${props.className}`}>
            {props.children}
        </div>
    )
}