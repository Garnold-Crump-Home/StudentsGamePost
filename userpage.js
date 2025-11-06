const API_BASE_URL = 'https://localhost:7091/api/games';
const addBtn = document.getElementById('addGameBtn');
const modal = document.getElementById('addGameModal');
const closeModal = document.getElementById('closeModal');
const cancelBtn = document.getElementById('cancelGame');
let editingId = null;

// Ensure user is logged in
const loggedUser = JSON.parse(localStorage.getItem('loggedInUser') || 'null');
if (!loggedUser) {
    alert('You must be logged in to view this page. Redirecting to login.');
    window.location.href = 'index.html';
}
const currentUsername = loggedUser && (loggedUser.username || loggedUser.Username || loggedUser.email || loggedUser.name);
const welcomeEl = document.getElementById('userWelcome');
if (welcomeEl) welcomeEl.textContent = currentUsername ? `Logged in as: ${currentUsername}` : '';

// Modal functions
function openModal() {
    modal.style.display = 'block';
    modal.setAttribute('aria-hidden', 'false');
    document.getElementById('gamename').focus();
}

function hideModal() {
    modal.style.display = 'none';
    modal.setAttribute('aria-hidden', 'true');
    document.getElementById('gameForm').reset();
    document.getElementById('imagePreviewContainer').style.display = 'none';
    document.getElementById('modalTitle').textContent = 'Add Game';
    editingId = null;
}

addBtn.addEventListener('click', openModal);
closeModal.addEventListener('click', hideModal);
cancelBtn.addEventListener('click', hideModal);
window.addEventListener('click', (e) => { if (e.target === modal) hideModal(); });

// Image preview
const imageInput = document.getElementById('gameimage');
const imgPreview = document.getElementById('imagePreview');
const imgPreviewContainer = document.getElementById('imagePreviewContainer');
imageInput.addEventListener('change', () => {
    const file = imageInput.files && imageInput.files[0];
    if (!file) { imgPreviewContainer.style.display = 'none'; return; }
    const url = URL.createObjectURL(file);
    imgPreview.src = url;
    imgPreviewContainer.style.display = 'block';
});

// Utility to convert image file -> Data URL
function fileToDataUrl(file) {
    return new Promise((resolve, reject) => {
        if (!file) return resolve(null);
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });
}

// Render games
async function renderGames() {
    const container = document.getElementById('gamesContainer');
    container.innerHTML = '';

    // Fetch games from DB
    let dbGames = [];
    try {
        const res = await fetch(API_BASE_URL);
        if (!res.ok) throw new Error(`Server error ${res.status}`);
        dbGames = await res.json();
    } catch (err) {
        console.error('Failed to load games from DB', err);
    }

    const existing = JSON.parse(localStorage.getItem('games') || '[]');

    existing.forEach(record => {
        const dbGame = dbGames.find(g => g.gameName === record.name);
        const views = dbGame ? dbGame.playersViews : 0;

        const wrap = document.createElement('div');
        wrap.className = 'game-wrap';

        const btn = document.createElement('button');
        btn.className = 'game-item';
        btn.title = record.name;

        const img = document.createElement('img');
        img.className = 'game-thumb';
        img.alt = record.name;
        img.src = record.imageDataUrl || 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="120" height="80"><rect width="100%" height="100%" fill="%23ddd"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#666" font-size="14">No Image</text></svg>';

        const label = document.createElement('div');
        label.className = 'game-label';
        label.textContent = record.name;

        const userEl = document.createElement('div');
        userEl.className = 'game-user';
        userEl.textContent = record.username ? `by ${record.username}` : '';

        const viewEl = document.createElement('div');
        viewEl.className = 'game-views';
        viewEl.textContent = `Views: ${views}`;

        btn.appendChild(img);
        btn.appendChild(label);
        btn.appendChild(userEl);
        btn.appendChild(viewEl);

        // Increment view and open WebGL game
        btn.addEventListener('click', async () => {
            try {
                if (dbGame) {
                    await fetch(`${API_BASE_URL}/increment/${encodeURIComponent(record.name)}`, { method: 'POST' });
                }

                const currentCount = parseInt(viewEl.textContent.replace('Views: ', '')) || 0;
                viewEl.textContent = `Views: ${currentCount + 1}`;

                if (record.gameUrl) {
                    const win = window.open(record.gameUrl);
                    if (!win) alert('Popup blocked. Please allow popups to view the game.');
                } else {
                    alert('No WebGL build associated with this game.');
                }
            } catch (err) {
                console.error('Error opening WebGL game', err);
            }
        });

        const actions = document.createElement('div');
        actions.className = 'game-actions';

        const isOwner = currentUsername && record.username && (record.username === currentUsername);
        if (isOwner) {
            const editBtn = document.createElement('button');
            editBtn.className = 'action-btn edit-btn';
            editBtn.type = 'button';
            editBtn.title = 'Edit';
            editBtn.textContent = 'Edit';
            editBtn.addEventListener('click', (e) => { e.stopPropagation(); editGame(record.id); });

            const delBtn = document.createElement('button');
            delBtn.className = 'action-btn delete-btn';
            delBtn.type = 'button';
            delBtn.title = 'Delete';
            delBtn.textContent = 'Delete';
            delBtn.addEventListener('click', (e) => { e.stopPropagation(); deleteGame(record.id); });

            actions.appendChild(editBtn);
            actions.appendChild(delBtn);
        }

        wrap.appendChild(btn);
        wrap.appendChild(actions);
        container.appendChild(wrap);
    });
}

