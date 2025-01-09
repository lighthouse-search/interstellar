import { credentials_object, read_image_from_file_insert_into_element } from "@/global";
import "./css/selector1.css";
import { useRef, useState } from "react";
import { Clubs } from "clubs";

export default function Selector1(props) {
  const [random, set_random] = useState(Math.random()+Math.random());
  const image_data = useRef(null);

  async function upload_image() {
    const image_upload = await Clubs(await credentials_object()).image().upload_media(image_data.current, props.type);
    if (image_upload[0].error == true) {
      alert(image_data.message);
      return;
    }

    props.onImageId(image_upload[0].id);
  }

  return (
    <div className="selector1 selector">
      <img className='AvatarPicture shading disable-select' style={{width:90,display:'flex',flexDirection:'column',alignSelf:'center',marginTop:0,borderRadius:4,backgroundColor:'grey'}} loading="lazy" id={random} src={props.src}/>
        <div className="selector1_content">
          <p className='selector_header'>{props.header}</p>
          <div className='selector_media_actions'>
          <button className="selector_media_button">
              <label for="pfp_upload" className='selector_media_label'>
                  <img className="navButton disable-select" src={`/icons/uploadbuttonsmaller.png`} alt="Upload Button"/>
                  <p className='disable-select'>Upload</p>
              </label>
          </button>
          <input onChange={(e) => { read_image_from_file_insert_into_element(e.target.files[0], random); image_data.current = e.target.files[0]; upload_image(); }} hidden={true} id="pfp_upload" name="file" type="file" accept=".png, .gif, .jpg, .jpeg, .webp"/>
          <button onClick={() => { document.getElementById(random).src = null; image_data.current = "clear"; }} className='selector_media_button shading hover'>
              <img className="navButton disable-select" src={`/icons/trash.svg`} alt="Clear Button"/>
              <p className='disable-select'>Clear</p>
          </button>
        </div>
      </div>
    </div>
  )
}