document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('buddyForm');
    const input = document.getElementById('buddyInput');
    const messages = document.getElementById('buddyMessages');
    const typing = document.getElementById('buddyTyping');
    const prompts = document.getElementById('buddyPrompts');
    const conversationId = document.getElementById('conversationId');
    const idKey = 'mindcare-buddy-conversation-id';
    const getCsrfToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    if (!form || !input || !messages) return;

    const id = sessionStorage.getItem(idKey) || (crypto.randomUUID ? `conv-${crypto.randomUUID()}` : `conv-${Date.now()}`);
    sessionStorage.setItem(idKey, id);
    if (conversationId) conversationId.value = id;

    const escapeHtml = value => value.replace(/[&<>'"]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[character]));

    const renderMessage = (message, type, time = 'Just now') => {
        const item = document.createElement('div');
        item.className = `buddy-message buddy-message-${type}`;
        const avatar = type === 'bot' ? '<i class="bi bi-stars"></i>' : '<i class="bi bi-person-fill"></i>';
        item.innerHTML = `<div class="message-avatar">${avatar}</div><div><div class="message-bubble">${escapeHtml(message)}</div><time>${time}</time></div>`;
        messages.appendChild(item);
        messages.scrollTop = messages.scrollHeight;
    };

    // Load initial history from server if conversation exists
    const loadHistory = async () => {
        try {
            const res = await fetch(`/api/chat/history/${id}`);
            if (res.ok) {
                const data = await res.json();
                if (data.messages && data.messages.length > 0) {
                    messages.innerHTML = '';
                    data.messages.forEach(m => {
                        const type = m.sender === 'child' ? 'user' : 'bot';
                        const time = new Date(m.createdAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
                        renderMessage(m.message, type, time);
                    });
                }
            }
        } catch {
            // fallback
        }
    };
    loadHistory();

    const sendMessage = async message => {
        const text = message.trim();
        if (!text || typing.classList.contains('buddy-is-typing')) return;

        renderMessage(text, 'user');
        input.value = '';
        input.style.height = 'auto';
        typing.classList.remove('d-none');
        typing.classList.add('buddy-is-typing');
        prompts?.classList.add('buddy-prompts-muted');

        const token = getCsrfToken();
        try {
            const endpoint = form.dataset.endpoint || '/Chat/SendMessage';
            const res = await fetch(endpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token,
                    'X-CSRF-TOKEN': token
                },
                body: JSON.stringify({ message: text, conversationId: id })
            });

            typing.classList.add('d-none');
            typing.classList.remove('buddy-is-typing');
            prompts?.classList.remove('buddy-prompts-muted');

            if (res.ok) {
                const data = await res.json();
                if (data.buddyReply) {
                    renderMessage(data.buddyReply.message, 'bot');
                }
            } else {
                renderMessage("I'm listening and I'm right here with you. Tell me a little more about how you're feeling.", 'bot');
            }
        } catch {
            typing.classList.add('d-none');
            typing.classList.remove('buddy-is-typing');
            prompts?.classList.remove('buddy-prompts-muted');
            renderMessage("I'm listening and I'm right here with you. Tell me a little more about how you're feeling.", 'bot');
        }
    };

    form.addEventListener('submit', event => {
        event.preventDefault();
        sendMessage(input.value);
    });

    input.addEventListener('keydown', event => {
        if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            sendMessage(input.value);
        }
    });

    input.addEventListener('input', () => {
        input.style.height = 'auto';
        input.style.height = `${Math.min(input.scrollHeight, 100)}px`;
    });

    document.querySelectorAll('.prompt-chip').forEach(button => {
        button.addEventListener('click', () => sendMessage(button.textContent.trim()));
    });

    document.getElementById('clearConversation')?.addEventListener('click', () => {
        const newId = `conv-${Date.now()}`;
        sessionStorage.setItem(idKey, newId);
        if (conversationId) conversationId.value = newId;
        messages.innerHTML = '';
        renderMessage('Hi! I’m MindCare Buddy. I’m happy to listen. How are you feeling today?', 'bot');
        input.focus();
    });
});
