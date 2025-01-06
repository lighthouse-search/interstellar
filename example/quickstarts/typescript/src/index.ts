import express, { Request, Response } from 'express';

const app = express();
const PORT = process.env.PORT || 1234;

// Middleware to parse JSON
app.use(express.json());

app.post('/internal/chunk/authorization', (req: Request, res: Response) => {
  const successful_authentication = {
    "asset_id": "",
  };

  res.end(JSON.stringify(successful_authentication));
});

app.post('/internal/get-upload-urls', (req: Request, res: Response) => {
  const successful_authentication = {
    "asset_id": "",
  };

  res.end(JSON.stringify(successful_authentication));
});

app.post('/internal/status/update', (req: Request, res: Response) => {
  const successful_authentication = {
    "asset_id": "",
  };

  res.end(JSON.stringify(successful_authentication));
});

// Start the server
app.listen(PORT, () => {
  console.log(`Server is running on port ${PORT}`);
});
