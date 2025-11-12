import express from 'express';
import path from 'path';
import { fileURLToPath } from 'url';
import fs from 'fs';
import multer from 'multer';
import cors from 'cors'; // <-- import cors

// ----------------------
// Setup paths
// ----------------------
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// ----------------------
// Express setup
// ----------------------
const app = express();
const PORT = process.env.PORT || 3000;

// ----------------------
// Enable CORS
// ----------------------
// Allow all origins (safe for testing)
app.use(cors());

// For production, restrict to your frontend origin:
// app.use(cors({ origin: 'https://friendly-eureka-x549vgxjvpqj296qp-3000.app.github.dev' }));

// ----------------------
// Middleware
// ----------------------
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// Serve public folder
app.use(express.static(path.join(__dirname, 'public')));

// ----------------------
// Folder to store uploaded games
// ----------------------
const GAME_BUILDS_FOLDER = path.join(__dirname, 'GameBuilds');
if (!fs.existsSync(GAME_BUILDS_FOLDER)) {
    fs.mkdirSync(GAME_BUILDS_FOLDER, { recursive: true });
}

// Serve uploaded games as static files
app.use('/GameBuilds', express.static(GAME_BUILDS_FOLDER));

// ----------------------
// Multer setup for file uploads
// ----------------------
const storage = multer.diskStorage({
    destination: (req, file, cb) => {
        cb(null, GAME_BUILDS_FOLDER);
    },
    filename: (req, file, cb) => {
        const uniqueName = `${Date.now()}_${file.originalname}`;
        cb(null, uniqueName);
    }
});
const upload = multer({ storage });

// ----------------------
// Routes
// ----------------------

// Serve index.html
app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

// Serve userpage.html
app.get('/userpage', (req, res) => {
    res.sendFile(path.join(__dirname, 'public', 'userpage.html'));
});

// Upload game
app.post('/api/games/upload', upload.single('gamefile'), (req, res) => {
    const { gamename, username } = req.body;
    const gamefile = req.file;

    if (!gamename || !username || !gamefile) {
        return res.status(400).json({ error: 'Missing required fields or file' });
    }

    // Return the public URL of uploaded game
    const gameUrl = `${req.protocol}://${req.get('host')}/GameBuilds/${gamefile.filename}`;
    res.json({
        gamename,
        username,
        gameUrl
    });
});

// ----------------------
// Start server
// ----------------------
app.listen(PORT, () => {
    console.log(`Server running on http://localhost:${PORT}`);
});
