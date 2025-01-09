import Base from "../base";
import './../global.css';
import "./css/home.css"

export default function Home1(props) {
    return (
        <Base className={`home1`}>
            <div className={`home1_children ${props.className}`} style={props.style}>
                {props.children}
            </div>
        </Base>
    )
}