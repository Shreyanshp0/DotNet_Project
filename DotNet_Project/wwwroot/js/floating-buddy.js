document.addEventListener('DOMContentLoaded', () => {
    const trigger = document.getElementById('floatingBuddyTrigger');
    const panel = document.getElementById('floatingBuddyPanel');
    const closeBtn = document.getElementById('floatingBuddyClose');
    if (!trigger || !panel) return;

    const form = document.getElementById('floatingBuddyForm');
    const input = document.getElementById('floatingBuddyInput');
    const messages = document.getElementById('floatingBuddyMessages');
    const typing = document.getElementById('floatingBuddyTyping');
    const idKey = 'mindcare-buddy-conversation-id';
    let historyLoaded = false;

    const getCsrfToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const getConversationId = () => {
        let id = sessionStorage.getItem(idKey);
        if (!id) {
            id = crypto.randomUUID ? `conv-${crypto.randomUUID()}` : `conv-${Date.now()}`;
            sessionStorage.setItem(idKey, id);
        }
        return id;
    };

    const appendMessage = (text, sender) => {
        if (!messages) return;
        const div = document.createElement('div');
        div.className = `floating-msg floating-msg-${sender}`;
        div.textContent = text;
        messages.appendChild(div);
        messages.scrollTop = messages.scrollHeight;
    };

    const loadHistoryOnce = async () => {
        if (historyLoaded || !messages) return;
        historyLoaded = true;
        try {
            const res = await fetch(`/api/chat/history/${getConversationId()}`);
            if (res.ok) {
                const data = await res.json();
                if (data.messages && data.messages.length > 0) {
                    messages.innerHTML = '';
                    data.messages.forEach(m => {
                        appendMessage(m.message, m.sender === 'child' ? 'user' : 'bot');
                    });
                }
            }
        } catch {
            // Keep default greeting
        }
    };

    const togglePanel = (open) => {
        const shouldOpen = typeof open === 'boolean' ? open : panel.classList.contains('d-none');
        panel.classList.toggle('d-none', !shouldOpen);
        trigger.setAttribute('aria-expanded', shouldOpen ? 'true' : 'false');
        if (shouldOpen) {
            loadHistoryOnce();
            input?.focus();
        }
    };

    trigger.addEventListener('click', () => togglePanel());
    closeBtn?.addEventListener('click', () => togglePanel(false));

    if (form && input && messages) {
        let isSending = false;

        const send = async () => {
            const text = input.value.trim();
            if (!text || isSending) return;
            isSending = true;

            appendMessage(text, 'user');
            input.value = '';
            input.style.height = 'auto';
            typing?.classList.remove('d-none');

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
                    body: JSON.stringify({ message: text, conversationId: getConversationId() })
                });

                if (res.ok) {
                    const data = await res.json();
                    if (data.buddyReply?.message) {
                        appendMessage(data.buddyReply.message, 'bot');
                    }
                } else {
                    appendMessage("I'm here with you. Tell me a little more about how you're feeling.", 'bot');
                }
            } catch {
                appendMessage("I'm here with you. Tell me a little more about how you're feeling.", 'bot');
            } finally {
                typing?.classList.add('d-none');
                isSending = false;
            }
        };

        form.addEventListener('submit', e => {
            e.preventDefault();
            send();
        });

        input.addEventListener('keydown', e => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                send();
            }
        });
    }
});
