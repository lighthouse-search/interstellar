import { Interstellar, getCreds } from "./index.js";
import { generatePublicPrivateKey } from "./internal.js";
import { getPlatformApiURLWithoutPathname } from "./routing.js";

async function request(email, birth_day, birth_month, birth_year) {
    const response = await Interstellar().fetch_wrapper(`${getPlatformApiURLWithoutPathname()}/login`, {
        method: 'POST', // *GET, POST, PUT, DELETE, etc.
        mode: 'cors', // no-cors, *cors, same-origin
        cache: 'default', // *default, no-cache, reload, force-cache, only-if-cached
        credentials: 'same-origin', // include, *same-origin, omit
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            email: email,
            birth_day,
            birth_month,
            birth_year
        }),
        redirect: 'error', // manual, *follow, error
        referrerPolicy: 'no-referrer', // no-referrer, *no-referrer-when-downgrade, origin, origin-when-cross-origin, same-origin, strict-origin, strict-origin-when-cross-origin, unsafe-url
    })
    
    const data = response.json();
    
    return data;
}

async function send_code(code, public_key) {
    const response = await Interstellar().fetch_wrapper(`${getPlatformApiURLWithoutPathname()}/authenticate`, {
        method: 'POST', // *GET, POST, PUT, DELETE, etc.
        mode: 'cors', // no-cors, *cors, same-origin
        cache: 'default', // *default, no-cache, reload, force-cache, only-if-cached
        credentials: 'same-origin', // include, *same-origin, omit
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            code: code,
            public_key: public_key
        }),
        redirect: 'error', // manual, *follow, error
        referrerPolicy: 'no-referrer', // no-referrer, *no-referrer-when-downgrade, origin, origin-when-cross-origin, same-origin, strict-origin, strict-origin-when-cross-origin, unsafe-url
    })
    
    const data = response.json();
    
    return data;
}

async function generate_public_private_key() {
    return await generatePublicPrivateKey();
}

const login = { request, send_code, generate_public_private_key };
export default login;