import './table1.css';

export default function Table1(props) {
    let data = []
    if (props.data) {
        data = props.data;
    }

    const ColumnItem = ((props) => {
        console.log(props.content);
        return (
            <div className='table1_column_item'>
                <p className='table1_column_item_text'>{props.content}</p>
            </div>
        )
    });

    const Column = ((props) => {
        const Column_Items = props.data.map((data) => {
            return (
                <ColumnItem content={data}/>
            )
        });

        return (
            <div className='table1_column'>
                <p className='table1_column_header'>{props.header}</p>
                <div className='table1_column_content'>
                    {Column_Items}
                </div>
            </div>
        )
    });

    const columns = Object.keys(data).map((key) => {
        return (
            <Column header={key} data={data[key]}/>
        )
    });

    return (
        <div className='table1'>
            {columns}
        </div>
    )
}