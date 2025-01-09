import "./css/warning1.css";

export default function Topbar_warning1(props) {
    return (
        <div className="warning1">
            <p className="warning1_text">{props.children}</p>
        </div>
    )
}