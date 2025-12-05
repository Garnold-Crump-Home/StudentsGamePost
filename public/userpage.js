const API_BASE_URL = (() => {
    const host = window.location.hostname;
    const port = 7239;
    if (host.includes('githubpreview.dev')) {
        return `http://localhost:7239/api/games`;
    }
    return `http://localhost:7239/api/games`;
})();

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
const currentUsername = loggedUser.username || loggedUser.Email || loggedUser.name;
document.getElementById('userWelcome').textContent = currentUsername ? `Logged in as: ${currentUsername}` : '';

// ----------------------
// Modal functions
// ----------------------
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
window.addEventListener('click', e => { if (e.target === modal) hideModal(); });

// ----------------------
// Image preview
// ----------------------
const imageInput = document.getElementById('gameimage');
const imgPreview = document.getElementById('imagePreview');
const imgPreviewContainer = document.getElementById('imagePreviewContainer');

imageInput.addEventListener('change', () => {
    const file = imageInput.files[0];
    if (!file) {
        imgPreviewContainer.style.display = 'none';
        return;
    }
    imgPreview.src = URL.createObjectURL(file);
    imgPreviewContainer.style.display = 'block';
});

// ----------------------
// Render games
// ----------------------
async function renderGames() {
    const container = document.getElementById('gamesContainer');
    container.innerHTML = '';

    let existing = JSON.parse(localStorage.getItem('games') || '[]');

    // Fetch latest views for all games
    await Promise.all(existing.map(async (record) => {
        try {
            const res = await fetch(`${API_BASE_URL}/${record.name}/getViews`);
            if (res.ok) {
                const data = await res.json();
                record.playersViews = data.playersViews;
            } else {
                record.playersViews = record.playersViews || 0;
            }
        } catch (err) {
            console.error('Error fetching views for', record.name, err);
            record.playersViews = record.playersViews || 0;
        }
    }));

    // Sort games by views descending
    existing.sort((a, b) => (b.playersViews || 0) - (a.playersViews || 0));

    // Save updated views to localStorage (optional)
    localStorage.setItem('games', JSON.stringify(existing));

    // Render each game
    existing.forEach(record => {
        const wrap = document.createElement('div');
        wrap.className = 'game-wrap';

        const btn = document.createElement('button');
        btn.className = 'game-item';
        btn.title = record.name;

        const img = document.createElement('img');
        img.className = 'game-thumb';
        img.alt = record.name;
        img.src = record.imageDataUrl || 'data:image/svg+xml;utf8,<svg xmlns="https://www.w3.org/2000/svg" width="120" height="80"><rect width="100%" height="100%" fill="%23ddd"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#666" font-size="14">No Image</text></svg>';

        const label = document.createElement('div');
        label.className = 'game-label';
        label.innerHTML = `${record.name} 
            ${record.username ? `(by ${record.username})` : ''} 
            <span class="game-views">[Views: ${record.playersViews || 0}]</span>`;

        btn.appendChild(img);
        btn.appendChild(label);

        // Increment views on click
        btn.addEventListener('click', async () => {
            if (!record.gameUrl) {
                alert('No WebGL build associated with this game.');
                return;
            }

            try {
                const res = await fetch(`${API_BASE_URL}/${record.name}/incrementViews`, { method: 'PATCH' });
                const data = await res.json();
                record.playersViews = data.playersViews;
                label.querySelector('.game-views').textContent = `[Views: ${record.playersViews}]`;

                // Re-sort list after increment
                renderGames();
            } catch (err) {
                console.error('Error incrementing views', err);
            }

            const win = window.open(record.gameUrl);
            if (!win) alert('Popup blocked. Allow popups to view the game.');
        });

        wrap.appendChild(btn);

        // Delete button logic (same as before)
        if (record.username === currentUsername) {
            const deleteBtn = document.createElement('button');
            deleteBtn.className = 'delete-btn';
            deleteBtn.textContent = 'Delete';
            deleteBtn.style.marginLeft = '8px';

            deleteBtn.addEventListener('click', async (e) => {
                e.stopPropagation();
                if (!confirm(`Delete "${record.name}"? This cannot be undone.`)) return;

                try {
                    const res = await fetch(`${API_BASE_URL}/${encodeURIComponent(record.name)}`, {
                        method: 'DELETE',
                        headers: { 'Content-Type': 'application/json' }
                    });

                    if (!res.ok) {
                        const data = await res.json();
                        throw new Error(data.message || 'Delete failed');
                    }

                    existing = existing.filter(g => g.name !== record.name);
                    localStorage.setItem('games', JSON.stringify(existing));

                    renderGames();
                    alert('Game deleted successfully!');
                } catch (err) {
                    console.error('Delete error', err);
                    alert(`Delete error: ${err.message}`);
                }
            });

            wrap.appendChild(deleteBtn);
        }

        container.appendChild(wrap);
    });
}



// ----------------------
// Form submit
// ----------------------
const form = document.getElementById('gameForm');
form.addEventListener('submit', async (e) => {
    e.preventDefault();

    const gamename = document.getElementById('gamename').value.trim();
    const imageFile = document.getElementById('gameimage').files[0];
    const gameFile = document.getElementById('gamefile').files[0];

    if (!gamename) { alert('Enter a game name'); return; }
    if (!gameFile) { alert('Select a game file'); return; }

    const formData = new FormData();
    formData.append('gamefile', gameFile);
    formData.append('gamename', gamename);
    if (imageFile) formData.append('gameimage', imageFile); // <-- FIXED

    try {
        const res = await fetch(`${API_BASE_URL}/upload`, {
            method: 'POST',
            body: formData
        });

        if (!res.ok) {
            const text = await res.text();
            throw new Error(`Upload failed: ${res.status} ${text}`);
        }

        const result = await res.json();
        let finalGameUrl = result.gameUrl;
        if (finalGameUrl && !finalGameUrl.endsWith('index.html')) {
            if (!finalGameUrl.endsWith('/')) finalGameUrl += '/';
            finalGameUrl += 'index.html';
        }

        const existing = JSON.parse(localStorage.getItem('games') || '[]');

        const newGame = {
            id: result.gameId,
            name: gamename,
            gameUrl: finalGameUrl,
            imageDataUrl: result.gameImageUrl, // <-- FIXED
            username: currentUsername
        };

        existing.push(newGame);
        localStorage.setItem('games', JSON.stringify(existing));

        hideModal();
        renderGames();
        alert('Game uploaded successfully!');
    } catch (err) {
        console.error('Upload error', err);
        alert(`Upload error: ${err.message}`);
    }
});

// Initial render
renderGames();
