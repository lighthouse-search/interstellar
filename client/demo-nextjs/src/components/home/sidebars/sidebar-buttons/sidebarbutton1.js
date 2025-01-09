import ToolTip from '@/components/miscellaneous/tooltip';
import './css/sidebarbutton1.css';
import Link from 'next/link'

export default function SidebarButton1(props) {
    return (
        <ToolTip text={props.alias}><Link href={props.href} className="Sidebarbutton">
            <button className='outline'>
                {props.icon && <img src={props.icon}/>}
                {props.children && props.children}
            </button>
        </Link></ToolTip>
    )
}