import ProfilePic from "@/components/user/profile_pic";
import "./css/sidebar2.css";
import SidebarButton1 from "./sidebar-buttons/sidebarbutton1";
import UserCard1 from "@/components/user/user_cards/user_card1";
import Link from "next/link";
import Login_Dialog from "@/components/internal_components/login_dialog";
import { Interstellar } from "@oracularhades/interstellar";
import { authentication_url, credentials_object } from "@/global";
import { useEffect, useRef, useState } from "react";
import UserDetailsRow from "@/components/user/user_cards/user_details_row";
import Nameplate from "@/components/internal_components/nameplate";
import Button_with_icon from "@/components/image/button_with_icon";
import { me } from "../../../global";

export default function Sidebar2(props) {
    const [account, set_account] = useState(null);
    const [profile, set_profile] = useState({});
    const [logged_in, set_logged_in] = useState(true);
    const [authentication_url_v, set_authentication_url] = useState("/guard/frontend/login");

    const should_run = useRef(true);
    async function get_me() {
        const me_check = await me();
        if (me_check != null) {
            set_account(me_check.account);
            set_profile(me_check.profile);
        }
        if (await credentials_object() == null) {
            set_logged_in(false);
            return;
        }
        const response = await Interstellar(await credentials_object()).account().me();
        if (response.ok == true) {
            set_account(response.account);
            set_profile(response.profile);
            await sessionStorage.setItem("me", JSON.stringify(response));
        }
    }

    useEffect(() => {
        if (should_run.current != true) { return; }
        should_run.current = false;
        
        set_authentication_url(authentication_url())
        get_me();
    });

    const Sidebar_min_button = ((props) => {
        return (
            <Link href={props.href} className="sidebar-bottom-min-button">
                <img src={props.icon}/>
            </Link>
        )
    });

    return (
        <div className="sidebar2">
            <Login_Dialog/>
            <div className="sidebar2_top sidebar2_frames">
                {/* <img className="sidebar_mfns_logo" src="/logo512.png"/> */}
                <SidebarButton1 alias="Home" href="/home" icon="/icons/home.svg"/>
                <SidebarButton1 alias="Search" href="/search" icon="/icons/search.svg"/>
                <SidebarButton1 alias="Livestreams" href="/livestreams" icon="/icons/livestreams.svg"/>
                <SidebarButton1 alias="Clips" href="/clips" icon="/icons/clips.svg"/>
                <SidebarButton1 alias="Thoughts" href="/thoughts" icon="/icons/thoughts.svg"/>
            </div>

            <div>
                <input placeholder="Search" className="search"/>
            </div>

            {logged_in == true && <div className="sidebar-bottom sidebar2_frames full_width_button">
                <Sidebar_min_button icon="/icons/bell-read.svg" href="/notifications"/>
                <Sidebar_min_button icon="/icons/upload.svg" href="/upload"/>
                <div className="row align_center column_gap_6">
                {logged_in == true && <ProfilePic className="sidebar_profile_pic" hover_style={{ marginTop: 6, marginLeft: "-812%" }} url={profile.profile_pic} hover={<UserCard1 className="menu_dropdown" user={{ header: profile.display_name, sub_header: "@"+profile.username, profile_pic: profile.profile_pic }}>
                    {profile && <UserDetailsRow header={<Nameplate show_pronouns={false} data={profile}/>} sub_header={"@"+profile.username} user={profile}/>}
                    <div className="menu_dropdown buttons">
                        <Button_with_icon icon="/icons/account-box.svg" href={`/@${profile.username}`}>Your profile</Button_with_icon>
                        <Button_with_icon icon="/icons/manage-accounts.svg">Change profile</Button_with_icon>
                        {/* <Button_with_icon icon="/icons/security.svg">Security</Button_with_icon> */}
                        <Button_with_icon icon="/icons/feedback.svg">Feedback</Button_with_icon>
                        <Button_with_icon icon="/icons/settings.svg" href="/settings">Settings</Button_with_icon>
                        <Button_with_icon icon="/icons/logout.svg" onClick={() => { document.getElementById("login_dialog").showModal(); }}>Logout</Button_with_icon>
                    </div>
                </UserCard1>}/>}
                {/* <p>{profile.username}</p> */}
                </div>
            </div>}
            {logged_in == false && <Link href={authentication_url_v}><button>Login</button></Link>}
        </div>
    )
}