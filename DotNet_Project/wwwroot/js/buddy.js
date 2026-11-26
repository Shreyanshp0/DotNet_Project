document.addEventListener('DOMContentLoaded', () => {
    const app = document.getElementById('buddyApp');
    const shell = document.getElementById('buddyShell');
    const form = document.getElementById('buddyForm');
    const input = document.getElementById('buddyInput');
    const messages = document.getElementById('buddyMessages');
    const typing = document.getElementById('buddyTyping');
    const prompts = document.getElementById('buddyPrompts');
    const conversationIdInput = document.getElementById('conversationId');
    const convListEl = document.getElementById('buddyConversationList');
    const newConvBtn = document.getElementById('clearConversation');
    const expandBtn = document.getElementById('toggleExpandBuddy');

    if (!form || !input || !messages) return;

    const sendEndpoint = form.dataset.endpoint || '/Chat/SendMessage';
    const conversationsEndpoint = app?.dataset.conversationsEndpoint || '/api/chat/conversations';
    const historyEndpoint = app?.dataset.historyEndpoint || '/api/chat/history';
    const idKey = app?.dataset.storageKey || 'mindcare-buddy-conversation-id';
    const sizeKey = app?.dataset.sizeKey || 'mindcare-buddy-shell-size';
    const userSenderKey = app?.dataset.userSender || 'child';
    const defaultGreeting = app?.dataset.greeting || 'Hi! I’m MindCare Buddy. I’m happy to listen. How are you feeling today?';
    const convPrefix = app?.dataset.convPrefix || 'conv';

    const getCsrfToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const newConversationId = () =>
        crypto.randomUUID ? `${convPrefix}-${crypto.randomUUID()}` : `${convPrefix}-${Date.now()}`;

    let activeId = sessionStorage.getItem(idKey) || '';

    const setActiveConversationId = (id) => {
        activeId = id;
        sessionStorage.setItem(idKey, id);
        if (conversationIdInput) conversationIdInput.value = id;
    };

    const escapeHtml = value => value.replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[c]));

    const renderMessage = (message, type, time = 'Just now') => {
        const item = document.createElement('div');
        item.className = `buddy-message buddy-message-${type}`;
        const avatar = type === 'bot' ? '<i class="bi bi-stars"></i>' : '<i class="bi bi-person-fill"></i>';
        item.innerHTML = `<div class="message-avatar">${avatar}</div><div><div class="message-bubble">${escapeHtml(message)}</div><time>${time}</time></div>`;
        messages.appendChild(item);
        messages.scrollTop = messages.scrollHeight;
    };

    const renderGreeting = () => {
        messages.innerHTML = '';
        renderMessage(defaultGreeting, 'bot');
    };

    const loadConversationMessages = async (convId) => {
        if (!convId) {
            renderGreeting();
            return;
        }
        try {
            const res = await fetch(`${historyEndpoint}/${encodeURIComponent(convId)}`);
            if (res.ok) {
                const data = await res.json();
                if (data.messages && data.messages.length > 0) {
                    messages.innerHTML = '';
                    data.messages.forEach(m => {
                        const type = m.sender === userSenderKey ? 'user' : 'bot';
                        const time = new Date(m.createdAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
                        renderMessage(m.message, type, time);
                    });
                    return;
                }
            }
        } catch {
            // fallback to greeting
        }
        renderGreeting();
    };

    const refreshConversationList = async (autoSelectLatestIfUnset = false) => {
        if (!convListEl) return;
        try {
            const res = await fetch(conversationsEndpoint);
            if (!res.ok) return;
            const list = await res.json();

            if (autoSelectLatestIfUnset) {
                const hasStoredInList = activeId && list.some(c => c.conversationId === activeId);
                if (!hasStoredInList) {
                    if (list.length > 0) {
                        setActiveConversationId(list[0].conversationId);
                    } else if (!activeId) {
                        setActiveConversationId(newConversationId());
                    }
                } else {
                    setActiveConversationId(activeId);
                }
            }

            if (list.length === 0) {
                convListEl.innerHTML = '<small class="text-muted">No previous conversations yet.</small>';
                return;
            }

            convListEl.innerHTML = '';
            list.forEach(conv => {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = `buddy-conv-item ${conv.conversationId === activeId ? 'active' : ''}`;
                const dateLabel = new Date(conv.lastMessageAt).toLocaleDateString([], { month: 'short', day: 'numeric' });
                btn.innerHTML = `<strong>${escapeHtml(conv.lastMessagePreview || 'Conversation')}</strong><small>${dateLabel} · ${conv.messageCount} messages</small>`;
                btn.addEventListener('click', async () => {
                    setActiveConversationId(conv.conversationId);
                    await loadConversationMessages(conv.conversationId);
                    await refreshConversationList(false);
                });
                convListEl.appendChild(btn);
            });
        } catch {
            // ignore
        }
    };

    // Initialize persisted conversation on load
    (async () => {
        await refreshConversationList(true);
        if (!activeId) setActiveConversationId(newConversationId());
        await loadConversationMessages(activeId);
    })();

    // Restore & persist resizable shell dimensions
    if (shell) {
        try {
            const savedSize = JSON.parse(localStorage.getItem(sizeKey) || 'null');
            if (savedSize && savedSize.height) {
                shell.style.height = `${savedSize.height}px`;
            }
            if (savedSize && savedSize.width && window.innerWidth >= 992) {
                shell.style.width = `${savedSize.width}px`;
            }
        } catch {
            // ignore
        }

        if (typeof ResizeObserver !== 'undefined') {
            let resizeTimer = null;
            const observer = new ResizeObserver(entries => {
                for (const entry of entries) {
                    clearTimeout(resizeTimer);
                    resizeTimer = setTimeout(() => {
                        const rect = entry.contentRect;
                        if (rect.height >= 400) {
                            try {
                                localStorage.setItem(sizeKey, JSON.stringify({
                                    height: Math.round(rect.height),
                                    width: Math.round(rect.width)
                                }));
                            } catch {
                                // ignore
                            }
                        }
                    }, 200);
                }
            });
            observer.observe(shell);
        }
    }

    expandBtn?.addEventListener('click', () => {
        if (!shell) return;
        const expanded = shell.classList.toggle('buddy-shell-expanded');
        expandBtn.innerHTML = expanded
            ? '<i class="bi bi-arrows-angle-contract me-1"></i> Default size'
            : '<i class="bi bi-arrows-angle-expand me-1"></i> Expand view';
    });

    const sendMessage = async message => {
        const text = message.trim();
        if (!text || typing.classList.contains('buddy-is-typing')) return;
        if (!activeId) setActiveConversationId(newConversationId());

        renderMessage(text, 'user');
        input.value = '';
        input.style.height = 'auto';
        typing.classList.remove('d-none');
        typing.classList.add('buddy-is-typing');
        prompts?.classList.add('buddy-prompts-muted');

        const token = getCsrfToken();
        try {
            const res = await fetch(sendEndpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token,
                    'X-CSRF-TOKEN': token
                },
                body: JSON.stringify({ message: text, conversationId: activeId })
            });

            typing.classList.add('d-none');
            typing.classList.remove('buddy-is-typing');
            prompts?.classList.remove('buddy-prompts-muted');

            if (res.ok) {
                const data = await res.json();
                if (data.conversationId) setActiveConversationId(data.conversationId);
                if (data.buddyReply?.message) {
                    renderMessage(data.buddyReply.message, 'bot');
                }
                await refreshConversationList(false);
            } else {
                renderMessage("I'm listening and right here with you. Please try sending your message again.", 'bot');
            }
        } catch {
            typing.classList.add('d-none');
            typing.classList.remove('buddy-is-typing');
            prompts?.classList.remove('buddy-prompts-muted');
            renderMessage("I'm listening and right here with you. Please check your connection and try again.", 'bot');
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
        input.style.height = `${Math.min(input.scrollHeight, 110)}px`;
    });

    document.querySelectorAll('.prompt-chip').forEach(button => {
        button.addEventListener('click', () => sendMessage(button.textContent.trim()));
    });

    newConvBtn?.addEventListener('click', () => {
        const freshId = newConversationId();
        setActiveConversationId(freshId);
        renderGreeting();
        refreshConversationList(false);
        input.focus();
    });
});
