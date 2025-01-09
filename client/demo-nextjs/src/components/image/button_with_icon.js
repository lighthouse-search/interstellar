import Link from 'next/link';
import './css/button_with_icon.css';

export default function Button_with_icon(props) {
    const Child = (() => {
        return (
            <button {...props} className={`button_with_icon ${props.className}`}>
                {props.icon && typeof props.icon == "string" && <img className={props.icon_classname} src={props.icon}/>}
                {props.children}
            </button>
        )
    });

    if (props.href) {
        return (
            <Link href={props.href} className="button_with_icon_link">
                <Child/>
            </Link>
        )
    }

    return (
        <Child/>
    )
}