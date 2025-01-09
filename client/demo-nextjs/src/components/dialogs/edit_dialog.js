import Dialog_Frame from "./dialog_frame";
import "./css/edit_dialog.css";
import { useEffect, useRef, useState } from "react";

export default function Edit_Dialog(props) {
    const [checkedItems, setCheckedItems] = useState([]);
    const [selectedAll, setSelectedAll] = useState(false);
    const [nonce, setNonce] = useState({ num: 0 });
    const [customInput, setCustomInput] = useState(null);

    let options = props.options;
    if (!options) {
        options = [];
    }

    let header = props.header;
    if (!header) {
        header = "Edit";
    }

    const shouldRun = useRef(true);

    let optionCountRemoveDuplicates = [];
    options.forEach((element) => {
        if (optionCountRemoveDuplicates.indexOf(element) == -1) {
            optionCountRemoveDuplicates.push(element);
        }
    });
    
    useEffect(() => {
        if (!props.selected) { return; }
        if (optionCountRemoveDuplicates.length > 0 && checkedItems.length == optionCountRemoveDuplicates.length) {
            if (selectedAll != true) {
                setSelectedAll(true);
            }
        } else {
            if (selectedAll != false) {
                setSelectedAll(false);
            }
        }

        if (shouldRun.current == true) {
            shouldRun.current = false;
            if (checkedItems != props.selected) {
                setCheckedItems(props.selected);
            }
        }
        async function check() {
            await checkedItems.forEach((element) => {
                if (document.getElementById(element)) {
                    document.getElementById(element).checked = true;
                }
            });
        }

        function what() {
            if (document.getElementById(props.id) && document.getElementById(props.id).open == true) {
                clearInterval(what);
                check();
            }
        }

        setInterval(what, 10);
    });

    let already_rendered = [];
    const EditDialogUl = options.map((data) => {
        let id = `${data}`;
        
        function press() {
            setNonce({ num: new Date().getTime() });
            if (props.selected) {
                if (props.selected.indexOf(data) == -1) {
                    let dataV = props.selected;
                    dataV.push(data);
                    setCheckedItems(dataV);
                    props.setSelected(dataV);
                } else {
                    let dataV = props.selected;
                    dataV.splice(props.selected.indexOf(data), 1);
                    setCheckedItems(dataV);
                    props.setSelected(dataV);
                }
            }
        }

        if (already_rendered.indexOf(data) != -1) {
            return;
        }
        already_rendered.push(data);

        return (
            <div onClick={() => { press(); }} className="edit_dialog_component secondary_element shade">
                <input id={id} type="checkbox"/>
                <p className="edit_dialog_component_text disable-select">{data}</p>
            </div>
        )
    });

    async function selectAll() {
        setCheckedItems(optionCountRemoveDuplicates);
        props.setSelected(optionCountRemoveDuplicates);
        await optionCountRemoveDuplicates.forEach((element) => {
            document.getElementById(element).checked = true;
        });
    }

    async function clearAll() {
        setCheckedItems([]);
        props.setSelected([]);
        await optionCountRemoveDuplicates.forEach((element) => {
            document.getElementById(element).checked = false;
        });
    }

    return (
        <Dialog_Frame id={props.id} header={header} className="edit_dialog">
            <div className="edit_dialog_container">
                {props.allowCustomInput == true && <div className="edit_dialog_container_add_div">
                    <input className="edit_dialog_container_add_div_input" placeholder="Add Item" value={customInput} onChange={(e) => { setCustomInput(e.target.value); }}></input>
                    <button onClick={() => { props.onAdd(customInput); }} className="button editDialogContainerAddDivButton">Add</button>
                </div>}
                {selectedAll == false && <p onClick={() => { selectAll(); }} className="edit_dialog_options_ul_select_picker greyText underline">Select All</p>}
                {selectedAll == true && <p onClick={() => { clearAll(); }} className="edit_dialog_options_ul_select_picker greyText underline">Unselect All</p>}
                <ul className="edit_dialog_options_ul">
                    {EditDialogUl}
                </ul>
                {/* <button onClick={() => { closeDialog(props.id); }} className="applyButton button">Apply</button> */}
            </div>
        </Dialog_Frame>
    )
}