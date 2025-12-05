import express from 'express';
import path from 'path';
import { fileURLToPath } from 'url';
import fs from 'fs';
import multer from 'multer';
import cors from 'cors'; 


const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);


const app = express();
const PORT = process.env.PORT || 3000;


app.use(cors());

app.use(express.json());
app.use(express.urlencoded({ extended: true }));


app.use(express.static(path.join(__dirname, 'public')));


const GAME_BUILDS_FOLDER = path.join(__dirname, 'GameBuilds');
if (!fs.existsSync(GAME_BUILDS_FOLDER)) {
    fs.mkdirSync(GAME_BUILDS_FOLDER, { recursive: true });
}


app.use('/GameBuilds', express.static(GAME_BUILDS_FOLDER));


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




app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, 'public', 'index.html'));
});


app.get('/userpage', (req, res) => {
    res.sendFile(path.join(__dirname, 'public', 'userpage.html'));
});


app.post('/api/games/upload', upload.single('gamefile'), (req, res) => {
    const { gamename, username } = req.body;
    const gamefile = req.file;

    if (!gamename || !username || !gamefile) {
        return res.status(400).json({ error: 'Missing required fields or file' });
    }

  
    const gameUrl = `${req.protocol}://${req.get('host')}/GameBuilds/${gamefile.filename}`;
    res.json({
        gamename,
        username,
        gameUrl
    });
});


app.listen(PORT, () => {
    console.log(`Server running on http://localhost:${PORT}`);
});
