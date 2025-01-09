import Input_row_header from "../../../input/input_row_header";

export default function Checkbox_with_text(props) {
    return (
        <Input_row_header header={props.header}>
            <input type="checkbox" {...props}/>
        </Input_row_header>
    )
}