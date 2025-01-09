async function shuffle_data_for_table(data, actions) {
    let output = {};
    
    await data.forEach(async (object) => {
        await Object.keys(object).map((key) => {
            if (!output[key]) {
                output[key] = [];
            }

            output[key].push(object[key]);
        });
    });

    if (actions) {
        output.actions = [];
        await data.forEach(async (object) => {
            output.actions.push(actions);
        });
    }

    return output;
}

export { shuffle_data_for_table };