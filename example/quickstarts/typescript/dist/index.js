"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const express_1 = __importDefault(require("express"));
const app = (0, express_1.default)();
const PORT = process.env.PORT || 1234;
// Middleware to parse JSON
app.use(express_1.default.json());
// Basic route
app.post('/internal/chunk-authorization', (req, res) => {
    const successful_authentication = {
        "asset_id": "",
        "file_type": ["mp4"],
        "uploaded": 0,
        "max_size": 764829806
    };
    res.end(JSON.stringify(successful_authentication));
});
// Start the server
app.listen(PORT, () => {
    console.log(`Server is running on port ${PORT}`);
});
