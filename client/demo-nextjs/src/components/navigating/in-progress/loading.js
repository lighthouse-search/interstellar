import LoadingSpinner from "@/components/miscellaneous/loadingspinner";
import "./css/loading.css";
import Base from "@/components/base";

export default function Loading(props) {
    return (
        <Base className="loading_div">
            <LoadingSpinner speed="600ms" style={{ width: 15, alignSelf: "center", justifySelf: "center" }}/>
            <div className="loading_div_loading_under_children">{props.loading_under_children}</div>
        </Base>
    )
}