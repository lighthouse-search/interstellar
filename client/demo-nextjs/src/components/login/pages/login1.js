"use client"
import './css/login1.css';
import FormStyle_1 from '../forms/form_style1';

import Input_with_header from '@/components/input/input_with_header';
import Login_With from '@/components/login/forms/login_with';
import Or_Bar from '@/components/login/forms/or_bar';
import Frame_AestheticMetadataPanel from '@/components/miscellaneous/frame_aesthetic_metadata_panel';
import './../../global.css';
import { useState } from 'react';
import { useRouter } from 'next/router';
import { Interstellar } from '@oracularhades/interstellar';

export default function Login1() {
  const router = useRouter();
  const [state, set_state] = useState(null);
  const [email, set_email] = useState(null);
  const [attempt_id, set_attempt_id] = useState(null);
  const [code, set_code] = useState(null);

  async function request_code() {
    const response = await Interstellar().login().request(email);
    set_state("code");
    set_attempt_id(response.attempt_id);
  }

  return (
    <Frame_AestheticMetadataPanel>
      {state == null && <FormStyle_1>
        <div className='FormStyle_1_div'>
          <Input_with_header header="Email" value={email} onChange={(e) => { set_email(e.target.value); }} placeholder={`name@${process.env.NEXT_PUBLIC_email_placeholder_domain}`}></Input_with_header>
          <button onClick={() => { request_code(); }} className='FormStyle_1_div_login_button'>Login</button>
        </div>
      </FormStyle_1>}

      {state == "code" && <FormStyle_1 header="Enter code">
        <div className='FormStyle_1_div'>
          <Input_with_header placeholder={`••••••`} value={code} onChange={(e) => { set_code(e.target.value); }} type="number"></Input_with_header>
          <button onClick={() => {  }} className='FormStyle_1_div_login_button'>Login</button>
        </div>
      </FormStyle_1>}
    </Frame_AestheticMetadataPanel>
  );
}