// Delete game
async function deleteGame(id) {
    const existing = JSON.parse(localStorage.getItem('games') || '[]');
    const rec = existing.find(r => r.id === id);
    if (!rec) return alert('Game not found');
    if (!currentUsername || rec.username !== currentUsername) return alert('You can only delete your own games.');
    if (!confirm('Delete this saved game?')) return;

    try {
        const res = await fetch(`${API_BASE_URL}/${encodeURIComponent(rec.name)}`, { method: 'DELETE' });
        if (!res.ok) throw new Error(`Server error ${res.status}`);
    } catch (err) {
        console.error('Failed to delete game from DB', err);
    }

    const filtered = existing.filter(r => r.id !== id);
    localStorage.setItem('games', JSON.stringify(filtered));
    renderGames();
}

// Edit game
function editGame(id) {
    const existing = JSON.parse(localStorage.getItem('games') || '[]');
    const rec = existing.find(r => r.id === id);
    if (!rec) return alert('Game not found');
    if (!currentUsername || rec.username !== currentUsername) return alert('You can only edit your own games.');

    editingId = id;
    document.getElementById('modalTitle').textContent = 'Edit Game';
    document.getElementById('gamename').value = rec.name || '';
    if (rec.imageDataUrl) {
        imgPreview.src = rec.imageDataUrl;
        imgPreviewContainer.style.display = 'block';
    } else {
        imgPreview.src = '';
        imgPreviewContainer.style.display = 'none';
    }
    openModal();
}

// Form submit (with WebGL build)
const form = document.getElementById('gameForm');
form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const name = document.getElementById('gamename').value.trim();
    const imageFile = document.getElementById('gameimage').files[0];
    const gameFile = document.getElementById('gamefile').files[0]; // WebGL zip/folder

    if (!name) { alert('Please enter a game name.'); return; }

    const imageDataUrl = await fileToDataUrl(imageFile);

    // For now: assume WebGL build is already uploaded to /games_builds/<id>/index.html
    const gameUrl = `/games_builds/${editingId || Date.now()}/index.html`;

    const record = {
        id: editingId || Date.now(),
        name,
        username: currentUsername || null,
        imageDataUrl,
        gameUrl,
        createdAt: new Date().toISOString()
    };

    const existing = JSON.parse(localStorage.getItem('games') || '[]');
    if (editingId) {
        const index = existing.findIndex(r => r.id === editingId);
        if (index >= 0) existing[index] = record;
    } else {
        existing.push(record);

        // Save to DB
        try {
            const res = await fetch(API_BASE_URL, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ gameName: name })
            });
            if (!res.ok) throw new Error(`Server error ${res.status}`);
            const data = await res.json();
            console.log('✅ Game added to database: ', data);
        } catch (err) {
            console.error('❌ Failed to save game:', err);
        }
    }

    localStorage.setItem('games', JSON.stringify(existing));
    hideModal();
    renderGames();
});

// Initial render
document.addEventListener('DOMContentLoaded', renderGames);
