import Input_with_header from "../input/input_with_header";
import Selector1 from "../media/selectors/selector1";
import Switch1 from "../switches/switch1";
import "./css/media_edit_dialog.css";
import Dialog_Frame from "./dialog_frame";

export default function Media_Edit_Dialog(props) {
    return (
        <Dialog_Frame id={props.id} header={props.header} className="Media_Edit_Dialog">
            <Selector1 header="Image" src={props.image}/>

            <Input_with_header header="Enabled" value={props.enabled} onChange={props.on_enabled}>
                <Switch1/>
            </Input_with_header>
            <Input_with_header header="Alias" value={props.alias} onChange={props.on_alias} placeholder="My amazing image"/>

            <button onClick={props.on_finish}>Finish</button>
        </Dialog_Frame>
    )
}