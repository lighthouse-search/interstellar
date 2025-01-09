function checkTime(dateV) {
    let date = Math.abs((dateV / 1000).toFixed(0));
    let currentDate = Math.abs((new Date().getTime() / 1000).toFixed(0));

    let diff = currentDate - date;

    let years = Math.floor(diff / 31557600);
    let months = Math.floor(diff / 2629800);
    let days = Math.floor(diff / 86400);
    let hours = Math.floor(diff / 3600) % 24;
    let minutes = Math.floor(diff / 60) % 60;
    let seconds = diff % 60;

    let timeV = "";
    if (years) {
        if (years == 1) {
            timeV = `${timeV} ${years} year`;
        } else {
            timeV = `${timeV} ${years} years`;
        }
    } else if (months) {
        if (months == 1) {
            timeV = `${timeV} ${months} month`;
        } else {
            timeV = `${timeV} ${months} months`;
        }
    } else if (days) {
        if (days == 1) {
            timeV = `${timeV} ${days} day`;
        } else {
            timeV = `${timeV} ${days} days`;
        }
    } else if (hours) {
        if (hours == 1) {
            timeV = `${timeV} ${hours} hour`;
        } else {
            timeV = `${timeV} ${hours} hours`;
        }
    } else if (minutes) {
        if (minutes == 1) {
            timeV = `${timeV} ${minutes} minute`;
        } else {
            timeV = `${timeV} ${minutes} minutes`;
        }
    } else if (seconds) {
        timeV = `${timeV} ${seconds}s`;
    }

    return timeV;
}

function numberEnd(value) {
    let valueOut = value;
    if (value && value >= 1000000000000000) {
        valueOut = value / 1000000000000000+"Q";
    } else if (value && value >= 1000000000000) {
        valueOut = value / 1000000000000+"T";
    } else if (value && value >= 1000000000) {
        valueOut = value / 1000000000+"B";
    } else if (value && value >= 1000000) {
        valueOut = value / 1000000+"M";
    } else if (value && value >= 10000) {
        valueOut = value / 10000+"K";
    } else if (value && value >= 1000) {
        valueOut = value / 1000+"K";
    };

    return valueOut;
}

async function credentials_object() {
    const auth_data = JSON.parse(await localStorage.getItem("auth"));

    if (!auth_data) {
      console.log("No auth data found.");
      return null;
    }

    return { deviceid: auth_data.device_id, privatekey: auth_data.private_key };
}

async function me() {
    const me_session = await sessionStorage.getItem("me") ? JSON.parse(await sessionStorage.getItem("me")) : null;
    return me_session;
}

async function is_me(profile_id) {
    const me_v = await me();
    return me_v && me_v.profile && profile_id == me_v.profile.id ? true : false
}

async function get_ping_time(url) {
    const startTime = Date.now();

    const response = await fetch(url + '?_=' + startTime, { method: 'HEAD', mode: "cors", cache: "no-cache" });

    const endTime = Date.now();
    return endTime - startTime;
}

export { checkTime, numberEnd, credentials_object, me, is_me, get_ping_time }