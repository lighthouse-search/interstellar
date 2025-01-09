"use client"
import './css/logout1.css';
import FormStyle_1 from '../forms/form_style1';
import Base from '@/components/base';
import './../../global.css';

export default function Logout1() {
  // We're going right into CSS here. Just some safety from bad inputs, we need to verify this is a real link.
  let link = null;
  try {
    link = new URL(process.env.NEXT_PUBLIC_login_background).href;
  } catch (error) {
    // Invalid link, ignore.
  }

  return (
    <Base className={`aesthetic_metadata_panel_frame`}>
      <div style={{ backgroundImage: `url(${link})` }} className='aesthetic_metadata_panel_left'>
        <div className='aesthetic_metadata_panel_left_div'>
          {process.env.NEXT_PUBLIC_logo && <img className='aesthetic_metadata_panel_left_logo' src={process.env.NEXT_PUBLIC_logo}/>}
          {process.env.NEXT_PUBLIC_dashboard_name && <h2 className='aesthetic_metadata_panel_left_header'>{process.env.NEXT_PUBLIC_dashboard_name}</h2>}
          <p>You are logging out, goodbye.</p>
        </div>
      </div>

      <div className='line'>.</div>

      <div className='aesthetic_metadata_panel_right'>
        <FormStyle_1 logo={<h2>Logout?</h2>}>
          <div className='FormStyle_1_div'>
            <button className='FormStyle_1_div_login_button'>Yes</button>
            <button className='FormStyle_1_div_login_button'>No</button>
          </div>
        </FormStyle_1>
      </div>
    </Base>
  );
}
