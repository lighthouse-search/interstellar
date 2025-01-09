import account from "./account.js";
import login from "./login.js";

function Interstellar(credsObject) {
    return {
        mode: account,
        upload: login,
    };
}

export { Interstellar };