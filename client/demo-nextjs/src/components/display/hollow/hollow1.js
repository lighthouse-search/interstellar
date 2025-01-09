import './css/hollow1.css';

export default function Hollow1(props) {
    return (
        <div className='hollow1'>
            <div className="hollow1_topbar third_element">
                <p className='hollow1_topbar_header'>{props.header}</p>
            </div>
            {props.children}
        </div>
    )
}