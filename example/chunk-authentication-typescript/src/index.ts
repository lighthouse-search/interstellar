import express, { Request, Response } from 'express';

const app = express();
const PORT = process.env.PORT || 1234;

// Middleware to parse JSON
app.use(express.json());

// Basic route
app.post('/internal/chunk-authorization', (req: Request, res: Response) => {
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
