const API_BASE_URL = 'https://localhost:7091/api/users';

async function signIn(email, password) {
    try {
        const res = await fetch(`${API_BASE_URL}/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });

        if (!res.ok) {
            const msg = await res.text();
            throw new Error(msg || 'Login failed');
        }

        const user = await res.json();
        console.log('✅ Logged in as:', user);
        alert(`Welcome back, ${user.username}!`);
    } catch (err) {
        alert('❌ ' + err.message);
    }
}

