const API_BASE_URL = '/api/users';
let currentUsername = null; // store logged-in username globally

async function signIn(email, password) {
    try {
        fetch(`${API_BASE_URL}/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, password })
});

        if (!res.ok) {
            const msg = await res.text();
            throw new Error(msg || 'Login failed');
        }

        const user = await res.json();

        // Save username globally and in localStorage
        currentUsername = user.username;
        localStorage.setItem('currentUsername', currentUsername);

        console.log('✅ Logged in as:', currentUsername);
        alert(`Welcome back, ${currentUsername}!`);

        // Optional: update UI element
        const statusEl = document.getElementById('loginStatus');
        if (statusEl) statusEl.textContent = `Logged in as: ${currentUsername}`;
    } catch (err) {
        alert('❌ ' + err.message);
    }
}

// When the page loads, restore username from localStorage
window.addEventListener('DOMContentLoaded', () => {
    currentUsername = localStorage.getItem('currentUsername');
    if (currentUsername) {
        console.log('✅ Restored logged-in user:', currentUsername);
        const statusEl = document.getElementById('loginStatus');
        if (statusEl) statusEl.textContent = `Logged in as: ${currentUsername}`;
    }
});
