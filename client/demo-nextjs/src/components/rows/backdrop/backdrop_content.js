import './css/backdrop-content.css';
import '../../global.css';

export default function Backdrop_content(props) {
    return (
        <div style={props.style} className={`backdrop_content secondary_element ${props.className}`}>
            <div className='backdrop_content_topbar'>
                {props.header && <p className='backdrop_content_topbar_header'>{props.header}</p>}
                {props.description && <p className='backdrop_content_topbar_description greyText'>{props.description}</p>}
            </div>
            {props.children}
        </div>
    )
}