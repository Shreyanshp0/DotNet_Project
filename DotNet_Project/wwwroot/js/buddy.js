document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('buddyForm');
    const input = document.getElementById('buddyInput');
    const messages = document.getElementById('buddyMessages');
    const typing = document.getElementById('buddyTyping');
    const prompts = document.getElementById('buddyPrompts');
    const conversationId = document.getElementById('conversationId');
    const storageKey = 'mindcare-buddy-conversation';
    const idKey = 'mindcare-buddy-conversation-id';
    if (!form || !input || !messages) return;

    const id = sessionStorage.getItem(idKey) || (crypto.randomUUID ? crypto.randomUUID() : `buddy-${Date.now()}`);
    sessionStorage.setItem(idKey, id);
    conversationId.value = id;

    const escapeHtml = value => value.replace(/[&<>'"]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[character]));
    const renderMessage = (message, type, time = 'Just now') => {
        const item = document.createElement('div');
        item.className = `buddy-message buddy-message-${type}`;
        const avatar = type === 'bot' ? '<i class="bi bi-stars"></i>' : '<i class="bi bi-person-fill"></i>';
        item.innerHTML = `<div class="message-avatar">${avatar}</div><div><div class="message-bubble">${escapeHtml(message)}</div><time>${time}</time></div>`;
        messages.appendChild(item);
        messages.scrollTop = messages.scrollHeight;
    };
    const saveConversation = () => {
        const items = [...messages.querySelectorAll('.buddy-message')].map(item => ({ type: item.classList.contains('buddy-message-user') ? 'user' : 'bot', text: item.querySelector('.message-bubble')?.textContent || '', time: item.querySelector('time')?.textContent || 'Just now' }));
        sessionStorage.setItem(storageKey, JSON.stringify(items));
    };
    const responseFor = message => {
        const text = message.toLowerCase();
        if (text.includes('sad')) return 'I am sorry you are having a sad moment. Would talking to a trusted adult or doing one small comforting thing help right now?';
        if (text.includes('stress') || text.includes('school')) return 'School stress can feel like a lot. We could break one worry into a small next step, or you could talk with a teacher or trusted adult.';
        if (text.includes('friend')) return 'Friends can bring both happy and tricky moments. What part would you like to talk about?';
        if (text.includes('happy')) return 'That is lovely to hear! What is helping you feel happy today?';
        if (text.includes('help') || text.includes('serious')) return 'You do not have to handle something serious alone. Please tell a trusted adult who can support you right now.';
        return 'I am listening. You can tell me a little more, or choose one small thing you are feeling right now.';
    };
    const sendMessage = message => {
        const text = message.trim();
        if (!text || typing.classList.contains('buddy-is-typing')) return;
        renderMessage(text, 'user');
        input.value = '';
        input.style.height = 'auto';
        typing.classList.remove('d-none');
        typing.classList.add('buddy-is-typing');
        prompts?.classList.add('buddy-prompts-muted');
        saveConversation();

        // Future integration seam: POST { Message: text, ConversationId: id } to form.dataset.endpoint.
        window.setTimeout(() => {
            typing.classList.add('d-none');
            typing.classList.remove('buddy-is-typing');
            prompts?.classList.remove('buddy-prompts-muted');
            renderMessage(responseFor(text), 'bot');
            saveConversation();
        }, 850);
    };

    const saved = JSON.parse(sessionStorage.getItem(storageKey) || '[]');
    if (saved.length) {
        messages.innerHTML = '';
        saved.forEach(item => renderMessage(item.text, item.type, item.time));
    }
    form.addEventListener('submit', event => { event.preventDefault(); sendMessage(input.value); });
    input.addEventListener('keydown', event => {
        if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); sendMessage(input.value); }
    });
    input.addEventListener('input', () => { input.style.height = 'auto'; input.style.height = `${Math.min(input.scrollHeight, 100)}px`; });
    document.querySelectorAll('.prompt-chip').forEach(button => button.addEventListener('click', () => sendMessage(button.textContent)));
    document.getElementById('clearConversation')?.addEventListener('click', () => {
        sessionStorage.removeItem(storageKey);
        messages.innerHTML = '';
        renderMessage('Hi Alex! I’m MindCare Buddy. I’m happy to listen. How are you feeling today?', 'bot');
        input.focus();
    });
});
