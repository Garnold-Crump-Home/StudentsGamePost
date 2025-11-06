const API_BASE_URL = 'https://localhost:7091/api/users';


document.addEventListener('DOMContentLoaded', () => {
    const createBtn = document.getElementById('submitBtn');
    createBtn?.addEventListener('click', async () => {
        const form = createBtn.closest('form');
        await createAccount(form);


    })
})

async function createAccount(form) {
    const username = form.querySelector('input[name="username"]').value.trim();
    const email = form.querySelector('input[name="email"]').value.trim();
    const password = form.querySelector('input[name="password"]').value.trim();

    if (!username) return displayResult('Username required');
    if (!email) return displayResult('Email required');
    if (!password) return displayResult('Password required');


    const payload = { Username: username, email, password };

    const res = await fetch(API_BASE_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    if (!res.ok) throw new Error('Failed to create account');

    displayResult(`Account created! Username: ${username}, Email: ${email}, Password: ${password}`);
    form.reset();
}
