import '../../global.css';
import './css/Settings_sidebar.css';
import Button_with_icon from "@/components/image/button_with_icon";

export default function Settings_Sidebar(props) {
    const Seperator = ((props) => {
        return (
            <div className='seperator'>
                <p>{props.header}</p>
                <div className='side_line'/>
            </div>
        )
    });

    const Settings_button = ((props) => {
        return (
            <Button_with_icon {...props} className={`${props.className} outline full_width`}>{props.children}</Button_with_icon>
        )
    });

    return (
        <div className="settings_sidebar">
            <Settings_button href="/settings/analytics">Analytics</Settings_button>
            <Settings_button href="/settings/video">Videos</Settings_button>
            <Settings_button href="/settings/livestreams">Livestreams</Settings_button>
            <Settings_button href="/settings/clips">Clips</Settings_button>
            <Settings_button href="/settings/music">Music</Settings_button>
            <Seperator/>
            <Settings_button href="/settings/autoclip">Autoclip</Settings_button>
            <Settings_button href="/settings/monetisation">Monetisation</Settings_button>
            <Settings_button href="/settings/ads">ADs</Settings_button>
            <Settings_button href="/settings/emotes">Emotes</Settings_button>
            <Settings_button href="/settings/variants">Variants</Settings_button>
            <Seperator/>
            <Settings_button href="/settings/moderation">Moderation</Settings_button>
            <Settings_button href="/settings/bots-and-commands">Bots & Commands</Settings_button>
            <Seperator/>
            <Settings_button href="/settings/webhooks">Webhooks</Settings_button>
            <Settings_button href="/settings/developers">Developers</Settings_button>
            <Settings_button href="/settings/sponsors">Sponsors</Settings_button>
            <Settings_button href="/settings/marketplace">Marketplace</Settings_button>
        </div>
    )
}